using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.MarketRent;
using Xunit;

namespace MoneyManager.Api.Tests;

public class PeerComparableStatisticsTests
{
    private static RentComparable Flat(decimal rent, decimal? sqm = null) => new(rent, sqm);

    [Fact]
    public void Nothing_is_estimated_below_the_minimum_sample_size()
    {
        // Two comparables would make the "market" one neighbour's asking price.
        var estimate = PeerComparableStatistics.Estimate(
            [Flat(1_000m), Flat(1_200m)], targetSizeSqm: null);

        Assert.Null(estimate);
    }

    [Fact]
    public void Median_is_used_rather_than_mean_so_one_outlier_cannot_drag_it()
    {
        var estimate = PeerComparableStatistics.Estimate(
            [Flat(1_000m), Flat(1_100m), Flat(1_200m), Flat(1_150m), Flat(9_000m)],
            targetSizeSqm: null);

        // The mean here is 2,690 — the median is the honest answer.
        Assert.Equal(1_150m, estimate!.Monthly);
        Assert.False(estimate.PerSquareMetre);
    }

    [Fact]
    public void Comparison_is_per_square_metre_when_sizes_are_known()
    {
        // 20 EUR/m² across the comparables, applied to a 50 m² target.
        var estimate = PeerComparableStatistics.Estimate(
            [Flat(800m, 40m), Flat(1_200m, 60m), Flat(1_000m, 50m)],
            targetSizeSqm: 50m);

        Assert.True(estimate!.PerSquareMetre);
        Assert.Equal(1_000m, estimate.Monthly);
    }

    [Fact]
    public void Size_scales_the_estimate()
    {
        var comparables = new[] { Flat(800m, 40m), Flat(1_200m, 60m), Flat(1_000m, 50m) };

        var small = PeerComparableStatistics.Estimate(comparables, targetSizeSqm: 30m)!;
        var large = PeerComparableStatistics.Estimate(comparables, targetSizeSqm: 90m)!;

        Assert.Equal(600m, small.Monthly);
        Assert.Equal(1_800m, large.Monthly);
    }

    [Fact]
    public void Falls_back_to_absolute_rents_when_the_target_has_no_size()
    {
        var estimate = PeerComparableStatistics.Estimate(
            [Flat(900m, 40m), Flat(1_000m, 50m), Flat(1_100m, 60m)],
            targetSizeSqm: null);

        Assert.False(estimate!.PerSquareMetre);
        Assert.Equal(1_000m, estimate.Monthly);
    }

    [Fact]
    public void Confidence_reflects_how_much_evidence_there_is()
    {
        var three = PeerComparableStatistics.Estimate(Rents(3), null)!;
        var five = PeerComparableStatistics.Estimate(Rents(5), null)!;
        var twelve = PeerComparableStatistics.Estimate(Rents(12), null)!;

        Assert.Equal(MarketRentConfidence.Low, three.Confidence);
        Assert.Equal(MarketRentConfidence.Medium, five.Confidence);
        Assert.Equal(MarketRentConfidence.High, twelve.Confidence);
    }

    [Fact]
    public void A_range_is_reported_around_the_estimate()
    {
        var estimate = PeerComparableStatistics.Estimate(Rents(8), null)!;

        Assert.True(estimate.Low <= estimate.Monthly);
        Assert.True(estimate.High >= estimate.Monthly);
    }

    private static RentComparable[] Rents(int count) =>
        Enumerable.Range(1, count).Select(i => new RentComparable(1_000m + i * 10m, null)).ToArray();
}

/// <summary>
/// Peer comparables are the one place that deliberately reads across the tenant boundary,
/// so these tests exist to prove the boundary is crossed only in the intended way.
/// </summary>
public sealed class PeerComparableRentProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MoneyManagerDbContext> _options;

    private const int Alice = 1;
    private const int Bob = 2;

    public PeerComparableRentProviderTests()
    {
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

    private int AddLetProperty(
        int userId, string city, decimal rent, decimal? sqm = 50m,
        string currency = "EUR", int? bedrooms = 2, PropertyType type = PropertyType.Apartment)
    {
        using var context = ContextFor(userId);

        var property = new RentalProperty
        {
            PropertyName = $"{city} flat for {userId}",
            Address = "17 Confidential Street",
            City = city,
            CurrencyCode = currency,
            SizeSqm = sqm,
            Bedrooms = bedrooms,
            PropertyType = type,
        };
        context.RentalProperties.Add(property);
        context.SaveChanges();

        context.Leases.Add(new Lease
        {
            RentalPropertyId = property.Id,
            TenantName = "Tenant",
            StartDate = DateTime.UtcNow.Date.AddYears(-1),
            MonthlyRent = rent,
            CurrencyCode = currency,
        });
        context.SaveChanges();

        return property.Id;
    }

    private static MarketRentQuery QueryFor(int excludeId, string city = "Budapest", decimal? sqm = 50m) =>
        new()
        {
            City = city,
            CountryCode = "HU",
            PropertyType = PropertyType.Apartment,
            CurrencyCode = "EUR",
            SizeSqm = sqm,
            Bedrooms = 2,
            ExcludePropertyId = excludeId,
        };

    [Fact]
    public async Task Comparables_are_drawn_from_other_users_properties()
    {
        // Alice has one property; all the evidence belongs to Bob.
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Bob, "Budapest", 1_100m);
        AddLetProperty(Bob, "Budapest", 1_200m);

        using var context = ContextFor(Alice);
        var provider = new PeerComparableRentProvider(context);

        var estimate = await provider.GetEstimateAsync(QueryFor(aliceProperty), default);

        Assert.NotNull(estimate);
        Assert.Equal(3, estimate!.SampleSize);
        Assert.Equal(1_100m, estimate.Monthly);
        Assert.Equal(PeerComparableRentProvider.ProviderKey, estimate.ProviderKey);
    }

    [Fact]
    public async Task Only_aggregates_escape_never_another_landlords_details()
    {
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Bob, "Budapest", 1_100m);
        AddLetProperty(Bob, "Budapest", 1_200m);

        using var context = ContextFor(Alice);
        var estimate = await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default);

        // The estimate is a record of numbers plus a note. Nothing that identifies whose
        // property produced it may appear in any of it.
        var rendered = System.Text.Json.JsonSerializer.Serialize(estimate);

        Assert.DoesNotContain("Confidential", rendered);
        Assert.DoesNotContain("flat for", rendered);
        Assert.DoesNotContain("Tenant", rendered);
        Assert.DoesNotContain("bob", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_property_is_never_its_own_comparable()
    {
        // Three lettings exist, but one of them is the property being valued.
        var aliceProperty = AddLetProperty(Alice, "Budapest", 5_000m);
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Bob, "Budapest", 1_100m);

        using var context = ContextFor(Alice);
        var estimate = await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default);

        // Only two genuine comparables remain, which is under the threshold. Including the
        // property itself would have "confirmed" its own inflated rent.
        Assert.Null(estimate);
    }

    [Fact]
    public async Task Too_few_comparables_yields_no_estimate()
    {
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 1_000m);

        using var context = ContextFor(Alice);
        Assert.Null(await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default));
    }

    [Fact]
    public async Task Properties_in_another_city_are_not_comparable()
    {
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Vienna", 1_000m);
        AddLetProperty(Bob, "Vienna", 1_100m);
        AddLetProperty(Bob, "Vienna", 1_200m);

        using var context = ContextFor(Alice);
        Assert.Null(await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default));
    }

    [Fact]
    public async Task Rents_in_another_currency_are_not_comparable()
    {
        // A median over mixed currencies would be arithmetic on unlike quantities.
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 400_000m, currency: "HUF");
        AddLetProperty(Bob, "Budapest", 420_000m, currency: "HUF");
        AddLetProperty(Bob, "Budapest", 440_000m, currency: "HUF");

        using var context = ContextFor(Alice);
        Assert.Null(await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default));
    }

    [Fact]
    public async Task A_property_with_no_city_cannot_be_compared()
    {
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Bob, "Budapest", 1_100m);
        AddLetProperty(Bob, "Budapest", 1_200m);

        using var context = ContextFor(Alice);
        var estimate = await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty) with { City = null }, default);

        // A nationwide median would be a number rather than an answer.
        Assert.Null(estimate);
    }

    [Fact]
    public async Task Ended_tenancies_are_not_evidence_of_the_current_market()
    {
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Bob, "Budapest", 1_100m);
        var expiredProperty = AddLetProperty(Bob, "Budapest", 1_200m);

        using (var context = ContextFor(Bob))
        {
            var lease = context.Leases.First(l => l.RentalPropertyId == expiredProperty);
            lease.EndDate = DateTime.UtcNow.Date.AddMonths(-1);
            context.SaveChanges();
        }

        using var reader = ContextFor(Alice);
        Assert.Null(await new PeerComparableRentProvider(reader)
            .GetEstimateAsync(QueryFor(aliceProperty), default));
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StubCurrentUser(int? userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;
    }
}
