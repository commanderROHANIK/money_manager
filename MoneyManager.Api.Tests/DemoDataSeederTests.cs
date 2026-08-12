using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// Seeding runs at startup, outside any request, and writes entities that the data layer refuses
/// to persist without an owner. That combination has two failure modes, and both of them look
/// like correct code.
///
/// <para>
/// The first is fatal and immediate. <c>ApplyOwnership</c> throws rather than write a row with no
/// owner, and at startup there is no <c>HttpContext</c> — so seeding through the ordinary
/// request-scoped tenant fails on the first <c>SaveChanges</c>, before the app starts listening.
/// That is a crash loop on every fresh preview environment: exactly the case seeding exists to
/// serve. It also fails asymmetrically, which is what makes it confusing to diagnose — <c>User</c>
/// is not an owned entity, so the account is created successfully and only the portfolio blows
/// up. <see cref="Seeding_creates_the_account_and_its_demo_portfolio"/> is the guard.
/// </para>
///
/// <para>
/// The second is silent and permanent. The obvious "only seed an empty database" check, asked
/// through a null tenant, compares <c>UserId</c> against NULL — which matches nothing, reports
/// empty on every boot, and duplicates the demo rows on every redeploy of the long-lived
/// environment. <see cref="Seeding_twice_leaves_one_demo_portfolio"/> is the guard, and it
/// deliberately counts rows across all tenants rather than through the filter that is the thing
/// under suspicion.
/// </para>
///
/// <para>
/// Both are fixed by the same thing: seeding through <c>SeedCurrentUser</c> rather than weakening
/// the ownership stamping. <see cref="Seeded_rows_belong_to_the_seeded_user_alone"/> checks that
/// the fix bought the isolation invariant nothing.
/// </para>
/// </summary>
public sealed class DemoDataSeederTests : IDisposable
{
    private const string Password = "seeded-password";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MoneyManagerDbContext> _options;

    public DemoDataSeederTests()
    {
        // Held open for the fixture's lifetime; closing it drops the database. A real SQLite
        // database rather than the in-memory provider, for the same reason TenantIsolationTests
        // uses one: the in-memory provider does not enforce relational semantics.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MoneyManagerDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var setup = ContextFor(null);
        setup.Database.EnsureCreated();
    }

    // ------------------------------------------------------------------
    // The two failure modes
    // ------------------------------------------------------------------

    [Fact]
    public async Task Seeding_creates_the_account_and_its_demo_portfolio()
    {
        await SeedAsync();

        using var context = ContextFor(null);

        var user = Assert.Single(context.Users);
        Assert.Equal("demo", user.Username);

        // Without a named owner this assertion is never reached: SaveChanges throws
        // "Cannot persist a user-owned entity outside an authenticated request" first.
        var properties = context.RentalProperties.IgnoreQueryFilters().ToList();
        Assert.NotEmpty(properties);
        Assert.All(properties, property => Assert.Equal(user.Id, property.UserId));

        // The portfolio is only worth seeding if it renders something. A property with no
        // tenancy and no ledger produces a dashboard of dashes.
        Assert.NotEmpty(context.Leases.IgnoreQueryFilters());
        Assert.NotEmpty(context.PropertyTransactions.IgnoreQueryFilters());

        Assert.All(context.Leases.IgnoreQueryFilters(), lease => Assert.Equal(user.Id, lease.UserId));
        Assert.All(context.PropertyTransactions.IgnoreQueryFilters(),
            transaction => Assert.Equal(user.Id, transaction.UserId));

        // Everything the seeder writes is owned, not only the three types that existed when this
        // test was written. A new kind of seeded row that forgets its owner is invisible to the
        // assertions above and fails at runtime on somebody else's machine.
        Assert.All(context.Loans.IgnoreQueryFilters(), loan => Assert.Equal(user.Id, loan.UserId));
        Assert.All(context.UpcomingEvents.IgnoreQueryFilters(), e => Assert.Equal(user.Id, e.UserId));
        Assert.All(context.PropertyEvents.IgnoreQueryFilters(), e => Assert.Equal(user.Id, e.UserId));
        Assert.All(context.PropertyValuations.IgnoreQueryFilters(), v => Assert.Equal(user.Id, v.UserId));
        Assert.All(context.ExchangeRates.IgnoreQueryFilters(), r => Assert.Equal(user.Id, r.UserId));
    }

    /// <summary>
    /// The demo exists to show the product answering its own question — "which of my properties is
    /// underperforming, and by how much". A portfolio of one has no answer to that, and a portfolio
    /// in one currency never exercises the conversion the dashboard is built around, so both are
    /// pinned here rather than left to whoever next edits the seeder.
    /// </summary>
    [Fact]
    public async Task The_demo_portfolio_can_demonstrate_the_product()
    {
        await SeedAsync();

        using var context = ContextFor(null);

        var properties = context.RentalProperties.IgnoreQueryFilters().ToList();

        // More than one, or there is nothing to compare.
        Assert.True(properties.Count >= 3, $"expected a comparable portfolio, got {properties.Count}");

        // More than one currency, or the portfolio total never has to convert and the applied-rate
        // disclosure — the whole point of the currency work — has nothing to disclose.
        Assert.True(
            properties.Select(p => p.CurrencyCode).Distinct().Count() >= 2,
            "the demo portfolio is single-currency, so conversion is never exercised");

        // A rate for the pair, so a machine with no network still shows a converted total rather
        // than "cannot be known" on the headline figure of the demo.
        Assert.NotEmpty(context.ExchangeRates.IgnoreQueryFilters());

        // One property with a valuation and one without, so the "no valuation on record, using
        // purchase price" warning appears next to a figure that did not need it. That contrast is
        // the honesty the product is sold on; a demo where every input is present never shows it.
        var valued = context.PropertyValuations.IgnoreQueryFilters().Select(v => v.RentalPropertyId).ToHashSet();
        Assert.Contains(properties, p => valued.Contains(p.Id));
        Assert.Contains(properties, p => !valued.Contains(p.Id));

        // A tenancy that has ended, so occupancy has something to report other than "all let".
        var leases = context.Leases.IgnoreQueryFilters().ToList();
        Assert.Contains(leases, l => l.EndDate is not null);
        Assert.Contains(leases, l => l.EndDate is null);

        // The two flags that ship on. A switched-on section with nothing behind it is the thing
        // this seeding exists to stop.
        Assert.NotEmpty(context.Loans.IgnoreQueryFilters());
        Assert.NotEmpty(context.UpcomingEvents.IgnoreQueryFilters());
    }

    /// <summary>
    /// Every seeded amount is positive, whatever it means. Direction lives in the category, via
    /// <c>TransactionCategoryInfo</c> — a negative expense here would be a second sign convention
    /// entering through the back door, and the analytics would subtract it twice.
    /// </summary>
    [Fact]
    public async Task Seeded_amounts_are_positive_and_take_their_direction_from_the_category()
    {
        await SeedAsync();

        using var context = ContextFor(null);

        var transactions = context.PropertyTransactions.IgnoreQueryFilters().ToList();

        Assert.NotEmpty(transactions);
        Assert.All(transactions, t => Assert.True(t.Amount > 0, $"{t.Description} is not positive"));

        // Both directions are represented, or the ledger only ever demonstrates half the model.
        Assert.Contains(transactions, t => TransactionCategoryInfo.DirectionOf(t.Category) == CashFlowDirection.Income);
        Assert.Contains(transactions, t => TransactionCategoryInfo.DirectionOf(t.Category) == CashFlowDirection.Expense);

        // A transaction is denominated in its property's currency. Mixing them is how a total in
        // forint acquires a euro row and stops being a number anyone can defend.
        var currencyOf = context.RentalProperties.IgnoreQueryFilters().ToDictionary(p => p.Id, p => p.CurrencyCode);

        Assert.All(transactions, t => Assert.Equal(currencyOf[t.RentalPropertyId], t.CurrencyCode));
    }

    [Fact]
    public async Task Seeding_twice_leaves_one_demo_portfolio()
    {
        await SeedAsync();

        // Counted rather than assumed. These used to be Assert.Single, which was the same claim
        // while the demo held one property — but the invariant under test is "a second run adds
        // nothing", not "the portfolio is small", and only one of those survives the seeder
        // growing. Comparing counts across the two runs states the real rule and gets stricter as
        // the demo gains rows, where a hard-coded 1 would simply have to be edited.
        int propertiesAfterFirstRun, leasesAfterFirstRun, transactionsAfterFirstRun;
        int loansAfterFirstRun, ratesAfterFirstRun;

        using (var afterFirstRun = ContextFor(null))
        {
            propertiesAfterFirstRun = afterFirstRun.RentalProperties.IgnoreQueryFilters().Count();
            leasesAfterFirstRun = afterFirstRun.Leases.IgnoreQueryFilters().Count();
            transactionsAfterFirstRun = afterFirstRun.PropertyTransactions.IgnoreQueryFilters().Count();
            loansAfterFirstRun = afterFirstRun.Loans.IgnoreQueryFilters().Count();
            ratesAfterFirstRun = afterFirstRun.ExchangeRates.IgnoreQueryFilters().Count();

            Assert.NotEqual(0, propertiesAfterFirstRun);
        }

        // The long-lived environment runs the seeder on every boot, so this is the ordinary
        // case rather than an edge one.
        await SeedAsync();

        using var context = ContextFor(null);

        // IgnoreQueryFilters throughout: the filter is the mechanism under suspicion here, so
        // counting through it would be asking the accused to testify.
        Assert.Single(context.Users);
        Assert.Equal(propertiesAfterFirstRun, context.RentalProperties.IgnoreQueryFilters().Count());
        Assert.Equal(leasesAfterFirstRun, context.Leases.IgnoreQueryFilters().Count());
        Assert.Equal(transactionsAfterFirstRun,
            context.PropertyTransactions.IgnoreQueryFilters().Count());
        Assert.Equal(loansAfterFirstRun, context.Loans.IgnoreQueryFilters().Count());

        // The seeded rate is the one row a second boot could plausibly duplicate by a different
        // route: it is upserted by pair elsewhere, so a seeder that re-added it would collide with
        // the unique index rather than merely double the demo.
        Assert.Equal(ratesAfterFirstRun, context.ExchangeRates.IgnoreQueryFilters().Count());
    }

    [Fact]
    public async Task Seeded_rows_belong_to_the_seeded_user_alone()
    {
        await SeedAsync();

        int seededUserId;
        using (var seeded = ContextFor(null))
        {
            seededUserId = seeded.Users.Single().Id;
        }

        // Whatever the seeder does, it must not have produced rows that leak across the tenant
        // boundary. Asked as a different user, the portfolio does not exist.
        using var stranger = ContextFor(seededUserId + 1);

        Assert.Empty(stranger.RentalProperties);
        Assert.Empty(stranger.Leases);
        Assert.Empty(stranger.PropertyTransactions);
    }

    // ------------------------------------------------------------------
    // The switches
    // ------------------------------------------------------------------

    [Fact]
    public async Task Nothing_is_seeded_when_seeding_is_disabled()
    {
        await SeedAsync(enabled: false);

        using var context = ContextFor(null);

        Assert.Empty(context.Users);
        Assert.Empty(context.RentalProperties.IgnoreQueryFilters());
    }

    [Fact]
    public async Task The_account_can_be_seeded_without_the_demo_portfolio()
    {
        // What a long-lived environment holding real records wants: a way in, and none of the
        // invented data.
        await SeedAsync(includeDemoData: false);

        using var context = ContextFor(null);

        Assert.Single(context.Users);
        Assert.Empty(context.RentalProperties.IgnoreQueryFilters());
    }

    [Fact]
    public async Task The_seeded_account_can_be_verified_with_the_configured_password()
    {
        await SeedAsync();

        using var context = ContextFor(null);
        var user = context.Users.Single();

        // The seeded account has to be an ordinary account: hashed by the same service the
        // login path verifies against, or the only way into the deployment does not open.
        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, Password);

        Assert.NotEqual(PasswordVerificationResult.Failed, result);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task SeedAsync(bool enabled = true, bool includeDemoData = true)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton(_options);
        services.Configure<SeedOptions>(options =>
        {
            options.Enabled = enabled;
            options.IncludeDemoData = includeDemoData;
            options.Username = "demo";
            options.Email = "demo@example.invalid";
            options.Password = Password;
        });

        // Awaited rather than returned: the provider owns the DbContexts the seeder builds, so
        // disposing it while the returned task is still running is a use-after-dispose.
        await using var provider = services.BuildServiceProvider();

        await DemoDataSeeder.SeedAsync(provider);
    }

    private MoneyManagerDbContext ContextFor(int? userId) => new(_options, new StubCurrentUser(userId));

    public void Dispose() => _connection.Dispose();

    private sealed class StubCurrentUser(int? userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;
    }
}
