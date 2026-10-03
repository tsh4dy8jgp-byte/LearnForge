using Microsoft.EntityFrameworkCore;
using static LearnForge.Api.Services.Analytics.Analytics;

namespace LearnForge.Api.Services.Learning;

// Derives learner progress on read from the evidence ledger, lesson progress and enrollments.
public sealed class LearningRecordService(AppDb db, ReleaseCache releases, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<ReleaseView> Release(string packId) =>
        await releases.Latest(db, packId) ?? throw new DomainError(404, "Course not found.");

    // Records learning activity and reactivates an archived course. The caller saves.
    public async Task Touch(string user, string packId)
    {
        var enrollment = await db.Enrollments.FindAsync(user, packId);
        if (enrollment is null) db.Enrollments.Add(new() { UserId = user, PackId = packId, EnrolledAt = Now, LastActivityAt = Now });
        else { enrollment.Status = EnrollmentStatus.Active; enrollment.LastActivityAt = Now; }
    }

    public async Task SetEnrollment(string user, string packId, EnrollmentStatus status)
    {
        await Release(packId);
        var enrollment = await db.Enrollments.FindAsync(user, packId);
        if (enrollment is null) db.Enrollments.Add(new() { UserId = user, PackId = packId, Status = status, EnrolledAt = Now, LastActivityAt = Now });
        else enrollment.Status = status;
        await db.SaveChangesAsync();
    }

    public async Task CompleteLesson(string user, string packId, string lessonId)
    {
        var release = await Release(packId);
        if (!release.Pack.Features.Lessons) throw new DomainError(400, "This pack does not offer lessons.");
        if (!release.LessonHashes.TryGetValue(lessonId, out var hash)) throw new DomainError(404, "Lesson not found.");
        var progress = await db.LessonProgress.FindAsync(user, packId, lessonId);
        if (progress is null) db.LessonProgress.Add(new() { UserId = user, PackId = packId, LessonId = lessonId, CompletedAt = Now, ContentHash = hash });
        else progress.ContentHash = hash; // Marking a revised lesson again clears the "updated" flag.
        await Touch(user, packId);
        await db.SaveChangesAsync();
    }

    public async Task<CourseProgressDto> CourseProgress(string user, string packId) =>
        await Build(user, await Release(packId), await db.Enrollments.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == user && e.PackId == packId));

    public async Task<DashboardDto> Dashboard(string user)
    {
        var enrollments = await db.Enrollments.AsNoTracking().Where(e => e.UserId == user && e.Status == EnrollmentStatus.Active)
            .OrderByDescending(e => e.LastActivityAt).ToListAsync();
        var courses = new List<CourseProgressDto>();
        foreach (var enrollment in enrollments)
            if (await releases.Latest(db, enrollment.PackId) is { } release) courses.Add(await Build(user, release, enrollment));
        var recent = await db.Attempts.AsNoTracking().Where(a => a.UserId == user).OrderByDescending(a => a.StartedAt).Take(5).ToListAsync();
        // Count only lessons that still exist in each pack's latest release.
        var completedLessons = 0;
        foreach (var pack in (await db.LessonProgress.AsNoTracking().Where(p => p.UserId == user).Select(p => new { p.PackId, p.LessonId }).ToListAsync()).GroupBy(p => p.PackId))
            if (await releases.Latest(db, pack.Key) is { } release) completedLessons += pack.Count(p => release.LessonHashes.ContainsKey(p.LessonId));
        return new DashboardDto(courses.ToArray(), recent.Select(Summary).ToArray(),
            await db.Attempts.CountAsync(a => a.UserId == user && a.Status == AttemptStatus.Completed),
            await db.Attempts.CountAsync(a => a.UserId == user && a.Status == AttemptStatus.InProgress),
            completedLessons);
    }

    public async Task<MasteryEvidence[]> Evidence(string user, string packId) =>
        (await db.Evidence.AsNoTracking().Where(e => e.UserId == user && e.PackId == packId)
            .Select(e => new { e.Id, e.FamilyId, e.ObjectiveIds, e.Source, e.Answered, e.FullyCorrect, e.At }).ToListAsync())
        .Where(e => EvidenceWriter.Countable(e.Source, e.Answered))
        .Select(e => new MasteryEvidence(e.Id, e.FamilyId, e.ObjectiveIds, e.FullyCorrect, e.Source == EvidenceSource.MockSubmission, e.At))
        .ToArray();

    private async Task<CourseProgressDto> Build(string user, ReleaseView release, Enrollment? enrollment)
    {
        var pack = release.Pack;
        // Progress is keyed by stable lesson IDs; lessons removed from the current release are ignored.
        var progress = (await db.LessonProgress.AsNoTracking().Where(p => p.UserId == user && p.PackId == pack.Id).ToListAsync())
            .Where(p => release.LessonHashes.ContainsKey(p.LessonId)).ToArray();
        var completed = progress.Select(p => p.LessonId).ToHashSet();
        var revised = progress.Where(p => p.ContentHash is not null && p.ContentHash != release.LessonHashes[p.LessonId]).Select(p => p.LessonId).ToArray();
        var mastery = MasteryEvaluator.Evaluate(pack.Objectives, await Evidence(user, pack.Id), pack.Mastery ?? new(), Now);
        ReadinessResult? readiness = null;
        if (pack.Goal == CourseGoal.Readiness)
        {
            // Readiness stays release-scoped and mock-only.
            var mocks = await db.Attempts.AsNoTracking()
                .Where(a => a.UserId == user && a.PackReleaseId == release.ReleaseId && a.Mode == AssessmentMode.Mock && a.Status == AttemptStatus.Completed)
                .Select(a => new { a.Id, a.Size, a.CompletedAt, a.CorrectPercent, a.Eligible }).ToListAsync();
            readiness = ReadinessEvaluator.Evaluate(mocks.Select(a => new ReadinessEvidence(a.Id, a.Size, a.CompletedAt!.Value, a.CorrectPercent, a.Eligible)), pack.Readiness ?? new(), Now);
        }
        var proficient = mastery.Count(m => m.State == MasteryState.Proficient);
        var met = pack.Goal switch
        {
            CourseGoal.Mastery => proficient == pack.Objectives.Length,
            CourseGoal.Completion => completed.Count == pack.Lessons.Length,
            _ => readiness?.Ready == true,
        };
        var objectives = pack.Objectives.ToDictionary(o => o.Id);
        var lessons = pack.Lessons.ToDictionary(l => l.Id);
        return new CourseProgressDto(pack.Id, pack.Title, pack.Version, enrollment?.Status, pack.Lessons.Length,
            completed.ToArray(), revised,
            mastery.Select(m => new ObjectiveMasteryDto(m.ObjectiveId, objectives[m.ObjectiveId].Title, objectives[m.ObjectiveId].Prerequisites,
                m.State, m.Correct, m.Considered, m.Independent, m.LastEvidenceAt, m.ReviewDue,
                pack.Lessons.Where(l => l.ObjectiveIds.Contains(m.ObjectiveId)).Select(l => l.Id).ToArray(),
                pack.Features.Practice && pack.Questions.Any(q => q.ScenarioId is null && q.ObjectiveIds.Contains(m.ObjectiveId)))).ToArray(),
            NextStepPlanner.Plan(pack, mastery, completed, readiness).Select(s => new NextStepDto(s.Kind, s.Reason,
                s.ObjectiveId, s.ObjectiveId is null ? null : objectives[s.ObjectiveId].Title,
                s.LessonId, s.LessonId is null ? null : lessons[s.LessonId].Title, s.BlueprintId)).ToArray(),
            new CourseGoalStatusDto(pack.Goal, met, proficient, pack.Objectives.Length, completed.Count, pack.Lessons.Length, readiness),
            enrollment?.LastActivityAt, pack.Features);
    }
}
