# Single-service image: the Vite bundle is built and copied into the API's wwwroot, so one
# container serves both the SPA and /api from one origin. That takes CORS out of the deployed
# path entirely and lets the UI address the API as the relative "/api" — which is what makes a
# per-PR preview environment work without rebuilding the bundle for each preview's domain.

# ---------------------------------------------------------------------------
# Stage 1 — the SPA
# ---------------------------------------------------------------------------
FROM node:22-alpine AS ui
WORKDIR /ui

# Manifests first, so a source-only change does not re-run npm ci. Both files are named
# explicitly rather than globbed: if package-lock.json ever goes missing, COPY should fail
# loudly here rather than npm ci failing obscurely on the next line.
COPY money-manager-ui/package.json money-manager-ui/package-lock.json ./

# Never --omit=dev. vite, vue-tsc, typescript and @vitejs/plugin-vue are all devDependencies,
# so a production-only install cannot build the bundle at all. This stage is discarded, so
# nothing is gained by trimming it.
RUN npm ci

# The whole directory, deliberately. `npm run build` runs `vue-tsc -b` first, and
# tsconfig.node.json includes vitest.config.ts — so a narrowed copy fails with TS6053 on a file
# that has nothing to do with the bundle. index.html also references src/style.css relatively,
# so the two have to arrive at the same relative depth.
COPY money-manager-ui/ ./
RUN npm run build

# ---------------------------------------------------------------------------
# Stage 2 — the API
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS api
WORKDIR /src

# Directory.Build.props sets TreatWarningsAsErrors, AnalysisLevel and EnforceCodeStyleInBuild,
# and MSBuild finds it only by walking up from the project directory. Copying it before the
# restore is what keeps this build enforcing the same rules as CI: without it the image builds
# green under laxer settings than the required API check, which is precisely what that file
# exists to prevent. .editorconfig carries the severities those analyzers read.
COPY Directory.Build.props .editorconfig ./

# Restore the project, never app.sln. The solution references the test project, which is not in
# this layer, so a solution restore fails outright — and publishing the solution would try to
# publish the tests as well. The project reference runs the other way (tests -> API), so the API
# publishes complete on its own.
COPY MoneyManager.Api/MoneyManager.Api.csproj MoneyManager.Api/
RUN dotnet restore MoneyManager.Api/MoneyManager.Api.csproj

COPY MoneyManager.Api/ MoneyManager.Api/

# NU1900 ("failed to retrieve information from remote source") is a NuGet warning raised by a
# flaky or proxied feed. TreatWarningsAsErrors promotes it to a build failure, which turns a
# transient network blip into a red build for reasons that have nothing to do with the code.
RUN dotnet publish MoneyManager.Api/MoneyManager.Api.csproj \
        --configuration Release \
        --no-restore \
        -p:WarningsNotAsErrors=NU1900 \
        --output /app/publish

# ---------------------------------------------------------------------------
# Stage 3 — runtime
# ---------------------------------------------------------------------------
#
# The full aspnet image rather than an alpine or chiselled variant, on purpose. SQLite reaches
# a native libe_sqlite3, and the portable publish above carries both the glibc and musl builds
# of it — but any later "optimisation" that pins a RID (-r linux-x64, --self-contained,
# PublishSingleFile) prunes that down to one, and on musl the app then dies with
# DllNotFoundException at the first query, which is Database.Migrate() on startup. Chiselled
# avoids the musl trap but runs as a non-root user, which cannot write to a root-owned mounted
# volume, and ships no shell to debug that with. Neither is a trade worth ~100 MB here.
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

COPY --from=api /app/publish ./
COPY --from=ui /ui/dist ./wwwroot

# WORKDIR above and the exec form here are both load-bearing. Static files resolve from
# ContentRoot + /wwwroot, and ContentRoot is the working directory — point them apart and the
# app starts cleanly, serves the API, and 404s every asset and the SPA shell, with no error
# logged anywhere to say why.
ENTRYPOINT ["dotnet", "MoneyManager.Api.dll"]
