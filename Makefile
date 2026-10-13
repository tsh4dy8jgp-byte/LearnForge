.DEFAULT_GOAL := setup
.PHONY: install setup dev test build check-content browser-test api-types check-api-types installer-test
install:
	bash scripts/install.sh
setup:
	bash scripts/install.sh --install-only
dev:
	bash scripts/dev.sh
test:
	dotnet test LearnForge.slnx
installer-test:
	node --test scripts/tests/*.test.mjs
build:
	dotnet build LearnForge.slnx -c Release
	npm run build --prefix apps/web
check-content:
	dotnet run --project tools/cli -- check packs/reasoning-foundations.json
	dotnet run --project tools/cli -- check packs/evidence-lab.json
	dotnet run --project tools/cli -- check packs/istqb-ctfl-4.json
	dotnet run --project tools/cli -- lint packs/istqb-ctfl-4.json
	dotnet run --project tools/cli -- check packs/istqb-ct-ai-2.json
	dotnet run --project tools/cli -- lint packs/istqb-ct-ai-2.json
	dotnet run --project tools/cli -- check packs/istqb-ct-genai-1.json
	dotnet run --project tools/cli -- lint packs/istqb-ct-genai-1.json
	dotnet run --project tools/cli -- check packs/ai-testing-genai-practical.json
	dotnet run --project tools/cli -- lint packs/ai-testing-genai-practical.json --strict
	dotnet run --project tools/cli -- check docs/examples/exam-sample.json
	dotnet run --project tools/cli -- lint docs/examples/exam-sample.json --strict
browser-test:
	cd apps/web && npx playwright test
api-types:
	dotnet build apps/api/LearnForge.Api.csproj -p:OpenApiGenerateDocumentsOnBuild=true
	npm run api:types --prefix apps/web
check-api-types: api-types
	git diff --exit-code -- apps/web/openapi apps/web/src/app/generated
