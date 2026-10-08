.PHONY: setup dev test build check-content browser-test api-types check-api-types
setup:
	dotnet restore LearnForge.slnx
	npm ci --prefix apps/web
dev:
	bash scripts/dev.sh
test:
	dotnet test LearnForge.slnx
build:
	dotnet build LearnForge.slnx -c Release
	npm run build --prefix apps/web
check-content:
	dotnet run --project tools/cli -- check packs/reasoning-foundations.json
	dotnet run --project tools/cli -- check packs/evidence-lab.json
	dotnet run --project tools/cli -- check packs/istqb-ctfl-4.json
	dotnet run --project tools/cli -- lint packs/istqb-ctfl-4.json
	dotnet run --project tools/cli -- check docs/examples/exam-sample.json
	dotnet run --project tools/cli -- lint docs/examples/exam-sample.json --strict
browser-test:
	cd apps/web && npx playwright test
api-types:
	dotnet build apps/api/LearnForge.Api.csproj -p:OpenApiGenerateDocumentsOnBuild=true
	npm run api:types --prefix apps/web
check-api-types: api-types
	git diff --exit-code -- apps/web/openapi apps/web/src/app/generated
