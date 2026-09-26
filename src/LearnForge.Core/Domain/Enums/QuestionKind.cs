namespace LearnForge.Core;

// Numeric and CodeOutput take a typed text response (Answer.Text) instead of option IDs or slots.
public enum QuestionKind { Single, Multiple, Matching, Dropdown, Sequence, Numeric, CodeOutput }
