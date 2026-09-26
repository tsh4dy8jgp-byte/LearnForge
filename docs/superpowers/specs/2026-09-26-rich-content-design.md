# SP2a design: rich, safe content

Status: design for review. Date: 2026-09-26. Part of the [learner roadmap](2026-09-25-learner-roadmap.md). Follows the [accessibility baseline](../../accessibility/README.md) and its [content contract](../../accessibility/content-and-authoring.md).

## Why

Lessons, prompts and explanations are plain text today. A pack cannot state a formula, a table, a definition, a worked example or a quoted source, so LearnForge cannot serve STEM, languages or the humanities as data only (roadmap findings A1, A3, A6, A7, A8, B5). This sub-project adds structured, accessible content that the compiler validates and the browser renders without ever handling HTML.

## Decisions

| Topic | Decision |
| --- | --- |
| Scope split | SP2 is delivered as SP2a (this document: rich text and structure) and SP2b (assets, figures, media, captions and the accessibility review publishing gate). |
| Rich fields | Lesson blocks, question prompt, options, slot text, explanation and scenario background. Titles, summaries, objective and blueprint titles stay plain text. |
| Where the AST lives | The compiler parses Markdown and TeX once. Stored releases, attempt snapshots and delivery DTOs carry the typed AST. Version 1 data is read through an explicit upcaster. |
| Markdown | Markdig 1.4.0 with HTML disabled, mapped onto LearnForge's own AST. Any construct outside the allowlist is a diagnostic. |
| Math | LearnForge's own TeX-subset parser produces MathML-shaped nodes. Angular renders native MathML. No JavaScript math library, no `innerHTML`. |
| Inline checks | Each check owns its question and is formative. A stateless endpoint grades it on the server. Nothing is written to the evidence ledger, and the check never enters the bank or a mock. |
| Studio | Validation shows a focusable diagnostic summary. On success, a preview renders the pack with the learner components, and answer keys appear only in a separate author panel. |
| Catalog | Packs declare subject, level, tags, language and estimated hours. The library shows them and filters by subject, level and language. |

## Goals and non-goals

**Goals**
- A v2 pack format with Markdown, math and seven new lesson block kinds; the four existing kinds accept Markdown.
- Stable block IDs, modules, a glossary, language and direction, catalog metadata.
- Duplicate JSON properties rejected in every schema version.
- A typed AST that the release, snapshot and API all share, generated into TypeScript.
- Accessible renderers, a glossary tab, inline checks, library filters and a Studio preview.
- Existing v1 packs, releases, attempts and lesson progress keep working unchanged.

**Non-goals**
- Figures, media, uploaded assets, the asset manifest and the review-record publish gate (SP2b).
- New question kinds, hints, per-option rationale (SP3).
- Full-text content search, notes and highlights (SP5).
- Localizing the application's own interface.

## Source format (schema version 2)

Authors opt in with `"schemaVersion": 2`. Version 1 sources still compile, and their text stays literal.

### Pack-level fields

| Field | Rule |
| --- | --- |
| `language` | Required. A canonical BCP 47 tag `language[-Script][-REGION]`, e.g. `en`, `pt-BR`, `zh-Hant`, `es-419`. The primary subtag must be an ISO 639-1 code, or a code from an embedded list of three-letter ISO 639-2/3 codes for languages without one, so results never depend on ICU. |
| `direction` | `ltr` (default) or `rtl`. The expected direction comes from the script subtag when present (`Arab`, `Hebr`, `Thaa`, `Syrc` are right-to-left), otherwise from the language (`ar`, `arc`, `ckb`, `dv`, `fa`, `he`, `ks`, `ps`, `sd`, `syr`, `ug`, `ur`, `yi`). A mismatch is `A11Y_LANGUAGE`. |
| `catalog` | Required: `{ subject, level, tags?, estimatedHours? }`. `subject` is plain text of 1–60 characters. `level` is `introductory`, `intermediate` or `advanced`. `tags` holds up to 10 identifiers. `estimatedHours` is 0.5–1000. |
| `modules` | Optional: `[{ id, title, lessonIds[] }]`, up to 100. When present, every lesson belongs to exactly one module, each module is nonempty, and the compiler orders the release's lessons by module order. |
| `glossary` | Optional: `[{ id, term, definition }]`, up to 2000. `term` is plain text; `definition` is inline rich text. |

Lessons gain optional `minutes` (1–600) and an optional `language` override. Objectives, blueprints, sources, readiness, goal and mastery are unchanged.

### Markdown subset

**Block content** (block bodies, question prompt, explanation, scenario background, worked-example parts, misconception parts, primary-source excerpt and translation):
- paragraphs;
- ordered and unordered lists, nested at most 3 levels;
- block quotes;
- fenced code with a required language label;
- everything allowed in inline content.

**Inline content** (options, slot text, glossary definitions, table headers and cells):
- text, emphasis, strong, inline code, hard line breaks;
- inline math `$…$`, where `\$` writes a literal dollar;
- links, restricted by scheme:

| Link | Meaning |
| --- | --- |
| `[text](https://…)` | External link. HTTPS only. |
| `[text](lesson:sets-intro)` or `[text](lesson:sets-intro#union)` | Lesson or block in this pack. Must resolve. |
| `[text](#union)` | Block in the same lesson. Must resolve. |
| `[union](term:union)` | Glossary term. Must resolve. |
| `[bonjour](lang:fr)` | Language span. The tag follows the pack `language` rules, and direction is derived. |

**Rejected with a diagnostic:**
- raw HTML;
- images (figures arrive in SP2b);
- headings, including setext headings; block titles provide structure;
- thematic breaks;
- indented code (use a fence with a language);
- `$$…$$` (use a `math` block, which requires a description);
- autolinks and empty link text;
- any other link scheme.

Dropdown slot options render inside native `<option>` elements, which can hold only text, so their `text` accepts plain inline text only.

### Lesson blocks

Every block has an `id` that is unique within its lesson and matches the existing identifier rule. Every block may have a plain-text `title` and a `language` override, with two exceptions. In a `code` block, `language` is the programming-language label, and there is no human-language override. A `definition` block takes its title from the glossary term.

| Kind | Fields | Rules |
| --- | --- | --- |
| `text` | `body` | block Markdown |
| `callout` | `body` | block Markdown |
| `example` | `body` | block Markdown |
| `code` | `language`, `code` | `code` is literal; `language` matches `^[a-z0-9+#.-]{1,30}$` |
| `math` | `tex`, `description` | display math; `description` is a required plain-language equivalent (`A11Y_ALTERNATIVE`) |
| `table` | `caption`, `columns[]`, `rows[][]`, `rowHeaders` | `caption` is required (`A11Y_ALTERNATIVE`). There are 1–20 columns and 1–500 rows, and every row has one cell per column. `rowHeaders` makes the first cell of each row a row header. Merged cells are not supported (`A11Y_STRUCTURE`). |
| `workedExample` | `problem`, `steps[]`, `result` | 1–30 steps |
| `misconception` | `claim`, `correction` | both required |
| `definition` | `termId`, `body?` | `termId` must resolve; the rendered title is the glossary term |
| `primarySource` | `excerpt`, `attribution { title, author?, date?, url? }`, `language?`, `translation?` | `url` is HTTPS; `date` is free text such as "c. 400 BCE". A `translation` requires `language` (`A11Y_LANGUAGE`). |
| `inlineCheck` | `question { kind, prompt, options, slots, selectCount, reuse, grading, explanation }` | Same per-kind rules as bank questions. No family, objectives, scenario or weight. |

There is no separate `list` block; lists are written in Markdown inside `text`.

### TeX subset

| Group | Supported |
| --- | --- |
| Atoms | letters (`mi`), numbers including decimals (`mn`), `+ - = < > ( ) [ ] , ; : ! / \| '` (`mo`), `\{ \}` |
| Structure | `^`, `_`, `{…}`, `\frac`, `\dfrac`, `\sqrt`, `\sqrt[n]`, `\left…\right` with `( ) [ ] \{ \} \| . \langle \rangle` |
| Letters | Greek `\alpha`…`\omega`, `\Gamma`…`\Omega`, `\varepsilon`, `\varphi`, `\ell` |
| Operators and relations | `\times \cdot \div \pm \mp \le \ge \neq \approx \equiv \sim \propto \in \notin \subset \subseteq \supset \supseteq \cup \cap \setminus \emptyset \infty \partial \nabla \to \rightarrow \leftarrow \Rightarrow \Leftarrow \Leftrightarrow \iff \mapsto \forall \exists \neg \land \lor \ldots \cdots \circ \angle \perp \parallel \degree` |
| Large operators | `\sum \prod \int \iint \oint \lim` with limits |
| Functions | `\sin \cos \tan \sec \csc \cot \arcsin \arccos \arctan \sinh \cosh \tanh \log \ln \exp \max \min \det \gcd \deg` |
| Text and style | `\text`, `\mathrm`, `\mathbf`, `\mathit`, `\mathbb`, `\mathcal` |
| Accents | `\vec`, `\hat`, `\bar`, `\overline`, `\underline`, `\dot`, `\ddot`, `\tilde` |
| Spacing | `\,` `\:` `\;` `\quad` `\qquad` `\!` |
| Environments | `matrix`, `pmatrix`, `bmatrix`, `vmatrix`, `cases`, `aligned` with `&` and `\\` |

Any other command, an unbalanced group or a misplaced `&` is an `LF3xx` diagnostic with its position.

### Example

```json
{ "id": "union", "kind": "workedExample", "title": "Union of two sets",
  "problem": "Find $A \\cup B$ for $A = \\{1, 2\\}$ and $B = \\{2, 3\\}$.",
  "steps": ["List every element of [A](term:set).", "Add the elements of $B$ that are not already listed."],
  "result": "$A \\cup B = \\{1, 2, 3\\}$" }
```

## Core (`src/LearnForge.Core`)

### Models

| Folder | Contents |
| --- | --- |
| `Domain/Source/*` | v2 source records exactly as authors write them; rich fields are strings. Strict: unknown properties are rejected. |
| `Domain/Legacy/*` | Today's records moved unchanged and renamed with a `V1` prefix (`V1Pack`, `V1Lesson`, `V1ContentBlock`, `V1Question`, `V1Option`, `V1Slot`, `V1Scenario`, `ContentBlockKind`). They read v1 sources, stored v1 releases and v1 snapshots. |
| `Domain/Content/*` | Release records. Names stay (`Pack`, `Lesson`, `Question`, `Option`, `Slot`, `Scenario`, `DeliveryQuestion`); rich fields change type. |
| `Domain/Rich/*` | The AST. |

Release records:
- `Pack` adds `int Format` (2 marks the AST release shape), `string? Language`, `TextDirection Direction`, `CatalogMetadata? Catalog`, `Module[] Modules`, `GlossaryEntry[] Glossary`, `InlineCheckKey[] Checks` and `Dictionary<string, string> LessonHashes`, computed at compile time. `SchemaVersion` records the source version.
- `Lesson(Id, Title, Summary, ObjectiveIds, LessonBlock[] Blocks, int? Minutes, string? Language)`.
- `Question.Prompt` and `Question.Explanation` become `RichBlock[]`. `Option.Text` and `Slot.Text` become `RichInline[]`. `Scenario.Background` becomes `RichBlock[]`.
- `InlineCheckKey(LessonId, BlockId, Grading, RichBlock[] Explanation)` holds the private half of each inline check.

AST types use System.Text.Json polymorphism with the discriminator `kind`. OpenAPI 10 emits them as `oneOf` with a discriminator mapping, and hey-api generates TypeScript unions. No derived record declares its own `Kind` property.

| Base type | Kinds |
| --- | --- |
| `RichBlock` | `paragraph(inlines)`, `list(ordered, start, items: RichBlock[][])`, `quote(blocks)`, `codeBlock(language, code)` |
| `RichInline` | `text(text)`, `emphasis(inlines)`, `strong(inlines)`, `code(code)`, `break`, `link(href, inlines)`, `lessonLink(lessonId, blockId?, inlines)`, `termRef(termId, inlines)`, `lang(language, direction, inlines)`, `math(tex, root: MathNode)` |
| `MathNode` | `mi(text, variant?)`, `mn(text)`, `mo(text, stretchy?, largeOp?)`, `mtext(text)`, `mrow(children)`, `mfrac(numerator, denominator)`, `msqrt(child)`, `mroot(base, index)`, `msub`, `msup`, `msubsup`, `munder`, `mover`, `munderover`, `mtable(rows, columnAlign?)`, `mspace(width)` |
| `LessonBlock` | one record per kind in the lesson block table; `math` carries `(tex, root, description)`, `code` carries `(language?, code)` and `inlineCheck` carries `(question: DeliveryQuestion)` |

The TeX source travels with each math node. It serves `PlainText()` and the Studio preview; it is data, not markup.

**Inline checks are safe by construction.** The compiler splits each source check into a public `inlineCheck` block, whose `DeliveryQuestion` has the block ID as its question ID and the lesson's objectives, and a private `InlineCheckKey` in `Pack.Checks`. `Lesson` never contains a key.

### Compiler pipeline (`ContentEngine.Compile`)

1. **Parse JSON:** at most 2 MB and depth 32 as today. `JsonDocumentOptions.AllowDuplicateProperties = false`, so a duplicate property is `LF002` with its path, in every schema version.
2. **Expand templates:** unchanged.
3. **Branch on `schemaVersion`:**
   - `1`: deserialize `V1Pack`, run today's validation unchanged, then `V1Upcaster.ToRelease`. Each v1 block becomes the lesson block of the same kind whose body is one `paragraph` holding one literal `text` node; v1 code blocks keep their literal code with no language label. Blocks get positional IDs `b1`, `b2`… (documented as unstable). Language is null and direction `ltr`.
   - `2`: deserialize the source records. `RichTextCompiler` (Markdig restricted pipeline plus mapper) and `TexParser` turn each rich field into AST nodes, collecting diagnostics.
   - Other values: `LF100` as today.
4. **Validate the release model:**
   - today's structural rules;
   - modules, glossary and term references, lesson and block links;
   - table shape;
   - language and direction;
   - the inline-check question contracts;
   - dropdown option text is plain.
5. **Return** `Compilation(Pack? Pack, Diagnostic[] Diagnostics, string Hash)` as today.

**Limits**

| Item | Limit |
| --- | --- |
| Markdown field | 20,000 characters |
| TeX expression | 2,000 characters |
| Markdown nesting | 8 levels |
| Math nesting | 16 levels |
| AST nodes per pack | 200,000 |

These bound every serialized document. `Json.Options.MaxDepth` rises from 32 to 128. The source keeps its depth-32 parse limit in step 1, and the API's HTTP JSON options set `MaxDepth = 128` explicitly.

### Diagnostics

`Diagnostic(string Code, string Path, string Message, string? Guidance = null)`. Paths use IDs and, inside a rich field, `:line:column`, for example `lessons.sets-intro.blocks.union.problem:1:12`.

| Code | Condition |
| --- | --- |
| `LF001` | Unreadable JSON or wrong shape (unchanged) |
| `LF002` | Duplicate JSON property |
| `LF100` | Structural rule (unchanged meaning) |
| `LF201` | Markdown construct outside the subset |
| `LF202` | Link scheme not allowed, or a non-HTTPS URL |
| `LF203` | Unresolved lesson, block or term reference |
| `LF204` | Limit exceeded |
| `LF301` | Unknown TeX command or environment |
| `LF302` | Unbalanced group, `\left` without `\right`, misplaced `&` or `\\` |
| `LF303` | TeX limit exceeded |
| `A11Y_LANGUAGE` | Missing or invalid language tag, direction mismatch, translation without language |
| `A11Y_STRUCTURE` | Duplicate block IDs, headings in Markdown, table row shape, empty link text |
| `A11Y_ALTERNATIVE` | Math block without description, table without caption |

Each new diagnostic carries plain-language guidance, e.g. "Use a `math` block with a `description` instead of `$$…$$`."

### Supporting changes
- `ReleaseReader.Read(string json)` returns the release `Pack`. Stored releases carry `format: 2` and precomputed lesson hashes; rows without `format` hold schema 1 records and are upcast. Lesson hashes for schema 1 content, whether from old rows or from v1 packs published after the upgrade, are computed on the `V1Lesson` records exactly as today, so existing lesson progress is never falsely flagged as updated. v2 hashes use the release `Lesson`.
- `RichText.PlainText(...)` flattens an AST, using the TeX source for math.
- `Grader`, `ExamComposer`, `MasteryEvaluator`, `ReadinessEvaluator` and `NextStepPlanner` are unchanged.
- Markdig 1.4.0 becomes Core's only package reference.

## API (`apps/api`)

### Catalog
- `CatalogSummaryDto` adds `Language?`, `Direction`, `Subject?`, `Level?`, `Tags` and `EstimatedHours?`. They are null or empty for v1 packs.
- `CourseCatalogDto` adds `Language?`, `Direction`, `CatalogMetadata? Catalog`, `Module[] Modules` and `GlossaryEntry[] Glossary`. `Lessons` is served as-is because it holds no keys.

### Inline checks
`POST /api/catalog/{packId}/checks` with `InlineCheckRequest(string Version, string LessonId, string BlockId, Answer Answer)`:
- It resolves the release by pack ID and version, so a check is graded against the content the learner read.
- It builds a `Question` from the public block and the private key, grades it with `Grader`, and returns `InlineCheckResultDto(Grade Grade, RichBlock[] Explanation)`.
- An unknown pack, version, lesson or block returns 404. An invalid request record returns 400 through `AddValidation()`.
- It allows anonymous callers. The existing antiforgery middleware still requires the CSRF header. A new `checks` rate-limit policy allows 60 requests per minute per user or IP.
- It writes no evidence and no attempt.

### Attempts
- `DeliveryQuestion` carries the new rich types. Scenarios in `AttemptView` carry rich backgrounds.
- `Grade` drops `Explanation`. `AttemptView.Feedback` and `AttemptView.Results` become `Dictionary<string, FeedbackDto>`, where `FeedbackDto(Grade Grade, RichBlock[] Explanation)` takes the explanation from the snapshot question.
- `AttemptSnapshot` adds `int ContentSchema`, which new snapshots write as 2. Snapshots without the property are schema 1: `AttemptService.Snapshot` reads them through `V1AttemptSnapshot` and the upcaster, so old attempts render their original text.
- Stored feedback is read through a record that tolerates the legacy `explanation` property. New rows no longer store explanation text.
- `EvidenceBackfill` and the export use the same read helpers.

### Authoring
- `/validate` returns diagnostics with guidance.
- New `POST /api/authoring/preview` (Publisher role) returns `PreviewDto(CourseCatalogDto Course, PreviewQuestion[] Questions, PreviewCheck[] Checks)`. `PreviewQuestion` is the delivery question plus its grading and explanation. `PreviewCheck` pairs a lesson and block ID with the check's grading and explanation. An invalid source returns 400 with the diagnostics.
- `/publish` is unchanged apart from storing the v2 release model.

### Infrastructure
- `ReleaseCache.Load` uses `ReleaseReader`. Seeding already compiles `packs/**/*.json`.
- No database schema change, so neither provider needs a migration.
- `make api-types` regenerates the unions, and `make check-api-types` guards drift.

## Web (`apps/web`)

### Renderers (`src/app/rich/*`)
Standalone components using `@switch` on `kind`. None uses `innerHTML`, `bypassSecurityTrust*` or a parser.

| Component | Renders |
| --- | --- |
| `lf-blocks` | `RichBlock[]`: `<p>`, `<ol start>` or `<ul>`, `<blockquote>`, and code in a scroll region |
| `lf-inlines` | `RichInline[]`: `<em>`, `<strong>`, `<code>`, `<br>`, links, term disclosures, `<span lang dir>` spans and inline math |
| `lf-math` | One component that recurses through `ng-template` and `ngTemplateOutlet`, so no component host element ever appears inside `<math>` |
| `lf-lesson-block` | The eleven lesson block kinds |
| `lf-inline-check` | `question-input` plus a "Check answer" button and the result |

Rendering rules:
- **External links:** `rel="noopener noreferrer"`, same tab.
- **Lesson links:** use the router to select the lesson, scroll to `block-{lessonId}-{blockId}` (which has `tabindex="-1"`) and move focus there.
- **Terms:** a button with `aria-expanded` and `aria-controls` that reveals the glossary definition inline, immediately after the term. There is no hover tooltip. Attempts carry no glossary, so terms in questions render as plain text with no button.
- **Language:** blocks and spans set `lang`, and set `dir` only when it differs from the parent.
- **Math:** inline math renders `<math>`. Math blocks render `<math display="block">` inside a `<figure>` whose visible description is linked through `aria-describedby`.
- **Tables:** `<table>` with `<caption>` and `<th scope="col">`, plus `<th scope="row">` when `rowHeaders` is set. The table sits in a focusable region (`tabindex="0"`, `role="region"`, labelled by the caption) so it scrolls on its own at narrow widths.
- **Code:** `<pre><code>` inside a focusable, labelled scroll region with a visible language label.
- **Worked examples:** the problem, an ordered list of steps, then the result, each with a visible label.
- **Misconceptions:** labelled "Common misconception" and "Correction".
- **Definitions:** a `<dfn>` term heading followed by the definition and body.
- **Primary sources:** `<figure>` containing `<blockquote lang>` and a `<figcaption>` attribution, followed by the translation under its own label.
- **Inline checks:** the check shows the result once through a polite live region, leaves focus on the button, and renders the explanation in reading order after the question. The button is disabled while a request is pending. Failures show an error with a retry.

### `question-input`
Gains a required `instanceId` input. Every generated ID (token bank, targets, groups) derives from instance, question and slot identity, which replaces the global `token-bank` ID. Choice and token labels render rich inline content; dropdown `<option>`s render plain text. Where markup is impossible (options, `aria-label`, review summaries), a plain-text helper reads math as Unicode text, e.g. `P ∧ Q`.

### Pages
- **Course:**
  - The lesson navigation groups lessons under module headings (`h3`) and shows minutes.
  - The lesson article carries `lang` and `dir`.
  - A new Glossary tab lists the terms alphabetically in a `<dl>` with anchors.
  - Blocks render through `lf-lesson-block`.
- **Attempt:** prompts, options, slots, scenarios and explanations render through the renderers. Each question passes a unique `instanceId`.
- **Library:**
  - Cards show subject, level, language (as a name via `Intl.DisplayNames`) and estimated hours.
  - Labelled `<select>`s filter by subject, level and language alongside the existing text search.
  - A polite status reports "N courses".
  - Packs without metadata appear only under "All".
- **Studio:**
  - Validate announces the result count and moves focus to a diagnostic summary heading. Each entry lists code, path, message and guidance.
  - On success, "Preview" calls `/api/authoring/preview` and renders every lesson and question with the learner components.
  - A separate "Answer keys (authors only)" panel lists keys and explanations. Inline checks in the preview are not interactive.

## Invariants and errors
- **Answer keys never reach the browser while an attempt is active.** Inline-check keys live only in `Pack.Checks` and are released by the check endpoint after the learner checks.
- **Releases are immutable.** The stored AST never depends on a later parser. Legacy data is read, never rewritten.
- **Content is data.** No layer produces or injects HTML. A string such as `<b>` in content renders as text.
- **Grading happens only on the server,** for bank questions and inline checks alike.
- **Stable IDs:** question, family, lesson and block IDs are author-controlled and stable. v1 positional block IDs are not stable.
- **Error responses:** 404 for an unknown pack, release, lesson or block; 400 for an invalid request or source; 429 when the check rate limit is exceeded.

`CLAUDE.md` gains these invariants:
- rich content is compiled to a typed AST in Core and rendered as data, never as HTML;
- inline-check keys live only in `Pack.Checks`;
- v1 content is read through `Legacy` records and `V1Upcaster`, never rewritten.

## Content migration
- `reasoning-foundations` is migrated by hand to schema 2 and version 2.0.0, keeping its templates, lesson IDs, question IDs and family IDs. It gains `language`, `catalog`, modules, a glossary, inline and display math, a table, a worked example, a misconception, a definition, a primary source and two inline checks, so every block kind is exercised. Dropdown blank labels (`P AND Q`, `P OR Q`) become math, so every short session shows math in an attempt. Learners who completed a changed lesson see "Updated since you read it", which is intended.
- `evidence-lab` stays at schema 1, exercising the v1 path in seeding, the catalog, attempts and end-to-end tests.
- CLI:
  - `init` writes a v2 starter.
  - A new `upgrade <v1.json> --language <tag> --out <v2.json>` writes a v2 source: it escapes Markdown-significant characters, assigns block IDs `b1`, `b2`… and adds a catalog stub. It inlines templates and prints a warning when it does.
  - `build` adds the checks to `grading.private.json`, and `search.json` uses `PlainText`.

## Accessibility (feature template)

| Item | Value |
| --- | --- |
| Feature and owners | SP2a rich content. Product, design, frontend, API/Core, content and QA owners are named in the release record. |
| Routes | `/courses`, `/courses/:id` (Learn, Map, Glossary, Practice tabs), `/attempts/:id`, `/studio`. |
| Journeys | J2 find a course, J3 read and navigate, J4 answer and save, J8 author and publish. |
| Criteria | 1.1.1, 1.3.1, 1.3.2, 1.4.4, 1.4.10, 1.4.11, 1.4.12, 1.4.13, 2.1.1, 2.4.3, 2.4.4, 2.4.6, 2.4.7, 2.5.3, 3.1.1, 3.1.2, 3.3.1, 4.1.2, 4.1.3 |
| Math | Native MathML, plus a required description for display math. Screen-reader behavior is recorded per environment in the verification matrix (NVDA with MathCAT, JAWS, VoiceOver). |
| Interim content review | Until SP2b adds the review-record publish gate, the manual content release checklist applies to every v2 release. SP2a makes no content conformance claim. |

### Interaction specification

| Element | Name | Role and relationships | Keyboard and pointer | Focus | Announcement |
| --- | --- | --- | --- | --- | --- |
| Term | Visible term | `button`, `aria-expanded`, `aria-controls` → definition | Enter, Space or tap toggles | Stays on the button | Expanded state; definition in reading order |
| Lesson link | Link text | `a` with router href | Enter or click | Target block (`tabindex="-1"`) | Block heading read on focus |
| Table region | Caption | `region` labelled by the caption; `th` scopes | Tab into the region, arrows scroll | Region, then its contents | Caption, then headers with each cell |
| Code region | "Code: {language}" | `region` | Tab into the region, arrows scroll | Region | Language label |
| Inline check | "Check answer" | `button`; the question group is named by its prompt | Enter, Space or tap | Stays on the button | One polite message: fully correct, partially correct or incorrect |
| Library filters | "Subject", "Level", "Language" | native `select` | Native | Unchanged | Polite "N courses" |
| Studio diagnostics | "N problems found" | heading plus list | Tab | Summary heading after Validate | Status with the count |

Reading order follows the source. Headings: course `h1`, lesson `h2`, module `h3` in the navigation, block titles `h3`. Content reflows at 320 CSS pixels, with only tables and code scrolling in their own regions. Colors reuse the measured tokens from the design system. Timing, authentication and irreversible actions are not affected.

### Acceptance evidence

| Requirement | Given, when, expected | Procedure | Evidence |
| --- | --- | --- | --- |
| 1.3.1 tables | A v2 table with row headers; each cell exposes its column and row header | axe + NVDA/VoiceOver | Not tested |
| 3.1.2 parts | A `lang:fr` span and a primary source in German; each is exposed with its language | DOM check + screen reader | Not tested |
| 1.4.10 reflow | At 320 px only table and code regions scroll horizontally | Playwright viewport + manual | Not tested |
| 4.1.2 terms | The term button toggles `aria-expanded` and reveals its definition | Vitest + keyboard | Not tested |
| 4.1.3 checks | Checking an inline check announces one result without moving focus | Screen reader | Not tested |
| 1.1.1 math | A display equation exposes MathML plus its description | Screen-reader matrix | Not tested |
| 2.4.3 links | A lesson link moves focus to the target block | Playwright | Not tested |
| Unique IDs | Two inline checks in one lesson produce no duplicate IDs | Vitest + axe `duplicate-id-aria` | Not tested |

## Testing
- **Core unit tests:**
  - every allowed Markdown construct produces its expected AST;
  - every rejected construct produces its code, path and position;
  - each link scheme; `\$` escaping;
  - TeX golden trees for each group in the subset;
  - TeX error positions and limits;
  - v1 upcast of both current demo packs preserves literal text (including `*`, `_` and `$`), and lesson hashes equal the values recorded before this change;
  - duplicate properties rejected in v1 and v2;
  - language and direction rules;
  - modules (ordering, exactly one module per lesson);
  - glossary and definition references;
  - table shape; dropdown plain text;
  - the inline-check split (serialized `Lesson` JSON contains no grading or explanation);
  - release JSON round trip;
  - node and depth limits.
- **API integration tests** (SQLite, plus PostgreSQL with `LEARNFORGE_TEST_POSTGRES`):
  - catalog responses for a v1 and a v2 pack;
  - a privacy test asserting that no inline-check or bank key or explanation text appears in any catalog response;
  - check endpoint: correct and incorrect answers; unknown version or block returns 404; malformed answer returns 400; an anonymous caller with a CSRF token succeeds; no evidence rows are written;
  - a v2 attempt round trip with rich feedback;
  - a stored v1 attempt fixture (legacy snapshot and feedback with explanation) still renders;
  - preview requires the Publisher role;
  - a published v2 pack is readable through `ReleaseCache`.
- **Vitest:**
  - every block and inline kind;
  - the MathML tree for a fraction, a sum with limits and a matrix;
  - `lang` and `dir` attributes;
  - table scopes and caption;
  - term disclosure;
  - literal `<b>` stays text;
  - `question-input` IDs are unique across two instances;
  - inline-check announcement and focus;
  - library filters and count.
- **Playwright:**
  - read the v2 lesson (math, table, term, lesson link) and answer an inline check with the keyboard;
  - an attempt shows a math prompt;
  - library filters;
  - Studio validate, diagnostic summary focus and preview with the author panel.
- **axe:** add `@axe-core/playwright` and scan the library, the course Learn and Glossary tabs, an attempt and the Studio preview for WCAG 2.0, 2.1 and 2.2 A/AA rules, with zero violations required.

## Documentation
- Update `docs/authoring.md` (the v2 format reference, Markdown and TeX subsets, link schemes, block kinds, diagnostics, `upgrade`), `docs/content-engine.md`, `docs/architecture.md`, `docs/api.md`, `docs/user-guide.md`, `docs/testing.md` and `CLAUDE.md`.
- In `docs/accessibility/content-and-authoring.md`, mark `A11Y_LANGUAGE`, `A11Y_STRUCTURE` and `A11Y_ALTERNATIVE` (math and tables) as implemented. Leave media, assets and the review gate planned for SP2b.
- In the roadmap, split SP2 into SP2a and SP2b.

## Verification
1. `dotnet build LearnForge.slnx -c Release` and `make check-content`: the v2 and v1 packs both compile.
2. `dotnet test LearnForge.slnx`, then again against PostgreSQL (`docker compose up -d db`).
3. `make check-api-types`, `npm run build --prefix apps/web` and `npm test --prefix apps/web`.
4. `make dev`, then `cd apps/web && npx playwright test`, including the axe scans.
5. Manual checks at http://127.0.0.1:4300:
   - the v2 lesson renders math, table, terms and a primary source;
   - an inline check works signed out;
   - a v1 attempt made before the upgrade still shows its text;
   - Studio preview works for a pack with a deliberate error and then a fixed one;
   - the library filters work at 320 px width.
