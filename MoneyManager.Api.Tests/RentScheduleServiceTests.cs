using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Rent;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// The two guarantees that live in the service rather than in the pure builder, and so cannot be
/// checked with literals: which ledger rows count as rent at all, and that the arrears rollup
/// obeys the tenant boundary.
///
/// <para>
/// Against real SQLite rather than the in-memory provider, for the same reason
/// <see cref="TenantIsolationTests"/> is: the in-memory provider does not enforce relational
/// semantics, and the global query filter is exactly the thing under test in the last case here.
/// </para>
/// </summary>
public sealed class RentScheduleServiceTests : IDisposable
{
    private static readonly DateTime AsOf = new(2025, 12, 15);

    private const int Alice = 1;
    private const int Bob = 2;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MoneyManagerDbContext> _options;

    public RentScheduleServiceTests()
    {
        // Held open for the fixture's lifetime; closing it drops the database.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MoneyManagerDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var setup = ContextFor(null);
        setup.Database.EnsureCreated();

        setup.Users.AddRange(
            new User { Id = Alice, Username = "alice", NormalizedUsername = "ALICE", Email = "a@e.com", NormalizedEmail = "A@E.COM" },
            new User { Id = Bob, Username = "bob", NormalizedUsername = "BOB", Email = "b@e.com", NormalizedEmail = "B@E.COM" });

        setup.SaveChanges();
    }

    private MoneyManagerDbContext ContextFor(int? userId) => new(_options, new StubCurrentUser(userId));

    /// <summary>A let property with one open-ended tenancy from 2025-01-01, 1,000 a month due on the 5th.</summary>
    private int SeedLetPropertyFor(int userId, string name)
    {
        using var context = ContextFor(userId);

        var property = new RentalProperty { PropertyName = name, Address = "somewhere", CurrencyCode = "EUR" };
        context.RentalProperties.Add(property);
        context.SaveChanges();

        context.Leases.Add(new Lease
        {
            RentalPropertyId = property.Id,
            TenantName = "Anna",
            StartDate = new DateTime(2025, 1, 1),
            MonthlyRent = 1_000m,
            CurrencyCode = "EUR",
            RentDueDayOfMonth = 5,
        });
        context.SaveChanges();

        return property.Id;
    }

    private void Record(int userId, int propertyId, DateTime date, decimal amount, TransactionCategory category)
    {
        using var context = ContextFor(userId);

        context.PropertyTransactions.Add(new PropertyTransaction
        {
            RentalPropertyId = propertyId,
            Date = date,
            Amount = amount,
            CurrencyCode = "EUR",
            Category = category,
        });

        context.SaveChanges();
    }

    private async Task<RentSchedule> ScheduleFor(int userId, int propertyId)
    {
        using var context = ContextFor(userId);
        var schedule = await new RentScheduleService(context).GetForPropertyAsync(propertyId, asOf: AsOf);

        Assert.NotNull(schedule);
        return schedule;
    }

    [Fact]
    public async Task A_deposit_does_not_settle_a_month()
    {
        var propertyId = SeedLetPropertyFor(Alice, "Alice's flat");

        // Exactly one month's rent in value, on the day rent fell due — and still not rent. A
        // deposit is the tenant's money held on their behalf; counting it would show January
        // collected and make the tenancy look more profitable than it is.
        Record(Alice, propertyId, new DateTime(2025, 1, 5), 1_000m, TransactionCategory.DepositReceived);

        var january = (await ScheduleFor(Alice, propertyId)).Periods.Single(p => p.Period == "2025-01");

        Assert.Equal(RentPeriodStatus.Unpaid, january.Status);
        Assert.Equal(0m, january.ReceivedAmount);
    }

    [Fact]
    public async Task Other_income_does_not_settle_a_month_either()
    {
        var propertyId = SeedLetPropertyFor(Alice, "Alice's flat");
        Record(Alice, propertyId, new DateTime(2025, 1, 5), 1_000m, TransactionCategory.OtherIncome);

        var january = (await ScheduleFor(Alice, propertyId)).Periods.Single(p => p.Period == "2025-01");

        Assert.Equal(RentPeriodStatus.Unpaid, january.Status);
    }

    [Fact]
    public async Task Rent_income_does_settle_a_month()
    {
        var propertyId = SeedLetPropertyFor(Alice, "Alice's flat");
        Record(Alice, propertyId, new DateTime(2025, 1, 5), 1_000m, TransactionCategory.RentIncome);

        var january = (await ScheduleFor(Alice, propertyId)).Periods.Single(p => p.Period == "2025-01");

        // The counterpart to the two tests above: without this pair, a filter that excluded
        // everything would pass them both.
        Assert.Equal(RentPeriodStatus.Paid, january.Status);
    }

    [Fact]
    public async Task Arrears_lists_only_properties_that_owe_something()
    {
        var behind = SeedLetPropertyFor(Alice, "Behind on rent");
        var square = SeedLetPropertyFor(Alice, "All paid up");

        // Twelve months have fallen due by 2025-12-15 — December's rent was due on the 5th.
        for (var month = 1; month <= 12; month++)
        {
            Record(Alice, square, new DateTime(2025, month, 5), 1_000m, TransactionCategory.RentIncome);
        }

        using var context = ContextFor(Alice);
        var arrears = await new RentScheduleService(context).GetArrearsAsync(AsOf);

        var only = Assert.Single(arrears);
        Assert.Equal(behind, only.PropertyId);
        Assert.Equal(12, only.OverduePeriodCount);
        Assert.Equal(12_000m, only.Arrears);
        Assert.Equal("2025-01", only.OldestOverduePeriod);
    }

    [Fact]
    public async Task Arrears_never_reach_across_the_tenant_boundary()
    {
        SeedLetPropertyFor(Alice, "Alice's flat");
        SeedLetPropertyFor(Bob, "Bob's flat");

        using var asBob = ContextFor(Bob);
        var arrears = await new RentScheduleService(asBob).GetArrearsAsync(AsOf);

        // Both are in arrears; Bob may only be told about his own. The service does no filtering
        // of its own — this passes because the query filter in the data layer does it, which is
        // the arrangement that survives someone forgetting a Where clause.
        var only = Assert.Single(arrears);
        Assert.Equal("Bob's flat", only.PropertyName);
    }

    [Fact]
    public async Task A_property_belonging_to_someone_else_has_no_schedule()
    {
        var alicesProperty = SeedLetPropertyFor(Alice, "Alice's flat");

        using var asBob = ContextFor(Bob);

        Assert.Null(await new RentScheduleService(asBob).GetForPropertyAsync(alicesProperty, asOf: AsOf));
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StubCurrentUser(int? userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;
    }
}
