# Security model

LearnForge treats authors and the API as trusted, and the browser as untrusted. A learner can edit JavaScript, local storage, clocks and submitted answers. The server recomputes access, timing, scoring, readiness and ownership.

## Implemented controls

- ASP.NET Core Identity hashing, 12-character minimum, upper/lowercase and number requirements, unique email and lockout.
- HttpOnly, Strict SameSite cookies; Secure cookies outside Development; security-stamp validation.
- Antiforgery validation on every unsafe /api request, including JSON and account deletion.
- Ownership predicates on attempts, completions and exports.
- Server grading and sanitized active-attempt projections.
- Revision checks, UUID idempotency and database uniqueness constraints.
- HTTPS-only source links, strict JSON deserialization, template depth/node/text budgets and no filesystem include, network fetch or code execution.
- Authentication/general rate limits, request body limits, security headers and generic errors.
- Immutable pack releases and attempt snapshots.
- Password-confirmed deletion and JSON export.

## Deployment requirements

Run public traffic behind HTTPS. Use Production, a real PostgreSQL secret, persisted restricted Data Protection keys and tested backups. If TLS terminates at a proxy, configure trusted forwarded headers; never blindly trust client forwarding headers.

Set explicit allowed hosts, disable open registration where invitation control exists, and grant Publisher only to trusted authors. Do not expose PostgreSQL or the API container directly.

Production still needs password recovery delivery, MFA/OIDC, security monitoring, dependency scanning, key rotation and independent penetration/accessibility review.

## Privacy and content

History, responses, exports and local drafts are personal data. Restrict logs, do not log request bodies, encrypt storage/backups and define retention. Account deletion removes live records; backup retention is an operator decision.

Pack JSON is data rendered through text interpolation and typed blocks. Do not add raw HTML, remote frames or code execution without a separate sanitization and isolation design. Keep private compiler artifacts out of web roots.
