using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure.Validation;
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
        private readonly CurrencyRollupService _rollups;

        public RentalPropertiesController(
            MoneyManagerDbContext context,
            PropertyAnalyticsService analytics,
            CurrencyRollupService rollups)
        {
            _context = context;
            _analytics = analytics;
            _rollups = rollups;
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
        /// Per-property metrics for the whole portfolio, plus totals. A portfolio spanning
        /// currencies is totalled by converting each property into the owner's base currency at
        /// the rates they have entered; a pair with no rate leaves the affected totals null and
        /// says which rate is missing, rather than adding unlike amounts together.
        ///
        /// <para>
        /// Conversion happens here, at the rollup, and nowhere else. The per-property metrics in
        /// <c>Properties</c> are passed through exactly as the calculator produced them, in each
        /// property's own currency — adding a rate never changes a single property's figures.
        /// </para>
        /// </summary>
        [HttpGet("analytics/portfolio")]
        public async Task<ActionResult<PortfolioAnalyticsDto>> GetPortfolioAnalytics([FromQuery] DateTime? asOf = null)
        {
            var all = await _analytics.GetForAllAsync(asOf);
            var rollup = await _rollups.LoadAsync();

            return Ok(PortfolioAnalyticsDto.From(all, rollup));
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
        [Required, MaxLength(160)] string PropertyName,
        [Required, MaxLength(300)] string Address,
        [MaxLength(120)] string? City = null,
        [MaxLength(20)] string? PostalCode = null,
        [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Country code must be two letters.")]
        string? CountryCode = null,
        PropertyType PropertyType = PropertyType.Apartment,
        [NonNegative] decimal? SizeSqm = null,
        [NonNegative] int? Bedrooms = null,
        [NonNegative] decimal? PurchasePrice = null,
        DateTime? PurchaseDate = null,
        PropertyStatus Status = PropertyStatus.Active,
        [NonNegative] decimal? SalePrice = null,
        DateTime? SaleDate = null,
        [MaxLength(2000)] string? Notes = null,
        [SupportedCurrency] string? CurrencyCode = null) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // A sale before the purchase inverts the holding period, which is the denominator of
            // every annualised return on this property.
            if (SaleDate is { } sold && PurchaseDate is { } bought && sold < bought)
            {
                yield return new ValidationResult(
                    "A property cannot be sold before it was purchased.", [nameof(SaleDate)]);
            }
        }
    }

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

    /// <summary>
    /// Portfolio totals and how they were arrived at.
    ///
    /// <para>
    /// <c>Currency</c> always names the unit the <c>Total*</c> figures are expressed in — the
    /// shared currency when the portfolio has one, the owner's base currency when the figures had
    /// to be converted to exist at all. There is deliberately no second, parallel set of
    /// "converted" totals: one number per metric, and one field saying what unit it is in, is
    /// what stops a caller rendering EUR figures under a HUF label.
    /// </para>
    /// <para>
    /// <c>MixedCurrency</c> says the portfolio spans currencies; <c>Converted</c> says a rate was
    /// applied to produce these totals. They are not the same question, and the UI needs both:
    /// the first explains why conversion was necessary, the second is what has to be shown to the
    /// user next to the number.
    /// </para>
    /// </summary>
    public record PortfolioAnalyticsDto(
        IReadOnlyList<PropertyMetrics> Properties,
        int PropertyCount,
        string? Currency,
        bool MixedCurrency,
        decimal? TotalInvested,
        decimal? TotalCurrentValue,
        decimal? TotalEquity,
        decimal? TotalMonthlyRent,
        decimal? TotalMonthlyCashFlow,
        // Underpriced properties only — a property already let above market contributes nothing
        // here, never a negative offset. See the comment above where this is assembled, in From.
        decimal? TotalAnnualRentUplift,
        decimal? PortfolioRoi,
        string BaseCurrency,
        bool Converted,
        IReadOnlyList<CurrencyPair> MissingRates,
        IReadOnlyList<AppliedRate> AppliedRates,
        IReadOnlyList<MetricWarning> Warnings)
    {
        public static PortfolioAnalyticsDto From(IReadOnlyList<PropertyMetrics> metrics, RollupContext rollup)
        {
            var currencies = metrics.Select(m => m.CurrencyCode).Distinct(StringComparer.Ordinal).ToList();

            if (metrics.Count == 0)
            {
                return new PortfolioAnalyticsDto(
                    metrics, 0, null, false, null, null, null, null, null, null, null,
                    rollup.BaseCurrency, false, [], [], []);
            }

            var target = rollup.ResolveTarget(currencies);
            var converted = currencies.Any(c => !string.Equals(c, target, StringComparison.OrdinalIgnoreCase));

            var missingRates = CurrencyRollup.MissingRates(currencies, rollup.Rates, target);

            var invested = Total(metrics, m => m.CashInvested, rollup, target);
            var equity = Total(metrics, m => m.Equity, rollup, target);
            var netCashFlow = Total(metrics, m => m.CumulativeNetCashFlow, rollup, target);

            // Underpriced properties only, not a net across the whole portfolio: a property
            // already let above market contributes a negative AnnualRentUplift, and folding that
            // in would answer "how much extra rent, net of the ones doing better than market" —
            // a different, less useful question than the one this figure names, and one that
            // could shrink or hide the opportunity the underpriced-properties widget exists to
            // surface. Matches that widget's own filter (rentGapPercent/annualRentUplift > 0)
            // exactly, so the headline total always agrees with the list under it.
            var underpricedRentUplift = Total(
                metrics.Where(m => (m.AnnualRentUplift ?? 0m) > 0m).ToList(),
                m => m.AnnualRentUplift,
                rollup,
                target);

            // ROI is a ratio, so it is recomputed from converted components rather than being
            // converted itself — multiplying a percentage by an exchange rate is nonsense. A
            // blocked leg makes the whole ratio unknowable: the pre-existing `?? 0m` below is
            // only safe for a portfolio that genuinely recorded no cash flow, never for one whose
            // cash flow could not be expressed in this currency.
            decimal? roi = !netCashFlow.Blocked && invested.Amount is > 0 && equity.Amount is { } equityAmount
                ? Math.Round(
                    (equityAmount + (netCashFlow.Amount ?? 0m) - invested.Amount.Value) / invested.Amount.Value,
                    4)
                : null;

            var warnings = new List<MetricWarning>();
            if (missingRates.Count > 0)
                warnings.Add(CurrencyRollup.MissingRateWarning(missingRates));

            return new PortfolioAnalyticsDto(
                metrics,
                metrics.Count,
                target,
                currencies.Count > 1,
                invested.Amount,
                Total(metrics, m => m.CurrentValue, rollup, target).Amount,
                equity.Amount,
                Total(metrics, m => m.ContractedMonthlyRent, rollup, target).Amount,
                Total(metrics, m => m.MonthlyCashFlow, rollup, target).Amount,
                underpricedRentUplift.Amount,
                roi,
                rollup.BaseCurrency,
                converted,
                missingRates,
                CurrencyRollup.AppliedRates(currencies, rollup.Rates, target),
                warnings);
        }

        private static RollupTotal Total(
            IReadOnlyList<PropertyMetrics> metrics,
            Func<PropertyMetrics, decimal?> select,
            RollupContext rollup,
            string target) =>
            CurrencyRollup.Sum(
                metrics.Select(m => (select(m), m.CurrencyCode)),
                rollup.Rates,
                target);
    }
}
