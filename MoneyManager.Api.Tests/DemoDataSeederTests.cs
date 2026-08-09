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
        var property = Assert.Single(context.RentalProperties.IgnoreQueryFilters());
        Assert.Equal(user.Id, property.UserId);

        // The portfolio is only worth seeding if it renders something. A property with no
        // tenancy and no ledger produces a dashboard of dashes.
        Assert.NotEmpty(context.Leases.IgnoreQueryFilters());
        Assert.NotEmpty(context.PropertyTransactions.IgnoreQueryFilters());

        Assert.All(context.Leases.IgnoreQueryFilters(), lease => Assert.Equal(user.Id, lease.UserId));
        Assert.All(context.PropertyTransactions.IgnoreQueryFilters(),
            transaction => Assert.Equal(user.Id, transaction.UserId));
    }

    [Fact]
    public async Task Seeding_twice_leaves_one_demo_portfolio()
    {
        await SeedAsync();

        int transactionsAfterFirstRun;
        using (var afterFirstRun = ContextFor(null))
        {
            transactionsAfterFirstRun = afterFirstRun.PropertyTransactions.IgnoreQueryFilters().Count();
        }

        // The long-lived environment runs the seeder on every boot, so this is the ordinary
        // case rather than an edge one.
        await SeedAsync();

        using var context = ContextFor(null);

        // IgnoreQueryFilters throughout: the filter is the mechanism under suspicion here, so
        // counting through it would be asking the accused to testify.
        Assert.Single(context.Users);
        Assert.Single(context.RentalProperties.IgnoreQueryFilters());
        Assert.Single(context.Leases.IgnoreQueryFilters());
        Assert.Equal(transactionsAfterFirstRun,
            context.PropertyTransactions.IgnoreQueryFilters().Count());
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
