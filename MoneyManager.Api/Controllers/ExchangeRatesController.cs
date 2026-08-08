using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    /// <summary>
    /// The user's own rate table. There is no feed behind this and there is not meant to be: the
    /// app makes no outbound network calls, so a consolidated total is only ever as good as the
    /// rates its owner typed in — which is also why every converted figure is labelled as
    /// converted, with the rate and its date.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public ExchangeRatesController(MoneyManagerDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExchangeRateDto>>> GetAll()
        {
            var rates = await _context.ExchangeRates
                .OrderBy(r => r.BaseCurrency)
                .ThenBy(r => r.QuoteCurrency)
                .ToListAsync();

            return rates.Select(ExchangeRateDto.From).ToList();
        }

        /// <summary>
        /// Records what one <paramref name="baseCurrency"/> is worth in
        /// <paramref name="quoteCurrency"/>, replacing whatever was on record for that pair.
        /// </summary>
        [HttpPut("{baseCurrency}/{quoteCurrency}")]
        public async Task<ActionResult<ExchangeRateDto>> Upsert(
            string baseCurrency,
            string quoteCurrency,
            [FromBody] ExchangeRateRequest request)
        {
            if (!TryNormalisePair(baseCurrency, quoteCurrency, out var from, out var to, out var problem))
                return problem;

            if (request.Rate <= 0)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["rate"] = ["A rate must be greater than zero."],
                }));
            }

            // Loaded through the filtered set, never FindAsync and never attached from the
            // request: an upsert is exactly where the reflex to Attach a client-built entity
            // shows up, and it would let a caller write to any row id they guessed.
            //
            // Either direction of the pair is the same fact, so entering EUR→HUF replaces an
            // existing HUF→EUR row rather than sitting beside it disagreeing with it.
            var existing = await _context.ExchangeRates.FirstOrDefaultAsync(r =>
                (r.BaseCurrency == from && r.QuoteCurrency == to)
                || (r.BaseCurrency == to && r.QuoteCurrency == from));

            var rate = existing ?? new ExchangeRate();

            rate.BaseCurrency = from;
            rate.QuoteCurrency = to;
            rate.Rate = request.Rate;
            rate.AsOf = (request.AsOf ?? DateTime.UtcNow).Date;
            rate.Source = ExchangeRateSource.Manual;

            if (existing is null)
                _context.ExchangeRates.Add(rate);

            await _context.SaveChangesAsync();

            return Ok(ExchangeRateDto.From(rate));
        }

        [HttpDelete("{baseCurrency}/{quoteCurrency}")]
        public async Task<IActionResult> Delete(string baseCurrency, string quoteCurrency)
        {
            if (!TryNormalisePair(baseCurrency, quoteCurrency, out var from, out var to, out var problem))
                return problem;

            var rate = await _context.ExchangeRates.FirstOrDefaultAsync(r =>
                (r.BaseCurrency == from && r.QuoteCurrency == to)
                || (r.BaseCurrency == to && r.QuoteCurrency == from));

            if (rate == null)
                return NotFound();

            _context.ExchangeRates.Remove(rate);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// Upper-cases both codes and checks them against the supported set. Normalising on write
        /// is what makes the pair a genuine key: without it "eur" and "EUR" are two rows, and the
        /// upsert above quietly becomes an insert.
        /// </summary>
        private bool TryNormalisePair(
            string baseCurrency,
            string quoteCurrency,
            [NotNullWhen(true)] out string? from,
            [NotNullWhen(true)] out string? to,
            [NotNullWhen(false)] out ActionResult? problem)
        {
            var errors = new Dictionary<string, string[]>();

            var normalisedFrom = SupportedCurrencies.Normalize(baseCurrency);
            var normalisedTo = SupportedCurrencies.Normalize(quoteCurrency);
            var supported = string.Join(", ", SupportedCurrencies.All);

            if (normalisedFrom is null)
                errors["baseCurrency"] = [$"'{baseCurrency}' is not a supported currency. Supported: {supported}."];

            if (normalisedTo is null)
                errors["quoteCurrency"] = [$"'{quoteCurrency}' is not a supported currency. Supported: {supported}."];

            if (normalisedFrom is not null && normalisedFrom == normalisedTo)
                errors["quoteCurrency"] = ["A currency is always worth one of itself; pick two different currencies."];

            if (errors.Count > 0 || normalisedFrom is null || normalisedTo is null)
            {
                from = null;
                to = null;
                problem = ValidationProblem(new ValidationProblemDetails(errors));
                return false;
            }

            from = normalisedFrom;
            to = normalisedTo;
            problem = null;
            return true;
        }
    }

    /// <summary>What one <c>BaseCurrency</c> buys in <c>QuoteCurrency</c>, as of a date.</summary>
    public record ExchangeRateRequest(decimal Rate, DateTime? AsOf = null);

    public record ExchangeRateDto(
        int Id,
        string BaseCurrency,
        string QuoteCurrency,
        decimal Rate,
        DateTime AsOf,
        ExchangeRateSource Source)
    {
        public static ExchangeRateDto From(ExchangeRate rate) => new(
            rate.Id,
            rate.BaseCurrency,
            rate.QuoteCurrency,
            rate.Rate,
            rate.AsOf,
            rate.Source);
    }
}
