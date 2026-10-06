using System.Security.Cryptography;
using System.Text;
using LearnForge.Core;
using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Services.Attempts;

public sealed class AttemptService(AppDb db, TimeProvider clock, ReleaseCache releases, LearningRecordService learning)
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
            await Finish(a, true);
            await db.SaveChangesAsync();
        }
    }

    public async Task<Attempt> Start(string user, StartRequest request)
    {
        if (!Guid.TryParse(request.RequestId, out _)) throw new DomainError(400, "Invalid session settings.");
        if (request.Mode == AssessmentMode.Mock && request.Focus is not null) throw new DomainError(400, "Focused practice uses learning mode.");
        if ((request.Focus == PracticeFocus.Objective) != (request.ObjectiveId is not null)) throw new DomainError(400, "Objective practice needs exactly one objective.");
        var replay = await db.Attempts.SingleOrDefaultAsync(a => a.UserId == user && a.StartKey == request.RequestId);
        if (replay is not null)
        {
            if (replay.PackId != request.PackId || replay.Mode != request.Mode || replay.Focus != request.Focus
                || replay.FocusObjectiveId != request.ObjectiveId || Snapshot(replay).Blueprint.Id != request.BlueprintId)
                throw new DomainError(409, "This request ID already belongs to another session.");
            await Expire(replay);
            return replay;
        }
        var release = await releases.Latest(db, request.PackId) ?? throw new DomainError(404, "Course not found.");
        var active = await db.Attempts.SingleOrDefaultAsync(a => a.ActiveKey == user + ":" + request.PackId);
        if (active is not null)
        {
            await Expire(active);
            if (active.Status == AttemptStatus.InProgress) throw new DomainError(409, $"Resume or finish your existing session: {active.Id}");
        }
        var pack = release.Pack;
        if ((request.Mode == AssessmentMode.Mock && !pack.Features.Assessments) ||
            (request.Mode == AssessmentMode.Learn && !pack.Features.Practice))
            throw new DomainError(400, "This pack does not support the requested session mode.");
        var blueprint = pack.Blueprints.FirstOrDefault(b => b.Id == request.BlueprintId) ?? throw new DomainError(400, "Unknown blueprint.");
        // The ledger holds every question of every finished attempt, so it is also the exposure history.
        var history = await db.Evidence.AsNoTracking().Where(e => e.UserId == user && e.PackId == request.PackId)
            .Select(e => new EvidenceRow(e.Id, e.QuestionId, e.FamilyId, e.ObjectiveIds, e.Source, e.Answered, e.FullyCorrect, e.At)).ToListAsync();
        var seen = history.Select(e => e.FamilyId).ToHashSet();
        var chosen = request.Focus is { } focus
            ? Focused(pack, blueprint, request, focus, history, seen)
            : Compose(pack, blueprint, seen, request.RequestId);
        chosen = chosen.OrderBy(q => q.ScenarioId is null ? 0 : 1).ThenBy(q => q.ScenarioId).Select(Shuffle).ToArray();
        var snapshot = new AttemptSnapshot(pack.Title, pack.Version, pack.Objectives, pack.Scenarios, blueprint, chosen, pack.Readiness ?? new(), pack.Goal);
        var attempt = new Attempt
        {
            UserId = user, PackReleaseId = release.ReleaseId, PackId = pack.Id, StartKey = request.RequestId,
            ActiveKey = user + ":" + pack.Id, Mode = request.Mode, Size = blueprint.Size, Focus = request.Focus,
            FocusObjectiveId = request.ObjectiveId, StartedAt = Now, Deadline = Now.AddMinutes(blueprint.Minutes),
            SnapshotJson = Json.Write(snapshot), FreshPercent = chosen.Count(q => !seen.Contains(q.FamilyId)) * 100m / chosen.Length
        };
        db.Attempts.Add(attempt);
        db.Audit.Add(new() { ActorId = user, Action = "attempt.started", ResourceId = attempt.Id });
        await learning.Touch(user, pack.Id);
        await db.SaveChangesAsync();
        return attempt;
    }

    // Validation proves every blueprint can be composed, and the constraints do not depend on the seed, but the bounded
    // search can still run out of steps for an unlucky seed. A new request ID is a new seed, so ask the learner to retry.
    private static Question[] Compose(Pack pack, Blueprint blueprint, HashSet<string> seen, string seed)
    {
        try { return ExamComposer.Compose(pack, blueprint, seen, seed); }
        catch (InvalidOperationException) { throw new DomainError(409, "This session could not be assembled. Start again; if it keeps failing, contact the course publisher."); }
    }

    private sealed record EvidenceRow(long Id, string QuestionId, string FamilyId, string[] ObjectiveIds,
        EvidenceSource Source, bool Answered, bool FullyCorrect, DateTime At);

    private Question[] Focused(Pack pack, Blueprint blueprint, StartRequest request, PracticeFocus focus, List<EvidenceRow> history, HashSet<string> seen)
    {
        var countable = history.Where(e => EvidenceWriter.Countable(e.Source, e.Answered)).ToArray();
        // A question counts as missed when its most recent countable answer was not fully correct.
        var missed = countable.GroupBy(e => e.QuestionId)
            .Where(g => !g.OrderByDescending(e => e.At).ThenByDescending(e => e.Id).First().FullyCorrect)
            .Select(g => g.Key).ToHashSet();
        var standalone = pack.Questions.Where(q => q.ScenarioId == null);
        IEnumerable<Question> pool;
        Func<Question, int> tier;
        switch (focus)
        {
            case PracticeFocus.Mistakes:
                pool = standalone.Where(q => missed.Contains(q.Id));
                tier = _ => 0;
                break;
            case PracticeFocus.Weak:
                var mastery = MasteryEvaluator.Evaluate(pack.Objectives,
                    countable.Select(e => new MasteryEvidence(e.Id, e.FamilyId, e.ObjectiveIds, e.FullyCorrect, e.Source == EvidenceSource.MockSubmission, e.At)),
                    pack.Mastery ?? new(), Now);
                var weak = mastery.Where(m => m.Considered > 0).OrderBy(m => (decimal)m.Correct / m.Considered)
                    .ThenBy(m => m.ObjectiveId, StringComparer.Ordinal).Take(2).Select(m => m.ObjectiveId).ToHashSet();
                pool = standalone.Where(q => q.ObjectiveIds.Any(weak.Contains));
                tier = q => seen.Contains(q.FamilyId) ? 1 : 0;
                break;
            default:
                if (!pack.Objectives.Any(o => o.Id == request.ObjectiveId)) throw new DomainError(400, "Unknown objective.");
                pool = standalone.Where(q => q.ObjectiveIds.Contains(request.ObjectiveId!));
                // Unseen families first, then earlier mistakes, then everything else.
                tier = q => !seen.Contains(q.FamilyId) ? 0 : missed.Contains(q.Id) ? 1 : 2;
                break;
        }
        var chosen = pool.OrderBy(tier).ThenBy(q => SeededOrder(request.RequestId, q.Id), StringComparer.Ordinal).Take(blueprint.Count).ToArray();
        if (chosen.Length == 0) throw new DomainError(409, focus == PracticeFocus.Objective
            ? "This objective has no standalone practice questions yet; its questions are part of case studies. Try a balanced session."
            : "Complete an assessment first to build targeted practice evidence.");
        return chosen;
    }

    // Deterministic per request, like ExamComposer, so a replayed start selects the same questions.
    private static string SeededOrder(string seed, string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed + id)));

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
        if (request.Check)
        {
            var grade = Grader.Score(q, request.Answer);
            feedback[q.Id] = grade;
            a.FeedbackJson = Json.Write(feedback);
            db.Evidence.Add(EvidenceWriter.Record(a, q, request.Answer, grade, EvidenceSource.LearningCheck, Now));
        }
        a.Revision++;
        db.Responses.Add(new() { AttemptId = a.Id, RequestId = request.RequestId, Payload = payload });
        await db.SaveChangesAsync();
    }
    public async Task Submit(Attempt a, int revision)
    {
        if (a.Status == AttemptStatus.Completed) return;
        Writable(a, revision);
        await Finish(a, false);
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
    public async Task Finish(Attempt a, bool timedOut)
    {
        var s = Snapshot(a); var answers = Answers(a);
        var grades = s.Questions.Select(q => Grader.Score(q, answers.GetValueOrDefault(q.Id))).ToArray();
        var completedAt = Now;
        a.Status = AttemptStatus.Completed; a.ActiveKey = null; a.CompletedAt = completedAt; a.TimedOut = timedOut;
        a.Earned = grades.Sum(g => g.Earned); a.Possible = grades.Sum(g => g.Possible);
        a.CorrectPercent = grades.Count(g => g.FullyCorrect) * 100m / grades.Length;
        a.Eligible = s.Goal == CourseGoal.Readiness && a.Mode == AssessmentMode.Mock && !timedOut && a.FreshPercent >= s.Readiness.MinimumFreshPercent;
        a.Revision++;
        db.Audit.Add(new() { ActorId = a.UserId, Action = timedOut ? "attempt.expired" : "attempt.submitted", ResourceId = a.Id });
        // Rows are saved with the transition, so the Revision concurrency token covers both.
        // Any question without a row gets one, including checks made before the ledger existed.
        var feedback = Json.Read<Dictionary<string, Grade>>(a.FeedbackJson);
        var recorded = (await db.Evidence.Where(e => e.AttemptId == a.Id).Select(e => e.QuestionId).ToListAsync()).ToHashSet();
        foreach (var (question, grade) in s.Questions.Zip(grades))
        {
            if (recorded.Contains(question.Id)) continue;
            var source = feedback.ContainsKey(question.Id) ? EvidenceSource.LearningCheck
                : a.Mode == AssessmentMode.Mock ? EvidenceSource.MockSubmission : EvidenceSource.LearningSubmission;
            db.Evidence.Add(EvidenceWriter.Record(a, question, answers.GetValueOrDefault(question.Id), grade, source, completedAt));
        }
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
            Json.Read<Dictionary<string, Grade>>(a.FeedbackJson), Now, results, summary, s.Goal);
    }
}
