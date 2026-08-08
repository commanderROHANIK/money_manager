using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// Applies the migrations and then uses the schema they produced.
///
/// <para>
/// Nothing else in the suite does. <see cref="TenantIsolationTests"/> builds its database with
/// <c>EnsureCreated()</c>, which goes straight from the model and never opens a migration file,
/// and CI's <c>dotnet ef migrations has-pending-model-changes</c> only compares the model against
/// the snapshot. So a migration whose <c>Up()</c> disagrees with both is green through build,
/// test and CI alike, and fails the first time someone runs the app — which is exactly the
/// "green build, broken on someone else's machine" trap the repo warns about.
/// </para>
/// </summary>
public sealed class MigrationSchemaTests : IDisposable
{
    private const int Alice = 1;
    private const int Bob = 2;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MoneyManagerDbContext> _options;

    public MigrationSchemaTests()
    {
        // Held open for the fixture's lifetime; closing it drops the database.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MoneyManagerDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var setup = ContextFor(null);
        setup.Database.Migrate();

        setup.Users.AddRange(
            new User { Id = Alice, Username = "alice", NormalizedUsername = "ALICE", Email = "a@e.com", NormalizedEmail = "A@E.COM" },
            new User { Id = Bob, Username = "bob", NormalizedUsername = "BOB", Email = "b@e.com", NormalizedEmail = "B@E.COM" });

        setup.SaveChanges();
    }

    private MoneyManagerDbContext ContextFor(int? userId) => new(_options, new StubMigrationUser(userId));

    private static ExchangeRate Rate(string from, string to, decimal rate) => new()
    {
        BaseCurrency = from,
        QuoteCurrency = to,
        Rate = rate,
        AsOf = new DateTime(2026, 7, 1),
    };

    [Fact]
    public void The_migrated_schema_stores_and_returns_an_exchange_rate()
    {
        using (var asAlice = ContextFor(Alice))
        {
            asAlice.ExchangeRates.Add(Rate("EUR", "HUF", 400.5m));
            asAlice.SaveChanges();
        }

        using var reader = ContextFor(Alice);
        var stored = Assert.Single(reader.ExchangeRates.ToList());

        Assert.Equal("EUR", stored.BaseCurrency);
        Assert.Equal("HUF", stored.QuoteCurrency);
        Assert.Equal(400.5m, stored.Rate);
        Assert.Equal(new DateTime(2026, 7, 1), stored.AsOf);
        Assert.Equal(ExchangeRateSource.Manual, stored.Source);
    }

    [Fact]
    public void A_user_can_only_hold_one_rate_per_pair()
    {
        // The unique index is what makes the endpoint's upsert an upsert. Without it a second
        // PUT would leave two rows for one pair, quietly disagreeing with each other.
        using var asAlice = ContextFor(Alice);
        asAlice.ExchangeRates.Add(Rate("EUR", "HUF", 400m));
        asAlice.SaveChanges();

        asAlice.ExchangeRates.Add(Rate("EUR", "HUF", 410m));

        Assert.Throws<DbUpdateException>(() => asAlice.SaveChanges());
    }

    [Fact]
    public void Two_users_can_hold_their_own_rate_for_the_same_pair()
    {
        using (var asAlice = ContextFor(Alice))
        {
            asAlice.ExchangeRates.Add(Rate("EUR", "HUF", 400m));
            asAlice.SaveChanges();
        }

        using (var asBob = ContextFor(Bob))
        {
            asBob.ExchangeRates.Add(Rate("EUR", "HUF", 395m));
            asBob.SaveChanges();
        }

        using var reader = ContextFor(Bob);
        Assert.Equal(395m, Assert.Single(reader.ExchangeRates.ToList()).Rate);
    }

    [Fact]
    public void The_migrated_users_table_carries_the_conversion_preference()
    {
        // Added by the same migration. An entity change with no migration behind it builds and
        // tests green and only fails when the column is actually read.
        using (var asAlice = ContextFor(Alice))
        {
            var alice = asAlice.Users.Single(u => u.Id == Alice);
            alice.AlwaysConvertToBaseCurrency = true;
            alice.BaseCurrency = "HUF";
            asAlice.SaveChanges();
        }

        using var reader = ContextFor(Alice);
        var stored = reader.Users.Single(u => u.Id == Alice);

        Assert.True(stored.AlwaysConvertToBaseCurrency);
        Assert.Equal("HUF", stored.BaseCurrency);
    }

    [Fact]
    public void The_conversion_preference_defaults_to_off()
    {
        using var reader = ContextFor(Bob);
        Assert.False(reader.Users.Single(u => u.Id == Bob).AlwaysConvertToBaseCurrency);
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StubMigrationUser(int? userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;
    }
}
