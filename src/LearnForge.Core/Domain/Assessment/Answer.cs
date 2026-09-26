namespace LearnForge.Core;

// Selection kinds use Selected or Slots; Numeric and CodeOutput use Text.
public sealed record Answer(string[] Selected, Dictionary<string, string> Slots, string? Text = null);
