---
name: pre-push-check
description: Run the same checks Money Manager's CI runs (API tests, UI type-check + build) locally before pushing. Invoke with /pre-push-check.
disable-model-invocation: true
allowed-tools: Bash
---

Mirror `.github/workflows/ci.yml` exactly, in order:

1. API: `dotnet restore app.sln && dotnet build app.sln --no-restore --configuration Release && dotnet test app.sln --no-build --configuration Release --verbosity normal`
2. UI: `cd money-manager-ui && npm ci && npm run build` (`vue-tsc` runs as part of the build script, so a type error fails this step too)

Report pass/fail for each step. If something fails, show the actual error output rather than just saying it failed.
