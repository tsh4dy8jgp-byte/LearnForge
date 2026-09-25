# API guide

The API is JSON over same-origin HTTP. Identity cookies carry the session. Unsafe API requests require the X-CSRF-TOKEN header from GET /api/auth/csrf.

## Public

GET /health checks database connectivity. GET /api/catalog returns release summaries. GET /api/catalog/{packId} returns teaching-safe course data, objectives, lessons, blueprints, references and the pack goal; it never returns questions, keys or personal progress.

## Authentication

POST /api/auth/register creates an account and signs in. POST /api/auth/login signs in with lockout protection. GET /api/auth/me returns the profile and Publisher claim. POST /api/auth/logout signs out. POST /api/auth/password changes a password and refreshes the security stamp. DELETE /api/me/account requires the current password and deletes live learner records.

Registration can be disabled with Auth:AllowRegistration=false. Password recovery email, MFA and OIDC are roadmap items.

## Learner

All require authorization and ownership:

- GET /api/me/dashboard
- GET /api/me/attempts
- POST /api/me/attempts with packId, blueprintId, mode, UUID requestId and optional focus
- GET /api/me/attempts/{id}
- PUT /api/me/attempts/{id}/responses
- POST /api/me/attempts/{id}/section
- POST /api/me/attempts/{id}/submit
- PUT /api/me/courses/{packId}/lessons/{lessonId}
- GET /api/me/courses/{packId}: course progress (enrollment, completed and revised lessons, objective mastery, next steps, goal status)
- PUT /api/me/enrollments/{packId} with `{ "status": "active" | "archived" }`
- GET /api/me/export

`POST /api/me/attempts` accepts `objectiveId` together with `focus: "objective"` (learning mode only). The dashboard lists active enrollments with the same course progress shape plus recent attempts. The export also contains `enrollments`, `lessonProgress` and `evidence`.

Active response projections omit keys. Completed attempts include released grades and explanations.

## Authoring

Publisher role required: POST /api/authoring/validate accepts source JSON and returns diagnostics/hash; POST /api/authoring/publish compiles and creates an immutable release. The same pack ID and version cannot be published twice. Use the CLI for CI and deterministic artifacts.

Invalid request bodies return 400 problem details with an `errors` object keyed by field. Validation errors return 400, unauthenticated 401, forbidden 403, missing owned resources 404, conflicts 409 and rate limits 429. Unexpected errors are generic problem responses with a trace log. Clients should reload on 409 and never infer a successful write from a dropped connection.
