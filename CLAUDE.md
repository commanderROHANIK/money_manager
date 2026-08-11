# Working in this repository

A rental-property investment tracker: ASP.NET Core 9 API (`MoneyManager.Api`) plus a Vue 3 SPA
(`money-manager-ui`), backed by SQLite. The product exists to answer one question — *"which of
my properties is underperforming, and by how much"* — which is why every figure is computed
from a recorded ledger rather than projected, and why anything unknowable is shown as unknown
rather than as zero.

`README.md` explains the domain. This file covers the things that break **silently** if you
get them wrong.

## Commands

```bash
dotnet build app.sln
dotnet test app.sln                    # analytics calculator + tenant isolation

cd money-manager-ui
npm ci
npm test
npm run typecheck                      # vue-tsc -b
npm run lint                           # --max-warnings 0: any new warning fails
npm run lint:fix
npm run test:coverage                  # enforces the floor in vitest.config.ts
```

Dev servers: API on `http://localhost:5296` (Swagger at `/swagger`, Development only), UI on
`http://localhost:5173`. The SQLite file is created and migrated on first run.

`.claude/hooks/session-start.sh` restores the toolchain automatically at session start. If it
reports the .NET SDK as unavailable, **the API genuinely cannot be built or tested in that
session** — say so plainly rather than reporting untested code as verified.

## Non-negotiable invariants

Each of these has a reason. Violating one usually still compiles, still passes review, and
fails at runtime or — worse — silently returns wrong numbers.

### Tenant isolation lives in the data layer

`MoneyManagerDbContext` applies a global query filter (`e.UserId == _currentUser.UserId`) to
every owned entity, and `ApplyOwnership()` stamps the owner during `SaveChanges`. A controller
that forgets to filter still cannot read across the boundary. Per-query filtering fails open
the moment someone forgets one, which is why it is not done that way.

- **Use `FirstOrDefaultAsync(x => x.Id == id)`, never `FindAsync`.** `Find` can return a
  change-tracked instance without querying at all, which bypasses the query filter entirely.
- **Never attach a client-constructed entity** (`Entry(e).State = Modified`). Load through the
  filtered set and copy named fields from a request DTO.
- **Never take ownership from a request body.** It is stamped from `ICurrentUser` and pinned on
  update. `TenantIsolationTests` covers this; do not weaken it.

A landlord seeing another landlord's portfolio is the one defect this product cannot ship with.

### Money and direction

- **Transaction amounts are always positive.** Direction comes from the category, via
  `TransactionCategoryInfo` (`Models/PropertyEnums.cs`), which holds the accounting treatment
  — income, operating, financing, capital — in one place. There is one sign convention, not two
  ways to write an expense.
- **Never sum across currencies.** Each property is denominated in one currency fixed at
  creation. Portfolio totals refuse to add unlike amounts and report `mixedCurrency` instead of
  producing a plausible wrong number. `sumSameCurrency` in `src/utils/money.ts` is the
  front-end counterpart — use it, and always pass the record's own `currencyCode` to
  `formatMoney` rather than hardcoding one.
- **SQLite has no decimal type.** `SumAsync` on a money column loses precision. Materialise
  with `ToListAsync` first and aggregate in memory. All of this is confined to the analytics
  service so that moving to PostgreSQL later stays a provider swap.
- **A rate the user entered is never overwritten by a fetched one.** `ExchangeRateRefreshService`
  skips any pair with a `Manual` row, in either direction — the two directions are the same fact.
  A landlord who recorded the rate their bank actually gave them on the day of a transfer means
  it, and a daily reference rate is not a correction to that. This is also why "automatic" is not
  a stored mode: it is what happens for the pairs nobody has expressed an opinion about, so it
  needs no per-user column.
- **The rate disclosed is the rate that was used.** `AppliedRate` carries `Source` and `AsOf`
  alongside the figure it was applied at, and the UI renders those rather than looking the pair up
  again. Re-reading the table at render time would show a number the total was never built from,
  which is worse than showing nothing — it invites the reader to check the arithmetic and find it
  wrong.

### Nullable means "cannot be known"

Every analytics metric is nullable, and **null means cannot be known — never zero.** A missing
input produces a warning string that the UI renders next to the number ("no valuation on
record, using purchase price"). A spreadsheet gives you a confident wrong number; saying which
inputs are soft is most of what makes this output worth trusting. Do not default a null to 0
to make a type check pass.

`formatPercent` and `formatDate` in `src/utils/labels.ts` render null as `—` for the same reason.

### Derived, never stored

Occupancy and current rent come from the tenancy running today (`Lease.IsActiveOn`), so they
cannot drift out of date. Do not add stored `isRented` or `currentRent` columns.

The rent schedule works the same way: `RentScheduleBuilder` derives every month from the
tenancies and the ledger on each request. There is no stored row per month and no stored
`isPaid` flag — editing a transaction changes what the schedule says immediately, which is only
true because nothing was written down. The one thing that *is* stored is the payment itself,
because that is a fact rather than a conclusion.

### The analytics calculator is pure

`Services/Analytics/PropertyAnalyticsCalculator` takes no `DbContext`, no clock, and no
configuration — everything arrives in a record, and time arrives as `asOf`. That purity is what
makes each formula checkable against the worked example in the `PropertyAnalyticsCalculatorTests`
docblock. If you change a formula, update the worked example **and** the test.

### Do not pin `Microsoft.IdentityModel.Tokens`

`MoneyManager.Api.csproj` deliberately leaves it unpinned so `JwtBearer` resolves a coherent
set. Pinning it older than the `Microsoft.IdentityModel.JsonWebTokens` that JwtBearer resolves
makes the handler call a `Base64UrlEncoder` overload that does not exist, and **every token
fails validation with IDX14102 at runtime** — through a completely green build.

The trap has a specific shape: you bump `Microsoft.AspNetCore.Authentication.JwtBearer`, a
restore fails, and adding an explicit `<PackageReference Include="Microsoft.IdentityModel.Tokens" ...>`
looks like the obvious fix. It is the bug. Let JwtBearer pick the set.

`Integration/AuthenticationTests.cs` is what catches it: it hosts the real app, logs in, and
presents the returned token back over HTTP. Nothing else does — no other test sends a request,
so IDX14102 is invisible to the rest of the suite. That is what lets a JwtBearer bump be reviewed
on its CI result rather than on faith, so do not stub the authentication handler out of it, and
do not add `Microsoft.IdentityModel.Tokens` to the test project either.

### Authorization is deny-by-default

`Program.cs` sets a `FallbackPolicy` requiring an authenticated user, so an endpoint that
forgets `[Authorize]` is still protected. Only `register` and `login` are `[AllowAnonymous]`.
Adding `[AllowAnonymous]` anywhere is a security decision — call it out in the PR.

### The browser makes no third-party request

Fonts are self-hosted via `@fontsource` specifically so the page reaches nobody but its own
origin on load. Do not add a CDN link, an analytics SDK, or telemetry.

This used to read "the app makes no outbound calls", which was true until exchange rates started
being fetched. The line that was actually load-bearing is the one above: what the *page* does.
A server-side fetch the operator can see, switch off, and point at a mirror is a different thing
from a script tag that reports every visitor to somebody else, and collapsing the two into one
rule meant the rule had to be broken rather than applied.

So the API may reach out, under these conditions, and adding a second such call means meeting
them again rather than citing this one as precedent:

- **It is behind a flag that removes it entirely.** `Features:AutomaticExchangeRates` off
  registers `NoExchangeRateProvider`, so there is no `HttpClient`, no request and no DNS lookup —
  not merely a hidden button. The registration is chosen from configuration in `Program.cs`,
  before the container exists.
- **It is behind an interface, and failure is ordinary.** `IExchangeRateProvider` returns an empty
  list for unreachable, slow or malformed — never throws — because the correct response to a rate
  provider being down is the rates already stored, not a failed dashboard.
- **It is rate-limited by a cache.** One fetch per user per window. A page load must not become an
  outbound request.
- **What it produces says where it came from.** `ExchangeRateSource` is stored on the row and
  travels on `AppliedRate`, so every figure derived from a fetched rate can name its origin and
  its date. A number from outside the ledger that cannot say where it came from does not belong
  in this product.
- **No credentials, and nothing is sent.** The ECB endpoint takes currency codes and needs no key.
  A provider requiring an API key, or one that would carry portfolio data outbound, is a different
  decision and not covered by this.

`ExchangeRateRefreshServiceTests` holds the first three; `CurrencyConverterTests` holds the
fourth. The suite itself fetches nothing: `ApiFactory` sets `Features__AutomaticExchangeRates` to
false, so a test that reached the real provider would be a deliberate act.

## Things that look wrong but are deliberate

Do not "fix" these:

- **`services/api.ts` uses a hard `window.location.assign('/login')` on 401**, not the router.
  Importing the router there would create a cycle: router → components → api → router.
- **`AuthController` verifies against a decoy hash when no user matches.** It makes a failed
  login cost the same whether or not the username exists, so response time stops being an
  account oracle. It is not dead code.
- **The auth rate limiter is partitioned by client IP.** The overload without a partition key
  hands out one shared bucket for the whole app, which is a self-inflicted denial of service
  rather than a mitigation.
- **`BaseInput` sets `inheritAttrs: false`** and splits attributes deliberately: `class`/`style`
  go to the wrapping `<label>`, everything else to the inner `<input>`.
- **`widgets.smoke.test.ts` fails on any Vue warning.** That is the point — it exists because
  `TenancyWidget` once shipped a template referencing three components its script never
  imported, and both `vue-tsc` and `vite build` passed. If a dependency bump introduces an
  unrelated deprecation warning, **filter by message prefix — do not delete the assertion.**

## Testing expectations

A change to… | needs a test in…
---|---
`Models/` or `Data/MoneyManagerDbContext.cs` | `MoneyManager.Api.Tests/TenantIsolationTests.cs`
`Services/Analytics/PropertyAnalyticsCalculator.cs` | `PropertyAnalyticsCalculatorTests.cs`, plus the worked example in its docblock
`Services/Rent/RentScheduleBuilder.cs` | `RentScheduleBuilderTests.cs`, plus the worked example in its docblock
`Services/Currency/` | `CurrencyConverterTests.cs` for the arithmetic and the provenance it carries, `ExchangeRateRefreshServiceTests.cs` for anything that fetches or writes a rate
`Controllers/` | an integration test in `MoneyManager.Api.Tests/Integration/`
`Program.cs`'s auth, or any `Microsoft.IdentityModel.*` / `JwtBearer` version | `Integration/AuthenticationTests.cs`
a new widget | fixture props in `src/__tests__/fixtures.ts`, so the smoke suite mounts it
`src/utils/` or `src/services/` | a colocated unit test

**Never delete or weaken an existing assertion to make a build pass.** If you believe a test is
wrong, leave it failing and say so in the PR description.

**Never add `eslint-disable`, `#pragma warning disable`, or a `!` null-forgiving operator to
silence a check.** Those turn a real signal into a silent one. Fix the cause or leave it visible.

**C# compiler warnings are errors.** `Directory.Build.props` sets `TreatWarningsAsErrors`,
because the codebase is at zero warnings and staying there is much cheaper than getting back
there. A possible-null dereference or an unused variable fails the build, locally and in CI.

## Migrations

```bash
dotnet ef migrations add <Name> --project MoneyManager.Api
```

Commit the migration, its `.Designer.cs`, and the updated `MoneyManagerDbContextModelSnapshot.cs`
together. Never edit a migration that has already been applied. Editing an entity without
generating a migration produces a fully green build and breaks at runtime on someone else's
machine — CI checks for this drift.

## CI

`.github/workflows/ci.yml` builds and tests both halves on every PR. Checks named **API** and
**UI** block merge.

The **UI** job also runs lint and coverage, both promoted out of the advisory workflow once the
tree was clean. `npm run lint` uses `--max-warnings 0` and coverage enforces the thresholds in
`vitest.config.ts`, so there is no warning backlog to hide in: any new finding fails the build.

If you raise real coverage, raise the thresholds in the same change. That ratchet is the point —
they are set just under measured reality to catch a regression, not as an aspiration.

The **API** job also runs `dotnet format style --verify-no-changes` and
`dotnet ef migrations has-pending-model-changes`. The second is the one that matters most:
editing an entity without generating a migration is green through build *and* test, and only
fails at runtime on someone else's machine.

`.github/workflows/quality.yml` runs one advisory check, **Gate changes**. It fails visibly on
the PR but is deliberately not a required check. Everything else that started there has been
promoted into `ci.yml` by moving the step into the matching job — which is why the required
check names are still just `API` and `UI`, and branch protection has never needed editing.

**If your change touches `.github/`, `.claude/`, `Directory.Build.props` or `eslint.config.js`,
add the `ci-change` label to the PR.** Those paths are the checks themselves, and a change that
weakens one should be a deliberate act rather than an unnoticed line in a large diff. The label
is an acknowledgement, not an approval — add it and say in the PR description what changed and
why. Do not instead widen the path list to avoid the check.

The linter is worth trusting on one rule in particular. `vue/no-undef-components` catches a
template using a component the script never imported — the defect that shipped in
`TenancyWidget`, which `vue-tsc` and `vite build` both pass cleanly. If it fires, it is right.

The **API** job also builds the `Dockerfile`. That is a third build path — compiling the API and
building the bundle do not assemble the artifact that ships — and its failure modes (a stale
`obj/` landing on the restore layer, `Directory.Build.props` not reaching the SDK stage) are
invisible to every other check. It sits inside the API job rather than a job of its own so the
required check names stay `API` and `UI`.

## Deployment

One container serving both halves from one origin: the Dockerfile builds the Vite bundle into the
API's `wwwroot`. `docs/deployment.md` covers the variables, the volume and the open verification
items. The things that break silently:

- **Never set `ASPNETCORE_ENVIRONMENT=Development` on a deployment.** It registers the developer
  exception page, which puts stack traces and the connection string on a public URL, and
  publishes the full Swagger surface. Both are unconditional. Gate Swagger on its own flag.
- **`UseDefaultFiles`/`UseStaticFiles` must stay ahead of `UseAuthorization`.** The SPA fallback's
  route pattern is `{*path:nonfile}`, so a path with a file extension matches no endpoint — and
  the deny-by-default `FallbackPolicy` applies to endpoint-less requests too. Served after
  authorization, every script and stylesheet 401s behind an `index.html` that still loads.
- **Seeding goes through `SeedCurrentUser`, never by relaxing `ApplyOwnership`.** Owned entities
  cannot be persisted without an owner and there is no `HttpContext` at startup, so seeding
  through the request-scoped tenant crash-loops the container before it listens. The same named
  owner is what makes the "already seeded" check work: asked through a null tenant, the query
  filter compares `UserId` against NULL, reports empty on every boot, and duplicates the demo rows
  on every redeploy.
- **One replica, pinned in `railway.json`.** `Database.Migrate()` runs on every boot and SQLite has
  no advisory lock. This is a constraint of the SQLite decision, not a preference.
- **A relative `ConnectionStrings:Default` is fatal outside Development.** The shipped default
  resolves into the container's own writable layer, so the app would boot, migrate, work, and lose
  everything on redeploy without logging anything.
