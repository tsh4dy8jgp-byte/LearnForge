using Microsoft.EntityFrameworkCore;
using Cards = LearnForge.Core.Flashcards;

namespace LearnForge.Api.Services.Flashcards;

public sealed class FlashcardService(AppDb db, ReleaseCache releases, LearningRecordService learning, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private async Task<ReleaseView> Release(string packId)
    {
        var release = await releases.Latest(db, packId) ?? throw new DomainError(404, "Subject not found.");
        if (!release.Pack.Features.Flashcards) throw new DomainError(400, "This subject has no flashcards.");
        return release;
    }

    public async Task<FlashcardSummaryDto> Summary(string user, string packId)
    {
        var release = await Release(packId);
        return await Summary(user, release.Pack);
    }
    public async Task<FlashcardSummaryDto> Summary(string user, Pack pack)
    {
        var stored = await db.FlashcardStates.AsNoTracking().Where(s => s.UserId == user && s.PackId == pack.Id).ToDictionaryAsync(s => s.UnitId);
        var cards = Cards.Expand(pack).Select(u =>
        {
            stored.TryGetValue(u.Id, out var state);
            var introduced = state?.Introduced == true && state.ContentHash == u.ContentHash;
            return new FlashcardStatusDto(u.Id, introduced, state?.Suspended == true, introduced ? state!.DueAt : null, state?.Revision ?? 0);
        }).ToArray();
        var due = cards.Count(c => !c.Suspended && c.Introduced && c.DueAt <= Now);
        var introducedCount = cards.Count(c => c.Introduced);
        var active = await db.FlashcardSessions.AsNoTracking().Where(s => s.UserId == user && s.PackId == pack.Id && s.ActiveKey != null).Select(s => s.Id).FirstOrDefaultAsync();
        return new(cards.Length, cards.Count(c => !c.Suspended && !c.Introduced), due, cards.Count(c => c.Suspended),
            introducedCount, introducedCount > 0 && due == 0, active, cards);
    }

    public async Task<FlashcardSessionDto> Start(string user, string packId, FlashcardStartRequest request)
    {
        // Preserve filter identities for existing sessions; a bounded study is a distinct request.
        var filterHash = request.Limit is null
            ? ContentHash.Of(new[] { request.Deck, request.Objective, request.Tag })
            : ContentHash.Of(new { request.Deck, request.Objective, request.Tag, request.Limit });
        var previous = await db.FlashcardSessions.SingleOrDefaultAsync(s => s.UserId == user && s.PackId == packId && s.StartKey == request.RequestId);
        if (previous is not null)
        {
            if (previous.FilterHash != filterHash) throw new DomainError(409, "This request ID was used with different filters.");
            return View(previous);
        }
        var active = await db.FlashcardSessions.SingleOrDefaultAsync(s => s.UserId == user && s.PackId == packId && s.ActiveKey != null);
        if (active is not null)
        {
            if (active.FilterHash != filterHash) throw new DomainError(409, "Finish your active review before changing filters.");
            return View(active);
        }
        var release = await Release(packId);
        var status = (await Summary(user, release.Pack)).Cards.ToDictionary(s => s.UnitId);
        var units = Cards.Expand(release.Pack).Where(u => !status[u.Id].Suspended &&
            (request.Deck is null || u.DeckId == request.Deck) && (request.Objective is null || u.ObjectiveIds.Contains(request.Objective)) &&
            (request.Tag is null || u.Tags.Contains(request.Tag))).ToArray();
        var siblings = new HashSet<string>();
        bool FirstSibling(FlashcardUnit u) => siblings.Add(u.DeckId + "/" + u.NoteId);
        var limit = request.Limit ?? 20;
        var due = units.Where(u => status[u.Id].Introduced && status[u.Id].DueAt <= Now)
            .OrderBy(u => status[u.Id].DueAt).ThenBy(u => u.Id, StringComparer.Ordinal).Where(FirstSibling).Take(limit).ToArray();
        var fresh = units.Where(u => !status[u.Id].Introduced).Where(FirstSibling).Take(Math.Min(10, limit - due.Length)).ToArray();
        var selected = due.Concat(fresh).ToArray();
        if (selected.Length == 0) throw new DomainError(400, "No new or due cards match your filters. Review is up to date.");
        var session = new FlashcardSession { UserId = user, PackId = packId, ReleaseId = release.ReleaseId,
            StartKey = request.RequestId, FilterHash = filterHash, ActiveKey = user + "/" + packId, UnitsJson = Json.Write(selected), StartedAt = Now };
        db.FlashcardSessions.Add(session);
        await learning.Touch(user, packId); await db.SaveChangesAsync();
        return View(session);
    }

    public async Task<FlashcardSessionDto> Get(string user, string packId, string id) => View(await Find(user, packId, id));
    private async Task<FlashcardSession> Find(string user, string packId, string id) =>
        await db.FlashcardSessions.SingleOrDefaultAsync(s => s.Id == id && s.UserId == user && s.PackId == packId)
        ?? throw new DomainError(404, "Review session not found.");

    public async Task<FlashcardSessionDto> Mutate(string user, string packId, string id, FlashcardOperation operation, FlashcardMutationRequest request)
    {
        var session = await Find(user, packId, id);
        var fingerprint = ContentHash.Of(new[] { operation.ToString().ToLowerInvariant(), Json.Write(request) });
        var replay = await db.FlashcardEvents.SingleOrDefaultAsync(e => e.SessionId == id && e.RequestId == request.RequestId);
        if (replay is not null)
        {
            if (replay.Fingerprint != fingerprint) throw new DomainError(409, "This request ID was used for another action.");
            return Json.Read<FlashcardSessionDto>(replay.ViewJson);
        }
        if (session.Revision != request.Revision) throw new DomainError(409, "Review changed in another window. Resume to reload it.");
        if (session.CompletedAt is not null) throw new DomainError(409, "This review is already finished.");
        var unit = Json.Read<FlashcardUnit[]>(session.UnitsJson)[session.Position];
        if (operation == FlashcardOperation.Reveal)
        {
            if (request.Rating is not null) throw new DomainError(400, "Reveal does not accept a rating.");
            session.Revealed = true;
        }
        else if (operation == FlashcardOperation.Rate)
        {
            if (!session.Revealed) throw new DomainError(400, "Reveal the answer before rating recall.");
            if (request.Rating is not { } rating || !Enum.IsDefined(rating)) throw new DomainError(400, "Choose Again, Hard, Good or Easy.");
            // An old pinned session can record history without overwriting the schedule of newer content,
            // including after the latest release removed every deck.
            var latest = await releases.Latest(db, packId);
            var current = latest is { Pack.Features.Flashcards: true } ? Cards.Expand(latest.Pack).FirstOrDefault(u => u.Id == unit.Id) : null;
            if (current?.ContentHash == unit.ContentHash)
            {
                var state = await db.FlashcardStates.FindAsync(user, packId, unit.Id);
                if (state is null)
                {
                    state = new() { UserId = user, PackId = packId, UnitId = unit.Id, ContentHash = unit.ContentHash };
                    db.FlashcardStates.Add(state);
                }
                var schedule = FlashcardScheduler.Rate(state.ContentHash == unit.ContentHash && state.Introduced ? state.Stage : -1, rating, Now);
                state.Stage = schedule.Stage; state.DueAt = schedule.DueAt; state.SchedulerVersion = schedule.SchedulerVersion;
                state.ContentHash = unit.ContentHash; state.Introduced = true; state.Revision++;
            }
            session.Position++; session.Revealed = false;
            if (session.Position == Json.Read<FlashcardUnit[]>(session.UnitsJson).Length) { session.CompletedAt = Now; session.ActiveKey = null; }
        }
        else if (operation == FlashcardOperation.Finish) { session.CompletedAt = Now; session.ActiveKey = null; }
        else throw new DomainError(400, "Unknown review action.");
        session.Revision++;
        var view = View(session);
        db.FlashcardEvents.Add(new() { UserId = user, SessionId = id, RequestId = request.RequestId, Fingerprint = fingerprint,
            Operation = operation, UnitId = unit.Id, Rating = operation == FlashcardOperation.Rate ? request.Rating : null, At = Now, ViewJson = Json.Write(view) });
        await learning.Touch(user, packId); await db.SaveChangesAsync();
        return view;
    }

    public async Task<FlashcardStatusDto> Suspend(string user, string packId, FlashcardSuspendRequest request)
    {
        var unit = Cards.Expand((await Release(packId)).Pack).FirstOrDefault(u => u.Id == request.UnitId) ?? throw new DomainError(404, "Card not found.");
        var state = await db.FlashcardStates.FindAsync(user, packId, unit.Id);
        if (state is null)
        {
            if (request.Revision != 0) throw new DomainError(409, "Card changed. Reload the card list.");
            state = new() { UserId = user, PackId = packId, UnitId = unit.Id, ContentHash = unit.ContentHash, DueAt = Now };
            db.FlashcardStates.Add(state);
        }
        else if (state.Revision != request.Revision && state.Suspended != request.Suspended) throw new DomainError(409, "Card changed. Reload the card list.");
        state.Suspended = request.Suspended; state.Revision++;
        await db.SaveChangesAsync();
        return new(unit.Id, state.Introduced && state.ContentHash == unit.ContentHash, state.Suspended,
            state.Introduced && state.ContentHash == unit.ContentHash ? state.DueAt : null, state.Revision);
    }

    public static FlashcardSessionDto View(FlashcardSession session)
    {
        var units = Json.Read<FlashcardUnit[]>(session.UnitsJson);
        var done = session.CompletedAt is not null;
        var unit = done ? null : units[session.Position];
        return new(session.Id, session.PackId, session.ReleaseId, session.Revision, session.Position, units.Length, session.Revealed, done,
            unit is null ? null : new(unit.Id, unit.DeckId, unit.NoteId, unit.Front, unit.Language,
                session.Revealed ? unit.Back : null, session.Revealed ? unit.Explanation : null, session.Revealed ? unit.Sources : []));
    }
}
