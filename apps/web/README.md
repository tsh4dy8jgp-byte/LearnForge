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
