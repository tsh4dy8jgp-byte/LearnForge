# Roadmap

The current release is a reusable foundation, not a claim that every institutional feature is complete.

WCAG 2.2 AA is a design and release requirement from the start of every feature. Apply the [accessibility design baseline](accessibility/README.md), including its implementation sequence and verification gates, to existing and planned work. Accessibility is not deferred to institutional hosting or a later learner-experience phase.

Near-term: rendered authoring preview, richer template schemas, semantic duplicate detection, QTI/Markdown/YAML adapters, object storage for media, materialized analytics, instructor cohorts/assignments and larger calibrated banks.

Before institutional hosting: email confirmation and recovery, MFA/OIDC/SAML, tenant isolation and role administration, trusted forwarded-header configuration, centralized audit export, dependency/accessibility/load/penetration testing.

Learning enhancements: explicit offline sync, spaced repetition, adaptive difficulty and localization. Rich media must ship with the required captions, transcripts and audio description defined in the [content accessibility contract](accessibility/content-and-authoring.md).

Each roadmap item should receive a content contract, threat model, migration plan and automated coverage before becoming supported behavior.
