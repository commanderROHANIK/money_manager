using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Currency;

namespace MoneyManager.Api.Controllers
{
    /// <summary>
    /// The user's rate table: the rows they typed in, and the ones the API fetched for the pairs
    /// they did not.
    ///
    /// <para>
    /// A manual row always wins. Fetching is what happens for pairs nobody has expressed an
    /// opinion about, so a landlord who recorded the rate their bank actually gave them on the day
    /// of a transfer keeps it — a daily reference rate is not a correction to that. Every row
    /// carries its <c>Source</c> and its date, which is what lets a converted total say where its
    /// number came from instead of merely asserting one.
    /// </para>
    ///
    /// <para>
    /// The fetch is gated on <c>Features:AutomaticExchangeRates</c>. With it off the no-op
    /// provider is registered and nothing is requested at all, which is the behaviour this
    /// controller had before rates were ever fetched.
    /// </para>
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class ExchangeRatesController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly ExchangeRateRefreshService _refresh;
        private readonly ICurrentUser _currentUser;

        public ExchangeRatesController(
            MoneyManagerDbContext context,
            ExchangeRateRefreshService refresh,
            ICurrentUser currentUser)
        {
            _context = context;
            _refresh = refresh;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExchangeRateDto>>> GetAll()
        {
            // Refreshed before listing rather than on a timer. A background job would need a user
            // to run as, and there is no HttpContext outside a request — the same constraint that
            // makes DemoDataSeeder go through SeedCurrentUser. The service caches per user, so
            // this costs one outbound call every few hours rather than one per page load.
            await RefreshAsync();

            var rates = await _context.ExchangeRates
                .OrderBy(r => r.BaseCurrency)
                .ThenBy(r => r.QuoteCurrency)
                .ToListAsync();

            return rates.Select(ExchangeRateDto.From).ToList();
        }

        /// <summary>
        /// Fetches now, ignoring the cache window, and returns the refreshed table.
        ///
        /// <para>
        /// Exists because "the rate is from this morning" is a reasonable thing to want on demand,
        /// and waiting out a cache window is not a satisfying answer to it. Manual rows are still
        /// untouched — this refreshes what was fetched, it does not overwrite what was asserted.
        /// </para>
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<IEnumerable<ExchangeRateDto>>> Refresh()
        {
            await RefreshAsync(force: true);

            var rates = await _context.ExchangeRates
                .OrderBy(r => r.BaseCurrency)
                .ThenBy(r => r.QuoteCurrency)
                .ToListAsync();

            return rates.Select(ExchangeRateDto.From).ToList();
        }

        /// <summary>
        /// Asks for every supported currency against the user's reporting currency, in one call.
        ///
        /// <para>
        /// Over-fetching slightly rather than working out which currencies the portfolio actually
        /// holds: that would mean querying properties, accounts, loans and stocks to save nothing —
        /// the provider returns every symbol in a single response either way.
        /// </para>
        /// </summary>
        private async Task RefreshAsync(bool force = false)
        {
            var userId = _currentUser.UserId;
            if (userId is null)
                return;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
            var baseCurrency = SupportedCurrencies.Normalize(user?.BaseCurrency);

            if (baseCurrency is null)
                return;

            await _refresh.RefreshAsync(baseCurrency, SupportedCurrencies.All, force, HttpContext.RequestAborted);
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

            // The set of pairs worth asking the provider about just changed: this one is now
            // spoken for. Nothing breaks without it, but leaving a stale window costs a pointless
            // request on the next read.
            await InvalidateAsync();

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

            // This is the one that matters. Removing a hand-entered rate is how a user says "stop
            // using mine, use the live one" — and the pair only gets fetched again if the window
            // is forgotten. Left cached, the next list shows the row gone and nothing in its
            // place, for up to six hours.
            await InvalidateAsync();

            return NoContent();
        }

        /// <summary>
        /// Forgets this user's fetch window, so the next read asks the provider again.
        /// </summary>
        private async Task InvalidateAsync()
        {
            var userId = _currentUser.UserId;
            if (userId is null)
                return;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (SupportedCurrencies.Normalize(user?.BaseCurrency) is { } baseCurrency)
                _refresh.Invalidate(baseCurrency);
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
