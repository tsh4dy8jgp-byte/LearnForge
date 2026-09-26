namespace LearnForge.Core;

// Code is the program a CodeOutput question asks about; no other kind carries one.
public sealed record Question(string Id, string FamilyId, QuestionKind Kind, string Prompt, string[] ObjectiveIds,
    Option[] Options, Slot[] Slots, int SelectCount, bool Reuse, Grading Grading, string Explanation,
    string? ScenarioId = null, decimal Weight = 1, CodeSample? Code = null);
