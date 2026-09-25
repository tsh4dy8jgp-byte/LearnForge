# Assessment and readiness

## Grading

The server grades every response. Clients submit stable option IDs and slot mappings; they never submit a score or trusted answer key.

Exact questions require the complete key. Partial questions award the proportion of correct required parts multiplied by question weight. Duplicate IDs, unknown IDs, too many selections, invalid slots and token reuse are rejected. Empty answers receive zero.

FullyCorrect is independent from earned points. A partly correct response can earn useful points without satisfying the readiness rule.

## Composition and sessions

A Blueprint defines count, duration, size, objective IDs and required formats. The composer groups scenario questions atomically, avoids duplicate families where possible, uses a stable SHA-256 seed and has a bounded backtracking budget. Impossible contracts fail with a diagnostic.

Focused learning sessions use completed evidence to select mistakes or weak objectives. They can be shorter and do not qualify for readiness. A mock has a server deadline; every read and write checks expiry. Completed attempts cannot be edited.

Each response has a client UUID and expected revision. Replaying the same request and payload is safe. Reusing an ID with a different payload is rejected. A stale revision returns conflict. Only one active attempt per user and pack is allowed.

## Readiness rule

For each size, the evaluator orders evidence by completion time and examines the latest configured number. It requires completed mocks, eligibility, the lookback window, fully correct percentage strictly greater than the threshold and minimum fresh-question percentage.

The short and full rules are independent and readiness is their OR. A newer failed, expired or ineligible mock breaks that length's latest streak. There is no cherry-picking.

With the default policy, five short mocks at 95, 93, 91, 97 and 96 qualify. Replacing the newest with exactly 90 does not. A learning attempt with 100 is ignored. An expired mock is retained but ineligible.

Readiness is a transparent practice recommendation, not a prediction or claim about an external exam.
