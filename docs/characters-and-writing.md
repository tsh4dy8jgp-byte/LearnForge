# Characters & Writing

The Simplified Chinese pilot contains 30 writing targets, a foundations introduction, six themed lessons, 30 recognition flashcards and 30 meaning questions. It is an introductory course contributing toward beginner learning, not a complete A1–A2 course or an official language qualification.

## Learning

Open **Chinese Characters & Writing** in the learning library. Start with **Before you write**, then use **Characters & writing**:

- **Watch:** play, pause or adjust speed, or select individual strokes. Static stroke steps follow the system reduced-motion preference and can be selected manually.
- **Trace:** draw over the faint model.
- **Copy:** write beside a visible stroke model.
- **Recall:** write from meaning and Pinyin; the character library and teaching examples are hidden during saved recall. Requesting a model or next-stroke hint makes the attempt assisted.
- **Stroke choices:** choose the next isolated stroke with a mouse, touch, or keyboard. This is recorded separately from drawing.

Drawing accepts mouse, touch and pen Pointer Events with local pointer capture. Page scrolling is disabled inside the drawing surface only. A cancelled gesture resets the current exercise; resize preserves the drawing model. Practice checks order, direction and approximate shape with generous tolerance. It does not assess calligraphy or provide an authoritative handwriting grade.

Public learners and publisher previews can practise without saving. Signed-in learners select a character and mode, then **Start saved practice**. Completed exercises save automatically; **Finish session** records an unfinished session without a completion. Failed result saves expose **Retry saving result**, which reuses the same request ID. Refreshing allows **Resume practice**; the saved character and mode are restored, and unfinished strokes are drawn again. No raw handwriting paths are stored.

Recognition flashcards and independent writing have separate schedules. Writing recall reuses the existing scheduler: no hints/no mistakes advances with Good, mistakes use Hard, and hints use Again. Trace/copy completions and stroke-choice completions do not advance independent writing. Browser handwriting results never create assessment evidence, mastery or exam readiness. Watching is not a writing completion.

Each character includes a synthetic pronunciation clip, tone-marked Pinyin, components, a writing tip, useful words, an example and a glossary. Other characters in these examples are recognition-only. Audio is isolated character pronunciation; notes explain connected-speech changes where needed. Synthetic recordings and teaching notes have not received native-speaking teacher review.

**Print writing sheets** uses the browser print dialog: each character has a model, two light tracing models and three blank grids. These are separate from existing printable flashcards. Recognition decks can also be exported as PDF or Anki; the bundled teaching font is embedded in Chinese exports.

## Content and assets

Packs opt in with `capabilities.writing: true` and a `characters` collection. The current engine accepts `language: "zh-Hans"`. Each record has a stable ID, single hanzi glyph, Pinyin, meaning, lesson/objective references, components, tip, word entries, translated example/glossary, audio URL/transcript, and stroke data (`strokes` SVG path strings plus ordered `medians`). See the pilot pack for the complete typed format.

The compiler checks identifiers, lesson/objective consistency, required teaching fields, bounded stroke geometry and same-origin bundled audio paths. API preview, validation, seeding and publishing additionally check audio-file availability. Assets must be shipped under `apps/web/public/writing`; they are copied to API/CLI outputs for validation and export. Publishers cannot supply executable HTML or remote SVG/audio resources through these records.

The stroke model defines the writing target. Teaching text uses the renamed **LearnForge WenKai Pilot** subset of LXGW WenKai GB 1.521; interface controls keep the platform typography. Printed font forms can differ slightly from the stroke outlines. The subset covers this pilot, not arbitrary Chinese text. For a larger pack, expand the licensed font subset and verify all glyphs; PDF exports can use `LEARNFORGE_CARD_FONT` for a broader font.

`apps/web/public/writing/NOTICE.txt` records sources, versions, attribution, font modifications and audio provenance. Full font, stroke-data and renderer licenses are bundled beside it. `manifest.json` records asset hashes and upstream archive hashes. Review any change to these assets and regenerate the manifest intentionally. `python3 scripts/check-writing-assets.py` verifies hashes, nonempty WAVs and pack references without extra dependencies; it also runs through `make check-content`.

## Persistence and API

Authenticated writing endpoints are under `/api/me/courses/{id}/writing`: GET summary, POST `/sessions`, GET `/sessions/{sessionId}`, and POST `/sessions/{sessionId}/result`. Start requests contain character ID, mode, course version and request ID. Results contain request ID, revision, completion, mistakes and hints. Data contracts are included in generated OpenAPI types.

There is one active writing session per user/course. Sessions pin the release and character model. Unique request keys and concurrency tokens prevent duplicate progress; changed replays and stale versions return conflict responses. Old sessions remain resumable, but a revised/removed character cannot advance the latest character's schedule. Progress matches stable character IDs plus a content hash. Starting saved writing also adds/reactivates the course in the learner dashboard.

Both SQLite and PostgreSQL have writing session/state migrations. Account history export includes writing summaries and pinned session history. Account deletion cascades to both tables. No changes are made to the assessment evidence ledger.

## Verification record

Automated coverage includes pack completeness and asset availability; malformed models/references; idempotency, ownership and revised-release behaviour; practice/assessment separation; account export/deletion; browser mouse/touch/pen strokes; keyboard stroke choices; reload/resume; reduced-motion steps; mobile resizing and printable sheet visibility.

Recorded checks: 212 backend tests, 52 frontend tests, three writing browser journeys and six exporter regressions passed. Production frontend build and bundled/sample content validation passed. Visual review covered the mobile layout, desktop Midnight theme, all 30 teaching-font/stroke-model pairs and Chinese PDF card fronts/backs/reference pages. No missing or incorrect glyphs were observed; the two rendering sources have expected stylistic differences.

Browser touch and pen checks use Chrome input emulation. A physical iPhone/iPad, Android touchscreen and active stylus have not been checked. A native-speaking educator should review pronunciation, teaching examples and the model/font comparisons before this pilot is described as teacher-validated. PostgreSQL migrations were generated; a live PostgreSQL deployment was not exercised. Japanese, Korean, Traditional Chinese and full offline downloads are deferred.
