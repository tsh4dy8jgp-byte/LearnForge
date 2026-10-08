# Contributing to LearnForge

LearnForge is a modular education and exam-practice framework. Contributions are welcome for the framework, example packs, documentation, and tests.

## Before opening a change

Run the checks that match your change:

```sh
dotnet test LearnForge.slnx
dotnet build LearnForge.slnx -c Release
npm test --prefix apps/web
npm run build --prefix apps/web
make check-content
make check-api-types
```

Content changes must pass through `ContentEngine`. Do not write directly to release tables. Keep answer keys out of learner delivery, preserve immutable releases, and keep question and family IDs stable when the competency has not changed.

New question kinds must update the Core contract, validation, grading, composition, sanitized delivery, Angular interaction, keyboard alternative, tests, and authoring documentation. Follow the accessibility requirements in `docs/accessibility/` when changing a learner, author, or content journey.

## Frontend changes

Follow the [Angular style guide](https://angular.dev/style-guide) and [frontend conventions](apps/web/README.md). Keep related code, templates and tests together by feature. Use subject-neutral components and pack capabilities instead of branching on example IDs. Preserve public routes, answer-release boundaries and keyboard interactions when refactoring.

## Pull requests

Describe the user outcome, the affected pack/API contracts, migrations, accessibility checks, and validation commands. Keep generated OpenAPI artifacts up to date with `make api-types`.
