# Accessibility design baseline

Applies to the current LearnForge framework and all packs.

**Target: WCAG 2.2 Level AA from the first supported release of every feature.** These documents define required design and acceptance behavior. They do not certify the current application; implementation and verification remain outstanding unless release evidence explicitly records otherwise.

## Design documents

| Document | Use it for |
| --- | --- |
| [Design system](design-system.md) | Layout, visual tokens, semantics, keyboard interaction, forms and dynamic states |
| [Learning and assessment](learning-and-assessment.md) | Complete learner journeys, all question formats, timing and authentication |
| [Content and authoring](content-and-authoring.md) | Pack contracts, media alternatives, accessible questions and publishing |
| [Conformance matrix](conformance-matrix.md) | Traceability for all 55 Level A and AA success criteria |
| [Verification and release](verification.md) | Test procedures, supported environments, evidence, defects and release gates |
| [Feature design template](feature-template.md) | Accessibility section to complete before building a feature |

Read this baseline with the [architecture](../architecture.md), [assessment rules](../assessment.md) and [roadmap](../roadmap.md). Accessibility acceptance belongs in each feature, content contract and pull request.

## Standard and interpretation

The normative reference is [W3C WCAG 2.2](https://www.w3.org/TR/WCAG22/), verified on 2026-09-25. AA includes every applicable Level A and AA criterion. Conformance concerns complete pages, responsive variations and all steps in a process, using accessibility-supported technology; other content must not interfere with access. A selected set of passing components or an automated score cannot establish conformance. See [W3C's conformance explanation](https://www.w3.org/WAI/WCAG22/Understanding/conformance).

In this document set:

- **WCAG requirement** means a requirement traced to a success criterion in the matrix. The linked standard controls its exact scope and exceptions.
- **Product rule** means LearnForge's chosen implementation or a stronger safeguard. It is required by this design, but must not be presented as a WCAG minimum.
- **Planned** means the feature or safeguard still needs implementation. Documentation is not passing evidence.

The six new A/AA criteria in 2.2 are 2.4.11, 2.5.7, 2.5.8, 3.2.6, 3.3.7 and 3.3.8. Criterion 4.1.1 is obsolete and removed; valid HTML and unique relationship IDs remain engineering requirements. Focus Appearance (2.4.13), enhanced focus visibility (2.4.12) and the enhanced 44-pixel target criterion (2.5.5) are AAA, not AA. See [W3C's 2.2 changes](https://www.w3.org/WAI/standards-guidelines/wcag/new-in-22/).

## Scope

The target covers signed-out and signed-in experiences, publisher interfaces, every supported language and viewport, and loading, empty, success, error, offline, expired and permission-denied states.

| Surface | Included experience |
| --- | --- |
| `/sign-in` | Registration, sign-in, errors and future recovery, verification and passkey flows |
| `/dashboard`, `/courses`, `/courses/:id` | Enrollment, archive/restore, search, lessons, map, practice setup, references, mastery and readiness |
| `/attempts`, `/attempts/:id` | History, all questions, case studies, autosave, conflicts, resume, timing, section locking, submission and results |
| `/settings` | Password changes, export, deletion and future display preferences |
| `/studio` | File import, source editing, diagnostics, preview and publishing |
| Shared and future surfaces | Navigation, support, notices, dialogs, rich media, offline reading, review tools and optional AI |

Embedded players, identity providers and required external activities belong to the complete process. Evaluate them before adoption. A publisher role, third-party origin, old content release or small audience does not remove a page from scope. Optional references must have accessible link context; required learning material needs an accessible delivery path.

Downloadable learning documents must have an accessible format and equivalent HTML access as a product rule. Do not imply that the web target alone certifies a PDF, email, native app or external service. Record these deliverables separately in the release inventory.

## People and design outcomes

| Access need | Outcome the design must enable |
| --- | --- |
| Screen reader or braille | Identify the current page, question, answer state, feedback and next action without visual inference |
| Keyboard, switch or voice input | Complete every action without dragging, pointer precision or a keyboard trap |
| Magnification or low vision | Read and operate the same experience with zoom, reflow, spacing changes and visible focus |
| Deaf or hard of hearing | Receive equivalent instructional speech, sound cues and live communication |
| Cognitive, learning or reading needs | Predictable steps, clear instructions, recoverable errors and control of time |
| Vestibular or photosensitive needs | Avoid flashing and disable nonessential motion |

Recruit learners with these access needs for prototype and release usability sessions. Their feedback supplements criterion checks; it does not substitute for the conformance matrix.

## Responsibilities and gates

These are accountable roles; assign named people in each release record. One person may hold several roles.

| Role | Required work |
| --- | --- |
| Product owner | Maintain scope, ensure accessible task outcomes and fund remediation |
| Design owner | Specify focus, labels, states, responsive behavior, contrast and alternatives before handoff |
| Frontend owner | Implement component contracts and browser/assistive-technology behavior |
| API/Core owner | Enforce timing, persistence, safe delivery and compiler contracts |
| Content owner | Review authored meaning, alternatives, languages and assessment equivalence |
| QA/accessibility reviewer | Execute independent manual checks, maintain evidence and retest defects |
| Release owner | Confirm every applicable row has passing evidence before a conformance claim |

No unresolved A/AA failure in the release scope is waived by severity, a deadline or a support workaround. Remediate it or remove the affected feature completely from the release and re-evaluate the remaining full pages/processes. An untested or failed item cannot be recorded as passing. A genuine criterion exception or non-applicability needs specific evidence and review, as described in [verification](verification.md).

## Implementation order

Current source inspection establishes these starting points, **not a runtime accessibility audit**:

| Starting point | Required follow-through |
| --- | --- |
| `app.html` has a skip link and main landmark; `app.routes.ts` provides route titles and navigation focuses the main landmark | Verify skip behavior, route titles and focus policy with assistive technology |
| the course feature uses Angular ARIA tabs; component and browser tests exist | Verify panel relationships, focus and complete course journeys with assistive technology |
| the shared question input uses native choices, click placement and move buttons | Add contextual target/clear names, stable focus and announcements; replace repeated bank/target IDs before multiple instances render |
| the attempt feature enforces a displayed server deadline; setup has no duration adjustment | Implement the timing contract before releasing a conforming timed-mock journey |
| the attempt feature and the account feature use browser confirmation in some flows | Verify current behavior; adopt the consistent confirmation pattern without assuming browser confirmation inherently fails WCAG |
| Auth fields use autocomplete; errors are generally page-level | Verify password managers, field error associations and authentication recovery |
| the authoring feature renders compiler diagnostics and publication results | Add result announcements, diagnostic navigation and a content accessibility gate |
| Playwright configuration runs Chromium; no axe dependency is declared | Add automated accessibility checks and the manual environment matrix; existing tests do not establish conformance |

Sequence delivery as follows:

1. **Shared foundation, before the next feature ships:** route/focus behavior, measured colors, form and status patterns, keyboard question fixes, timing controls, evidence inventory and accessibility test harness.
2. **Learning record:** apply these contracts to enrollment, mastery, progress and next steps now. Apply it to the default experience.
3. **Content and questions:** require accessible content schemas, media alternatives and equivalent interaction before exposing a new block or question kind.
4. **Future learner tools and deployment:** review tools, preferences/offline reading, identity and AI each inherit the baseline at design time. Personalization improves a default experience that already meets AA.

## References and maintenance

Each matrix row links to W3C's criterion-specific explanatory material. The [ARIA Authoring Practices Guide](https://www.w3.org/WAI/ARIA/apg/) provides informative widget patterns, not an automatic conformance guarantee. Recheck references when the adopted standard or a relied-upon pattern changes. Record changes to this baseline in the same review as the affected feature and tests.
