namespace LearnForge.Api.Contracts.Learning;

public sealed record CourseProgressDto(
    string PackId,
    string Title,
    string Version,
    EnrollmentStatus? Enrollment,
    int LessonCount,
    string[] CompletedLessons,
    string[] RevisedLessons,
    ObjectiveMasteryDto[] Objectives,
    NextStepDto[] NextSteps,
    CourseGoalStatusDto Goal,
    DateTime? LastActivityAt,
    PackCapabilities Capabilities);
