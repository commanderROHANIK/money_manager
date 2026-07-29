using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Analytics;
using MoneyManager.Api.Services.Currency;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RentalPropertiesController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly PropertyAnalyticsService _analytics;
        private readonly ExchangeRateService _exchangeRates;
        private readonly ICurrentUser _currentUser;

        public RentalPropertiesController(
            MoneyManagerDbContext context,
            PropertyAnalyticsService analytics,
            ExchangeRateService exchangeRates,
            ICurrentUser currentUser)
        {
            _context = context;
            _analytics = analytics;
            _exchangeRates = exchangeRates;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RentalPropertyDto>>> GetAll()
        {
            var properties = await _context.RentalProperties.ToListAsync();
            var ids = properties.Select(p => p.Id).ToList();

            var leases = await _context.Leases
                .Where(l => ids.Contains(l.RentalPropertyId))
                .ToListAsync();

            var today = DateTime.UtcNow.Date;

            return properties
                .Select(p => RentalPropertyDto.From(p, ActiveLeaseFor(leases, p.Id, today)))
                .ToList();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RentalPropertyDto>> GetById(int id)
        {
            var property = await _context.RentalProperties.FirstOrDefaultAsync(p => p.Id == id);
            if (property == null)
                return NotFound();

            var today = DateTime.UtcNow.Date;
            var leases = await _context.Leases.Where(l => l.RentalPropertyId == id).ToListAsync();

            return RentalPropertyDto.From(property, ActiveLeaseFor(leases, id, today));
        }

        /// <summary>Investment performance for one property.</summary>
        [HttpGet("{id}/analytics")]
        public async Task<ActionResult<PropertyMetrics>> GetAnalytics(int id, [FromQuery] DateTime? asOf = null)
        {
            var metrics = await _analytics.GetForPropertyAsync(id, asOf);
            return metrics is null ? NotFound() : Ok(metrics);
        }

        /// <summary>
        /// Per-property metrics for the whole portfolio, plus totals in the user's base
        /// currency. Where a rate is missing for one of the currencies held, totals are
        /// withheld and the currencies are named — a total covering only the properties
        /// there happen to be rates for would read as a portfolio total without being one.
        /// </summary>
        [HttpGet("analytics/portfolio")]
        public async Task<ActionResult<PortfolioAnalyticsDto>> GetPortfolioAnalytics([FromQuery] DateTime? asOf = null)
        {
            var all = await _analytics.GetForAllAsync(asOf);
            var converter = await _exchangeRates.GetConverterAsync(HttpContext.RequestAborted);

            var baseCurrency = await _context.Users
                .Where(u => u.Id == _currentUser.UserId)
                .Select(u => u.BaseCurrency)
                .FirstOrDefaultAsync() ?? "EUR";

            return Ok(PortfolioAnalyticsDto.From(all, converter, baseCurrency));
        }

        [HttpPost]
        public async Task<ActionResult<RentalPropertyDto>> Create([FromBody] RentalPropertyRequest request)
        {
            var property = new RentalProperty();
            Apply(request, property);

            _context.RentalProperties.Add(property);
            await _context.SaveChangesAsync();

            // The purchase is the first thing that happened to this asset; recording it here
            // means the timeline is never empty for a property that has one.
            if (property.PurchaseDate is { } purchased)
            {
                _context.PropertyEvents.Add(new PropertyEvent
                {
                    RentalPropertyId = property.Id,
                    OccurredOn = purchased,
                    Type = PropertyEventType.Purchase,
                    Title = "Property purchased",
                    Description = property.PurchasePrice is { } price
                        ? $"Purchased for {price:N0} {property.CurrencyCode}"
                        : null,
                    IsSystemGenerated = true,
                });

                if (property.PurchasePrice is { } purchasePrice)
                {
                    _context.PropertyValuations.Add(new PropertyValuation
                    {
                        RentalPropertyId = property.Id,
                        ValuedOn = purchased,
                        Value = purchasePrice,
                        CurrencyCode = property.CurrencyCode,
                        Source = ValuationSource.PurchasePrice,
                    });
                }

                await _context.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetById), new { id = property.Id },
                RentalPropertyDto.From(property, null));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] RentalPropertyRequest request)
        {
            var property = await _context.RentalProperties.FirstOrDefaultAsync(p => p.Id == id);
            if (property == null)
                return NotFound();

            Apply(request, property);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var property = await _context.RentalProperties.FirstOrDefaultAsync(p => p.Id == id);
            if (property == null)
                return NotFound();

            _context.RentalProperties.Remove(property);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Ordered by most recent start date so that overlapping leases (a data-entry error,
        // or an intentional handover) resolve to the newest tenancy rather than an arbitrary
        // one determined by insertion order.
        private static Lease? ActiveLeaseFor(List<Lease> leases, int propertyId, DateTime on) =>
            leases
                .Where(l => l.RentalPropertyId == propertyId && l.IsActiveOn(on))
                .OrderByDescending(l => l.StartDate)
                .FirstOrDefault();

        private static void Apply(RentalPropertyRequest request, RentalProperty property)
        {
            property.PropertyName = request.PropertyName;
            property.Address = request.Address;
            property.City = request.City;
            property.PostalCode = request.PostalCode;
            property.CountryCode = request.CountryCode?.ToUpperInvariant();
            property.PropertyType = request.PropertyType;
            property.SizeSqm = request.SizeSqm;
            property.Bedrooms = request.Bedrooms;
            property.PurchasePrice = request.PurchasePrice;
            property.PurchaseDate = request.PurchaseDate;
            property.Status = request.Status;
            property.SalePrice = request.SalePrice;
            property.SaleDate = request.SaleDate;
            property.Notes = request.Notes;
            property.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? property.CurrencyCode
                : request.CurrencyCode.ToUpperInvariant();
        }
    }

    public record RentalPropertyRequest(
        string PropertyName,
        string Address,
        string? City = null,
        string? PostalCode = null,
        string? CountryCode = null,
        PropertyType PropertyType = PropertyType.Apartment,
        decimal? SizeSqm = null,
        int? Bedrooms = null,
        decimal? PurchasePrice = null,
        DateTime? PurchaseDate = null,
        PropertyStatus Status = PropertyStatus.Active,
        decimal? SalePrice = null,
        DateTime? SaleDate = null,
        string? Notes = null,
        string? CurrencyCode = null);

    /// <summary>
    /// Occupancy and current rent are derived from the active tenancy rather than stored on
    /// the property, so they cannot drift out of date. They keep their original field names
    /// so the existing widgets continue to work against the richer model.
    /// </summary>
    public record RentalPropertyDto(
        int Id,
        string PropertyName,
        string Address,
        string? City,
        string? PostalCode,
        string? CountryCode,
        PropertyType PropertyType,
        decimal? SizeSqm,
        int? Bedrooms,
        decimal? PurchasePrice,
        DateTime? PurchaseDate,
        PropertyStatus Status,
        decimal? SalePrice,
        DateTime? SaleDate,
        string? Notes,
        string CurrencyCode,
        bool IsRented,
        decimal RentAmount,
        DateTime? RentDueDate,
        string? TenantName)
    {
        public static RentalPropertyDto From(RentalProperty p, Lease? activeLease) => new(
            p.Id,
            p.PropertyName,
            p.Address,
            p.City,
            p.PostalCode,
            p.CountryCode,
            p.PropertyType,
            p.SizeSqm,
            p.Bedrooms,
            p.PurchasePrice,
            p.PurchaseDate,
            p.Status,
            p.SalePrice,
            p.SaleDate,
            p.Notes,
            p.CurrencyCode,
            activeLease is not null,
            activeLease?.MonthlyRent ?? 0m,
            activeLease is null ? null : NextRentDue(activeLease),
            activeLease?.TenantName);

        private static DateTime NextRentDue(Lease lease)
        {
            var today = DateTime.UtcNow.Date;
            var day = Math.Clamp(lease.RentDueDayOfMonth, 1, 28);

            var candidate = new DateTime(today.Year, today.Month, day);
            if (candidate < today)
                candidate = candidate.AddMonths(1);

            return candidate;
        }
    }

    public record PortfolioAnalyticsDto(
        IReadOnlyList<PropertyMetrics> Properties,
        int PropertyCount,
        string Currency,
        bool MixedCurrency,
        DateOnly? FxAsOf,
        IReadOnlyList<string> UnconvertedCurrencies,
        decimal? TotalInvested,
        decimal? TotalCurrentValue,
        decimal? TotalEquity,
        decimal? TotalMonthlyCashFlow,
        decimal? TotalAnnualRentUplift,
        decimal? PortfolioRoi)
    {
        public static PortfolioAnalyticsDto From(
            IReadOnlyList<PropertyMetrics> metrics,
            CurrencyConverter converter,
            string baseCurrency)
        {
            var currencies = metrics.Select(m => m.CurrencyCode).Distinct().ToList();
            var mixed = currencies.Count > 1;

            if (metrics.Count == 0)
            {
                return new PortfolioAnalyticsDto(
                    metrics, 0, baseCurrency, false, null, [],
                    null, null, null, null, null, null);
            }

            // Resolve one factor per currency up front, so every figure in a total is
            // converted at the same rate rather than each being looked up independently.
            var factors = new Dictionary<string, decimal>();
            var unconverted = new List<string>();
            DateOnly? fxAsOf = null;

            foreach (var currency in currencies)
            {
                if (converter.Convert(1m, currency, baseCurrency) is not { } converted)
                {
                    unconverted.Add(currency);
                    continue;
                }

                factors[currency] = converted.Amount;

                // A total is only as current as the stalest rate that went into it.
                if (converted.RateAsOf is { } asOf && (fxAsOf is null || asOf < fxAsOf))
                    fxAsOf = asOf;
            }

            // A total covering only the properties we happen to have rates for reads as a
            // portfolio total but is not one. Withhold it and name what is missing instead.
            if (unconverted.Count > 0)
            {
                return new PortfolioAnalyticsDto(
                    metrics, metrics.Count, baseCurrency, mixed, fxAsOf, unconverted,
                    null, null, null, null, null, null);
            }

            var invested = SumConverted(metrics, m => m.CashInvested, factors);
            var equity = SumConverted(metrics, m => m.Equity, factors);
            var netCashFlow = SumConverted(metrics, m => m.CumulativeNetCashFlow, factors) ?? 0m;

            decimal? roi = invested is > 0 && equity is not null
                ? Math.Round((equity.Value + netCashFlow - invested.Value) / invested.Value, 4)
                : null;

            return new PortfolioAnalyticsDto(
                metrics,
                metrics.Count,
                baseCurrency,
                mixed,
                fxAsOf,
                [],
                invested,
                SumConverted(metrics, m => m.CurrentValue, factors),
                equity,
                SumConverted(metrics, m => m.MonthlyCashFlow, factors),
                SumConverted(metrics, m => m.AnnualRentUplift, factors),
                roi);
        }

        private static decimal? SumConverted(
            IEnumerable<PropertyMetrics> metrics,
            Func<PropertyMetrics, decimal?> select,
            IReadOnlyDictionary<string, decimal> factors)
        {
            decimal total = 0m;
            var any = false;

            foreach (var metric in metrics)
            {
                if (select(metric) is not { } value)
                    continue;

                total += value * factors[metric.CurrencyCode];
                any = true;
            }

            return any ? Math.Round(total, 2) : null;
        }
    }
}
