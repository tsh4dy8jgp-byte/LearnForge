# Accessible design system

Status: required design; implementation must be verified. Parent: [accessibility baseline](README.md). Criterion coverage: [matrix](conformance-matrix.md).

## Layout and visual specifications

Use semantic design tokens for text, muted text, links, surfaces, borders, focus, selection, success and error. Each theme needs a measured foreground/background pair table covering default, hover, focus, selected, error and disabled states. Existing brand colors are not approved until measured in their rendered context.

| Property | WCAG acceptance | LearnForge product rule |
| --- | --- | --- |
| Text contrast | At least 4.5:1; 3:1 for large text (at least 18 pt regular or 14 pt bold). Relevant exemptions include inactive controls and logos | Use 4.5:1 for all instructional and interface text, including muted captions |
| Controls and graphics | At least 3:1 against adjacent colors for visual information needed to identify controls, states and meaningful graphics, subject to the criterion's exceptions | Visible control boundaries and selected states; measure icons, progress indicators and chart marks |
| Color use | Meaning cannot depend on color alone | Pair errors, answer correctness and mastery with words or another visible cue |
| Text size | Resize text to 200% without losing content or operation, within the criterion's scope | Relative units; no fixed-height text containers or disabled browser zoom |
| Reflow | At 320 CSS px width for vertical content, or 256 CSS px height for horizontal content, preserve information and operation without two-dimensional scrolling except genuinely two-dimensional content | Stack sidebars, question navigator and forms; keep horizontal overflow local to qualifying tables/diagrams |
| Text spacing | No loss with line height 1.5, paragraph spacing 2, letter spacing 0.12 and word spacing 0.16 times font size, applied together where supported by the language/script | These are override test values, not compulsory default typography |
| Orientation | Support portrait and landscape unless orientation is essential | No orientation lock for any current learner task |
| Pointer targets | At least 24 × 24 CSS px or a valid 2.5.8 exception | Default standalone buttons and answer targets to at least 44 × 44 CSS px; document smaller controls |

Sources: [text contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html), [non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html), [color](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html), [resize](https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html), [reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html), [spacing](https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html), [orientation](https://www.w3.org/WAI/WCAG22/Understanding/orientation.html), [target size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html).

For a smaller target relying on spacing, a 24 CSS px diameter circle centered on its bounding box must not intersect another target or such a circle around another undersized target. The other 2.5.8 exceptions cover an equivalent control on the same page, inline targets, unmodified user-agent controls and essential presentation. Record the particular exception; a generic gap between buttons is not sufficient evidence.

Text links remain identifiable without color alone. Content uses real text rather than images of text except where the criterion permits it. Charts provide a nearby summary and the underlying values in an accessible table. Account for forced-colors mode: focus, selections, boundaries and errors must remain discernible without background images or color fills. Forced-colors verification is a product compatibility rule.

## Semantics and page shell

Use native HTML controls before custom ARIA. Links navigate; buttons act. Avoid clickable generic elements, nested interactive controls, positive `tabindex` and broad `role="application"`. Give every relationship a unique ID, including repeated questions and inline checks. Associate headings, legends, labels, descriptions and errors with the controls they describe.

Each route has a descriptive document title, a clear primary heading and one main landmark. Label additional navigation regions distinctly; expose the active route with `aria-current="page"`. The first keyboard destination is a visible-on-focus skip link that moves focus to main content. Keep navigation order and action names consistent across pages. Provide more than one way to locate non-process pages: the library/search, main navigation and contextual course links. A sequential test step does not need a separate site-search entry.

Set the document language. Mark language changes in content and supply direction metadata independently; language and direction are different properties. Use semantic headings, lists, table headers and DOM order that remain meaningful with CSS removed. Instructions name controls and items rather than only describing location, shape, sound or color.

Sources: [relationships](https://www.w3.org/WAI/WCAG22/Understanding/info-and-relationships.html), [sequence](https://www.w3.org/WAI/WCAG22/Understanding/meaningful-sequence.html), [multiple ways](https://www.w3.org/WAI/WCAG22/Understanding/multiple-ways.html), [page language](https://www.w3.org/WAI/WCAG22/Understanding/language-of-page.html), [language changes](https://www.w3.org/WAI/WCAG22/Understanding/language-of-parts.html).

## Keyboard and focus contract

All actions work with keyboard alone and with a single pointer without dragging. Preserve native key behavior. Ship no global single-character shortcuts by default; a future shortcut must be disableable, remappable to include a non-character key, or active only when its component has focus.

WCAG AA requires visible keyboard focus and that author-created content not entirely hide the focused component. **Product rule:** keep the entire focused control and its focus indicator visible, including beneath sticky headers and footers. Start with a 2 CSS px solid outline, 2 CSS px offset and at least 3:1 contrast against adjacent surfaces; adapt the treatment when backgrounds change. This is a LearnForge design token, not a claim that 2.4.13 is an AA requirement. Do not remove outlines without an effective replacement. Configure scroll padding/margins and test at zoom.

| Event | Focus behavior |
| --- | --- |
| Completed client-side route navigation | Update title, then focus the new page heading or main region after it renders; do not also announce the entire page |
| Browser Back/Forward | Restore the previous meaningful position if it still exists; otherwise focus the destination heading |
| User selects another lesson or question | Focus the new lesson/question heading, including its ordinal context |
| Autosave, progress update or background fetch | Leave focus in place; announce a concise relevant status |
| Item is removed, archived or reordered | Retain the acted-on item/control where possible, or move to the nearest logical surviving control |
| Form submission fails | Focus a linked error summary; individual links move to their invalid field |
| Confirmation is cancelled | Return focus to its opener |
| Action completes and removes its opener | Focus the result heading or next meaningful action |

Sources: [keyboard](https://www.w3.org/WAI/WCAG22/Understanding/keyboard.html), [shortcuts](https://www.w3.org/WAI/WCAG22/Understanding/character-key-shortcuts.html), [focus visible](https://www.w3.org/WAI/WCAG22/Understanding/focus-visible.html), [focus not obscured](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html), [focus order](https://www.w3.org/WAI/WCAG22/Understanding/focus-order.html).

## Component contracts

| Component | Required design and acceptance |
| --- | --- |
| Button or link | Accessible name contains its visible label; contextual suffixes distinguish repeated actions such as “Clear answer for Cause.” Icon-only controls have specific names |
| Form field | Persistent visible label; appropriate input type/autocomplete; instructions and required state before entry; associated errors and `aria-invalid` when invalid; preserve other entered values |
| Radio/checkbox group | Native inputs in a fieldset; legend contains task context and selection instructions; checked state exposed; no automatic navigation on selection |
| Tabs | Labelled tablist, tab/panel relationships, selected state and one tab stop. Arrow keys move among tabs; Tab exits the tablist. Product rule: support Home/End. Use Enter/Space activation if loading introduces noticeable delay |
| Modal confirmation | Labelled dialog, focus inside, background inert, Tab/Shift+Tab contained, Escape and visible Cancel close it. Initial focus favors Cancel for irreversible actions, or a heading for long content; restore focus on cancellation |
| Inline confirmation | A labelled region immediately following its trigger; focus its heading when opened, provide explicit confirm/cancel, retain context, and return to trigger when cancelled. Do not label it modal or trap focus |
| Disclosure | Native `details`/`summary` or a button with expanded state and associated panel; hidden contents are not keyboard destinations |
| Progress or mastery | Name, current value/state and meaningful denominator where applicable. “Proficient: 4 of last 5 families correct” is available as text |
| Tooltip or popover | Available on focus as well as hover, hoverable, persistent while in use and dismissible as required; no unique instructions only in a tooltip |
| Search/filter | Named input and clear action; announce settled result count; do not move focus or reorder the control itself during typing |

Tabs and dialogs follow the informative [APG tabs](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/) and [modal dialog](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/) patterns. Test the rendered Angular components rather than assuming a package guarantees behavior. Other sources: [label in name](https://www.w3.org/WAI/WCAG22/Understanding/label-in-name.html), [error identification](https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html), [hover/focus content](https://www.w3.org/WAI/WCAG22/Understanding/content-on-hover-or-focus.html).

## Dynamic states, motion and help

Create a stable polite status region for brief save, result-count and completion updates. Reserve alerts for urgent errors requiring attention. Do not move focus to routine messages, announce every timer second, place an entire form in a live region, or speak the same message through both focus and an alert. Busy state must have readable text. If disabling a focused control would lose focus, keep it focusable with an enforced unavailable state or restore focus deliberately. Prevent duplicate submissions in the handler and server.

Every component design includes loading, empty, invalid, unavailable, success and failure states. Errors include a specific recovery action. Messages and recovery actions remain available until dismissed or resolved; a disappearing toast is never the sole way to act. Do not expose hidden answer keys in accessible names, descriptions or live regions.

Product rules: no autoplay audio, flashing effects or auto-advancing instructional content; honor reduced-motion preferences and disable decorative animation. If future moving or auto-updating content is introduced, meet pause/stop/hide requirements before release. Add a consistently positioned Help entry to the shell, with readable self-help and a keyboard-accessible barrier-report form. Do not require authentication to report an inaccessible sign-in flow. Consistent placement is required by 3.2.6 when repeated help mechanisms exist; adding this help entry is a product decision.

Sources: [status messages](https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html), [pause/stop/hide](https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html), [flashing](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html), [consistent help](https://www.w3.org/WAI/WCAG22/Understanding/consistent-help.html).
