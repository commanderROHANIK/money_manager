using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.MarketRent
{
    /// <summary>
    /// Estimates market rent from comparable let properties across the whole userbase.
    ///
    /// This is the answer to "no reliable free rental-price API exists for most markets":
    /// the data is already here, it costs nothing, it works in any city, and it gets better
    /// as more landlords sign up.
    ///
    /// It is the one place in the codebase that deliberately reads across the tenant
    /// boundary. Four rules make that safe, and all four are covered by tests:
    ///   1. Only aggregates leave this class — a median, a range and a count. No address,
    ///      no name, no id, no row.
    ///   2. Nothing is returned below <see cref="PeerComparableStatistics.MinimumSampleSize"/>
    ///      comparables drawn from at least
    ///      <see cref="PeerComparableStatistics.MinimumDistinctOwners"/> different landlords.
    ///   3. The caller's own properties are never evidence. Without this the sample is
    ///      attacker-controlled: anyone able to add properties could surround a single
    ///      neighbour with decoy rents and read that neighbour's exact figure back out of
    ///      the median.
    ///   4. One property contributes one data point however many leases it carries, so a
    ///      single flat with overlapping tenancies cannot satisfy the minimum on its own.
    /// </summary>
    public sealed class PeerComparableRentProvider(MoneyManagerDbContext context) : IMarketRentProvider
    {
        public const string ProviderKey = "peer-comparables";

        public string Key => ProviderKey;

        public int Priority => 100;

        public async Task<MarketRentEstimate?> GetEstimateAsync(MarketRentQuery query, CancellationToken ct)
        {
            // Without a city there is no market to compare against; a nationwide median
            // would be a number rather than an answer.
            var city = RentalProperty.NormalizeCity(query.City);
            if (city is null)
                return null;

            var country = query.CountryCode?.Trim().ToUpperInvariant();
            var today = DateTime.UtcNow.Date;

            // IgnoreQueryFilters is deliberate and load-bearing here — see the class remarks.
            // The projection is the security boundary: it selects the numbers the maths needs
            // plus two ids that are used for grouping and then discarded, never returned.
            var rows = await context.Leases
                .IgnoreQueryFilters()
                .Where(lease =>
                    lease.StartDate <= today
                    && (lease.EndDate == null || lease.EndDate >= today)
                    && lease.MonthlyRent > 0
                    && lease.RentalPropertyId != query.ExcludePropertyId)
                .Join(
                    context.RentalProperties.IgnoreQueryFilters(),
                    lease => lease.RentalPropertyId,
                    property => property.Id,
                    (lease, property) => new { lease, property })
                .Where(x =>
                    // Rule 3: the caller's own portfolio is not evidence about the market.
                    x.property.UserId != query.ExcludeUserId
                    // A sold or archived property is not a current letting.
                    && x.property.Status == PropertyStatus.Active
                    // Matched on the stored normalised value: SQLite's UPPER() is ASCII-only,
                    // so comparing on City itself would put "Győr" and "GYŐR" in different
                    // markets — the common case here rather than an edge case.
                    && x.property.NormalizedCity == city
                    && x.property.PropertyType == query.PropertyType
                    // Rents in different currencies cannot share a median.
                    && x.property.CurrencyCode == query.CurrencyCode
                    // Same-named cities exist in different countries; pooling them would
                    // produce one median over two unrelated markets.
                    && (country == null
                        || x.property.CountryCode == null
                        || x.property.CountryCode.ToUpper() == country)
                    // An unknown bedroom count is not evidence that the property is
                    // comparable. Letting null match everything pooled studios with houses,
                    // and made decoys match every query.
                    && (query.Bedrooms == null
                        || (x.property.Bedrooms != null
                            && x.property.Bedrooms >= query.Bedrooms - 1
                            && x.property.Bedrooms <= query.Bedrooms + 1)))
                .Select(x => new
                {
                    x.property.UserId,
                    PropertyId = x.property.Id,
                    x.lease.MonthlyRent,
                    x.property.SizeSqm,
                    x.lease.StartDate,
                })
                .ToListAsync(ct);

            // Rule 4. Overlapping active leases on one property are a known outcome — a
            // data-entry error, or an intentional handover — and counting them separately
            // would let a single flat clear the sample minimum by itself. The newest
            // tenancy is the one that describes the current market.
            var comparables = rows
                .GroupBy(row => row.PropertyId)
                .Select(group => group.OrderByDescending(row => row.StartDate).First())
                .Select(row => new RentComparable(row.UserId, row.MonthlyRent, row.SizeSqm))
                .ToList();

            var estimate = PeerComparableStatistics.Estimate(comparables, query.SizeSqm);

            if (estimate is null)
                return null;

            return new MarketRentEstimate
            {
                Monthly = estimate.Monthly,
                CurrencyCode = query.CurrencyCode,
                ProviderKey = Key,
                Confidence = estimate.Confidence,
                Low = estimate.Low,
                High = estimate.High,
                SampleSize = estimate.SampleSize,
                Notes = estimate.PerSquareMetre
                    ? $"Median of {estimate.SampleSize} comparable lettings in {query.City}, by floor area."
                    : $"Median of {estimate.SampleSize} comparable lettings in {query.City}.",
            };
        }
    }
}
