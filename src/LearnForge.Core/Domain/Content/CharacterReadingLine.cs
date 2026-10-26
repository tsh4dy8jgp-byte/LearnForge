namespace LearnForge.Core;

// Punctuation tokens can omit pronunciation and meaning; word tokens supply both.
public sealed record CharacterReadingLine(WritingWord[] Words, string Translation);
