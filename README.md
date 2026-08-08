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

### Multi-tenancy

Each property is denominated in a single currency, fixed at creation, so per-property
analytics involve no FX at all. Conversion happens only at a rollup — portfolio totals and
the bank-balance summary — using rates the user enters themselves under Settings; nothing is
fetched, because the app makes no outbound calls. A pair with no rate on record leaves the
affected totals null and names the rate that would fill them in, rather than adding unlike
amounts into a plausible wrong number, and any total that did come from a conversion is
labelled with the rate and the date it was recorded.

Whether a portfolio that already shares one currency is converted into the user's base
currency is their choice, in Settings. It is off by default, so a landlord holding only HUF
gets exact totals without entering a rate at all.

Tenant isolation is enforced in the data layer, not in controllers: every owned entity has a
global query filter, and the owner is stamped in `SaveChanges` and pinned on update. A
controller that forgets to filter still cannot read across the boundary, and ownership can
never be supplied through a request body. `TenantIsolationTests` covers this.

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

A rate feed (rates are typed in, and dated rate history for past-period analytics is not
modelled — the latest rate on record is used and the output says so); an automatic market-rent feed
(market estimates are entered by hand today, behind the same `RentPricePoint` model a feed
would write to); IRR; tax and depreciation modelling; billing and subscriptions.
