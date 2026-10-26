namespace LearnForge.Core;

// NoteIds and CharacterIds have matching positions: each note asks for that character.
public sealed record CharacterContrast(string Id, string[] CharacterIds, string[] NoteIds, string Explanation);
