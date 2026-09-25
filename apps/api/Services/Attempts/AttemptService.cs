using LearnForge.Core;
using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Services.Attempts;

public sealed class AttemptService(AppDb db, TimeProvider clock)
{
    public DateTime Now => clock.GetUtcNow().UtcDateTime;
    public static AttemptSnapshot Snapshot(Attempt a) => Json.Read<AttemptSnapshot>(a.SnapshotJson);
    public static Dictionary<string, Answer> Answers(Attempt a) => Json.Read<Dictionary<string, Answer>>(a.AnswersJson);
    public static string Section(Question q) => q.ScenarioId ?? "general";
    public static string[] Sections(Attempt a) => Snapshot(a).Questions.Select(Section).Distinct().ToArray();

    public async Task<Attempt> Find(string user, string id)
    {
        var a = await db.Attempts.SingleOrDefaultAsync(x => x.Id == id && x.UserId == user)
            ?? throw new DomainError(404, "Attempt not found.");
        await Expire(a);
        return a;
    }
    public async Task Expire(Attempt a)
    {
        if (a.Status == AttemptStatus.InProgress && a.Mode == AssessmentMode.Mock && a.Deadline <= Now)
        {
            Finish(a, true);
            await db.SaveChangesAsync();
        }
    }

    public async Task<Attempt> Start(string user, StartRequest request)
    {
        if (!Guid.TryParse(request.RequestId, out _)) throw new DomainError(400, "Invalid session settings.");
        if (request.Mode == AssessmentMode.Mock && request.Focus is not null) throw new DomainError(400, "Focused practice uses learning mode.");
        var replay = await db.Attempts.SingleOrDefaultAsync(a => a.UserId == user && a.StartKey == request.RequestId);
        if (replay is not null)
        {
            if (replay.PackId != request.PackId || replay.Mode != request.Mode || replay.Focus != request.Focus || Snapshot(replay).Blueprint.Id != request.BlueprintId) throw new DomainError(409, "This request ID already belongs to another session.");
            await Expire(replay);
            return replay;
        }
        var release = await db.Packs.Where(p => p.PackId == request.PackId).OrderByDescending(p => p.PublishedAt).FirstOrDefaultAsync()
            ?? throw new DomainError(404, "Course not found.");
        var active = await db.Attempts.SingleOrDefaultAsync(a => a.ActiveKey == user + ":" + request.PackId);
        if (active is not null)
        {
            await Expire(active);
            if (active.Status == AttemptStatus.InProgress) throw new DomainError(409, $"Resume or finish your existing session: {active.Id}");
        }
        var pack = Json.Read<Pack>(release.ContentJson);
        var blueprint = pack.Blueprints.FirstOrDefault(b => b.Id == request.BlueprintId) ?? throw new DomainError(400, "Unknown blueprint.");
        var history = await db.Attempts.Where(a => a.UserId == user && a.PackId == request.PackId).ToListAsync();
        var seen = history.SelectMany(a => Snapshot(a).Questions).Select(q => q.FamilyId).ToHashSet();
        Question[] chosen;
        if (request.Focus is { } focus)
        {
            var graded = history.Where(a => a.Status == AttemptStatus.Completed).SelectMany(a =>
            {
                var answers = Answers(a);
                return Snapshot(a).Questions.Select(q => (Question: q, Grade: Grader.Score(q, answers.GetValueOrDefault(q.Id))));
            }).ToArray();
            var missed = graded.Where(x => !x.Grade.FullyCorrect).Select(x => x.Question.Id).ToHashSet();
            var weak = graded.SelectMany(x => x.Question.ObjectiveIds.Select(o => new ObjectiveGradeEvidence(o, x.Grade.FullyCorrect)))
                .GroupBy(x => x.Id).OrderBy(g => g.Count(x => x.FullyCorrect) / (double)g.Count()).Take(2).Select(g => g.Key).ToHashSet();
            chosen = pack.Questions.Where(q => q.ScenarioId == null && (focus == PracticeFocus.Mistakes ? missed.Contains(q.Id) : q.ObjectiveIds.Any(weak.Contains)))
                .OrderBy(q => seen.Contains(q.FamilyId)).ThenBy(_ => Guid.NewGuid()).Take(blueprint.Count).ToArray();
            if (chosen.Length == 0) throw new DomainError(409, "Complete an assessment first to build targeted practice evidence.");
        }
        else chosen = ExamComposer.Compose(pack, blueprint, seen, request.RequestId);
        chosen = chosen.OrderBy(q => q.ScenarioId is null ? 0 : 1).ThenBy(q => q.ScenarioId).Select(Shuffle).ToArray();
        var snapshot = new AttemptSnapshot(pack.Title, pack.Version, pack.Objectives, pack.Scenarios, blueprint, chosen, pack.Readiness);
        var attempt = new Attempt
        {
            UserId = user, PackReleaseId = release.Id, PackId = pack.Id, StartKey = request.RequestId,
            ActiveKey = user + ":" + pack.Id, Mode = request.Mode, Size = blueprint.Size, Focus = request.Focus,
            StartedAt = Now, Deadline = Now.AddMinutes(blueprint.Minutes), SnapshotJson = Json.Write(snapshot),
            FreshPercent = chosen.Count(q => !seen.Contains(q.FamilyId)) * 100m / chosen.Length
        };
        db.Attempts.Add(attempt);
        db.Audit.Add(new() { ActorId = user, Action = "attempt.started", ResourceId = attempt.Id });
        await db.SaveChangesAsync();
        return attempt;
    }

    private static Question Shuffle(Question q)
    {
        var options = q.Options.OrderBy(_ => Guid.NewGuid()).ToArray();
        if (q.Kind == QuestionKind.Sequence && options.Select(o => o.Id).SequenceEqual(q.Grading.Correct)) options = options.Skip(1).Append(options[0]).ToArray();
        return q with { Options = options, Slots = q.Slots.Select(s => s with { Options = s.Options.OrderBy(_ => Guid.NewGuid()).ToArray() }).ToArray() };
    }

    public async Task Save(Attempt a, ResponseRequest request)
    {
        if (!Guid.TryParse(request.RequestId, out _)) throw new DomainError(400, "A UUID request ID is required.");
        var payload = Json.Write(request);
        var replay = await db.Responses.SingleOrDefaultAsync(e => e.AttemptId == a.Id && e.RequestId == request.RequestId);
        if (replay is not null)
        {
            if (replay.Payload != payload) throw new DomainError(409, "Request ID was reused with a different answer.");
            return;
        }
        Writable(a, request.Revision);
        var snapshot = Snapshot(a);
        var q = snapshot.Questions.FirstOrDefault(q => q.Id == request.QuestionId) ?? throw new DomainError(400, "Question is not in this attempt.");
        if (snapshot.Blueprint.LockSections && a.Mode == AssessmentMode.Mock && Section(q) != Sections(a)[a.SectionIndex]) throw new DomainError(409, "This section is locked.");
        var feedback = Json.Read<Dictionary<string, Grade>>(a.FeedbackJson);
        if (feedback.ContainsKey(q.Id)) throw new DomainError(409, "This answer was checked and is locked.");
        if (Grader.Validate(q, request.Answer) is { } error) throw new DomainError(400, error);
        if (request.Check && a.Mode != AssessmentMode.Learn) throw new DomainError(400, "Mock answers are released after submission.");
        var answers = Answers(a);
        answers[q.Id] = request.Answer;
        a.AnswersJson = Json.Write(answers);
        if (request.Check) { feedback[q.Id] = Grader.Score(q, request.Answer); a.FeedbackJson = Json.Write(feedback); }
        a.Revision++;
        db.Responses.Add(new() { AttemptId = a.Id, RequestId = request.RequestId, Payload = payload });
        await db.SaveChangesAsync();
    }
    public async Task Submit(Attempt a, int revision)
    {
        if (a.Status == AttemptStatus.Completed) return;
        Writable(a, revision);
        Finish(a, false);
        await db.SaveChangesAsync();
    }
    public async Task NextSection(Attempt a, int revision)
    {
        Writable(a, revision);
        if (!Snapshot(a).Blueprint.LockSections || a.Mode != AssessmentMode.Mock || a.SectionIndex >= Sections(a).Length - 1) throw new DomainError(400, "No section transition is available.");
        a.SectionIndex++; a.Revision++;
        db.Audit.Add(new() { ActorId = a.UserId, Action = "attempt.sectionLocked", ResourceId = a.Id });
        await db.SaveChangesAsync();
    }
    private static void Writable(Attempt a, int revision)
    {
        if (a.Status != AttemptStatus.InProgress) throw new DomainError(409, "This attempt is already complete.");
        if (a.Revision != revision) throw new DomainError(409, "A newer response exists. Reload the attempt before saving.");
    }
    public void Finish(Attempt a, bool timedOut)
    {
        var s = Snapshot(a); var answers = Answers(a);
        var grades = s.Questions.Select(q => Grader.Score(q, answers.GetValueOrDefault(q.Id))).ToArray();
        a.Status = AttemptStatus.Completed; a.ActiveKey = null; a.CompletedAt = Now; a.TimedOut = timedOut;
        a.Earned = grades.Sum(g => g.Earned); a.Possible = grades.Sum(g => g.Possible);
        a.CorrectPercent = grades.Count(g => g.FullyCorrect) * 100m / grades.Length;
        a.Eligible = a.Mode == AssessmentMode.Mock && !timedOut && a.FreshPercent >= s.Readiness.MinimumFreshPercent;
        a.Revision++;
        db.Audit.Add(new() { ActorId = a.UserId, Action = timedOut ? "attempt.expired" : "attempt.submitted", ResourceId = a.Id });
    }

    public AttemptView View(Attempt a)
    {
        var s = Snapshot(a); var answers = Answers(a);
        var complete = a.Status == AttemptStatus.Completed;
        var results = complete ? s.Questions.ToDictionary(q => q.Id, q => Grader.Score(q, answers.GetValueOrDefault(q.Id))) : null;
        var summary = complete ? new AttemptResultSummary(a.Earned, a.Possible,
            a.Possible == 0 ? 0 : a.Earned * 100 / a.Possible, a.CorrectPercent, a.Eligible, a.FreshPercent) : null;
        return new AttemptView(a.Id, a.PackId, s.Title, s.Version, a.Mode, a.Size, a.Status, a.Revision,
            a.StartedAt, a.Deadline, a.CompletedAt, a.SectionIndex, Sections(a),
            s.Blueprint.LockSections && a.Mode == AssessmentMode.Mock, a.TimedOut, a.Focus,
            s.Questions.Select(DeliveryQuestion.From).ToArray(), s.Scenarios, s.Objectives, answers,
            Json.Read<Dictionary<string, Grade>>(a.FeedbackJson), Now, results, summary);
    }
}
