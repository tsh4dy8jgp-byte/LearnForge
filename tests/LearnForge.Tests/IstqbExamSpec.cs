using LearnForge.Core;

namespace LearnForge.Tests;

// Independently transcribed from ISTQB Exam Structures and Rules Tables v1.19 (CTFL pp. 3-5, CT-AI v2.0 p. 15,
// CT-GenAI p. 25), not inferred from the authored banks. Groups list objective numbers without the pack prefix.
public sealed record IstqbExamSpec(
    string PackId,
    string Prefix,
    int Lessons,
    int Questions,
    int SyllabusObjectives,
    string[] Papers,
    int PassPoints,
    int TotalPoints,
    int[] ChapterCounts,
    int[] ChapterPoints,
    int[] LevelCounts,
    int K3Points,
    int SharedFamilyExtras,
    (string Objectives, int Count, KnowledgeLevel Level)[] Groups)
{
    private const KnowledgeLevel K1 = KnowledgeLevel.K1, K2 = KnowledgeLevel.K2, K3 = KnowledgeLevel.K3;

    public static readonly IstqbExamSpec Ctfl4 = new("istqb-ctfl-4", "fl-", 24, 366, 64, ["a", "b", "c", "d", "e", "f", "g"],
        26, 40, [8, 6, 4, 11, 9, 2], [8, 6, 4, 11, 9, 2], [8, 24, 8], 1, 46,
    [
        ("1.1.1 1.2.2", 1, K1), ("1.5.2", 1, K1),
        ("1.1.2 1.2.1 1.2.3", 1, K2), ("1.3.1", 1, K2),
        ("1.4.1 1.4.2 1.4.3 1.4.4 1.4.5", 3, K2), ("1.5.1 1.5.3", 1, K2),
        ("2.1.2", 1, K1), ("2.1.3", 1, K1),
        ("2.2.1 2.2.2", 1, K2), ("2.2.3 2.3.1", 1, K2),
        ("2.1.1 2.1.6", 1, K2), ("2.1.4 2.1.5", 1, K2),
        ("3.1.1 3.2.1 3.2.3 3.2.5", 2, K1),
        ("3.1.2 3.1.3", 1, K2), ("3.2.2 3.2.4", 1, K2),
        ("4.1.1", 1, K2), ("4.3.1 4.3.2 4.3.3", 2, K2),
        ("4.4.1 4.4.2 4.4.3", 2, K2), ("4.5.1 4.5.2", 1, K2),
        ("4.2.1 4.2.2 4.2.3 4.2.4 4.5.3", 5, K3),
        ("5.1.2 5.1.6 5.2.1 5.3.1", 1, K1),
        ("5.1.1 5.1.3", 1, K2), ("5.1.7", 1, K2),
        ("5.2.2 5.2.3 5.2.4", 1, K2), ("5.3.2 5.3.3", 1, K2),
        ("5.4.1", 1, K2), ("5.1.4 5.1.5 5.5.1", 3, K3),
        ("6.1.1", 1, K2), ("6.2.1", 1, K1)
    ]);

    public static readonly IstqbExamSpec CtAi2 = new("istqb-ct-ai-2", "ai-", 18, 203, 43, ["a", "b", "c", "d"],
        29, 44, [6, 3, 7, 7, 6, 9, 2], [6, 3, 8, 8, 7, 10, 2], [0, 36, 4], 2, 0,
    [
        ("1.1.1 1.1.2 1.1.3 1.1.4 1.1.5 1.1.6 1.1.7 1.1.8", 6, K2),
        ("2.1.1", 1, K2), ("2.1.2", 1, K2), ("2.2.1", 1, K2),
        ("3.1.1 3.1.2 3.1.4 3.2.1 3.2.3 3.4.1 3.4.3", 6, K2), ("3.3.1", 1, K3),
        ("4.1.1", 1, K2), ("4.1.2", 1, K2), ("4.1.3", 1, K2), ("4.2.1", 1, K2), ("4.2.2", 1, K3), ("4.3.1", 1, K2), ("4.3.2", 1, K2),
        ("5.1.1", 1, K2), ("5.1.2", 1, K2), ("5.1.3", 1, K2), ("5.1.4", 1, K2), ("5.1.5", 1, K3), ("5.1.6", 1, K2),
        ("6.1.1", 1, K2), ("6.1.2", 1, K2), ("6.1.3", 1, K2), ("6.1.4", 1, K2), ("6.1.5", 1, K3),
        ("6.1.7", 1, K2), ("6.1.8", 1, K2), ("6.1.9", 1, K2), ("6.1.10", 1, K2),
        ("7.1.1", 1, K2), ("7.1.2", 1, K2)
    ]);

    public static readonly IstqbExamSpec CtGenAi1 = new("istqb-ct-genai-1", "genai-", 16, 197, 37, ["a", "b", "c", "d"],
        30, 46, [7, 11, 10, 5, 7], [7, 16, 11, 5, 7], [8, 26, 6], 2, 0,
    [
        ("1.1.1", 1, K1), ("1.1.2", 2, K2), ("1.1.3", 1, K2), ("1.1.4", 1, K2), ("1.2.1", 1, K2), ("1.2.2", 1, K2),
        ("2.1.1", 2, K2), ("2.1.2", 1, K2), ("2.1.3", 1, K2),
        ("2.2.1", 1, K3), ("2.2.2", 1, K3), ("2.2.3", 1, K3), ("2.2.4", 1, K3), ("2.2.5", 1, K3),
        ("2.3.1", 1, K2), ("2.3.2", 1, K2),
        ("3.1.1", 1, K1), ("3.1.2", 1, K3), ("3.1.3", 1, K2), ("3.1.4", 1, K1),
        ("3.2.1", 1, K2), ("3.2.2", 2, K2), ("3.2.3", 1, K2), ("3.3.1", 1, K2), ("3.4.1", 1, K1),
        ("4.1.1", 1, K2), ("4.1.2", 1, K2), ("4.1.3", 1, K2), ("4.2.1", 1, K2), ("4.2.2", 1, K2),
        ("5.1.1", 1, K1), ("5.1.2", 1, K2), ("5.1.3", 1, K2), ("5.1.4", 1, K1),
        ("5.2.1", 1, K2), ("5.2.2", 1, K1), ("5.2.3", 1, K1)
    ]);

    public static IstqbExamSpec For(string packId) => new[] { Ctfl4, CtAi2, CtGenAi1 }.Single(s => s.PackId == packId);

    public string[] ObjectiveIds(string objectives) => objectives.Split(' ').Select(id => Prefix + id).ToArray();

    public decimal Points(KnowledgeLevel? level) => level == KnowledgeLevel.K3 ? K3Points : 1;
}
