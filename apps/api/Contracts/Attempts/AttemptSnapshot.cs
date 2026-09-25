namespace LearnForge.Api.Contracts.Attempts;

public sealed record AttemptSnapshot(string Title, string Version, Objective[] Objectives, Scenario[] Scenarios,
    Blueprint Blueprint, Question[] Questions, ReadinessPolicy Readiness);
