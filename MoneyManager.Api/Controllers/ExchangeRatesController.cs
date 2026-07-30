using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    /// <summary>
    /// Rates are shared reference data rather than per-user records, so any signed-in user
    /// reads the same table. Entering them by hand is the path that always works; an
    /// automatic feed writes the same rows with a different <c>Source</c>.
    ///
    /// Reads are open to any signed-in user; writes are not. Because one table backs every
    /// tenant's totals, an ordinary account able to write here could misstate every other
    /// user's portfolio by entering a wrong rate, or withhold it entirely by deleting one.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/exchange-rates")]
    public class ExchangeRatesController(MoneyManagerDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExchangeRate>>> GetAll()
        {
            return await context.ExchangeRates
                .AsNoTracking()
                .OrderBy(r => r.FromCurrency)
                .ThenBy(r => r.ToCurrency)
                .ThenByDescending(r => r.AsOf)
                .ToListAsync();
        }

        /// <summary>
        /// Upserts the rate for a pair on a date. Idempotent so re-entering a correction, or
        /// re-running a feed for the same day, updates rather than accumulating duplicates.
        /// </summary>
        [HttpPut]
        [Authorize(Roles = TokenProvider.AdminRole)]
        public async Task<ActionResult<ExchangeRate>> Upsert([FromBody] ExchangeRateRequest request)
        {
            var from = Normalise(request.FromCurrency);
            var to = Normalise(request.ToCurrency);

            if (from.Length != 3 || to.Length != 3)
                return BadRequest(new { message = "Currencies must be three-letter ISO 4217 codes." });

            if (from == to)
                return BadRequest(new { message = "A currency cannot have a rate against itself." });

            if (request.Rate <= 0)
                return BadRequest(new { message = "A rate must be greater than zero." });

            var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

            var existing = await context.ExchangeRates
                .FirstOrDefaultAsync(r => r.FromCurrency == from && r.ToCurrency == to && r.AsOf == asOf);

            if (existing is null)
            {
                existing = new ExchangeRate
                {
                    FromCurrency = from,
                    ToCurrency = to,
                    AsOf = asOf,
                };
                context.ExchangeRates.Add(existing);
            }

            existing.Rate = request.Rate;
            existing.Source = "manual";

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two upserts for the same pair and date can both find nothing and both
                // insert; the unique index is what actually decides it. Re-applying to the
                // row that won keeps this idempotent rather than returning a 500.
                context.ChangeTracker.Clear();

                var winner = await context.ExchangeRates
                    .FirstOrDefaultAsync(r => r.FromCurrency == from && r.ToCurrency == to && r.AsOf == asOf);

                if (winner is null)
                    throw;

                winner.Rate = request.Rate;
                winner.Source = "manual";
                await context.SaveChangesAsync();

                return Ok(winner);
            }

            return Ok(existing);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = TokenProvider.AdminRole)]
        public async Task<IActionResult> Delete(int id)
        {
            var rate = await context.ExchangeRates.FirstOrDefaultAsync(r => r.Id == id);
            if (rate is null)
                return NotFound();

            context.ExchangeRates.Remove(rate);
            await context.SaveChangesAsync();
            return NoContent();
        }

        private static string Normalise(string code) =>
            (code ?? string.Empty).Trim().ToUpperInvariant();
    }

    public record ExchangeRateRequest(
        string FromCurrency,
        string ToCurrency,
        decimal Rate,
        DateOnly? AsOf = null);
}
