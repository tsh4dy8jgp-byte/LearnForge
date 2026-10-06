# Authoring guide

Every content release must follow the [accessible content and authoring design](accessibility/content-and-authoring.md). Current text packs need manual accessibility review; the richer media/schema checks described there are planned additions, not fields accepted by today's compiler.

## Build a pack

For an exam-prep bank, the quickest route is the compact [exam/1 format](#compact-exam-format-exam1): copy [`docs/examples/exam-sample.json`](examples/exam-sample.json), or generate a 100–150-question bank with the [exam question generator prompt](prompts/exam-question-generator.md). For courses with lessons, or full control over every field, start with a copy of the complete starter:

~~~sh
dotnet run --project tools/cli -- init packs/my-subject.json
dotnet run --project tools/cli -- check packs/my-subject.json
~~~

Use lowercase IDs with letters, numbers, dots, underscores or hyphens. IDs are stable API and analytics identifiers. Increase the semantic version whenever published content changes.

Every objective needs at least one lesson and one question. Every lesson and question must link to existing objectives. Every question needs an explanation and a familyId. Use the same family ID for genuinely equivalent variants; use a new family for a new competency or prompt pattern.

A pack declares an optional `goal`: `readiness` (the default, for exam preparation), `mastery` (every objective proficient) or `completion` (every lesson read). `readiness` may be omitted for non-exam packs. The optional `mastery` object tunes the mastery rule: `window` (default 5), `minimumEvidence` (default 3), `proficientPercent` (default 80) and `reviewAfterDays` (default 60), with 1 ≤ minimumEvidence ≤ window ≤ 20, proficientPercent 50–100 and reviewAfterDays 1–365. A mastery goal requires at least `minimumEvidence` question families per objective, and the compiler rejects packs that cannot reach it.

Keep lesson IDs stable. Learner progress is keyed by lesson ID, and changing a lesson's content flags it as updated for learners who completed it.

## Question contracts

Single questions have one correct option and selectCount 1. Multiple questions have exactly selectCount correct options and at least one distractor. Sequence questions use a complete permutation as grading.correct. Matching questions use slots, a shared options bank and grading.matches; set reuse true when a token may fill multiple slots. Dropdown questions put their option banks on each slot and also use grading.matches.

Numeric and codeOutput questions take a typed answer instead of a selection. They have no options, slots or matches, selectCount 0 and exact scoring. List 1–20 accepted answers in grading.correct.
- **Numeric:** each key is an invariant-culture number such as `4` or `-2.5`. The optional grading.tolerance sets the allowed absolute difference and defaults to 0.
- **codeOutput:** the question carries `code: { language, source }`. The program is shown as text and never executed. A response matches an accepted output when both are equal after trimming, normalizing line endings and collapsing runs of spaces or tabs.

Question kinds are single, multiple, sequence, matching, dropdown, numeric and codeOutput. The Angular player provides drag/drop plus click or keyboard alternatives for matching and ordering. The server remains authoritative for all contracts.

## Templates

The optional top-level templates object holds JSON fragments. A question or block can contain a $use property and values. A whole-string placeholder such as {{prompt}} inserts a typed value; text containing a placeholder replaces it as a string. Keep values small and explicit. Recursive references, large expansions, unknown placeholders and unknown properties fail validation.

## Exams

A blueprint names its count, duration, short/full size, objective IDs and requiredKinds. The optional `objectiveWeights` object gives each blueprint objective a positive weight (at most 1000), for example an exam's published domain percentages. Each objective then receives its largest-remainder share of the count, within one question either way and never fewer than one. A question counts toward its first objective that the blueprint includes. The compiler composes every blueprint during validation. Start with enough independent families that repeat avoidance does not exhaust the bank. A scenario is selected as an atomic group, so all its questions must fit the blueprint objective filter.

Set lockSections true when mock learners must finish one scenario before moving forward. The UI asks for confirmation and the API enforces the lock. Learning mode remains navigable and gives immediate feedback.

## Review and publish

Run the CLI check and build commands in CI. Review the question diff between releases. Open delivery output as a learner and inspect the private grading output only in a restricted author environment. Have a subject expert check keys, explanations, source links, objective mapping and misleading distractors.

Register a local account, grant Publisher and sign in again to use Content Studio. Studio creates course, exam, or hybrid starters, saves private revision-checked drafts, previews the safe learner projection, accepts JSON source (including exam/1), shows compiler diagnostics and [quality warnings](#quality-lint), and publishes an immutable release. Warnings never block publishing. The server rejects duplicate pack/version pairs. Existing attempts retain their original release.

The profile is a starter preset; the source `capabilities` object controls whether lessons, practice, and assessments are available. A course can omit questions and blueprints. An exam pack can omit lessons. Keep `goal` separate: it describes completion, mastery, or exam readiness.

Do not include credentials, personal data, proprietary answer keys or unreviewed external URLs. The demonstration packs are original CC0 material and do not represent official exams.

## Compact exam format (exam/1)

A file whose top level contains `"format": "exam/1"` is a compact exam source. The compiler expands it into an ordinary exam pack (profile `exam`, no lessons) and validates that pack with the same rules as any other. It therefore works everywhere a pack works: `packs/`, configured pack directories, Content Studio and every CLI command. `build --out` writes the expanded pack to `pack.private.json` for review. [`docs/examples/exam-sample.json`](examples/exam-sample.json) shows every kind and a case study.

Top level: `format`, `id`, `version`, `title`, `description`, `license`, `domains`, `mocks` and `questions`, plus the optional `caseStudies`, `sources`, `readiness`, `goal` and `mastery`. Unknown properties are rejected.

- `domains`: `[{ "id", "title", "weight"?, "prerequisites"? }]`. Domains become objectives. Give every domain a weight (its share of each mock) or none.
- `mocks`: `{ "short": { "count", "minutes", "title"? }, "full"?: { … }, "lockCaseStudies"?: false, "requiredKinds"?: [] }`. These become the `short` and `full` blueprints over every domain, weighted when the domains have weights. `lockCaseStudies` becomes `lockSections`.
- `caseStudies`: `[{ "id", "title", "background" }]`. A question joins one with `"caseStudy": "<id>"`.

Each question has `id`, `kind`, `prompt`, `explanation` and either `domain` or `domains`. Optional: `caseStudy`, `family` (defaults to the ID), `weight` (default 1) and `scoring` (`exact` or `partial`). Each kind uses only its own fields; any other answer field is an LF110 error.

| kind | Fields | Becomes |
|---|---|---|
| single | `answer`, `distractors` | one key, selectCount 1, exact scoring |
| multiple | `answers` (2+), `distractors` (1+) | selectCount = number of answers, partial scoring |
| sequence | `steps` in the correct order | a permutation key, partial scoring |
| matching | `pairs: [{ item, match }]`, `extraMatches` | slots `p1…`, a shared bank of distinct matches plus extras; `reuse` when a match repeats; partial scoring |
| dropdown | `blanks: [{ text, answer, distractors }]` | slots `b1…`, each with its own choices, partial scoring |
| numeric | `answer` or `answers` (numbers), `tolerance` | accepted values, exact scoring |
| codeOutput | `code: { language, source }`, `answer` or `answers` | accepted outputs, exact scoring |

Authors never write option IDs. Each option ID is `o` plus eight hex digits of a SHA-256 hash of the question ID, its bank and its text. Whether an option is the key never affects its ID, and banks are stored in ID order. Learners' browsers see option IDs, so with these IDs neither the ID nor the stored order reveals the key; attempts also shuffle the order. Rewording an option changes its ID. That is harmless because attempts snapshot their questions, but bump the version as for any change. Duplicate option texts within a bank (ignoring case) are rejected.

## Quality lint

`dotnet run --project tools/cli -- lint <source> [--json] [--strict]` compiles the source, then reports heuristic warnings about likely answer giveaways and bank gaps. Content Studio shows the same warnings after **Validate & preview**. Warnings never fail a compile or block publishing; `--strict` makes the CLI exit with 1 when any remain. `make check-content` lints the sample strictly. Messages describe lengths, counts and prompt words, never a key's text.

| Code | Flags |
|---|---|
| LF201 | A key that is the longest option and at least 1.3× (and 10 characters) longer than the mean distractor (single, multiple, each dropdown blank) |
| LF202 | Across 20+ single-choice questions, the key is the longest (or the shortest) option in more than 40% of them |
| LF203 | Absolutes (always, never, only, …) only in distractors, or hedges (usually, may, …) only in the key |
| LF204 | A prompt word of five or more letters repeated in the key but in no distractor |
| LF205 | The key's wording appears in the stem (or blank text) while no distractor's does |
| LF206 | A stem ending in "a" or "an" that only some options fit |
| LF207 | "All/none of the above", "both A and B" or references to option letters; options are shuffled and unlabeled |
| LF208 | Options that read the same once case and punctuation are ignored |
| LF209 | Too few options: single under 4, multiple under 2 distractors, dropdown blank under 3, matching without an extra match, sequence outside 3–8 steps |
| LF210 | Predictable option IDs: names such as `correct` or `wrong`, sequence IDs ascending in key order, or one key ID in half the single-choice questions |
| LF211 | A lowercase "not", "except" or "least" in the question sentence |
| LF212 | An explanation under 60 characters |
| LF213 | Identical question content in two different families |
| LF220 | Across 20+ questions, one format above 70% or fewer than three formats |
| LF221 | A weighted domain with under 0.75× or over 1.5× its expected share of questions (and at least 3 questions off) |
| LF222 | A readiness goal that no mock length can reach: (attempts − 1) × count + ⌈fresh% × count⌉ exceeds the eligible families |
| LF223 | A case study with fewer than two questions |

The bundled demonstration packs predate the linter and show several of these cues. Their releases are immutable, so they are left as they are.

