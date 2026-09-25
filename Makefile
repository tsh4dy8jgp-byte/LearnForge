.PHONY: setup dev test build check-content browser-test
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
browser-test:
	cd apps/web && npx playwright test
