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
    /// <summary>
    /// Each flat gets its own owner unless a test is specifically about ownership, so the
    /// distinct-owner rule does not quietly swallow the case under test.
    /// </summary>
    private static RentComparable Flat(decimal rent, decimal? sqm = null, int owner = 0) =>
        new(owner == 0 ? NextOwner() : owner, rent, sqm);

    private static int _owner;

    /// <summary>Negative, so an auto-assigned owner can never collide with an explicit one.</summary>
    private static int NextOwner() => -Interlocked.Increment(ref _owner);

    [Fact]
    public void Nothing_is_estimated_below_the_minimum_sample_size()
    {
        // Two comparables would make the "market" one neighbour's asking price.
        var estimate = PeerComparableStatistics.Estimate(
            [Flat(1_000m), Flat(1_200m)], targetSizeSqm: null);

        Assert.Null(estimate);
    }

    [Fact]
    public void Three_flats_owned_by_one_landlord_are_not_a_market()
    {
        // The sample size is met, but every figure in it is one person's pricing, so the
        // median restates that person's rent rather than describing a market.
        var estimate = PeerComparableStatistics.Estimate(
            [Flat(1_000m, owner: 7), Flat(1_100m, owner: 7), Flat(1_200m, owner: 7)],
            targetSizeSqm: null);

        Assert.Null(estimate);
    }

    [Fact]
    public void Two_landlords_are_not_enough_however_many_flats_they_own()
    {
        var estimate = PeerComparableStatistics.Estimate(
            [Flat(1_000m, owner: 1), Flat(1_100m, owner: 1), Flat(1_200m, owner: 2), Flat(1_300m, owner: 2)],
            targetSizeSqm: null);

        Assert.Null(estimate);
    }

    [Fact]
    public void The_sized_subset_has_to_clear_the_thresholds_in_its_own_right()
    {
        // Four owners overall, but only one of them has a property with a size recorded.
        // Falling through to the per-square-metre branch would publish that one landlord's
        // rent per square metre as "the market".
        var estimate = PeerComparableStatistics.Estimate(
            [
                Flat(1_000m, sqm: 50m, owner: 1),
                Flat(1_100m, owner: 2),
                Flat(1_200m, owner: 3),
                Flat(1_300m, owner: 4),
            ],
            targetSizeSqm: 50m);

        Assert.NotNull(estimate);
        Assert.False(estimate!.PerSquareMetre);
    }

    [Fact]
    public void The_published_range_is_never_two_individual_rents()
    {
        // At the smallest permitted sample a nearest-rank quartile returns the lowest and
        // highest values verbatim, which republishes two landlords' exact rents as a
        // "range". Interpolating is what stops that.
        var estimate = PeerComparableStatistics.Estimate(
            [Flat(1_000m), Flat(1_100m), Flat(1_200m)], targetSizeSqm: null)!;

        Assert.NotEqual(1_000m, estimate.Low);
        Assert.NotEqual(1_200m, estimate.High);
        Assert.InRange(estimate.Low, 1_000m, estimate.Monthly);
        Assert.InRange(estimate.High, estimate.Monthly, 1_200m);
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
        Enumerable.Range(1, count).Select(i => new RentComparable(i, 1_000m + i * 10m, null)).ToArray();
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
    private const int Carol = 3;
    private const int Dave = 4;

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
            new User { Id = Bob, Username = "bob", NormalizedUsername = "BOB", Email = "b@e.com", NormalizedEmail = "B@E.COM" },
            new User { Id = Carol, Username = "carol", NormalizedUsername = "CAROL", Email = "c@e.com", NormalizedEmail = "C@E.COM" },
            new User { Id = Dave, Username = "dave", NormalizedUsername = "DAVE", Email = "d@e.com", NormalizedEmail = "D@E.COM" });
        setup.SaveChanges();
    }

    private MoneyManagerDbContext ContextFor(int? userId) => new(_options, new StubCurrentUser(userId));

    private int AddLetProperty(
        int userId, string city, decimal rent, decimal? sqm = 50m,
        string currency = "EUR", int? bedrooms = 2, PropertyType type = PropertyType.Apartment,
        PropertyStatus status = PropertyStatus.Active)
    {
        using var context = ContextFor(userId);

        var property = new RentalProperty
        {
            PropertyName = $"{city} flat for {userId}",
            Address = "17 Confidential Street",
            City = city,
            CountryCode = "HU",
            CurrencyCode = currency,
            SizeSqm = sqm,
            Bedrooms = bedrooms,
            PropertyType = type,
            Status = status,
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

    /// <summary>Adds a second, overlapping tenancy to a property that already has one.</summary>
    private void AddOverlappingLease(int userId, int propertyId, decimal rent)
    {
        using var context = ContextFor(userId);

        context.Leases.Add(new Lease
        {
            RentalPropertyId = propertyId,
            TenantName = "Tenant",
            StartDate = DateTime.UtcNow.Date.AddMonths(-1),
            MonthlyRent = rent,
            CurrencyCode = "EUR",
        });
        context.SaveChanges();
    }

    /// <summary>Three unrelated landlords at 1,000 / 1,100 / 1,200 — the ordinary case.</summary>
    private void AddThreePeerLettings()
    {
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Carol, "Budapest", 1_100m);
        AddLetProperty(Dave, "Budapest", 1_200m);
    }

    private static MarketRentQuery QueryFor(
        int excludeId, string city = "Budapest", decimal? sqm = 50m, int owner = Alice) =>
        new()
        {
            City = city,
            CountryCode = "HU",
            PropertyType = PropertyType.Apartment,
            CurrencyCode = "EUR",
            SizeSqm = sqm,
            Bedrooms = 2,
            ExcludePropertyId = excludeId,
            ExcludeUserId = owner,
        };

    [Fact]
    public async Task Comparables_are_drawn_from_other_users_properties()
    {
        // Alice has one property; the evidence belongs to three unrelated landlords.
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddThreePeerLettings();

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
        AddThreePeerLettings();

        using var context = ContextFor(Alice);
        var estimate = await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default);

        var rendered = System.Text.Json.JsonSerializer.Serialize(estimate);

        Assert.DoesNotContain("Confidential", rendered);
        Assert.DoesNotContain("flat for", rendered);
        Assert.DoesNotContain("Tenant", rendered);
        Assert.DoesNotContain("bob", rendered, StringComparison.OrdinalIgnoreCase);

        // Substring checks only catch the fields that happen to exist today. Pinning the
        // whole shape is what makes this hold in future: a field carrying identity — an
        // owner, an address, a property id — fails here the moment it is added, rather than
        // when someone thinks to extend the list of forbidden words.
        var fields = System.Text.Json.JsonDocument.Parse(rendered)
            .RootElement.EnumerateObject()
            .Select(p => p.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            new[] { "Confidence", "CurrencyCode", "High", "Low", "Monthly", "Notes", "ProviderKey", "SampleSize" },
            fields);
    }

    [Fact]
    public async Task A_property_is_never_its_own_comparable()
    {
        // Three lettings exist, but one of them is the property being valued.
        var aliceProperty = AddLetProperty(Alice, "Budapest", 5_000m);
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Carol, "Budapest", 1_100m);

        using var context = ContextFor(Alice);
        var estimate = await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default);

        // Only two genuine comparables remain, which is under the threshold. Including the
        // property itself would have "confirmed" its own inflated rent.
        Assert.Null(estimate);
    }

    [Fact]
    public async Task A_caller_cannot_seed_the_comparables_with_their_own_decoys()
    {
        // The attack this rules out: Alice brackets a single real neighbour with two
        // properties of her own at absurd rents. If her portfolio counted as evidence, the
        // median of {1, Bob's rent, 999999} would be Bob's rent exactly — an aggregate in
        // name that discloses one identifiable landlord's figure. Bisecting the decoys
        // recovers it to any precision.
        var target = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Alice, "Budapest", 1m);
        AddLetProperty(Alice, "Budapest", 999_999m);
        AddLetProperty(Bob, "Budapest", 1_000m);

        using var context = ContextFor(Alice);
        var estimate = await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(target), default);

        Assert.Null(estimate);
    }

    [Fact]
    public async Task A_callers_whole_portfolio_is_excluded_not_just_the_one_property()
    {
        // Three comparables exist and all clear the sample size, but two are Alice's own.
        var target = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Alice, "Budapest", 1_000m);
        AddLetProperty(Alice, "Budapest", 1_100m);
        AddLetProperty(Bob, "Budapest", 1_200m);

        using var context = ContextFor(Alice);
        Assert.Null(await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(target), default));
    }

    [Fact]
    public async Task Three_flats_owned_by_one_landlord_are_not_a_market()
    {
        // The old threshold counted rows, so this passed as evidence and published a median
        // that was simply Bob's middle rent.
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Bob, "Budapest", 1_100m);
        AddLetProperty(Bob, "Budapest", 1_200m);

        using var context = ContextFor(Alice);
        Assert.Null(await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default));
    }

    [Fact]
    public async Task Overlapping_tenancies_on_one_flat_are_still_one_comparable()
    {
        // Overlapping active leases are a known data-entry outcome. Counting them as
        // separate evidence let a single property clear the sample minimum by itself.
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        var bobProperty = AddLetProperty(Bob, "Budapest", 1_000m);
        AddOverlappingLease(Bob, bobProperty, 1_100m);
        AddOverlappingLease(Bob, bobProperty, 1_200m);

        using var context = ContextFor(Alice);
        Assert.Null(await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default));
    }

    [Fact]
    public async Task A_sold_property_is_not_evidence_of_the_current_market()
    {
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Carol, "Budapest", 1_100m);
        AddLetProperty(Dave, "Budapest", 1_200m, status: PropertyStatus.Sold);

        using var context = ContextFor(Alice);

        // A sold property whose open-ended lease was never closed would otherwise be read
        // as a live letting for ever.
        Assert.Null(await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default));
    }

    [Fact]
    public async Task City_matching_folds_case_beyond_ascii()
    {
        // SQLite's UPPER() leaves "ő" alone, so matching on the raw column put these in
        // separate markets. This app's own users are the ones with such city names.
        var aliceProperty = AddLetProperty(Alice, "Győr", 900m);
        AddLetProperty(Bob, "GYŐR", 1_000m);
        AddLetProperty(Carol, "győr", 1_100m);
        AddLetProperty(Dave, "Győr ", 1_200m);

        using var context = ContextFor(Alice);
        var estimate = await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty, city: "Győr"), default);

        Assert.NotNull(estimate);
        Assert.Equal(3, estimate!.SampleSize);
    }

    [Fact]
    public async Task An_unknown_bedroom_count_is_not_evidence_of_comparability()
    {
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 1_000m, bedrooms: null);
        AddLetProperty(Carol, "Budapest", 1_100m, bedrooms: null);
        AddLetProperty(Dave, "Budapest", 1_200m, bedrooms: null);

        using var context = ContextFor(Alice);

        // Letting null match every query pooled studios with houses — and made a decoy
        // match whatever was being valued.
        Assert.Null(await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default));
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
        // Three unrelated landlords, so only the city keeps them out of the sample.
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Vienna", 1_000m);
        AddLetProperty(Carol, "Vienna", 1_100m);
        AddLetProperty(Dave, "Vienna", 1_200m);

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
        AddLetProperty(Carol, "Budapest", 420_000m, currency: "HUF");
        AddLetProperty(Dave, "Budapest", 440_000m, currency: "HUF");

        using var context = ContextFor(Alice);
        Assert.Null(await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty), default));
    }

    [Fact]
    public async Task A_property_with_no_city_cannot_be_compared()
    {
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddThreePeerLettings();

        using var context = ContextFor(Alice);
        var estimate = await new PeerComparableRentProvider(context)
            .GetEstimateAsync(QueryFor(aliceProperty) with { City = null }, default);

        // A nationwide median would be a number rather than an answer.
        Assert.Null(estimate);
    }

    [Fact]
    public async Task Ended_tenancies_are_not_evidence_of_the_current_market()
    {
        // Three unrelated landlords, so only the ended tenancy takes the sample below the
        // threshold.
        var aliceProperty = AddLetProperty(Alice, "Budapest", 900m);
        AddLetProperty(Bob, "Budapest", 1_000m);
        AddLetProperty(Carol, "Budapest", 1_100m);
        var expiredProperty = AddLetProperty(Dave, "Budapest", 1_200m);

        using (var context = ContextFor(Dave))
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

/// <summary>
/// The ownership guard in <see cref="MoneyManagerDbContext.SaveChanges"/> is the other half
/// of the tenant boundary: peer comparables control what can be read across it, this
/// controls what can be written across it.
/// </summary>
public sealed class OwnershipGuardTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MoneyManagerDbContext> _options;

    private const int Alice = 1;
    private const int Bob = 2;

    public OwnershipGuardTests()
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

    private static RentalProperty NewProperty(int userId = 0) =>
        new() { UserId = userId, PropertyName = "Flat", Address = "1 Street", City = "Budapest" };

    [Fact]
    public void Inside_a_request_the_owner_comes_from_the_token_not_the_payload()
    {
        using var context = ContextFor(Alice);

        // A UserId that arrived in a request body, naming someone else.
        context.RentalProperties.Add(NewProperty(userId: Bob));
        context.SaveChanges();

        var stored = context.RentalProperties.IgnoreQueryFilters().Single();
        Assert.Equal(Alice, stored.UserId);
    }

    [Fact]
    public void An_existing_row_cannot_be_reassigned_to_another_user()
    {
        using (var context = ContextFor(Alice))
        {
            context.RentalProperties.Add(NewProperty());
            context.SaveChanges();
        }

        using (var context = ContextFor(Alice))
        {
            var property = context.RentalProperties.Single();
            property.UserId = Bob;
            context.SaveChanges();
        }

        using var reader = ContextFor(null);
        Assert.Equal(Alice, reader.RentalProperties.IgnoreQueryFilters().Single().UserId);
    }

    [Fact]
    public void Writing_outside_a_request_without_opening_the_scope_throws()
    {
        using var context = ContextFor(null);
        context.RentalProperties.Add(NewProperty(userId: Alice));

        // An owner alone is not permission. Before this, "no current user" was itself read
        // as permission, so any future path that lost its principal would have written
        // whatever owner the payload carried.
        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public void Background_work_may_assign_an_owner_inside_the_scope()
    {
        using var context = ContextFor(null);
        using (context.AllowExplicitOwnerAssignment())
        {
            context.RentalProperties.Add(NewProperty(userId: Alice));
            context.SaveChanges();
        }

        Assert.Equal(Alice, context.RentalProperties.IgnoreQueryFilters().Single().UserId);
    }

    [Fact]
    public void The_scope_closes_again_afterwards()
    {
        using var context = ContextFor(null);

        using (context.AllowExplicitOwnerAssignment())
        {
            context.RentalProperties.Add(NewProperty(userId: Alice));
            context.SaveChanges();
        }

        context.RentalProperties.Add(NewProperty(userId: Bob));
        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    [Fact]
    public void A_request_cannot_open_the_scope_at_all()
    {
        using var context = ContextFor(Alice);

        // The guard that keeps this from becoming a way to write any owner from inside a
        // request: there is a current user, so the window cannot be opened.
        Assert.Throws<InvalidOperationException>(() => context.AllowExplicitOwnerAssignment());
    }

    [Fact]
    public void Persisting_with_no_owner_and_no_user_still_throws()
    {
        using var context = ContextFor(null);
        using var scope = context.AllowExplicitOwnerAssignment();

        context.RentalProperties.Add(NewProperty());

        Assert.Throws<InvalidOperationException>(() => context.SaveChanges());
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StubCurrentUser(int? userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;
    }
}
