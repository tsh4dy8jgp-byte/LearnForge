# Authoring guide

Every content release must follow the [accessible content and authoring design](accessibility/content-and-authoring.md). Current text packs need manual accessibility review; the richer media/schema checks described there are planned additions, not fields accepted by today's compiler.

## Build a pack

Start with a copy of the complete starter:

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

Question kinds are single, multiple, sequence, matching and dropdown. The Angular player provides drag/drop plus click or keyboard alternatives for matching and ordering. The server remains authoritative for all contracts.

## Templates

The optional top-level templates object holds JSON fragments. A question or block can contain a $use property and values. A whole-string placeholder such as {{prompt}} inserts a typed value; text containing a placeholder replaces it as a string. Keep values small and explicit. Recursive references, large expansions, unknown placeholders and unknown properties fail validation.

## Exams

A blueprint names its count, duration, short/full size, objective IDs and requiredKinds. The compiler composes every blueprint during validation. Start with enough independent families that repeat avoidance does not exhaust the bank. A scenario is selected as an atomic group, so all its questions must fit the blueprint objective filter.

Set lockSections true when mock learners must finish one scenario before moving forward. The UI asks for confirmation and the API enforces the lock. Learning mode remains navigable and gives immediate feedback.

## Review and publish

Run the CLI check and build commands in CI. Review the question diff between releases. Open delivery output as a learner and inspect the private grading output only in a restricted author environment. Have a subject expert check keys, explanations, source links, objective mapping and misleading distractors.

Register a local account, grant Publisher and sign in again to use Content Studio. Studio accepts JSON, shows compiler diagnostics and publishes an immutable release. The server rejects duplicate pack/version pairs. Existing attempts retain their original release.

Do not include credentials, personal data, proprietary answer keys or unreviewed external URLs. The demonstration packs are original CC0 material and do not represent official exams.
