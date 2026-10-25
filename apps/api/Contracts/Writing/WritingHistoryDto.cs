namespace LearnForge.Api.Contracts.Writing;

public sealed record WritingHistoryDto(WritingSummaryDto[] Courses, WritingSessionDto[] Sessions);
