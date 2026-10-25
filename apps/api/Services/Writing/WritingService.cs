using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Services.Writing;

// Browser stroke matching supplies practice feedback, never assessment evidence.
public sealed class WritingService(AppDb db, ReleaseCache releases, TimeProvider clock, LearningRecordService learning)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task<WritingSummaryDto> Summary(string user, string packId)
    {
        var release = await releases.Latest(db, packId) ?? throw new DomainError(404, "Course not found.");
        var states = await db.WritingStates.AsNoTracking().Where(s => s.UserId == user && s.PackId == packId).ToListAsync();
        var progress = release.Pack.WritingCharacters.Select(c =>
        {
            var state = states.SingleOrDefault(s => s.CharacterId == c.Id && s.ContentHash == ContentHash.Of(c));
            return new WritingCharacterProgressDto(c.Id, state?.AssistedCompletions ?? 0, state?.IndependentCompletions ?? 0,
                state?.SequenceCompletions ?? 0, state?.Stage ?? -1, state?.DueAt);
        }).ToArray();
        var active = await db.WritingSessions.Where(s => s.UserId == user && s.PackId == packId && s.ActiveKey != null).Select(s => s.Id).SingleOrDefaultAsync();
        return new(packId, release.Pack.Version, progress.Count(p => p.DueAt <= Now), active, progress);
    }
    public async Task<WritingSessionDto> Start(string user, string packId, WritingStartRequest request)
    {
        if (!Enum.IsDefined(request.Mode)) throw new DomainError(400, "Unsupported writing mode.");
        var replay = await db.WritingSessions.SingleOrDefaultAsync(s => s.UserId == user && s.PackId == packId && s.StartKey == request.RequestId);
        if (replay is not null)
        {
            var view = await View(replay);
            if (view.Character.Id != request.CharacterId || view.Mode != request.Mode || view.Version != request.Version)
                throw new DomainError(409, "This request ID was already used for different practice.");
            return view;
        }
        var release = await releases.Latest(db, packId) ?? throw new DomainError(404, "Course not found.");
        if (release.Pack.Version != request.Version) throw new DomainError(409, "The course has changed. Reload before starting practice.");
        var character = release.Pack.WritingCharacters.SingleOrDefault(c => c.Id == request.CharacterId)
            ?? throw new DomainError(404, "Character not found.");
        if (!release.Pack.Features.Writing) throw new DomainError(400, "Writing is not enabled for this course.");
        if (await db.WritingSessions.AnyAsync(s => s.UserId == user && s.PackId == packId && s.ActiveKey != null))
            throw new DomainError(409, "Resume or finish your active writing session first.");
        var session = new WritingSession { UserId = user, PackId = packId, ReleaseId = release.ReleaseId,
            StartKey = request.RequestId, ActiveKey = user + "/" + packId, CharacterJson = Json.Write(character), Mode = request.Mode, StartedAt = Now };
        db.WritingSessions.Add(session); await learning.Touch(user, packId); await db.SaveChangesAsync(); return await View(session);
    }
    public async Task<WritingSessionDto> Get(string user, string packId, string id) => await View(await Owned(user, packId, id));
    public async Task<WritingSessionDto> Finish(string user, string packId, string id, WritingResultRequest request)
    {
        var session = await Owned(user, packId, id);
        var hash = ContentHash.Of(request);
        if (session.ResultKey == request.RequestId)
        {
            if (session.ResultHash != hash) throw new DomainError(409, "This result request ID was already used with different data.");
            return await View(session);
        }
        if (session.FinishedAt is not null || session.Revision != request.Revision) throw new DomainError(409, "Practice has already changed. Reload and retry.");
        var character = Json.Read<WritingCharacter>(session.CharacterJson);
        var latest = await releases.Latest(db, packId);
        // Old pinned sessions remain resumable, but cannot advance revised or retired characters.
        if (request.Completed && session.Mode != WritingMode.Watch && latest?.Pack.WritingCharacters.Any(c => c.Id == character.Id && ContentHash.Of(c) == ContentHash.Of(character)) == true)
        {
            var state = await db.WritingStates.SingleOrDefaultAsync(s => s.UserId == user && s.PackId == packId && s.CharacterId == character.Id);
            var contentHash = ContentHash.Of(character);
            if (state is null)
            {
                state = new WritingState { UserId = user, PackId = packId, CharacterId = character.Id, ContentHash = contentHash };
                db.WritingStates.Add(state);
            }
            else if (state.ContentHash != contentHash)
            {
                state.ContentHash = contentHash; state.Stage = -1; state.DueAt = null;
                state.AssistedCompletions = 0; state.IndependentCompletions = 0; state.SequenceCompletions = 0;
            }
            if (session.Mode == WritingMode.Sequence) state.SequenceCompletions++;
            else if (session.Mode == WritingMode.Recall)
            {
                if (request.Hints == 0) state.IndependentCompletions++; else state.AssistedCompletions++;
                var rating = request.Hints > 0 ? ReviewRating.Again : request.Mistakes > 0 ? ReviewRating.Hard : ReviewRating.Good;
                var schedule = FlashcardScheduler.Rate(state.Stage, rating, Now);
                state.Stage = schedule.Stage; state.DueAt = schedule.DueAt;
            }
            else state.AssistedCompletions++;
            state.Revision++;
        }
        session.Completed = request.Completed; session.Mistakes = request.Mistakes; session.Hints = request.Hints;
        session.ResultKey = request.RequestId; session.ResultHash = hash; session.FinishedAt = Now; session.ActiveKey = null; session.Revision++;
        await db.SaveChangesAsync(); return await View(session);
    }
    public async Task<WritingHistoryDto> History(string user)
    {
        var sessions = await db.WritingSessions.AsNoTracking().Where(s => s.UserId == user).OrderBy(s => s.StartedAt).ToArrayAsync();
        var courses = new List<WritingSummaryDto>();
        foreach (var id in sessions.Select(s => s.PackId).Distinct()) courses.Add(await Summary(user, id));
        var views = new List<WritingSessionDto>();
        foreach (var session in sessions) views.Add(await View(session));
        return new(courses.ToArray(), views.ToArray());
    }
    private async Task<WritingSession> Owned(string user, string packId, string id) =>
        await db.WritingSessions.SingleOrDefaultAsync(s => s.Id == id && s.UserId == user && s.PackId == packId)
        ?? throw new DomainError(404, "Writing session not found.");
    private async Task<WritingSessionDto> View(WritingSession s) => new(s.Id, s.PackId, (await releases.Get(s.ReleaseId)).Pack.Version,
        Json.Read<WritingCharacter>(s.CharacterJson), s.Mode, s.Revision, s.StartedAt, s.FinishedAt, s.Completed, s.Mistakes, s.Hints);
}
