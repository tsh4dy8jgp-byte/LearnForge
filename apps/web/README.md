# LearnForge web client

This is the Angular 22 standalone, zoneless learner and authoring client. It is normally run from the repository root:

~~~sh
npm ci --prefix apps/web
npm start --prefix apps/web
~~~

The development server listens on port 4300 and proxies `/api` and `/health` to the ASP.NET Core API on port 5080. See the root [README](../../README.md), [setup guide](../../docs/setup.md) and [learner guide](../../docs/user-guide.md).

Build and exercise the client with:

~~~sh
npm run build --prefix apps/web
cd apps/web
npx playwright install chromium
npx playwright test
~~~

The client never grades answers. It receives sanitized question projections, sends stable answer IDs to the API and renders released feedback after the server authorizes it. Question interactions include click and keyboard alternatives for drag-oriented controls.

## Organization and conventions

Follow the [Angular style guide](https://angular.dev/style-guide). Use feature directories rather than folders named `pages`, `components` or `services`. Keep a component's TypeScript, HTML and optional SCSS under the same hyphenated base name, with its `.spec.ts` alongside. Small, closely related templates can remain inline. Prefer `inject()`, readonly inputs/outputs/models, protected template-only members and computed presentation state.

The application root contains its shell, routes, configuration, generated API contracts and aliases. Features are `authentication`, `dashboard`, `learning-library`, `attempts`, `authoring`, `account` and `appearance`. Reusable learning components live in `learning`; answer interactions live in `assessment`. Transport and CSRF handling live in `http`; branding and titles live in `site`.

`AttemptState` and `DraftEditorState` are provided by their route components so their state and timers are destroyed when the page is left. Presentation components use inputs/events or that explicitly scoped state. Keep API mutations in page/state orchestration, never in the content renderers. Preserve keyboard alternatives, native control semantics, focus behavior and server-authorized feedback.

Run the colocated Vitest tests with `npm test --prefix apps/web`. Generate contracts with `make api-types` from the repository root and verify drift with `make check-api-types`. Never hand-edit `src/app/generated` or duplicate its wire types. Themes, layout presets and shared CSS remain global in `src`; selectors and browser routes are stable extension interfaces.
