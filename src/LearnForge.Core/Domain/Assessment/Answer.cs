namespace LearnForge.Core;

// Selection kinds use Selected or Slots; Numeric, CodeOutput and ShortAnswer use Text.
public sealed record Answer(string[] Selected, Dictionary<string, string> Slots, string? Text = null);
