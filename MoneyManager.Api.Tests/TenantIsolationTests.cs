using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// Isolation is enforced by the data layer — global query filters plus owner stamping in
/// SaveChanges — so it is tested there, against a real SQLite database rather than the
/// in-memory provider, which does not enforce relational semantics.
///
/// These are the tests that protect the business: a landlord seeing another landlord's
/// portfolio is the one defect this product cannot ship with.
/// </summary>
public sealed class TenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MoneyManagerDbContext> _options;

    private const int Alice = 1;
    private const int Bob = 2;

    public TenantIsolationTests()
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

    private MoneyManagerDbContext ContextFor(int? userId) =>
        new(_options, new StubCurrentUser(userId));

    private int SeedPropertyFor(int userId, string name)
    {
        using var context = ContextFor(userId);
        var property = new RentalProperty { PropertyName = name, Address = "somewhere", CurrencyCode = "EUR" };
        context.RentalProperties.Add(property);
        context.SaveChanges();
        return property.Id;
    }

    [Fact]
    public void Owner_is_stamped_from_the_request_not_the_payload()
    {
        // Bob's session tries to create a row already labelled as Alice's.
        using (var context = ContextFor(Bob))
        {
            context.RentalProperties.Add(new RentalProperty
            {
                UserId = Alice,
                PropertyName = "Mislabelled",
                CurrencyCode = "EUR",
            });
            context.SaveChanges();
        }

        using var asAlice = ContextFor(Alice);
        Assert.Empty(asAlice.RentalProperties.ToList());

        using var asBob = ContextFor(Bob);
        Assert.Single(asBob.RentalProperties.ToList());
    }

    [Fact]
    public void Listing_returns_only_the_requesting_users_rows()
    {
        SeedPropertyFor(Alice, "Alice's flat");
        SeedPropertyFor(Bob, "Bob's flat");

        using var asAlice = ContextFor(Alice);
        var visible = asAlice.RentalProperties.ToList();

        Assert.Single(visible);
        Assert.Equal("Alice's flat", visible[0].PropertyName);
    }

    [Fact]
    public void Fetching_another_users_row_by_id_finds_nothing()
    {
        var aliceProperty = SeedPropertyFor(Alice, "Alice's flat");

        using var asBob = ContextFor(Bob);
        Assert.Null(asBob.RentalProperties.FirstOrDefault(p => p.Id == aliceProperty));
    }

    [Fact]
    public void Ownership_cannot_be_transferred_by_updating_a_row()
    {
        var aliceProperty = SeedPropertyFor(Alice, "Alice's flat");

        using (var asAlice = ContextFor(Alice))
        {
            var property = asAlice.RentalProperties.First(p => p.Id == aliceProperty);
            property.UserId = Bob;              // attempt to hand the row over
            property.PropertyName = "Renamed";
            asAlice.SaveChanges();
        }

        using var asBob = ContextFor(Bob);
        Assert.Empty(asBob.RentalProperties.ToList());

        using var asAlice2 = ContextFor(Alice);
        Assert.Equal("Renamed", asAlice2.RentalProperties.Single().PropertyName);
    }

    [Fact]
    public void Every_owned_entity_type_is_filtered_not_just_properties()
    {
        var propertyId = SeedPropertyFor(Alice, "Alice's flat");

        using (var asAlice = ContextFor(Alice))
        {
            asAlice.Loans.Add(new Loan { LoanName = "Mortgage", CurrencyCode = "EUR" });
            asAlice.BankAccounts.Add(new BankAccount { AccountName = "Current" });
            asAlice.Stocks.Add(new Stock { Ticker = "AAPL" });
            asAlice.UpcomingEvents.Add(new UpcomingEvent { Title = "Inspection" });
            asAlice.Leases.Add(new Lease { RentalPropertyId = propertyId, TenantName = "Tenant", MonthlyRent = 1000 });
            asAlice.PropertyTransactions.Add(new PropertyTransaction
            {
                RentalPropertyId = propertyId,
                Amount = 1000,
                Category = TransactionCategory.RentIncome,
            });
            asAlice.PropertyValuations.Add(new PropertyValuation { RentalPropertyId = propertyId, Value = 200_000 });
            asAlice.RentPricePoints.Add(new RentPricePoint { RentalPropertyId = propertyId, Amount = 1000 });
            asAlice.PropertyEvents.Add(new PropertyEvent { RentalPropertyId = propertyId, Title = "Bought" });
            asAlice.ExchangeRates.Add(new ExchangeRate
            {
                BaseCurrency = "EUR",
                QuoteCurrency = "HUF",
                Rate = 400m,
            });
            asAlice.AgendaAcknowledgements.Add(new AgendaAcknowledgement { Key = "manual:1" });
            asAlice.SaveChanges();
        }

        using var asBob = ContextFor(Bob);

        Assert.Empty(asBob.Loans.ToList());
        Assert.Empty(asBob.BankAccounts.ToList());
        Assert.Empty(asBob.Stocks.ToList());
        Assert.Empty(asBob.UpcomingEvents.ToList());
        Assert.Empty(asBob.Leases.ToList());
        Assert.Empty(asBob.PropertyTransactions.ToList());
        Assert.Empty(asBob.PropertyValuations.ToList());
        Assert.Empty(asBob.RentPricePoints.ToList());
        Assert.Empty(asBob.PropertyEvents.ToList());
        Assert.Empty(asBob.ExchangeRates.ToList());
        Assert.Empty(asBob.AgendaAcknowledgements.ToList());
    }

    [Fact]
    public void Two_users_can_acknowledge_the_same_derived_key_independently()
    {
        // The key names a lease or a loan, not a row either user owns exclusively by
        // construction the way a RentalPropertyId foreign key would — so this is the case that
        // actually exercises the unique index being scoped to (UserId, Key) rather than to Key
        // alone.
        using (var asAlice = ContextFor(Alice))
        {
            asAlice.AgendaAcknowledgements.Add(new AgendaAcknowledgement { Key = "loan:1" });
            asAlice.SaveChanges();
        }

        using (var asBob = ContextFor(Bob))
        {
            asBob.AgendaAcknowledgements.Add(new AgendaAcknowledgement { Key = "loan:1" });
            asBob.SaveChanges();
        }

        using var asAlice2 = ContextFor(Alice);
        Assert.Single(asAlice2.AgendaAcknowledgements.ToList());

        using var asBob2 = ContextFor(Bob);
        Assert.Single(asBob2.AgendaAcknowledgements.ToList());
    }

    [Fact]
    public void An_unauthenticated_context_sees_nothing_rather_than_everything()
    {
        SeedPropertyFor(Alice, "Alice's flat");
        SeedPropertyFor(Bob, "Bob's flat");

        // The failure mode that matters: no tenant must mean no rows, never all rows.
        using var anonymous = ContextFor(null);
        Assert.Empty(anonymous.RentalProperties.ToList());
    }

    [Fact]
    public void Persisting_without_an_authenticated_user_is_refused()
    {
        using var anonymous = ContextFor(null);
        anonymous.RentalProperties.Add(new RentalProperty { PropertyName = "Orphan" });

        Assert.Throws<InvalidOperationException>(() => anonymous.SaveChanges());
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StubCurrentUser(int? userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;
    }
}
