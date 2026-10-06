namespace LearnForge.Core;

// A dropdown blank: the lead-in text, its correct choice and the wrong choices.
public sealed record ExamBlank(string Text, string Answer, string[] Distractors);
