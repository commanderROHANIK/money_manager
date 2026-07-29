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
    /// boundary. Two rules make that safe, and both are covered by tests:
    ///   1. Only aggregates leave this class — a median, a range and a count. No address,
    ///      no name, no id, no row.
    ///   2. Nothing is returned below <see cref="PeerComparableStatistics.MinimumSampleSize"/>,
    ///      so an estimate can never be a restatement of one neighbour's rent.
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
            if (string.IsNullOrWhiteSpace(query.City))
                return null;

            var city = query.City.Trim().ToUpperInvariant();
            var today = DateTime.UtcNow.Date;

            // IgnoreQueryFilters is deliberate and load-bearing here — see the class remarks.
            // The projection is the security boundary: it selects two numbers and nothing else.
            var comparables = await context.Leases
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
                    x.property.City != null
                    && x.property.City.ToUpper() == city
                    && x.property.PropertyType == query.PropertyType
                    // Rents in different currencies cannot share a median.
                    && x.property.CurrencyCode == query.CurrencyCode
                    && (query.Bedrooms == null
                        || x.property.Bedrooms == null
                        || (x.property.Bedrooms >= query.Bedrooms - 1
                            && x.property.Bedrooms <= query.Bedrooms + 1)))
                .Select(x => new { x.lease.MonthlyRent, x.property.SizeSqm })
                .ToListAsync(ct);

            var estimate = PeerComparableStatistics.Estimate(
                comparables.Select(c => new RentComparable(c.MonthlyRent, c.SizeSqm)).ToList(),
                query.SizeSqm);

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
