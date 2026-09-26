# WCAG 2.2 A/AA conformance matrix

Status: design traceability, **not an audit result**. All 55 rows initially have the verification status **Not tested**. Store per-release results using the [evidence record](verification.md#release-evidence), including every applicable page, role and state. Future/conditional features are not automatically Not applicable.

Criterion links lead to W3C's explanatory documents; the [WCAG 2.2 Recommendation](https://www.w3.org/TR/WCAG22/) controls exact requirements and exceptions. The result column states LearnForge's required implementation, sometimes stronger than the criterion. Stronger defaults are identified in the design documents.

Design references: **DS** = [design system](design-system.md), **LA** = [learning and assessment](learning-and-assessment.md), **CA** = [content and authoring](content-and-authoring.md). Procedures **T01–T12** are in [verification](verification.md#test-procedures); **J1–J8** are the [complete journeys](learning-and-assessment.md#journey-contracts).

Owners: **Design** = design owner; **FE** = frontend; **API** = API/Core; **Content** = content owner; **QA** = accessibility verification. The listed owner supplies implementation/content evidence; QA validates every row. Assign actual names at release planning.

## Perceivable — 20 criteria

| Criterion | Level | Required LearnForge result | Design | Evidence procedure | Owner |
| --- | --- | --- | --- | --- | --- |
| [1.1.1 Non-text Content](https://www.w3.org/WAI/WCAG22/Understanding/non-text-content.html) | A | Figures, controls and diagrams have purpose-equivalent names/alternatives; decorative content is ignored; any narrow exception is reviewed | CA, DS | T08, T05 | Content, FE |
| [1.2.1 Audio-only and Video-only (Prerecorded)](https://www.w3.org/WAI/WCAG22/Understanding/audio-only-and-video-only-prerecorded.html) | A | Audio-only has an equivalent transcript; silent video has an equivalent descriptive alternative or audio | CA | T09; inspect each asset | Content |
| [1.2.2 Captions (Prerecorded)](https://www.w3.org/WAI/WCAG22/Understanding/captions-prerecorded.html) | A | Synchronized audio/video has accurate synchronized captions, unless its specific text-alternative exception is established | CA | T09 | Content |
| [1.2.3 Audio Description or Media Alternative (Prerecorded)](https://www.w3.org/WAI/WCAG22/Understanding/audio-description-or-media-alternative-prerecorded.html) | A | Prerecorded synchronized visual information has description or an equivalent media alternative; also satisfy 1.2.5 at AA | CA | T09 | Content |
| [1.2.4 Captions (Live)](https://www.w3.org/WAI/WCAG22/Understanding/captions-live.html) | AA | Any live synchronized media has captions; verify absence before recording non-applicability | CA | T09; live delivery fixture | Content |
| [1.2.5 Audio Description (Prerecorded)](https://www.w3.org/WAI/WCAG22/Understanding/audio-description-prerecorded.html) | AA | Required visual information in synchronized video is described in audio; transcript alone is insufficient | CA | T09; narration/description review | Content |
| [1.3.1 Info and Relationships](https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html) | A | Headings, lists, tables, response groups, labels and errors expose their structure and relationships | DS, CA | T01, T04, T08 | FE, Content |
| [1.3.2 Meaningful Sequence](https://www.w3.org/WAI/WCAG22/Understanding/meaningful-sequence.html) | A | DOM and reading order preserve lesson, scenario, blank and response meaning across layouts | DS, CA | T01, T05, T08 | FE, Content |
| [1.3.3 Sensory Characteristics](https://www.w3.org/WAI/WCAG22/Understanding/sensory-characteristics.html) | A | Instructions identify named controls/items rather than only position, color, shape or sound | CA, DS | T08, T05 | Content, Design |
| [1.3.4 Orientation](https://www.w3.org/WAI/WCAG22/Understanding/orientation.html) | AA | All current tasks work in portrait and landscape without an orientation restriction | DS | T03 | FE |
| [1.3.5 Identify Input Purpose](https://www.w3.org/WAI/WCAG22/Understanding/identify-input-purpose.html) | AA | In-scope personal-data fields expose recognized purposes through appropriate autocomplete metadata | DS, LA | T04 | FE |
| [1.4.1 Use of Color](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html) | A | Correctness, mastery, links, errors and selected states have non-color cues | DS, LA | T03, T05 | Design, FE |
| [1.4.2 Audio Control](https://www.w3.org/WAI/WCAG22/Understanding/audio-control.html) | A | Product default is no autoplay audio; any audio autoplaying over three seconds requires pause/stop or independent volume control | DS, CA | T09 | FE, Content |
| [1.4.3 Contrast (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) | AA | Text meets 4.5:1, or 3:1 where the large-text minimum applies; inspect real surfaces and exceptions | DS | T03; measured pair table | Design |
| [1.4.4 Resize Text](https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html) | AA | Text resizes to 200% without losing content or operation, within the criterion's scope | DS | T03 | FE |
| [1.4.5 Images of Text](https://www.w3.org/WAI/WCAG22/Understanding/images-of-text.html) | AA | Lessons, prompts and instructions use real text except permitted essential/customizable cases | CA, DS | T08 | Content, Design |
| [1.4.10 Reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html) | AA | Preserve tasks at 320 CSS px width or 256 CSS px height as applicable; justify local two-dimensional exceptions | DS | T03 | FE |
| [1.4.11 Non-text Contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html) | AA | Needed control/state and graphic information meets 3:1 against adjacent colors, subject to criterion exceptions | DS | T03 | Design |
| [1.4.12 Text Spacing](https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html) | AA | Combined line/paragraph/letter/word spacing overrides cause no loss where language/script supports them | DS | T03 | FE |
| [1.4.13 Content on Hover or Focus](https://www.w3.org/WAI/WCAG22/Understanding/content-on-hover-or-focus.html) | AA | Additional hover/focus content is dismissible, hoverable and persistent as required | DS | T03, T02 | FE |

## Operable — 20 criteria

| Criterion | Level | Required LearnForge result | Design | Evidence procedure | Owner |
| --- | --- | --- | --- | --- | --- |
| [2.1.1 Keyboard](https://www.w3.org/WAI/WCAG22/Understanding/keyboard.html) | A | All current functions, including question responses and publisher actions, work by keyboard | DS, LA | T02, T05, T11 | FE |
| [2.1.2 No Keyboard Trap](https://www.w3.org/WAI/WCAG22/Understanding/no-keyboard-trap.html) | A | Learners can exit every widget; modal containment includes accessible dismissal | DS | T02 | FE |
| [2.1.4 Character Key Shortcuts](https://www.w3.org/WAI/WCAG22/Understanding/character-key-shortcuts.html) | A | No global single-character shortcuts by default; future ones support off/remap or focus-only activation | DS | T02, T12 | FE |
| [2.2.1 Timing Adjustable](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html) | A | Mocks offer pre-start adjustment through 10×; every other content-controlled limit has a qualifying mechanism or documented exception | LA | T06 | API, FE |
| [2.2.2 Pause, Stop, Hide](https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html) | A | Qualifying automatic movement lasting over five seconds can be paused/stopped/hidden; qualifying auto-updates have control regardless of duration | DS, CA | T09, T12 | FE, Content |
| [2.3.1 Three Flashes or Below Threshold](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html) | A | No content exceeds allowed flash frequency/thresholds; product UI avoids flashing entirely | DS, CA | T09 | Content, FE |
| [2.4.1 Bypass Blocks](https://www.w3.org/WAI/WCAG22/Understanding/bypass-blocks.html) | A | Working visible-on-focus skip link bypasses repeated shell content; landmarks identify major regions | DS | T01, T02 | FE |
| [2.4.2 Page Titled](https://www.w3.org/WAI/WCAG22/Understanding/page-titled.html) | A | Every route and substantive view has a descriptive, updated document title | DS | T01 | FE |
| [2.4.3 Focus Order](https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html) | A | Focus sequence preserves meaning and operation through route, question, dialog and save transitions | DS, LA | T02, T07 | FE |
| [2.4.4 Link Purpose (In Context)](https://www.w3.org/WAI/WCAG22/Understanding/link-purpose-in-context.html) | A | Destination/purpose is understandable from the link and its programmatically determined context | DS, CA | T01, T08 | Content, FE |
| [2.4.5 Multiple Ways](https://www.w3.org/WAI/WCAG22/Understanding/multiple-ways.html) | AA | Non-process pages can be found in multiple ways; do not apply the process exception to the whole catalog | DS | T01 | Design, FE |
| [2.4.6 Headings and Labels](https://www.w3.org/WAI/WCAG22/Understanding/headings-and-labels.html) | AA | Headings and labels describe topic or purpose, including repeated question/slot actions | DS, LA | T01, T04, T05 | Content, FE |
| [2.4.7 Focus Visible](https://www.w3.org/WAI/WCAG22/Understanding/focus-visible.html) | AA | Every keyboard-operable control has visible focus in all supported themes | DS | T02, T03 | Design, FE |
| [2.4.11 Focus Not Obscured (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html) | AA | Author-created content never completely hides the focused component; product rule keeps it fully visible | DS | T02, T03 | FE |
| [2.5.1 Pointer Gestures](https://www.w3.org/WAI/WCAG22/Understanding/pointer-gestures.html) | A | Multipoint or path-based gestures, if introduced, have a single-pointer alternative unless essential | DS, LA | T02, T12 | FE |
| [2.5.2 Pointer Cancellation](https://www.w3.org/WAI/WCAG22/Understanding/pointer-cancellation.html) | A | Pointer actions use release activation or a qualifying cancellation/undo mechanism | LA | T02 | FE |
| [2.5.3 Label in Name](https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html) | A | Accessible names contain visible text labels; test voice activation using that text | DS | T02, T04 | FE |
| [2.5.4 Motion Actuation](https://www.w3.org/WAI/WCAG22/Understanding/motion-actuation.html) | A | No required device/user motion; future motion features have UI alternatives and can disable motion response, unless excepted | DS, LA | T12 | FE |
| [2.5.7 Dragging Movements](https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html) | AA | Matching and ordering work with individual pointer activations without dragging; also satisfy keyboard access | LA | T02, T05 | FE |
| [2.5.8 Target Size (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) | AA | Targets meet 24 × 24 CSS px or a specifically tested exception; product defaults to 44 × 44 standalone controls | DS | T03; target measurements | Design, FE |

## Understandable — 13 criteria

| Criterion | Level | Required LearnForge result | Design | Evidence procedure | Owner |
| --- | --- | --- | --- | --- | --- |
| [3.1.1 Language of Page](https://www.w3.org/WAI/WCAG22/Understanding/language-of-page.html) | A | Each page declares its actual default human language | DS, CA | T08 | FE, Content |
| [3.1.2 Language of Parts](https://www.w3.org/WAI/WCAG22/Understanding/language-of-parts.html) | AA | Changes of human language are exposed programmatically within the criterion's scope | DS, CA | T08 | Content, FE |
| [3.2.1 On Focus](https://www.w3.org/WAI/WCAG22/Understanding/on-focus.html) | A | Receiving focus alone never submits, navigates away or causes an unexpected context change | DS | T02, T04 | FE |
| [3.2.2 On Input](https://www.w3.org/WAI/WCAG22/Understanding/on-input.html) | A | Input changes do not unexpectedly change context; answer selection does not advance or submit | DS, LA | T04, T05 | FE |
| [3.2.3 Consistent Navigation](https://www.w3.org/WAI/WCAG22/Understanding/consistent-navigation.html) | AA | Repeated navigation retains the same relative order across pages | DS | T01 | Design, FE |
| [3.2.4 Consistent Identification](https://www.w3.org/WAI/WCAG22/Understanding/consistent-identification.html) | AA | The same function has consistent identification throughout the product | DS | T01, T05 | Design, Content |
| [3.2.6 Consistent Help](https://www.w3.org/WAI/WCAG22/Understanding/consistent-help.html) | A | Repeated help mechanisms retain consistent relative placement; product adds shared accessible help | DS | T12 | Design, FE |
| [3.3.1 Error Identification](https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html) | A | Detected input errors identify affected fields/items and explain the problem in text | DS, LA | T04, T11 | FE, API |
| [3.3.2 Labels or Instructions](https://www.w3.org/WAI/WCAG22/Understanding/labels-or-instructions.html) | A | Fields and tasks provide labels, required state and necessary format/response instructions before entry | DS, LA | T04, T05 | Content, FE |
| [3.3.3 Error Suggestion](https://www.w3.org/WAI/WCAG22/Understanding/error-suggestion.html) | AA | Known corrections are provided unless they would compromise security or purpose | DS, LA | T04, T11 | API, FE |
| [3.3.4 Error Prevention (Legal, Financial, Data)](https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html) | AA | Test responses and destructive data actions have applicable reversal, checking or review/confirmation safeguards | LA | T10 | API, FE |
| [3.3.7 Redundant Entry](https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html) | A | Reuse or offer selection of already supplied information within a process, unless a specific exception applies | LA | T04 | API, FE |
| [3.3.8 Accessible Authentication (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html) | AA | Every authentication step permits a qualifying assistance mechanism or alternative; paste/managers and recovery are tested | LA | T04 | API, FE |

## Robust — 2 criteria

| Criterion | Level | Required LearnForge result | Design | Evidence procedure | Owner |
| --- | --- | --- | --- | --- | --- |
| [4.1.2 Name, Role, Value](https://www.w3.org/WAI/WCAG22/Understanding/name-role-value.html) | A | All controls expose correct names, roles, values/states and change notifications, including repeated custom widgets | DS, LA | T02, T04, T05 | FE |
| [4.1.3 Status Messages](https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html) | AA | Save, result, progress and relevant error statuses are available to assistive technology without moving focus | DS, LA | T07, T11 | FE |

## Full conformance review

There are 31 Level A and 24 Level AA criteria in this matrix. Do not add obsolete 4.1.1 as a WCAG 2.2 gate, or silently omit inherited criteria when selecting automated rules. AAA enhancements do not replace failed A/AA requirements.

In addition to criterion results, the release owner verifies the [conformance requirements](https://www.w3.org/TR/WCAG22/#conformance-reqs): scope covers whole pages and complete processes, relied-upon technology is accessibility supported, and other content does not interfere. Non-interference includes audio control, no keyboard trap, flash limits and pause/stop/hide requirements. A third-party embed cannot be ignored merely because it is not relied upon. Record the conclusion with the release evidence before publishing a claim.
