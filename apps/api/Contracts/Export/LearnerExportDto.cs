namespace LearnForge.Api.Contracts.Export;

public sealed record LearnerExportDto(DateTime ExportedAt, DashboardDto Dashboard, AttemptView[] Attempts,
    EnrollmentExportDto[] Enrollments, LessonProgressExportDto[] LessonProgress, EvidenceExportDto[] Evidence, DraftDto[] Drafts);
