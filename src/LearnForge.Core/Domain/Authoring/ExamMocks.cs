namespace LearnForge.Core;

// Becomes the "short" and optional "full" blueprints over every domain, weighted when the domains carry weights.
public sealed record ExamMocks(ExamMock Short, ExamMock? Full = null, bool LockCaseStudies = false, QuestionKind[]? RequiredKinds = null);
