namespace LearnForge.Core;

public sealed record Pack(int SchemaVersion, string Id, string Version, string Title, string Description,
    string License, Objective[] Objectives, Lesson[] Lessons, Question[] Questions, Scenario[] Scenarios,
    Blueprint[] Blueprints, ReadinessPolicy Readiness, SourceReference[] Sources);
