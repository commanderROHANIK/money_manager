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
dotnet test app.sln                       # 30 tests: analytics calculator + tenant isolation
cd money-manager-ui && npm ci && npm test # 41 tests: widget smoke suite
cd money-manager-ui && npm run build      # vue-tsc runs here, so type errors fail the build
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

### Authorization is deny-by-default

`Program.cs` sets a `FallbackPolicy` requiring an authenticated user, so an endpoint that
forgets `[Authorize]` is still protected. Only `register` and `login` are `[AllowAnonymous]`.
Adding `[AllowAnonymous]` anywhere is a security decision — call it out in the PR.

### No outbound network calls

The app makes none, and fonts are self-hosted via `@fontsource` specifically so the page makes
no third-party request on load. Do not add a CDN link, an analytics SDK, or telemetry.

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
`Controllers/` | an integration test (see `MoneyManager.Api.Tests/Integration/` once it exists)
a new widget | fixture props in `src/__tests__/fixtures.ts`, so the smoke suite mounts it
`src/utils/` or `src/services/` | a colocated unit test

**Never delete or weaken an existing assertion to make a build pass.** If you believe a test is
wrong, leave it failing and say so in the PR description.

**Never add `eslint-disable`, `#pragma warning disable`, or a `!` null-forgiving operator to
silence a check.** Those turn a real signal into a silent one. Fix the cause or leave it visible.

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
**UI** block merge. Quality checks (lint, formatting, coverage) run separately and are advisory
— they will show red without blocking you.

If a lint warning appears in a file you did not touch, leave it. Fix only what you changed.
