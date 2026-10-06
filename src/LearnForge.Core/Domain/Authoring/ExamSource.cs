namespace LearnForge.Core;

// The compact "exam/1" source: plain-text answers and distractors that ContentEngine expands into an exam pack.
// Option IDs, selection counts, families, scoring and mock blueprints are derived, so authors (and LLMs) cannot leak keys through them.
public sealed record ExamSource(string Format, string Id, string Version, string Title, string Description, string License,
    ExamDomain[] Domains, ExamMocks Mocks, ExamQuestion[] Questions, Scenario[]? CaseStudies = null,
    SourceReference[]? Sources = null, ReadinessPolicy? Readiness = null, CourseGoal Goal = CourseGoal.Readiness,
    MasteryPolicy? Mastery = null);
