namespace LearnForge.Core;

// Optional, pack-owned teaching activities. Review continues to use the flashcard scheduler.
public sealed record CharacterStudy(string RecognitionDeckId, string ConfusionDeckId,
    CharacterContrast[] Contrasts, CharacterReading[] Readings);
