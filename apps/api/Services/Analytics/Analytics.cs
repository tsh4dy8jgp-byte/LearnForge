using Microsoft.EntityFrameworkCore;

namespace LearnForge.Api.Services.Analytics;

public static class Analytics
{
    public static AttemptSummaryDto Summary(Attempt a)
    {
        var snapshot = AttemptService.Snapshot(a);
        return new(a.Id, a.PackId, snapshot.Title, snapshot.Version, a.Size, a.Mode, a.Status,
            a.StartedAt, a.CompletedAt, a.Deadline, a.CorrectPercent, a.Eligible, a.TimedOut,
            a.FreshPercent, a.Focus, a.Possible == 0 ? 0 : a.Earned * 100 / a.Possible, snapshot.Questions.Length);
    }

    public static async Task<DashboardDto> Dashboard(AppDb db, string user, DateTime now)
    {
        var attempts = await db.Attempts.Where(a => a.UserId == user).OrderByDescending(a => a.StartedAt).ToListAsync();
        var releases = (await db.Packs.OrderByDescending(p => p.PublishedAt).ToListAsync()).DistinctBy(p => p.PackId);
        var completions = await db.LessonProgress.Where(c => c.UserId == user).ToListAsync();
        var courses = releases.Select(release => BuildCourse(release, attempts, completions, now)).ToArray();
        return new(courses, attempts.Select(Summary).ToArray(), attempts.Count(a => a.Status == AttemptStatus.Completed),
            attempts.Count(a => a.Status == AttemptStatus.InProgress), completions.Count);
    }

    private static CourseDashboardDto BuildCourse(PackRelease release, IReadOnlyCollection<Attempt> attempts,
        IReadOnlyCollection<LessonProgress> completions, DateTime now)
    {
        var pack = Json.Read<Pack>(release.ContentJson);
        var history = attempts.Where(a => a.PackReleaseId == release.Id).ToArray();
        var results = history.Where(a => a.Status == AttemptStatus.Completed).SelectMany(a =>
        {
            var answers = AttemptService.Answers(a);
            return AttemptService.Snapshot(a).Questions.Select(q => new GradedEvidence(
                q, Grader.Score(q, answers.GetValueOrDefault(q.Id)), a.Mode == AssessmentMode.Mock));
        }).ToArray();

        var objectives = pack.Objectives.Select(objective =>
        {
            var samples = results.Where(x => x.Question.ObjectiveIds.Contains(objective.Id)).ToArray();
            var independent = samples.Where(x => x.Independent).ToArray();
            return new ObjectiveDashboardDto(objective.Id, objective.Title, objective.Prerequisites,
                samples.Length, independent.Length,
                independent.Length == 0 ? null : independent.Count(x => x.Grade.FullyCorrect) * 100m / independent.Length,
                pack.Lessons.Where(l => l.ObjectiveIds.Contains(objective.Id)).Select(l => l.Id).ToArray());
        }).ToArray();

        var recommendations = objectives.OrderBy(o => o.CorrectPercent ?? -1).Take(3)
            .Select(o => new RecommendationDto(o.Id, o.Title, o.LessonIds.FirstOrDefault(),
                o.IndependentSamples == 0 ? "No independent assessment evidence yet." : $"{o.CorrectPercent:0}% fully correct across {o.IndependentSamples} mock questions."))
            .ToArray();
        var evidence = history.Where(a => a.Mode == AssessmentMode.Mock && a.Status == AttemptStatus.Completed)
            .Select(a => new ReadinessEvidence(a.Id, a.Size, a.CompletedAt!.Value, a.CorrectPercent, a.Eligible));
        return new CourseDashboardDto(pack.Id, pack.Title, pack.Version, pack.Lessons.Length,
            completions.Count(c => c.PackId == release.PackId), objectives, recommendations,
            ReadinessEvaluator.Evaluate(evidence, pack.Readiness ?? new(), now));
    }

}
