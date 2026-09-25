using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Services.Learning;

// Rebuilds ledger rows and enrollments for data created before the ledger existed. It runs in the
// migration step and is safe to repeat or to run on several instances at once. In-progress attempts
// need nothing: AttemptService.Finish records any question without a row when the attempt completes.
public static class EvidenceBackfill
{
    // Completed attempts that predate the ledger. Nonzero after an upgrade means the migration step (--migrate) has not run.
    public static Task<int> PendingAsync(AppDb db, CancellationToken cancellationToken = default) =>
        Pending(db).CountAsync(cancellationToken);

    private static IQueryable<Attempt> Pending(AppDb db) =>
        db.Attempts.Where(a => a.Status == AttemptStatus.Completed && !db.Evidence.Any(e => e.AttemptId == a.Id));

    public static async Task<int> RunAsync(AppDb db, CancellationToken cancellationToken = default)
    {
        var pending = await Pending(db).Select(a => a.Id).ToListAsync(cancellationToken);
        var written = 0;
        foreach (var id in pending)
        {
            db.ChangeTracker.Clear();
            var attempt = await db.Attempts.AsNoTracking().SingleAsync(a => a.Id == id, cancellationToken);
            var snapshot = AttemptService.Snapshot(attempt);
            var answers = AttemptService.Answers(attempt);
            var feedback = Json.Read<Dictionary<string, Grade>>(attempt.FeedbackJson);
            foreach (var question in snapshot.Questions)
            {
                var answer = answers.GetValueOrDefault(question.Id);
                var source = feedback.ContainsKey(question.Id) ? EvidenceSource.LearningCheck
                    : attempt.Mode == AssessmentMode.Mock ? EvidenceSource.MockSubmission : EvidenceSource.LearningSubmission;
                db.Evidence.Add(EvidenceWriter.Record(attempt, question, answer, Grader.Score(question, answer), source, attempt.CompletedAt ?? attempt.StartedAt));
            }
            try { await db.SaveChangesAsync(cancellationToken); written++; }
            catch (DbUpdateException) { /* Another instance backfilled this attempt first. */ }
        }
        db.ChangeTracker.Clear();
        var active = (await db.Attempts.Select(a => new { a.UserId, a.PackId }).Distinct().ToListAsync(cancellationToken))
            .Concat(await db.LessonProgress.Select(p => new { p.UserId, p.PackId }).Distinct().ToListAsync(cancellationToken))
            .Distinct();
        var enrolled = (await db.Enrollments.Select(e => new { e.UserId, e.PackId }).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var pair in active.Where(p => !enrolled.Contains(p)))
        {
            db.Enrollments.Add(new() { UserId = pair.UserId, PackId = pair.PackId });
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException) { /* Enrolled concurrently. */ }
            db.ChangeTracker.Clear();
        }
        return written;
    }
}
