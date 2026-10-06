namespace LearnForge.Api.Contracts.Content;

// Preview uses the same safe projections as delivery; drafts never become public catalog releases.
// Warnings are quality lint (likely answer giveaways); they never block publishing.
public sealed record AuthoringPreviewDto(bool Success, string Hash, Diagnostic[] Diagnostics,
    CourseCatalogDto? Catalog, DeliveryQuestion[] Questions, Scenario[] Scenarios, Diagnostic[] Warnings);
