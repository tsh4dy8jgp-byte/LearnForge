# Characters & Writing

The Simplified Chinese pilot contains 30 writing targets, a foundations introduction, six themed lessons, 30 recognition flashcards, 10 contrast flashcards, six short readings and 30 meaning questions. It is an introductory course contributing toward beginner learning, not a complete A1–A2 course or an official language qualification.

## Today's study

The pilot opens on **Today's study**. A signed-in learner starts a short sequence: up to three recognition cards, up to two similar-character choices, then a supported reading. Each review activity selects due cards before new ones and honors suspended cards. Up-to-date activities are skipped. The five-minute estimate is guidance; there is no timer.

Recognition ratings use the existing flashcard scheduler. In comparison practice, choose one of two characters, inspect the answer and distinguishing feature, then **Save and continue**: a correct choice uses Good; an incorrect choice uses Again and is due in about ten minutes. Each pair can also be practised from the overview. The comparison cards are available in the normal Flashcards view and exports.

Review actions save on the server with request replay protection. Failed transport/server saves offer a retry of the same action. A reload offers **Resume saved review**, including reviews begun in Flashcards. A revealed comparison resumed after a reload uses self-rating because the browser's original selection is not stored. The overall sequence and reading check-in are per visit; only review ratings and existing handwriting progress persist. Finishing early preserves completed ratings and leaves unrated cards unchanged.

Reading suggestions use recognition introductions and completed guided/independent handwriting practice. This signals exposure, not mastery. A reading is recommended when all its target characters have been introduced; otherwise it is clearly offered as a preview with help. Any reading can be previewed, including while signed out. Tap a word for Pinyin and meaning, reveal/hide pronunciation and translation independently, and expand the reading tip or extra-vocabulary glossary. Reading check-ins do not create assessment evidence or change review dates. A writing reminder links to the separate writing schedule.

## Authoring study activities

Packs opt in with an optional `characterStudy` object and both writing and flashcard capabilities. Older packs remain unchanged. The pilot release is now `1.1.0`; the existing character records and recognition notes retain their IDs and content, preserving their schedules.

- `recognitionDeckId`: a basic recognition deck with one note per writing character; each note ID matches the character ID and its front is the glyph.
- `confusionDeckId`: a separate deck of basic contrast notes.
- `contrasts`: stable `id`, two `characterIds`, two corresponding `noteIds`, and an `explanation`. Each referenced note is tagged with the contrast ID, has exactly the corresponding glyph as its answer, and supplies its prompt/explanation. This explicit mapping supports pinned session answers without guessing from teaching prose.
- `readings`: stable `id`, `title`, distinct `requiredCharacterIds`, 1–12 translated `lines`, a teaching `note`, and `supportWords`. Each line has `words` (text/Pinyin/meaning tokens) and `translation`; punctuation tokens may have empty Pinyin/meaning. Every hanzi must be covered by a prerequisite or an explicitly glossed support word, and every prerequisite must occur in the text.

The compiler validates references, answer mappings, limits and reading coverage. Content remains versioned plain text; it cannot provide executable markup or remote assets. New teaching text, like the original pilot notes, still needs native-speaking teacher review. The feature adds no database tables, AI service or third-party runtime dependency.

`POST /api/me/courses/{id}/flashcards/sessions` accepts optional `limit` (1–20). Omitting it retains the existing 20-card limit and 10-new-card cap. A supplied limit participates in request/filter identity; changing it on replay is rejected.

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
