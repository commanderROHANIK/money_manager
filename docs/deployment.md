# Deployment

A development deployment on [Railway](https://railway.com): one always-on environment tracking
`main`, plus a throwaway environment per pull request. The point is to review a change by opening
a link instead of pulling the branch and running two dev servers.

This is **not** a production configuration, and several things below are accepted rather than
solved. They are called out as such.

## Shape

One service, one container, one origin:

```
<env>.up.railway.app/           → the SPA (Vite bundle, built into wwwroot)
<env>.up.railway.app/api/...    → the controllers
<env>.up.railway.app/health     → liveness, for the platform healthcheck
```

The `Dockerfile` at the repo root builds the UI, publishes the API, and copies the bundle into
the API's `wwwroot`. Serving both from one origin means the deployed app makes no cross-origin
request, and the SPA addresses the API as the relative `/api` — which is what lets one build run
in any preview environment without being rebuilt for that environment's domain.

`railway.json` carries the settings that must not drift: the Dockerfile builder, the healthcheck
path, and `numReplicas: 1`. Everything else is set in the Railway dashboard.

## Environment variables

| Variable | Value | Why |
|---|---|---|
| `ASPNETCORE_URLS` | `http://0.0.0.0:8080` | Literal, not a variable reference. See below — this one cost a deploy. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | See the warning below — do **not** set `Development`. |
| `JwtSettings__SecretKey` | generated | `openssl rand -base64 48`. Startup refuses a key under 32 bytes. |
| `ConnectionStrings__Default` | `Data Source=/data/moneymanager.db` | Must match the volume mount path. |
| `Auth__AllowRegistration` | `false` | Public URL. Accounts are seeded instead. |
| `Seed__Enabled` | `true` | A fresh volume is empty; an empty app cannot be reviewed. |
| `Seed__Username` | e.g. `demo` | |
| `Seed__Password` | set explicitly | **No default exists and startup fails without one.** |
| `Seed__IncludeDemoData` | `true` for previews | Consider `false` on a long-lived environment holding real records. |

### Bind a literal address, never `${{PORT}}`

This section previously recommended `ASPNETCORE_HTTP_PORTS=${{PORT}}` on the reasoning that
`ASPNETCORE_URLS` would produce an unparseable URL if `PORT` were missing, and that
`ASPNETCORE_HTTP_PORTS` "cannot fail that way". **That was wrong, and it cost the first
deployment.** It does not fail loudly — it fails silently, which is worse.

What actually happened: no `PORT` variable existed on the service, because no domain had been
generated yet. `${{PORT}}` resolved to an empty string, and `ASPNETCORE_HTTP_PORTS=""` **overrode
the base image's default of 8080 with nothing**. ASP.NET then fell back to its own built-in
default and logged:

```
Now listening on: http://localhost:5000
```

Loopback only, on a port nothing routes to. The container was healthy, the app had migrated and
seeded successfully, and the only symptom was Railway's healthcheck reporting
`service unavailable` — with no error anywhere, because nothing had gone wrong from the app's
point of view.

So: **`ASPNETCORE_URLS=http://0.0.0.0:8080`**, with the host and port written out. No variable
reference to resolve, and the bind address stated rather than inherited. The domain's target port
must then be `8080` — a domain generated against an earlier, broken deployment can keep pointing
at whatever port Railway detected back then, which presents as a 502 rather than a timeout.

The corollary is worth stating too, because the previous version of this section got it backwards:
the widely repeated "Kestrel binds to localhost by default" is true of `dotnet run`, and *also*
true of the published image the moment anything blanks out its port configuration. The image's
`ASPNETCORE_HTTP_PORTS=8080` default is only a default.

### Never set `ASPNETCORE_ENVIRONMENT=Development`

The tempting way to get Swagger on a dev deployment. It would:

- register the **developer exception page**, which puts stack traces and the connection string on
  a public URL, and
- publish the full Swagger surface.

Both are unconditional in Development. (The hardcoded development signing key is *not* reachable
as long as `JwtSettings__SecretKey` is set — that fallback requires a blank key **and**
Development — but the two above are reason enough on their own.)

If Swagger is wanted here, gate it on its own flag rather than on the environment name, and keep
the middleware ahead of `UseAuthorization`.

## Storage

A Railway volume mounted at `/data`, holding the SQLite file.

**The mount path and `ConnectionStrings__Default` are a matched pair.** `Microsoft.Data.Sqlite`
does not create a missing directory, so a drift between them fails at `Database.Migrate()` with
`SQLITE_CANTOPEN`, which presents to the operator as "healthcheck never passed".

Startup refuses a **relative** connection string outside Development. That guard exists because
`appsettings.json` ships `Data Source=moneymanager.db` so a fresh clone runs with no setup, and
that same default in a container resolves into the image's own writable layer: the app boots,
migrates, serves traffic, and loses everything on the next deploy, with nothing logged.

Constraints that come with choosing SQLite, all of them accepted for a dev deployment:

- **One replica, pinned in `railway.json`.** `Database.Migrate()` runs on every boot and SQLite
  has no advisory lock, so two overlapping containers writing `__EFMigrationsHistory` against one
  file is a real hazard. This is a hard constraint, not a preference.
- **No horizontal scaling**, for the same reason.
- **WAL is enabled**, and not by us: EF's SQLite provider issues `PRAGMA journal_mode = 'wal'` on
  connect, which the first deployment's logs confirm. An earlier version of this document claimed
  the opposite and warned that readers would block writers — that warning was wrong, and the
  sidecar `-wal`/`-shm` files on the volume are expected rather than a symptom.
- **DataProtection keys are not persisted.** The app logs a warning that it is storing them in
  `/root/.aspnet/DataProtection-Keys`, which is inside the container rather than on the volume, so
  they are regenerated on every deploy. Harmless as things stand — authentication is JWT, signed
  with `JwtSettings__SecretKey`, and nothing here uses DataProtection — but it would matter the
  day anything starts issuing cookies or protected tokens.
- **No backups.** One volume holds everything. Accepted; worth revisiting before anything real
  lives here.
- The base image runs as root, which is why the root-owned volume is writable. Switching to a
  chiselled or non-root image breaks that with `SQLite Error 14: unable to open database file`.

Moving to PostgreSQL is deferred. It is not a provider swap: the three existing migrations are
SQLite-generated and would need regenerating for Npgsql, and `MigrationSchemaTests` is built on an
in-memory SQLite connection, so it would either need a real PostgreSQL in CI or stop testing what
is deployed.

## Accounts

Registration is closed (`Auth__AllowRegistration=false`), so `POST /api/auth/register` returns
404 — 404 rather than 403, so a closed deployment does not confirm the endpoint is there.

That makes the seeded account the entire way in, which is why `Seed__Password` has no default and
startup fails without one. A built-in default would ship one known credential to every environment
built from this image.

**Preview URLs are public.** Railway puts no gate in front of them. Anyone with the link reaches
the login screen of a live instance holding seeded data.

Seeding is idempotent and runs on every boot. It creates the account only if that username is
absent, and the demo portfolio only if the seeded user has no properties.

## Deploys

- **Wait for CI** is enabled, so `main` deploys only after the **API** and **UI** checks pass.
  It requires `ci.yml` to trigger `on: push` for `main`, which it does. It has a known failure
  pattern where any third-party check on the commit can hold the deploy — verify it fires on the
  first real merge rather than assuming.
- **Healthcheck** is `/health`. A failed healthcheck **keeps the previous deployment serving**
  rather than taking the service down, so a red deploy is a blocked rollout and not an outage.
- **PR environments** are enabled and are torn down on merge or close. Each gets its own empty
  volume, which is why seeding is load-bearing rather than a nicety.
- **Do not enable the Serverless toggle.** Hobby services run continuously by default, which is
  what an always-on dev environment wants.
- `railway.json` is read from the repository root. If a service root directory is ever configured,
  config-as-code needs an absolute repo path, and there are reports of the `builder` directive
  being ignored in some setups — harmless here, since Railway auto-detects the Dockerfile anyway.

## Confirmed on the first deployment

Written down because these were all reasoned about at length before anything ran, and the
reasoning turned out to be right in a way that only a real boot could establish:

- The volume mounts and the database lives on it — `Mounting volume on: .../vol_…`, followed by
  all three migrations applying against it.
- **Seeding does not crash the container.** The demo portfolio persisted at startup without
  `ApplyOwnership` throwing, which is the failure this design was built to avoid.
- **The idempotency guard asks a real question.** The check ran as
  `SELECT EXISTS (… WHERE "r"."UserId" = @__ef_filter__UserId_0)` with the filter parameter bound
  to the seeded user's id. Through a null tenant it would have compared against NULL, matched
  nothing, and re-seeded the demo rows on every single boot.

## Still to verify on a running instance

Each of these is a real possibility rather than a formality:

1. **That the auth rate limiter actually partitions per client.** `UseForwardedHeaders` runs with
   `KnownProxies` cleared, which switches off per-hop verification entirely, and `ForwardLimit=1`
   reads the rightmost `X-Forwarded-For` entry. That entry is the real client only if exactly one
   hop appends to the header. If Railway's edge fronts a regional router, the value is a constant
   internal address and the limiter silently collapses back to one bucket for the whole
   deployment — which, because the policy covers `/api/auth/me`, means ten requests a minute
   across every page load. **Log the resolved `RemoteIpAddress` and confirm it varies per
   client.** Until then the limiter is not proven.
2. **That a fresh preview environment's `/data` volume is empty** rather than copied from the base
   environment. Evidence points that way — Railway sells volume-data copying as an opt-in feature
   — but the seeding design rests on it.
3. **That PR environments are available on the current plan.** Not clearly documented either way.

## Local development is unchanged

`dotnet run` still serves the API on `:5296` and Vite still serves the UI on `:5173`.

Two things to know:

- **`dotnet run` will now 404 on a deep link.** `MoneyManager.Api/wwwroot` does not exist in a
  checkout — the Dockerfile creates it — so the SPA fallback has no `index.html` to serve. Use the
  Vite dev server, which is what `.env.development` points at.
- **Pointing a local UI at the deployed API fails with an opaque CORS error.** The default
  `Cors:AllowedOrigins` is `localhost:5173` and will not match the Railway origin. CORS is
  correctly a non-issue for the deployed app, which is same-origin.
