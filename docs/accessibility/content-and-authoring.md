# Accessible content and authoring

Status: required design. Rich blocks and accessibility schema additions are **planned**, not accepted fields in today's strict pack format. Parent: [baseline](README.md).

## Content is part of conformance

The content owner and frontend owner jointly own the delivered lesson, question and feedback. Correct markup cannot compensate for an inadequate alternative, misleading instruction or inaccessible required resource. Review both demonstration packs and each new production release. Preserve the current rules: typed data, compiler validation, sanitized delivery, server grading and immutable published versions.

Apply [authoring guidance](../authoring.md) to current text/callout/example/code blocks now. Add the following contracts when SP2/SP3 introduces richer content. Do not insert unrecognized accessibility properties into current pack files: the compiler rejects unknown fields.

## Proposed content contracts

| Content type | Required author input and rendering behavior |
| --- | --- |
| Pack and localized content | Valid language tag and explicit direction where needed; blocks or inline spans can override language. English is an explicit migration default only for content verified as English |
| Lesson and headings | Stable IDs, descriptive titles and a meaningful hierarchy; typed headings/lists/links, never whitespace or font styling as the only structure |
| Figure | Explicit informative/decorative classification. Informative figures require a purpose-equivalent alternative; complex figures also need a nearby detailed description or data table. Decorative images render with empty alt and no unnecessary accessible name |
| Diagram/chart | Name, description of relationships/trend and an accessible representation of relevant data; preserve labels, units and uncertainty without relying on color or position |
| Table | Caption/purpose and programmatic header relationships; simple headers by scope, complex headers by explicit associations. Split unnecessarily complex tables. Never use data tables for page layout |
| Math | Preserve a structured expression and readable equivalent explanation. Evaluate semantic MathML or a tested accessible renderer with the supported screen readers; raw TeX or a raster image alone is not the delivery contract |
| Code | Real selectable text, meaningful language label and indentation; syntax color is supplemental. Provide wrapping or a usable local scroll region without trapping the page keyboard flow |
| Link/source | Meaningful link text in context, safe destination and content-language cues where useful. Product rule: announce a new window or download in link context when applicable |
| Media | Kind, language, title, accessible player and the alternatives in the media table below; alternatives use versioned asset references |
| Question and scenario | Instructions, response labels, selection/reuse/units rules and alternative interaction metadata alongside the same stable grading IDs |
| Feedback/hints | Readable structure and equivalent alternatives, each governed by its own authorized release point |

Informative sources: [non-text alternatives](https://www.w3.org/WAI/WCAG22/Understanding/non-text-content.html), [structural relationships](https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html), [sensory instructions](https://www.w3.org/WAI/WCAG22/Understanding/sensory-characteristics.html), [images of text](https://www.w3.org/WAI/WCAG22/Understanding/images-of-text.html), [link purpose](https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html).

A figure's alt text conveys what the learner needs from the stimulus, not an unsolicited answer. For example, describe the plotted values needed to reason about a trend rather than labelling one option “correct.” Do not label informative imagery decorative to evade review. The narrow test/sensory exceptions in 1.1.1 require documented rationale and descriptive identification; they do not exempt an entire assessment from accessibility. Where a different interaction is needed, preserve the assessed learning objective and scoring semantics.

## Media requirements

| Media | A/AA delivery requirement | LearnForge publishing rule |
| --- | --- | --- |
| Prerecorded audio only | Equivalent alternative for the audio information (1.2.1) | Reviewed transcript identifies speakers and meaningful sounds |
| Prerecorded silent video | Equivalent time-based media alternative or equivalent audio track (1.2.1) | Provide a descriptive transcript; include sequence, actions and visual information |
| Prerecorded video with audio | Synchronized captions for audio (1.2.2); audio description or media alternative at A (1.2.3); audio description for video information at AA (1.2.5) | Reviewed captions plus description of relevant visuals through the main narration or a described version/track. Also provide a transcript for study/search |
| Live video with audio | Captions for live audio in synchronized media (1.2.4) | Do not launch live delivery without a captioning arrangement and accessible player; review any recording under prerecorded rules |

Sources: [1.2.1](https://www.w3.org/WAI/WCAG22/Understanding/audio-only-and-video-only-prerecorded.html), [1.2.2](https://www.w3.org/WAI/WCAG22/Understanding/captions-prerecorded.html), [1.2.3](https://www.w3.org/WAI/WCAG22/Understanding/audio-description-or-media-alternative-prerecorded.html), [1.2.4](https://www.w3.org/WAI/WCAG22/Understanding/captions-live.html), [1.2.5](https://www.w3.org/WAI/WCAG22/Understanding/audio-description-prerecorded.html).

**A transcript alone does not replace required captions or AA audio description.** If narration already communicates all relevant visual information, record that review rather than adding redundant description. Clearly identified media alternatives for existing text have specific exceptions in 1.2.1–1.2.3; reviewers must establish the exception, not infer it from a filename. Machine-generated captions must be reviewed for timing, names, equations, terminology and meaningful sounds before publication.

Players expose names, roles and states for play/pause, seek, volume, captions, description and fullscreen; all operations work by keyboard and single-pointer input. Caption styling must remain readable over the video. Product rules prohibit autoplay audio and require pause controls, adjustable playback speed where supported, and alternatives reachable beside the player. If an embed cannot meet the task contract, replace it before release. An inaccessible player is not remedied simply by adding a transcript link.

## Compiler and publishing gate

The following diagnostics are proposed additions to `ContentEngine`, not checks currently implemented. All importers and the publish endpoint must use the same checks; a browser-only gate is insufficient.

| Proposed check | Machine rejection condition | Human review still required |
| --- | --- | --- |
| `A11Y_LANGUAGE` | Missing/invalid required language or direction metadata | Correct language and pronunciation boundaries |
| `A11Y_ALTERNATIVE` | Missing required figure alternative, inconsistent decorative metadata or unresolved description reference | Meaning, adequacy and assessment equivalence |
| `A11Y_MEDIA` | Missing required caption/transcript/description assets or a recorded reviewed-narration decision | Accuracy, synchronization and whether the description covers visuals |
| `A11Y_STRUCTURE` | Malformed heading/table relationships, duplicate node IDs or inaccessible relationship targets in the typed AST | Sensible hierarchy and reading order |
| `A11Y_INTERACTION` | New question kind lacks its required alternative interaction metadata | Full operability and equivalent task difficulty |
| `A11Y_ASSET` | Missing, unsafe or mismatched asset reference in the release manifest | Appropriate visual/audio content and flashing review |

Expand templates before validation. Report diagnostic code, pack/block/question path, plain-language reason and correction guidance. A syntactically nonempty alt string is not proof of a useful alternative. Empty captions, placeholder descriptions and nonsensical language tags must not pass semantic review merely because a file exists.

For the current text-only schema, complete a manual release checklist covering headings as rendered, clear instructions, code, language, question labels and required reference access. When the richer schema ships, require both machine checks and a human accessibility review record bound to the exact pack version/asset hashes. Publishing must reject missing/stale review records; changing content after review invalidates that approval. The immutable release stores the reviewed asset references. Legacy releases need a separately tracked remediation and learner-access plan, including preserved historical attempts.

## Studio design

Provide labelled file and source inputs with format/size instructions and an accessible plain-text editing path. JSON syntax coloring must not be the only error cue. After Validate, announce a concise result count and focus a diagnostic summary for failures. Each entry identifies its content location and offers a keyboard action to reach the corresponding source/preview location; if precise editor selection is unavailable, focus the editor with the path available as text.

Preview the same sanitized renderer used for learners, including narrow screens, long content and alternate input paths. Clearly separate author-only answer-key views from learner delivery. Preview must expose alternatives, captions and language structure for review. “Compiler passed” and “Accessibility review complete” are distinct states. Before Publish, show pack identity, version, review status and confirmation; announce success with the new version and a link to the course.

The publisher's own interface must pass J8. Keep exported reports readable as HTML; any future PDF learning resource requires an accessible document review plus equivalent HTML access. Do not claim that a successful web audit certifies exported documents.

## Content release checklist

- Review every distinct lesson/question stimulus and alternative; template sampling must cover all semantic variations, with every generated item receiving structural validation.
- Inspect all media and alternatives in their final player, not just their source files.
- Verify multilingual segments, long text, symbols, equations, source links and meaningful reading order.
- Complete all interactions without seeing the screen and without dragging, preserving feedback-release restrictions.
- Record reviewer, date, pack/version, asset hashes, test environments, defects and retest evidence in the release record.
