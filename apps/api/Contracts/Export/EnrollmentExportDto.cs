namespace LearnForge.Api.Contracts.Export;

public sealed record EnrollmentExportDto(string PackId, EnrollmentStatus Status, DateTime EnrolledAt, DateTime LastActivityAt);
