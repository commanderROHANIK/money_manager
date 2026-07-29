using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Analytics;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RentalPropertiesController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly PropertyAnalyticsService _analytics;

        public RentalPropertiesController(MoneyManagerDbContext context, PropertyAnalyticsService analytics)
        {
            _context = context;
            _analytics = analytics;
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
        /// Per-property metrics for the whole portfolio, plus totals. Totals are only summed
        /// across properties sharing a currency; mixed portfolios report which currencies
        /// are present rather than adding unlike amounts together.
        /// </summary>
        [HttpGet("analytics/portfolio")]
        public async Task<ActionResult<PortfolioAnalyticsDto>> GetPortfolioAnalytics([FromQuery] DateTime? asOf = null)
        {
            var all = await _analytics.GetForAllAsync(asOf);
            return Ok(PortfolioAnalyticsDto.From(all));
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
        string? Currency,
        bool MixedCurrency,
        decimal? TotalInvested,
        decimal? TotalCurrentValue,
        decimal? TotalEquity,
        decimal? TotalMonthlyCashFlow,
        decimal? TotalAnnualRentUplift,
        decimal? PortfolioRoi)
    {
        public static PortfolioAnalyticsDto From(IReadOnlyList<PropertyMetrics> metrics)
        {
            var currencies = metrics.Select(m => m.CurrencyCode).Distinct().ToList();
            var mixed = currencies.Count > 1;

            // Adding amounts in different currencies would produce a confident nonsense
            // number. Until exchange rates land, say so instead.
            if (mixed || metrics.Count == 0)
            {
                return new PortfolioAnalyticsDto(
                    metrics, metrics.Count, mixed ? null : currencies.FirstOrDefault(),
                    mixed, null, null, null, null, null, null);
            }

            var invested = SumOrNull(metrics.Select(m => m.CashInvested));
            var equity = SumOrNull(metrics.Select(m => m.Equity));
            var cashFlow = SumOrNull(metrics.Select(m => m.MonthlyCashFlow));
            var netCashFlow = SumOrNull(metrics.Select(m => m.CumulativeNetCashFlow)) ?? 0m;

            decimal? roi = invested is > 0 && equity is not null
                ? Math.Round((equity.Value + netCashFlow - invested.Value) / invested.Value, 4)
                : null;

            return new PortfolioAnalyticsDto(
                metrics,
                metrics.Count,
                currencies.FirstOrDefault(),
                false,
                invested,
                SumOrNull(metrics.Select(m => m.CurrentValue)),
                equity,
                cashFlow,
                SumOrNull(metrics.Select(m => m.AnnualRentUplift)),
                roi);
        }

        private static decimal? SumOrNull(IEnumerable<decimal?> values)
        {
            var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
            return present.Count == 0 ? null : Math.Round(present.Sum(), 2);
        }
    }
}
