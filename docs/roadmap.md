# Roadmap

LearnForge is a subject-neutral learning and assessment foundation. The [README](../README.md) lists implemented capabilities; the items below are future work, not supported contracts. Authoring preview, numeric and program-output answers, weighted blueprints, enrollment, cross-release lesson progress, mastery, next steps, generated API types and appearance preferences are already implemented.

WCAG 2.2 AA is a design and release requirement for every feature. Follow the [accessibility baseline](accessibility/README.md), including content alternatives, timing, keyboard/focus behavior and manual verification. Passing automated tests does not establish conformance.

## Content and assessment

- Restricted Markdown and math compiled into typed content rather than raw HTML; stable block IDs, modules, glossary terms, language/direction and catalog metadata.
- Figures, tables, worked examples, primary sources, inline checks and media with reviewed alternatives, captions, transcripts and audio description as applicable. See the [content accessibility contract](accessibility/content-and-authoring.md).
- Pack-scoped assets with hashed manifests, MIME validation and storage abstraction; stricter duplicate-property validation.
- QTI/Markdown/YAML adapters, richer templates and semantic duplicate detection, all routed through the compiler.
- Additional question formats, strategy-based renderers, per-option rationale, hints, difficulty and richer numeric policies; independent validation and compatibility review before adoption.

## Learner tools

- Spaced review and flashcards, sibling-question practice, confidence calibration and transparent adaptive practice. Assisted evidence must remain separate from independent readiness evidence.
- Study plans, target dates, reminders, block-anchored notes/bookmarks, search and richer results analysis.
- Offline reading and explicit sync, localization, calendar export and accessible confirmation dialogs. Adjustable mock timing and unique interaction-instance IDs remain accessibility foundation work, not prerequisites to defer until personalization.

## Shared and institutional deployment

- Account recovery, email confirmation, passkeys/MFA, OIDC/SAML, session and role administration, cohorts/assignments and tenant isolation.
- Multi-replica coordination for expiry/seeding, observability, centralized audit export, trusted proxy configuration and measured caching/reporting improvements.
- Dependency, accessibility, load and penetration testing before institutional hosting.

## Optional AI

A future AI module must be provider-neutral and off by default. Ground explanations and advisory feedback in pack content, label and cite generated responses, honor feedback-release rules, and never let AI determine scores or readiness. Optional semantic search and hints require their own privacy and answer-disclosure review.

Each feature needs a content/interface contract, threat model, compatibility and migration plan, automated coverage and accessibility evidence before becoming supported behavior. Preserve server-side grading, immutable releases/snapshots, stable IDs, idempotent writes and sanitized learner delivery throughout.
