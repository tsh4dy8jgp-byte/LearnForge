# Accessible learning and assessment

Status: required design; new contracts below are planned. Parent: [baseline](README.md). Shared interaction rules: [design system](design-system.md).

## Journey contracts

The `J` identifiers are stable references for the [verification procedures](verification.md).

| Journey | Required outcome, including failure paths |
| --- | --- |
| J1 Account access | Register, sign in, recover access and change password using keyboard, screen reader and password manager; retain valid fields after errors; explain lockout and recovery without a memory puzzle |
| J2 Find and manage a course | Search the library, enroll, archive and restore; report result counts and updated enrollment while maintaining focus; archive explains that learning records remain |
| J3 Read and navigate | Operate all course tabs, select lessons, read the objective map and references, mark completion and recognize revised content; provide equivalent structured access to diagrams |
| J4 Answer and save | Complete each question kind, check learning feedback, navigate questions, recover failed saves and resolve stale revisions without losing the learner's response silently |
| J5 Timed mock | Configure time before starting, understand section locks, resume with the same deadline, submit after review and understand expiry and readiness eligibility |
| J6 Review progress | Read historical answers, explanations, fully-correct and partial-credit scores, mastery, readiness and next steps with equivalent text/table access |
| J7 Manage account data | Export history and review/cancel/confirm permanent deletion; announce errors and completion; password confirmation supports paste and managers |
| J8 Author and publish | Import/edit JSON, navigate diagnostics, preview learner delivery, review content alternatives and confirm publication of the intended immutable version |

For each journey, retain task context through authentication redirects, validation and retries. Reuse information already supplied in the same process unless it is invalid or a documented security/essential exception applies. Do not introduce a repeated-entry burden just to work around a missing state model. See [redundant entry](https://www.w3.org/WAI/WCAG22/Understanding/redundant-entry.html).

## Question player

Expose the question number, total, prompt, scenario context and response instructions before the controls. Include question context in the response group's accessible name. Navigating to another question focuses its heading; selecting an answer does not advance automatically. The question navigator names each destination and exposes current, answered, unanswered and locked states in text, with a discoverable reason for locks.

| Format | Interaction and feedback contract |
| --- | --- |
| Single choice | Native radio group; arrow keys change selection, Tab enters/leaves; visible labels and checked state match the saved response |
| Select N of M | Native checkboxes and visible selection count. At the limit, explain how to deselect/change an answer; do not rely on unexplained disabled choices. Announce limit errors without clearing selections |
| Dropdown blanks | Native select per blank with a unique label describing that blank, not only “Choose”; placeholder is not a valid answer; preserve reading order of surrounding text |
| Matching | Both drag/drop and select-token-then-select-target use the same answer contract. Name destination and current assignment, e.g. “Cause: evaporation.” Provide “Clear Cause”; announce source, destination and any displaced assignment. State whether reuse is allowed |
| Sequence | Up/down buttons work with keyboard and individual taps; expose item position and total. Keep focus with the moved item, including when its previous button becomes unavailable at an edge. Announce “Evaporation moved to 2 of 4.” Persist the same order for all input methods |

Drag alternatives must support a **single pointer without dragging** as well as keyboard. Keyboard-only drag emulation does not by itself satisfy 2.5.7. Activate destructive pointer actions on release or allow cancellation/undo; do not commit on pointer-down. Sources: [dragging](https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html), [pointer cancellation](https://www.w3.org/WAI/WCAG22/Understanding/pointer-cancellation.html).

Render repeated question instances with unique, stable identifiers derived from instance, question and slot identity. Do not use global `token-bank` or slot-only IDs. Correctness, locked feedback and saving state must be programmatically available. Announce newly released feedback once; keep the explanation in ordinary reading order. Screen-reader output must obey the same feedback-release policy as visual delivery: hidden DOM, ARIA labels and descriptions must not contain unreleased answers.

For new SP3 kinds, include the accessible interaction in the initial renderer contract: numeric fields with units and format hints; labelled text/cloze fields; categorize controls using per-item destination selection; hotspots with an equivalent named-region selection where it preserves the learning objective; written responses with labelled editors and readable rubrics. If an equivalent stimulus cannot preserve the assessed skill, obtain content/accessibility review before supporting the question kind. An alternate interaction must not reveal the key or grade differently.

## Timing decision

**Product decision:** ordinary practice mocks do not assume that timing is essential. Offer a pre-start duration adjustment from 1× through 10× the blueprint duration, with familiar presets and a labelled custom multiplier in that range. Show the resulting minutes before the learner selects Start. This implements the adjustment option in [SC 2.2.1](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html); the criterion also permits disabling a limit, a qualifying extension mechanism or a documented exception. A 1.5×/2× option alone does not implement its tenfold adjustment option.

Existing untimed learning mode is useful, but differs in feedback and readiness and does not establish conformance for the timed-mock process. Do not require disclosure of a diagnosis or staff permission to select the supported duration. Do not use “exam” as an automatic essential-timing exception. A future externally regulated assessment needs a separately reviewed timing design and documented criterion rationale.

### Planned server and persistence contract

These are semantic requirements, not JSON fields supported by the current API. Implement typed Core/API contracts and regenerate frontend types together.

1. Start setup accepts a supported multiplier, validates it on the server and shows the effective duration. Permit increments of 0.5 from 1 through 10; reject unsupported values. The base duration comes from the immutable blueprint, never from a client-provided deadline.
2. Snapshot base duration, multiplier, effective duration and timing-policy version into the attempt. Derive the absolute deadline on the server. Include the multiplier in idempotency payload comparison; replaying a start must not extend an existing attempt.
3. Return the effective policy and deadline for display. Reload, another tab, reconnect, migration and expiry workers use the same stored deadline. Do not silently switch to the default duration. Learning mode remains untimed.
4. Retain normal mock grading, answer-release, fresh-family and completion rules. An adjusted duration alone must not disqualify readiness evidence; evaluate on-time completion against the effective deadline. Explain the chosen duration in results without implying a standardized exam comparison. Accessibility input methods are not instructional assistance and must not lower mastery/readiness evidence.
5. Preserve release immutability and historical attempt snapshots. Provide migrations for both database providers and an explicit default policy for old attempts. Test timeout/submit races and idempotent replay with the new timing data.

This is a planned extension to [assessment behavior](../assessment.md), not a statement that the current deadline model already supports accommodations.

### Timer and other time limits

Show a readable time-remaining label and let learners hide the visual countdown while retaining warnings. Product defaults: announce once at 5 minutes and 1 minute remaining where those thresholds occur, and once on expiry; never announce every tick. Allow the learner to request the current time. Do not move focus for warnings. A voluntary submission confirmation does not pause the server clock; state that clearly.

Review every content-controlled limit, including session inactivity, recovery codes, temporary notices and publisher drafts. For an inactivity limit using the extension option, show a warning with at least 20 seconds to respond and a simple Extend session action, allowing at least ten extensions. Measure actual usable response time, including rendering and network behavior. Session extensions must not change an assessment deadline. A specific security token may need a documented exception; a generic “security” label is not a blanket exemption for session timeouts.

On expiry, preserve the last accepted answers, announce the result and explain unsaved local changes honestly. Move focus to the completion heading only when the completed view replaces the task. Never claim an unsent answer was submitted. Treat draft preservation and reauthentication recovery as product safeguards in addition to applicable A/AA requirements.

## Submission, locking and recovery

Before final submission, show answered/unanswered counts, links to review editable responses and an explicit confirmation. Provide correction before irreversible section locking; the review covers that section and explains which earlier sections are already locked. The confirmation's Cancel/Escape behavior returns the learner to the original control. Learning feedback release also explains that checking locks the response when that is the behavior.

Use the shared inline or modal confirmation contract. Do not treat an alert announcement as a complete confirmation interaction. Permanent account deletion similarly exposes affected data, cancellation and a separate final action. This design uses review/confirmation to satisfy the applicable test-response and data-protection requirements in [3.3.4](https://www.w3.org/WAI/WCAG22/Understanding/error-prevention-legal-financial-data.html).

Autosave retains stable focus and clearly distinguishes Saving, Saved and Not saved. A stale revision or offline draft presents labelled choices to retry or use the server response, explains the effect and preserves the local response until the learner resolves it. Recovering focus must not trigger another answer write. Error recovery must be keyboard and screen-reader operable, including after authentication expiry.

## Authentication and future services

Passwords are allowed when mechanisms such as password managers and copy/paste assist entry. Preserve appropriate autocomplete metadata; allow paste in every credential and code field. Prefer one code field over fragmented boxes that obstruct pasting. Do not require memorized secrets, transcription or puzzles without a qualifying alternative or assistance mechanism at that step. LearnForge's product rule avoids cognitive challenges and security questions, even where AA permits particular object-recognition/personal-content exceptions. Passkeys and any fallback/recovery path must each be tested. See [accessible authentication](https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html).

Future flashcards use an explicit reveal action, retain reading order and expose grading choices without gesture-only controls. Offline reading includes the same alternatives, language and navigation as online delivery. Optional AI provides a Stop action, readable citations and a completed-response announcement; do not put token-by-token output into an assertive live region. Generated diagrams, math and explanations follow the same content-review and feedback-release rules as authored content.
