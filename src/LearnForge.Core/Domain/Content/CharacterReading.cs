namespace LearnForge.Core;

public sealed record CharacterReading(string Id, string Title, string[] RequiredCharacterIds,
    CharacterReadingLine[] Lines, string Note, WritingWord[] SupportWords);
