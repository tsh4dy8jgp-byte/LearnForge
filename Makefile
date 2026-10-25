.DEFAULT_GOAL := setup
.PHONY: install setup dev test installer-test build check-content browser-test api-types check-api-types setup-flashcards test-flashcard-exports
install:
	bash scripts/install.sh
setup-flashcards:
	python3 -m venv .venv-flashcards
	.venv-flashcards/bin/pip install -r tools/flashcards/requirements.txt
test-flashcard-exports:
	dotnet build tools/cli --no-restore
	.venv-flashcards/bin/pip install -r tools/flashcards/requirements-test.txt
	.venv-flashcards/bin/python -m unittest discover -s tools/flashcards -v
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
	python3 scripts/check-writing-assets.py
	dotnet run --project tools/cli -- check packs/chinese-writing-pilot.json
	dotnet run --project tools/cli -- check packs/reasoning-foundations.json
	dotnet run --project tools/cli -- check packs/evidence-lab.json
	dotnet run --project tools/cli -- check packs/recall-foundations.json
	dotnet run --project tools/cli -- check docs/examples/exam-sample.json
	dotnet run --project tools/cli -- check docs/examples/flashcards-sample.json
	dotnet run --project tools/cli -- lint docs/examples/exam-sample.json --strict
browser-test:
	cd apps/web && npx playwright test
api-types:
	dotnet build apps/api/LearnForge.Api.csproj -p:OpenApiGenerateDocumentsOnBuild=true
	npm run api:types --prefix apps/web
check-api-types: api-types
	git diff --exit-code -- apps/web/openapi apps/web/src/app/generated
