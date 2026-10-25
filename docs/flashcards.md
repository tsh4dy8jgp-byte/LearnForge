# Flashcards

LearnForge supports subject flashcards in the web app, Anki, and on paper. Basic, reversible, and cloze notes share one validated source. Recall ratings are self-assessment: they never create assessment evidence, mastery, mock freshness, or readiness.

## Authoring

Use the subject-creation interview prompt, import JSON in Content Studio, or create a **Flashcards** starter. The editor supports decks, note types, language, objectives, tags, and sources. **Validate & preview** checks the pack and shows every generated front/back. Export a validated draft or publish a new immutable version.

See [the complete example](examples/flashcards-sample.json). Full packs and compact `exam/1` sources accept `flashcardDecks`. Explicit capabilities must set `flashcards: true`; otherwise decks enable it automatically. Standalone subjects use `profile: "flashcards"`, `goal: "review"`, and empty lesson/question/scenario/blueprint arrays.

```json
{
  "id": "terms", "title": "Important terms",
  "notes": [{
    "id": "retrieval", "kind": "basic", "objectiveIds": ["recall"],
    "front": "What is retrieval practice?",
    "back": "Attempting to recall information from memory.",
    "explanation": "Try to remember before revealing the answer.",
    "language": "en", "tags": ["terms"], "sources": []
  }]
}
```

Notes require ID, kind, objective IDs, and explanation. Basic/reversible use `front` and `back`; cloze uses `text` with `{{c1::answer}}` or `{{c1::answer::hint}}`. Repeated numbers hide deletions together; different numbers create separate cards. Nested deletions are unsupported. Reverse directions must make sense independently. Text is plain text with line breaks, not executable HTML.

Supported languages: en, ru, kk, uk, de, fr, es, it, pt, tr, nl, pl, zh-Hans. PDF font coverage is checked. Chinese PDF and Anki exports use the bundled pilot font; additional Chinese content may require a broader configured font. Packs allow 2 MB of source, 2,000 notes, and 5,000 expanded cards. IDs/tags use lowercase letters, numbers, dots, underscores and hyphens; source links require titles and HTTP(S) URLs.

Preserve deck/note IDs and cloze numbers for corrections; use new IDs for new targets or changed note kinds. Edited card content, including explanation/metadata, becomes new in LearnForge. Removed cards disappear from queues while history remains. Anki does not automatically delete previously imported notes absent from a later export.

## Review

Sign in and open **Flashcards**. Filter by deck, objective, or tag; recall, reveal, then choose Again/Hard/Good/Easy. Sessions select up to 20 cards, due first, at most 10 new, and one card per note. Finish early to change filters. After reloading, **Resume review** restores the authoritative session.

Stages: 1, 3, 7, 14, 30, 60, 120 days. Good advances one stage; Easy two. Hard keeps the stage and schedules half its interval, minimum one day. Again resets the stage and schedules ten minutes later. Time is stored in UTC and displayed locally. Suspend cards to exclude them from future sessions; active sessions retain their selected cards.

“Review up to date” means at least one card is introduced and no unsuspended introduced cards are due; new cards may remain. It changes over time and does not establish mastery. Dashboard counts cover the subject; filters constrain review/export selection. Ratings from old pinned sessions cannot overwrite revised content's schedules.

## Anki and paper

- **Anki deck:** `.apkg` with fixed LearnForge v1 note types, stable GUIDs, objective tags, explanations and sources. Import using Anki Desktop File > Import or a compatible mobile app. Cards start new; LearnForge personal history is excluded. Re-import updates compatible notes according to Anki settings. Keep note-type fields/templates stable for updates.
- **Text backup:** ZIP with UTF-8 tab-separated `.txt` files by note type, `note-types.json`, and instructions. Install types by importing the `.apkg` first or create them from the specifications. Keep LearnForgeId as the first field, match by note type, and enable HTML for escaped text/line breaks.
- **PDF:** portrait A4/US Letter; 90 × 60 mm cards at 11-point text (single-character Chinese fronts use a centred 48-point teaching glyph). Duplex sheets hold eight cards with matching backs; choose long/short binding. Foldable sheets hold four 180 × 60 mm pairs with cut marks and dashed fold lines. Partial sheets retain blank matching positions.

Print instructions/calibration pages 1–2 separately. Card footers specify the card-page range; explanations/references follow in an appendix. Print at **Actual size / 100%**. Try one sheet, check matching IDs and measure calibration before printing the deck. Back offsets accept -5 to +5 mm. Physical printer calibration is a manual check.

Overlong fronts/backs produce a card-specific error instead of clipping/shrinking. Split them and retry. Full explanations and references are also available in readable HTML. PDFs contain real text and embedded fonts but are not claimed as PDF/UA; use the web view for accessible reading.

## Setup and CLI

Exports require Python 3.11+ and a Unicode TrueType font. Docker installs pinned dependencies and DejaVu Sans. Native setup:

```sh
make setup-flashcards
export LEARNFORGE_EXPORT_PYTHON="$PWD/.venv-flashcards/bin/python"
make dev
```

Linux uses DejaVu Sans; macOS uses `/Library/Fonts/Roboto-Regular.ttf` when available. Otherwise set `LEARNFORGE_CARD_FONT` to a Unicode `.ttf`. The API also accepts `Flashcards:Python`. Review works without export dependencies.

```sh
dotnet run --project tools/cli -- export-flashcards docs/examples/flashcards-sample.json --format apkg --out output/anki
dotnet run --project tools/cli -- export-flashcards docs/examples/flashcards-sample.json --format text --out output/text
dotnet run --project tools/cli -- export-flashcards docs/examples/flashcards-sample.json --format pdf --paper a4 --mode duplex --binding long --out output/pdf-duplex
dotnet run --project tools/cli -- export-flashcards docs/examples/flashcards-sample.json --format pdf --paper letter --mode foldable --out output/pdf-foldable
```

Filters: `--deck`, `--objective`, `--tag`; alignment: `--offset-x`, `--offset-y`. Commands refuse to overwrite existing artifacts. Isolated temporary files are cleaned afterward. The API permits two exports per process, a 120-second timeout, and a 50 MB output cap; excess requests receive 429. Persistent export jobs, offline review, runtime AI and Anki synchronization are outside this release.

`make test-flashcard-exports` verifies archives, note types, card counts, new-card scheduling defaults, PDF geometry, pagination and overflow. It also imports decks/text through Anki's official backend, checks repeat imports, and verifies updates preserve scheduling. Anki application UI checks and physical printing remain manual release checks.
