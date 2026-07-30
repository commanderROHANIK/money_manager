using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.MarketRent
{
    /// <summary>
    /// Asks each registered provider in priority order and records the answer.
    ///
    /// The composite is the seam that makes a paid data source a drop-in later: adding an
    /// HTTP-backed provider with a lower Priority puts it in front of peer comparables
    /// without any caller changing.
    /// </summary>
    public sealed class MarketRentService(
        MoneyManagerDbContext context,
        IEnumerable<IMarketRentProvider> providers,
        ILogger<MarketRentService> logger)
    {
        public async Task<MarketRentEstimate?> EstimateAsync(RentalProperty property, CancellationToken ct)
        {
            var query = new MarketRentQuery
            {
                City = property.City,
                CountryCode = property.CountryCode,
                PropertyType = property.PropertyType,
                CurrencyCode = property.CurrencyCode,
                SizeSqm = property.SizeSqm,
                Bedrooms = property.Bedrooms,
                ExcludePropertyId = property.Id,
                ExcludeUserId = property.UserId,
            };

            foreach (var provider in providers.OrderBy(p => p.Priority))
            {
                try
                {
                    if (await provider.GetEstimateAsync(query, ct) is { } estimate)
                        return estimate;
                }
                catch (Exception ex)
                {
                    // One unreachable provider must not stop the others from answering.
                    logger.LogWarning(ex,
                        "Market rent provider {Provider} failed for property {PropertyId}.",
                        provider.Key, property.Id);
                }
            }

            return null;
        }

        /// <summary>
        /// Estimates and records a market rent for one property, returning the stored point.
        /// Null means no provider had anything trustworthy to say, which is a normal outcome
        /// for a market where this instance has little data.
        /// </summary>
        public async Task<RentPricePoint?> RefreshAsync(RentalProperty property, CancellationToken ct)
        {
            var estimate = await EstimateAsync(property, ct);
            if (estimate is null)
                return null;

            var effectiveFrom = DateTime.UtcNow.Date;

            // One estimate per property per provider per day, updated in place. Appending
            // unconditionally made the endpoint an unbounded writer: a caller could grow the
            // table without limit just by re-requesting a refresh, and the history filled
            // with same-day duplicates that say the same thing.
            var point = await context.RentPricePoints
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    r => r.RentalPropertyId == property.Id
                         && r.Source == RentPriceSource.MarketEstimate
                         && r.ProviderKey == estimate.ProviderKey
                         && r.EffectiveFrom == effectiveFrom,
                    ct);

            if (point is null)
            {
                point = new RentPricePoint
                {
                    UserId = property.UserId,
                    RentalPropertyId = property.Id,
                    EffectiveFrom = effectiveFrom,
                    Source = RentPriceSource.MarketEstimate,
                    ProviderKey = estimate.ProviderKey,
                };

                context.RentPricePoints.Add(point);
            }

            point.Amount = estimate.Monthly;
            point.CurrencyCode = estimate.CurrencyCode;
            point.Notes = estimate.Notes;

            await context.SaveChangesAsync(ct);

            return point;
        }

        /// <summary>
        /// Properties whose newest market estimate is older than <paramref name="maxAge"/>,
        /// or which have never had one.
        /// </summary>
        public async Task<List<RentalProperty>> FindStaleAsync(TimeSpan maxAge, CancellationToken ct)
        {
            var cutoff = DateTime.UtcNow.Date - maxAge;

            // Runs from a background job with no request user, so the tenant filter would
            // otherwise match nothing at all and the job would silently do no work.
            //
            // AsNoTracking is load-bearing rather than an optimisation: this returns rows
            // belonging to every user, and tracking them would leave other tenants' entities
            // in the change tracker of whatever context called this. The ownership guard in
            // SaveChanges pins UserId but does not stop their other columns being written.
            return await context.RentalProperties
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.Status == PropertyStatus.Active)
                .Where(p => !context.RentPricePoints
                    .IgnoreQueryFilters()
                    .Any(r => r.RentalPropertyId == p.Id
                              && r.Source == RentPriceSource.MarketEstimate
                              && r.EffectiveFrom >= cutoff))
                .ToListAsync(ct);
        }
    }
}
