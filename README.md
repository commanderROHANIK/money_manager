# Money Manager — rental property investment tracker

Tracks a rental portfolio and tells you what it actually returns: how much is invested in
each property, what it yields, what it cashflows, how much equity has built up, and where
the rent is trailing the market. Bank accounts, loans and stocks are tracked alongside as
secondary assets.

The question the product exists to answer is *"which of my properties is underperforming,
and by how much"* — so every figure is computed from a recorded ledger rather than a
projection, and any figure that cannot be known is shown as unknown rather than as zero.

## Running it

Requirements: .NET 9 SDK and Node 20+.

```bash
# API — http://localhost:5296 (Swagger at /swagger)
cd MoneyManager.Api
dotnet run

# UI — http://localhost:5173
cd money-manager-ui
npm install
npm run dev
```

The database is a SQLite file (`MoneyManager.Api/moneymanager.db`) created and migrated
automatically on first run. Register a user in the UI to get started.

### Configuration

In Development the app runs with no setup. Outside Development it refuses to start without
a JWT signing key of at least 32 bytes:

```bash
JwtSettings__SecretKey=$(openssl rand -base64 48)
```

See `.env.example` and `money-manager-ui/.env.example` for the full set. The UI reads its
API URL from `VITE_API_BASE_URL`.

### Tests

```bash
dotnet test
```

## How it fits together

- **`MoneyManager.Api`** — ASP.NET Core 9, EF Core, SQLite.
- **`money-manager-ui`** — Vue 3, TypeScript, Vite, Tailwind, Chart.js.

### The domain

A property is the aggregate root. Everything that determines its return hangs off it:

| Entity | Why it exists |
|---|---|
| `PropertyTransaction` | Every movement of money. Without it no return is measurable. |
| `Lease` | Rent belongs to a tenancy, not a building — this is what makes occupancy and vacancy expressible. |
| `RentPricePoint` | The rent timeline: what you charge *and* what the market is estimated to pay, in one table so the gap is a single query. |
| `PropertyValuation` | Equity and appreciation need a value timeline, not a guess. |
| `PropertyEvent` | The property's history, written automatically as tenancies start, capital is spent and rents change. |
| `Loan` | Links to a property as its mortgage, which is what makes equity and leveraged return computable. |

Two conventions worth knowing before adding to it:

- **Transaction amounts are always positive.** Direction comes from the category, so there is
  one sign convention rather than two ways to write an expense. `TransactionCategoryInfo`
  holds the accounting treatment of each category (income, operating, financing, capital) in
  one place.
- **Occupancy and current rent are derived, never stored.** They come from the tenancy
  running today, so they cannot drift out of date.

### The analytics engine

`Services/Analytics/PropertyAnalyticsCalculator` is pure — no database, no clock, no
configuration. Everything arrives in a record, which is what makes each formula checkable
against the worked example in `PropertyAnalyticsCalculatorTests`.

Every metric is nullable, and every missing input produces a warning that the UI renders
next to the numbers ("no valuation on record, using purchase price"). A null means *cannot
be known*, never zero. This is deliberate: a spreadsheet gives you a confident wrong
number, and saying which inputs are soft is most of what makes the output worth trusting.

Cap rate excludes financing so it stays comparable between properties; deposits are
excluded from income because they are repayable.

### Currency

Each property is denominated in a single currency, fixed at creation, so per-property
analytics involve no FX at all. Conversion happens only at the portfolio rollup, against the
user's base currency, using rates from the `ExchangeRate` table — set them under **Settings**.

Only one direction of each pair is stored; the inverse is derived, and unrelated pairs are
crossed through EUR. An unreachable pair yields **null, never 1:1** — treating an unknown
rate as parity would report a forint portfolio as though forints were euros. Where a held
currency has no rate, portfolio totals are withheld entirely and that currency is named,
because a total covering only the properties there happen to be rates for reads as a
portfolio total without being one.

### Market rent

`IMarketRentProvider` produces estimates; `PeerComparableRentProvider` is the default and
needs no external service. It takes the median rent per square metre of comparable let
properties — same city, same type, bedrooms within one, same currency — from across the
whole userbase, and returns nothing below three comparables.

This is the one place that deliberately reads across the tenant boundary. Only aggregates
leave it: a median, a range and a count, never an address, a name or a row. The minimum
sample size is what stops an estimate being a restatement of one neighbour's rent. Both
properties are covered by tests, and both must survive any change here.

A background service refreshes stale estimates. Because it runs with no authenticated user,
it reads with `IgnoreQueryFilters()` and writes with an explicit owner — without that the
tenant filter turns it into a job that silently does nothing.

Every market figure is rendered with its provider, as-at date and sample size. An
authoritative-looking wrong rent is worse than no rent at all.

### Multi-tenancy

Tenant isolation is enforced in the data layer, not in controllers: every owned entity has a
global query filter, and the owner is stamped in `SaveChanges` and pinned on update. A
controller that forgets to filter still cannot read across the boundary, and ownership can
never be supplied through a request body. `TenantIsolationTests` covers this.

One deliberate exception: with no authenticated user at all, `SaveChanges` accepts an
explicitly assigned owner, which is what lets background work write on a user's behalf.
Inside a request there is always a current user, so the owner still always comes from the
token.

Entities' parent navigation properties are excluded from responses. EF fixes them up when
the parent is tracked in the same context, and serialising one would return the entire
property graph — bloated, cyclic, and disclosing more than the endpoint intended.

Two consequences to respect when extending it:

- Use `FirstOrDefaultAsync(x => x.Id == id)`, never `FindAsync`. `Find` can return a
  change-tracked instance without querying, which bypasses the query filter.
- Never attach a client-constructed entity (`Entry(e).State = Modified`). Load through the
  filtered set and copy named fields from a request DTO.

### SQLite note

SQLite has no native decimal type, so money columns must be materialised before being
summed — `SumAsync` on a decimal column loses precision. All aggregation happens in memory
after `ToListAsync`. Keeping it inside the analytics service means moving to PostgreSQL
later is a provider swap.

## Not built yet

An automatic exchange-rate feed (rates are entered by hand; an ECB-backed provider would
write the same rows with a different `Source`); a paid market-rent data source behind the
existing `IMarketRentProvider` seam; IRR; tax and depreciation modelling; refresh tokens and
password reset; billing and subscriptions.
