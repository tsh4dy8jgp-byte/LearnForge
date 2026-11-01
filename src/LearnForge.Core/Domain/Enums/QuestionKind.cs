namespace LearnForge.Core;

// Numeric, CodeOutput and ShortAnswer take a typed text response (Answer.Text) instead of option IDs or slots.
public enum QuestionKind
{
    Single,
    Multiple,
    Matching,
    Dropdown,
    Sequence,
    Numeric,
    CodeOutput,
    ShortAnswer
}
