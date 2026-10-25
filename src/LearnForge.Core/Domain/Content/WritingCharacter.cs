namespace LearnForge.Core;

// Stroke geometry is versioned with the pack; it is never fetched from a third-party CDN.
public sealed record WritingCharacter(string Id, string Glyph, string Pinyin, string Meaning,
    string LessonId, string[] ObjectiveIds, string Components, string Tip, WritingWord[] Words,
    string Sentence, string SentencePinyin, string Translation, string Glossary,
    string AudioUrl, string AudioTranscript, WritingStrokeData StrokeData, string Language = "zh-Hans");
