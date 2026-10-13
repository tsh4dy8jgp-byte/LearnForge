namespace LearnForge.Core;

public sealed record ReviewSchedule(int Stage, DateTime DueAt, int SchedulerVersion = 1);
