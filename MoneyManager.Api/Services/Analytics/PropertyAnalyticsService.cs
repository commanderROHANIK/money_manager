using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Analytics
{
    /// <summary>
    /// Assembles the calculator's input from stored entities. All aggregation happens in
    /// memory after materialising: SQLite has no native decimal type, so summing money
    /// columns in SQL loses precision.
    /// </summary>
    public sealed class PropertyAnalyticsService(MoneyManagerDbContext context)
    {
        public async Task<PropertyMetrics?> GetForPropertyAsync(int propertyId, DateTime? asOf = null)
        {
            var property = await context.RentalProperties
                .FirstOrDefaultAsync(p => p.Id == propertyId);

            return property is null ? null : await BuildAsync(property, asOf);
        }

        public async Task<IReadOnlyList<PropertyMetrics>> GetForAllAsync(DateTime? asOf = null)
        {
            var properties = await context.RentalProperties.ToListAsync();

            var results = new List<PropertyMetrics>(properties.Count);
            foreach (var property in properties)
            {
                results.Add(await BuildAsync(property, asOf));
            }

            return results;
        }

        private async Task<PropertyMetrics> BuildAsync(RentalProperty property, DateTime? asOf)
        {
            var effectiveAsOf = (asOf ?? DateTime.UtcNow).Date;

            var transactions = await context.PropertyTransactions
                .Where(t => t.RentalPropertyId == property.Id)
                .ToListAsync();

            var leases = await context.Leases
                .Where(l => l.RentalPropertyId == property.Id)
                .ToListAsync();

            var latestValuation = await context.PropertyValuations
                .Where(v => v.RentalPropertyId == property.Id && v.ValuedOn <= effectiveAsOf)
                .OrderByDescending(v => v.ValuedOn)
                .FirstOrDefaultAsync();

            var latestMarketRent = await context.RentPricePoints
                .Where(r => r.RentalPropertyId == property.Id
                            && r.Source == RentPriceSource.MarketEstimate
                            && r.EffectiveFrom <= effectiveAsOf)
                .OrderByDescending(r => r.EffectiveFrom)
                .FirstOrDefaultAsync();

            var mortgage = await context.Loans
                .Where(l => l.RentalPropertyId == property.Id && l.LoanType == LoanType.Mortgage)
                .OrderBy(l => l.Id)
                .FirstOrDefaultAsync();

            var activeLease = leases.FirstOrDefault(l => l.IsActiveOn(effectiveAsOf));

            var input = new PropertyAnalyticsInput
            {
                PropertyId = property.Id,
                PropertyName = property.PropertyName,
                CurrencyCode = property.CurrencyCode,
                PurchasePrice = property.PurchasePrice,
                PurchaseDate = property.PurchaseDate,
                CurrentValuation = latestValuation?.Value,
                ValuationDate = latestValuation?.ValuedOn,
                Status = property.Status,
                SalePrice = property.SalePrice,
                SaleDate = property.SaleDate,
                ActiveMonthlyRent = activeLease?.MonthlyRent,
                MarketMonthlyRent = latestMarketRent?.Amount,
                MortgageOriginalAmount = mortgage?.LoanAmount,
                MortgageBalance = mortgage?.RemainingBalance,
                MortgageMonthlyPayment = mortgage?.MonthlyPayment,
                Transactions = transactions
                    .Select(t => new LedgerEntry(t.Date, t.Amount, t.Category))
                    .ToList(),
                Occupancy = leases
                    .Select(l => new OccupancyWindow(l.StartDate, l.EndDate))
                    .ToList(),
                AsOf = effectiveAsOf,
            };

            return PropertyAnalyticsCalculator.Compute(input);
        }
    }
}
