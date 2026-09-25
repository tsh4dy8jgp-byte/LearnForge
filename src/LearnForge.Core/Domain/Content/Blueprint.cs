namespace LearnForge.Core;

public sealed record Blueprint(string Id, string Title, int Count, int Minutes, AssessmentSize Size,
    string[] ObjectiveIds, QuestionKind[] RequiredKinds, bool LockSections = false);
