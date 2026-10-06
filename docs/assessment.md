# Assessment and readiness

The [accessible learning and assessment design](accessibility/learning-and-assessment.md) specifies required question interactions, review/confirmation and planned pre-start mock-duration adjustment. The behavior below describes the current implementation; it does not establish that timing or other WCAG 2.2 AA requirements have been verified.

## Grading

The server grades every response. Clients submit stable option IDs and slot mappings; they never submit a score or trusted answer key.

Exact questions require the complete key. Partial questions award the proportion of correct required parts multiplied by question weight. Duplicate IDs, unknown IDs, too many selections, invalid slots and token reuse are rejected. Empty answers receive zero.

FullyCorrect is independent from earned points. A partly correct response can earn useful points without satisfying the readiness rule.

## Composition and sessions

A Blueprint defines count, duration, size, objective IDs and required formats. The composer groups scenario questions atomically, avoids duplicate families where possible, uses a stable SHA-256 seed and has a bounded backtracking budget. Impossible contracts fail with a diagnostic.

A weighted blueprint (`objectiveWeights`) also distributes its count across objectives. Each objective's share is computed by largest remainder; a paper may land one question either side of it, and every objective gets at least one. Each question counts once, under its first objective that the blueprint includes. Case studies still move as a whole, so one is skipped when it would push an objective past its range. These constraints do not depend on the seed: validation proves a paper exists. The search budget can still run out for an unusual seed; starting a session then returns 409, and a new request (a new seed) usually succeeds.

Focused learning sessions use completed evidence to select mistakes or weak objectives. They can be shorter and do not qualify for readiness. A mock has a server deadline; every read and write checks expiry. Completed attempts cannot be edited.

Each response has a client UUID and expected revision. Replaying the same request and payload is safe. Reusing an ID with a different payload is rejected. A stale revision returns conflict. Only one active attempt per user and pack is allowed.

## Readiness rule

For each size, the evaluator orders evidence by completion time and examines the latest configured number. It requires completed mocks, eligibility, the lookback window, fully correct percentage strictly greater than the threshold and minimum fresh-question percentage.

The short and full rules are independent and readiness is their OR. A newer failed, expired or ineligible mock breaks that length's latest streak. There is no cherry-picking.

With the default policy, five short mocks at 95, 93, 91, 97 and 96 qualify. Replacing the newest with exactly 90 does not. A learning attempt with 100 is ignored. An expired mock is retained but ineligible.

Readiness is a transparent practice recommendation, not a prediction or claim about an external exam.

## Mastery

Each objective shows one of four states: not started, getting started (fewer than `minimumEvidence` question families), developing, or proficient. The rule looks at the learner's latest answer in each question family, keeps the latest `window` families (default 5), and requires at least `proficientPercent` (default 80%) fully correct: 4 of the last 5 qualifies.

Mock answers always count, and unanswered mock items count as incorrect. Learning-mode answers count only when answered, because they are first tries before feedback; skipped learning items are ignored. Proficient objectives become "review due" after `reviewAfterDays` (default 60).

Evidence comes from the append-only evidence ledger: one row per attempt and question, written when feedback is released. Mastery follows stable objective and family IDs across releases. It is a transparent study aid; readiness is unchanged and remains release-scoped and mock-only.

Next steps follow the prerequisite graph: objectives whose prerequisites are proficient come first, an unread linked lesson is suggested before practice, review-due objectives follow, and a readiness pack suggests a timed mock once every objective is proficient.
