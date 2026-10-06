using System.Text.Json;

namespace LearnForge.Core;

// One authored question. Each kind uses only its own answer fields; ExamSourceAdapter rejects the rest.
// Answer and Answers are JSON values so numeric keys can be written as numbers.
public sealed record ExamQuestion(string Id, QuestionKind Kind, string Prompt, string Explanation, string? Domain = null,
    string[]? Domains = null, string? CaseStudy = null, string? Family = null, decimal Weight = 1, ScoringPolicy? Scoring = null,
    JsonElement? Answer = null, JsonElement[]? Answers = null, string[]? Distractors = null, string[]? Steps = null,
    ExamPair[]? Pairs = null, string[]? ExtraMatches = null, ExamBlank[]? Blanks = null, decimal? Tolerance = null,
    CodeSample? Code = null);
