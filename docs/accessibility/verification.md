# Accessibility verification and release

Status: required verification plan. The repository's existing tests are documented in [testing](../testing.md). New automation and manual checks below are planned; no conformance result is asserted by this document.

## Release evidence

Create `docs/accessibility/evidence/<release-id>/` when a release is evaluated. Do not populate it with assumed passes. Record the commit/build, deployed environment, pack versions and asset hashes; all in-scope routes, roles and feature flags; complete process boundaries; reviewer names; dates; and exact OS/browser/assistive-technology versions. Evidence must be reproducible without an operational database or real learner records.

Maintain one record for each [matrix](conformance-matrix.md) criterion, with links to affected pages, states and test artifacts. Valid outcomes are **Pass, Fail, Not applicable, Not tested**. A release starts Not tested. Conditional future media or motion criteria become Not applicable only after the reviewer establishes that the relevant content is absent throughout the defined scope. Reopen them when the feature or content changes.

| Evidence field | Required value |
| --- | --- |
| Criterion and requirement | SC ID, product rule if stronger, and design/journey references |
| Scope | Route, role, state, viewport, content release and reproduction fixture |
| Environment | Exact browser/OS/AT versions, settings and input method |
| Method/result | Automated rule and/or manual procedure, expected versus observed behavior |
| Artifact | Test report, screenshot, trace or concise screen-reader/focus observations |
| Disposition | Pass/fail/not tested, or precise non-applicability/exception rationale |
| Accountability | Tester/date, defect link, owner and retest evidence |

An exception record identifies the exact criterion provision, why it applies, affected content and alternatives, reviewer and trigger for re-evaluation. A budget or deadline is not a criterion exception. A real failure stays Fail even when a workaround exists. Sampled testing must record its coverage and limits; an untested component, state, content variant or process cannot inherit a pass by assumption.

## Environment coverage

This is the initial supported verification matrix, not a statement that it has already passed. Record exact versions per release and refresh them as support changes.

| Environment | Required coverage |
| --- | --- |
| Windows + Chrome + NVDA | All J1–J8 journeys; keyboard, browse/forms modes and announcements |
| Windows + Firefox + NVDA | Question formats, forms, tabs, dialogs and any math/media renderer |
| macOS + Safari + VoiceOver | All J1–J8 journeys; control navigation and reading order |
| iOS + Safari + VoiceOver | J1–J7 plus J8's supported responsive workflow; touch exploration, rotor, orientation and zoom |
| Android + Chrome + TalkBack | J1–J7 plus J8's supported responsive workflow; exploration, gestures and control names |
| Keyboard without screen reader | All routes in Chrome, Firefox and Safari; Tab, Shift+Tab, arrows, Enter, Space, Escape |
| Windows forced colors and desktop zoom | Every shared component/state, all page templates and active/completed attempt views |
| Voice input and single-pointer touch | Match spoken visible labels to controls; complete matching/ordering without dragging |

Mobile emulation in Playwright does not replace a mobile assistive-technology/device test. The current Playwright project is Chromium-only; add Firefox/WebKit coverage explicitly before describing those engines as automated. An unavailable test environment leaves its checks Not tested and must be resolved before claiming the stated support scope.

## Fixtures and automation

Use disposable learner and publisher accounts. Fixtures include every current question kind, case studies with locked sections, enough data for history/pagination, long labels, empty courses, revised lessons, mastered/unstarted objectives, repeated question instances, server failures, stale revisions, unsaved drafts and near-expiry attempts. Add mixed language/RTL, media and rich-content fixtures before their first release.

Planned layers:

- **Component tests:** accessible names/relationships, keyboard operation, focus after reorder/remove, error association and stable status regions. Check behavior, not snapshots that merely restate the template.
- **Browser journeys:** J1–J8 across critical states, with asserted focus destination and retained response after retries. Add an axe-compatible browser scanner such as `@axe-core/playwright`; it is not currently a dependency. Configure all available WCAG 2.2 A/AA rule tags, including inherited 2.0/2.1 rules, and record the actual engine version/rule set. Fail on applicable violations and review incomplete results. Best-practice findings are tracked separately.
- **Core/API/compiler tests:** effective duration and validation, start replay, expiry/submit races, historical snapshots, alternative metadata and unsafe/key-bearing delivery, with both database providers where persistence changes.
- **Manual evaluation:** the procedures below, with assistive technology and content review. Automated scans cannot judge the whole task, alternatives, timing rationale or semantic equivalence.

A clean scan is one piece of evidence, not an AA verdict. Do not suppress a rule or exclude an inaccessible region to obtain a green report. A proven scanner false positive needs a narrowly scoped explanation and equivalent manual evidence.

## Test procedures

Each procedure has an ID used in the conformance matrix. Execute it over all relevant journeys and states; record criterion-specific results rather than one blanket pass.

### T01 Structure and navigation

Open every route as each relevant role, including direct entry and Back/Forward. Inspect title, primary heading, landmarks, heading/list/table structure, current navigation and DOM reading order. Use the skip link and a screen-reader heading/link list. Locate non-process pages through navigation and library/search. Verify links and repeated controls have understandable, consistent names. **Pass:** location, relationships and destination are understandable without visual arrangement; skip and route focus work.

### T02 Keyboard, pointer and focus

Complete each J1–J8 task with the pointer unplugged, then complete drag interactions using individual taps/clicks. Traverse forwards and backwards, open/close dialogs, switch tabs and reorder to both list boundaries. Inspect visible focus beneath sticky content at zoom; verify no trap, unexpected focus jump or lost focus during saves. Press, move off and release a pointer target to check cancellation behavior. Inspect shortcut/motion settings if introduced. **Pass:** every action has its required input path, correct name/state and predictable focus.

### T03 Visual presentation and adaptation

Measure rendered contrast for all token pairs/states using a contrast tool; include text over images and meaningful graphics. Verify information without color. Test 200% text resizing, a 1280 CSS px-wide desktop layout at 400% zoom (approximately 320 CSS px available width), and a 320 CSS px viewport; also test the 256 CSS px height condition for horizontally flowing content. Browser zoom and a narrow viewport are separate checks. Exceptions remain local to genuinely two-dimensional content.

Apply all four text-spacing overrides together: line height 1.5, paragraph spacing 2, letter spacing 0.12 and word spacing 0.16 times font size. Test portrait/landscape, long translations, forced colors and reduced motion. Measure target bounds in CSS pixels, including small Clear/Move controls and any spacing exception. Open hover/focus content, move onto it and dismiss it. **Pass:** no loss of information/action, clipping or obscured focus; all measured minima and the selected product rules hold.

### T04 Forms, errors and authentication

Use registration, sign-in, setup, settings and future recovery flows. Read labels, required states, instructions, autocomplete purposes and errors with a screen reader. Submit invalid values, follow summary links, correct one field and verify other values persist. Change selections without activating submit and verify no unexpected context change. Use a real password manager and paste credentials/codes through every step. Check repeated-entry handling. **Pass:** each step is understandable and usable without unaided memory/transcription; errors identify the field and suggest correction when known and safe.

### T05 Questions and results

Complete every format in learning and mock mode, including clear/replace, selection limits, token reuse, sequence boundaries and multiple instances. Repeat with keyboard, screen reader and single-pointer controls. Compare saved answer payloads and scores across methods. Check scenario context, ordinal position, navigator states, feedback timing and result tables. Inspect delivery and the accessibility tree for unreleased keys. **Pass:** equivalent task completion and scoring, coherent focus/status and no answer leakage.

### T06 Time limits and resume

Start mocks at 1×, 1.5× and 10× and test a supported custom multiplier; reject invalid values server-side. Verify displayed and stored effective duration. Reload, retry the same start request, use a second tab and simulate delayed/offline saves; the deadline remains authoritative. Test warnings and expiry without per-second screen-reader speech. Verify adjusted timing alone does not change eligibility.

For each other content-controlled timeout, record its 2.2.1 mechanism or exact exception. If using extension, demonstrate at least 20 usable seconds and at least ten extensions, keyboard/screen-reader access and no unintended assessment extension. **Pass:** every applicable limit meets its documented mechanism; accepted and unsaved responses are distinguished accurately after expiry.

### T07 Dynamic states and recovery

Trigger loading, empty, saved, save failure, API unavailable, stale revision, archive/restore, completion and search-result changes. Listen to announcements and record focus before/after. Test dismissible messages and recovery controls after their initial appearance. **Pass:** relevant status is announced without forcing focus or repeated speech, and recovery remains available without silent data loss.

### T08 Content and language

Inspect each released content type and semantically distinct stimulus with its alternatives; verify table headers, descriptions, code/math, language boundaries, RTL order and instructions without sensory-only references. Confirm descriptions preserve the same reasoning task. Test future downloadable resources separately. **Pass:** structure and alternatives communicate equivalent information, with correct language and authorized feedback.

### T09 Media, motion and audio

Play each asset in its final player; compare captions/transcripts with speech, speaker changes, meaningful sounds and visual information. Check audio description or recorded complete-narration rationale for synchronized video. Test live captions when live media is in scope. Operate controls with keyboard and screen reader; test pause/stop/hide and volume controls where applicable. Inspect animation/video for flashing and review questionable assets with an appropriate flash-analysis method before use. **Pass:** required alternatives are accurate and reachable, controls work and no interfering audio/motion/flash behavior remains.

### T10 Irreversible actions

Review, correct, cancel and confirm test submission, section locking, feedback locking where applicable, account deletion and publishing. Test Escape, focus return, duplicate activation and server failure. **Pass:** the specified review/correction opportunity exists before finalization, consequences are clear, and failed or cancelled actions do not commit.

### T11 Publishing

Import invalid JSON, a structurally invalid pack and a valid pack lacking human accessibility review. Follow diagnostics to content, correct issues and preview all relevant renderers. Verify planned review metadata becomes stale when source/assets change and is enforced server-side at publication. **Pass:** the author workflow is accessible and only the exact reviewed release can pass its publication gate. Until this gate is implemented, record the missing implementation as outstanding.

### T12 Help and future input methods

Locate help consistently across pages and breakpoints, including signed-out barriers. Submit a report with keyboard and screen reader. If motion input, path/multipoint gestures, auto-updating content or shortcuts are introduced, verify their ordinary-control alternatives and disable/remap/pause behavior as applicable. **Pass:** help and input alternatives remain discoverable and usable across the process.

## Delivery gates

Release gates apply whether or not LearnForge publishes a formal conformance claim. Existing gaps must be remediated before the first release under this baseline; documenting them is not permission to ship a failing journey.

| Stage | Required evidence before proceeding |
| --- | --- |
| Design ready | Completed [feature template](feature-template.md), criterion mapping, names/focus/states, content alternatives and timing decisions; assign owners |
| Change ready to merge | Relevant component/API/browser checks and targeted manual procedures; no new applicable A/AA failures; describe outstanding legacy failures explicitly |
| Content ready to publish | Compiler validation plus content-owner review for the exact source/assets and learner preview |
| Release ready | All 55 rows dispositioned; every applicable criterion passes over the whole stated scope; complete J1–J8 evidence for included features and required environments; failures retested; named release sign-off |
| After release | Re-evaluate affected criteria when code, content, dependencies or supported environments change; periodically review support reports and repeat complete-journey checks |

Severity determines response order, not permission to claim conformance. Highest priority goes to blocked access, traps, inaccessible authentication/submission and lost work. Keep every unresolved failure linked to an owner and target release. For a production regression, assess impact promptly, repair or disable the affected feature safely, update the statement's known limitations and retest before restoring a conformance claim.

## Accessibility statement and claim

Publish an accessible statement with the target, evaluated scope/date, methods/environments, known limitations and a working reporting channel. Product service target: acknowledge barrier reports within two working days and provide an owner and next-update date; assign actual support coverage before publication. An honest “working toward WCAG 2.2 AA” statement is appropriate while checks remain incomplete.

A formal claim is optional. If made, include claim date, WCAG title/version/URI, level, precise covered pages (including subdomains) and relied-upon web technologies. Link the evaluation record and keep claims aligned with current evidence. Never describe partial conformance or a passing scan as full AA conformance. Source: [WCAG conformance claims](https://www.w3.org/TR/WCAG22/#conformance-claims).
