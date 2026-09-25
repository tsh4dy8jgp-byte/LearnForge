# Content engine

LearnForge content is strict JSON. The compiler turns a source pack into a validated Pack record, a SHA-256 source hash and diagnostics. It is bounded and deterministic enough for CI and publishing.

## Source shape

A direct pack is a JSON object with schemaVersion, id, version, title, description, license, objectives, lessons, questions, scenarios, blueprints, readiness and sources. The CLI initializer creates a complete minimal example.

The alternate wrapper supports reusable templates:

~~~json
{
  "templates": {
    "choice": {
      "id": "{{id}}",
      "familyId": "{{family}}",
      "kind": "single",
      "prompt": "{{prompt}}",
      "objectiveIds": ["reasoning"],
      "options": [{"id":"yes","text":"Yes"},{"id":"no","text":"No"}],
      "slots": [],
      "selectCount": 1,
      "reuse": false,
      "grading": {"policy":"exact","correct":["yes"]},
      "explanation": "{{explanation}}"
    }
  },
  "pack": {
    "questions": [{"$use":"choice","values":{"id":"q-1","family":"family-1","prompt":"Is this supported?","explanation":"Check the evidence."}}]
  }
}
~~~

Whole-string placeholders insert typed JSON values. Embedded placeholders produce strings. Values are expanded into a JSON AST, so a value cannot escape into a new property or execute code. `$use` references a named template and may be nested.

The current engine does not fetch files or URLs, execute expressions, interpret YAML/Markdown, or render raw HTML. Those are roadmap adapters and need their own trust model.

## Validation

The compiler checks schema/version and size limits, stable identifiers, uniqueness and references, objective prerequisites and cycles, lesson blocks, question keys and slots, scenario IDs, HTTPS source links, readiness bounds and blueprint feasibility. It uses strict JSON options, a 2 MB source cap, depth 32, template depth 24, 100,000 expanded nodes and a four-million-character expansion budget.

Validation should run before a pull request is merged and before publishing. The API repeats it; a browser authoring client is never the authority.

~~~sh
dotnet run --project tools/cli -- check packs/my-subject.json
dotnet run --project tools/cli -- check packs/my-subject.json --json
dotnet run --project tools/cli -- check packs/my-subject.json --watch
~~~

## Build artifacts

The build command requires a fresh or empty destination and emits delivery.json (teaching-safe questions without keys), grading.private.json, pack.private.json, validation-report.json, search.json and deterministic manifest.json. Keep private files outside a web root and learner-facing artifact store. The API stores compiled full packs because it grades on the server; active attempts remain sanitized.

~~~sh
dotnet run --project tools/cli -- build packs/my-subject.json --out build/my-subject-1.0.0
dotnet run --project tools/cli -- diff packs/old.json packs/new.json
~~~

Diff reports added, removed and changed question IDs. It is a structural release aid, not a psychometric equivalence check.

## Cross-validation workflow

Compiler validation catches structural errors. Subject review must still compare each key, explanation, objective, source and scenario against authoritative material. Review delivery and private artifacts separately, compose every blueprint and run the browser journey. Semantic duplicate detection, QTI import and richer authoring preview are future work.
