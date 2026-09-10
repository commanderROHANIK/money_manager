using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Infrastructure.Validation;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Analytics;
using MoneyManager.Api.Services.Currency;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Authorize]
    [FeatureGate(Feature.Stocks)]
    [Route("api/[controller]")]
    public class StocksController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly CurrencyRollupService _rollups;

        public StocksController(MoneyManagerDbContext context, CurrencyRollupService rollups)
        {
            _context = context;
            _rollups = rollups;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Stock>>> GetAll()
        {
            return await _context.Stocks.ToListAsync();
        }

        /// <summary>
        /// Total current value held across every holding, plus the per-currency breakdown it was
        /// built from — the same shape as <c>BankAccountsController.GetTotalBalance</c>, for the
        /// same reason: <c>SharesOwned * CurrentPrice</c> summed across holdings while ignoring
        /// <c>CurrencyCode</c> is a confident nonsense number the moment a portfolio holds stock
        /// priced in more than one currency.
        /// </summary>
        [HttpGet("summary/total-value")]
        public async Task<ActionResult<StockValueSummaryDto>> GetTotalValue()
        {
            // Materialized before summing on purpose: SQLite has no native decimal type, so
            // aggregating decimals in SQL either fails or loses precision.
            var stocks = await _context.Stocks.ToListAsync();
            var rollup = await _rollups.LoadAsync();

            return StockValueSummaryDto.From(stocks, rollup);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Stock>> GetById(int id)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id);
            if (stock == null)
                return NotFound();
            return stock;
        }

        [HttpPost]
        public async Task<ActionResult<Stock>> Create([FromBody] StockRequest request)
        {
            var stock = new Stock();
            Apply(request, stock);

            _context.Stocks.Add(stock);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = stock.Id }, stock);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] StockRequest request)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id);
            if (stock == null)
                return NotFound();

            Apply(request, stock);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id);
            if (stock == null)
                return NotFound();

            _context.Stocks.Remove(stock);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static void Apply(StockRequest request, Stock stock)
        {
            stock.Ticker = request.Ticker.ToUpperInvariant();
            stock.SharesOwned = request.SharesOwned;
            stock.PurchasePrice = request.PurchasePrice;
            stock.CurrentPrice = request.CurrentPrice;
            stock.PurchaseDate = request.PurchaseDate;
            stock.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? stock.CurrencyCode
                : request.CurrencyCode.ToUpperInvariant();
        }
    }

    public record StockRequest(
        [Required, MaxLength(16)] string Ticker,
        [NonNegative] int SharesOwned,
        [NonNegative] decimal PurchasePrice,
        [NonNegative] decimal CurrentPrice,
        DateTime PurchaseDate,
        [SupportedCurrency] string? CurrencyCode = null);

    /// <summary>
    /// <c>Currency</c> names the unit <c>TotalValue</c> is in, and is never a guess: when the
    /// holdings share a currency it is that one, and when they do not it is the owner's base
    /// currency, which is also the only case where a rate is applied.
    ///
    /// <para>
    /// <c>ByCurrency</c> is the part that is always true. If a rate is missing the headline total
    /// is null rather than approximate, and the breakdown still tells the user exactly what they
    /// hold.
    /// </para>
    /// </summary>
    public record StockValueSummaryDto(
        decimal? TotalValue,
        string Currency,
        bool MixedCurrency,
        bool Converted,
        string BaseCurrency,
        IReadOnlyList<CurrencyTotal> ByCurrency,
        IReadOnlyList<CurrencyPair> MissingRates,
        IReadOnlyList<AppliedRate> AppliedRates,
        IReadOnlyList<MetricWarning> Warnings)
    {
        public static StockValueSummaryDto From(IReadOnlyList<Stock> stocks, RollupContext rollup)
        {
            var byCurrency = stocks
                .GroupBy(s => s.CurrencyCode.Trim().ToUpperInvariant(), StringComparer.Ordinal)
                .Select(g => new CurrencyTotal(g.Key, Math.Round(g.Sum(s => s.SharesOwned * s.CurrentPrice), 2)))
                .OrderBy(t => t.CurrencyCode, StringComparer.Ordinal)
                .ToList();

            // No holdings is not the same shape of unknown as a missing rate: nothing held is
            // genuinely zero, and reporting it as such needs no rate at all.
            if (byCurrency.Count == 0)
            {
                return new StockValueSummaryDto(
                    0m, rollup.BaseCurrency, false, false, rollup.BaseCurrency, byCurrency, [], [], []);
            }

            var currencies = byCurrency.Select(t => t.CurrencyCode).ToList();
            var target = rollup.ResolveTarget(currencies);
            var missingRates = CurrencyRollup.MissingRates(currencies, rollup.Rates, target);

            // Summed from the holdings rather than from the rounded subtotals above, so the
            // headline figure is not the sum of a set of roundings.
            var total = CurrencyRollup.Sum(
                stocks.Select(s => ((decimal?)(s.SharesOwned * s.CurrentPrice), s.CurrencyCode)),
                rollup.Rates,
                target);

            var warnings = new List<MetricWarning>();
            if (missingRates.Count > 0)
                warnings.Add(CurrencyRollup.MissingRateWarning(missingRates));

            return new StockValueSummaryDto(
                total.Amount,
                target,
                currencies.Count > 1,
                currencies.Any(c => !string.Equals(c, target, StringComparison.OrdinalIgnoreCase)),
                rollup.BaseCurrency,
                byCurrency,
                missingRates,
                CurrencyRollup.AppliedRates(currencies, rollup.Rates, target),
                warnings);
        }
    }
}
