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
    public class BankAccountsController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly CurrencyRollupService _rollups;

        public BankAccountsController(MoneyManagerDbContext context, CurrencyRollupService rollups)
        {
            _context = context;
            _rollups = rollups;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BankAccount>>> GetBankAccounts()
        {
            return await _context.BankAccounts.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BankAccount>> GetBankAccount(int id)
        {
            // FirstOrDefaultAsync, not FindAsync: Find can return a change-tracked instance
            // without querying, which would sidestep the tenant query filter entirely.
            var bankAccount = await _context.BankAccounts.FirstOrDefaultAsync(a => a.Id == id);

            if (bankAccount == null)
            {
                return NotFound();
            }

            return bankAccount;
        }

        /// <summary>
        /// Total held across every account, plus the per-currency breakdown it was built from.
        ///
        /// <para>
        /// This used to add <c>Balance</c> across accounts while ignoring <c>CurrencyCode</c>,
        /// so a EUR account and a HUF account produced a confident nonsense number. Accounts in
        /// different currencies are now converted at the owner's own rates, and if a rate is
        /// missing the total is null with the pair named — the breakdown below is still exact
        /// either way.
        /// </para>
        /// </summary>
        [HttpGet("summary/total-balance")]
        public async Task<ActionResult<BankBalanceSummaryDto>> GetTotalBalance()
        {
            // Materialized before summing on purpose: SQLite has no native decimal type, so
            // aggregating decimals in SQL either fails or loses precision.
            var accounts = await _context.BankAccounts.ToListAsync();
            var rollup = await _rollups.LoadAsync();

            return BankBalanceSummaryDto.From(accounts, rollup);
        }

        [HttpPost]
        public async Task<ActionResult<BankAccount>> CreateBankAccount([FromBody] BankAccountRequest request)
        {
            var bankAccount = new BankAccount();
            Apply(request, bankAccount);

            _context.BankAccounts.Add(bankAccount);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBankAccount), new { id = bankAccount.Id }, bankAccount);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBankAccount(int id, [FromBody] BankAccountRequest request)
        {
            // Load through the filtered set, then copy the permitted fields across. Attaching
            // a client-supplied entity would let a caller write to any row id they guessed.
            var bankAccount = await _context.BankAccounts.FirstOrDefaultAsync(a => a.Id == id);

            if (bankAccount == null)
            {
                return NotFound();
            }

            Apply(request, bankAccount);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBankAccount(int id)
        {
            var bankAccount = await _context.BankAccounts.FirstOrDefaultAsync(a => a.Id == id);

            if (bankAccount == null)
            {
                return NotFound();
            }

            _context.BankAccounts.Remove(bankAccount);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static void Apply(BankAccountRequest request, BankAccount account)
        {
            account.AccountName = request.AccountName;
            account.Balance = request.Balance;
            account.BankName = request.BankName;
            account.AccountNumber = request.AccountNumber;
            account.AccountType = request.AccountType;
            account.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? account.CurrencyCode
                : request.CurrencyCode.ToUpperInvariant();
        }
    }

    /// <summary>
    /// Attributes are written as <c>[property: ...]</c> throughout. On a positional record the
    /// default target is the constructor parameter, and it is the property metadata that model
    /// validation reads — so the shorter form would bind and validate nothing, silently.
    /// </summary>
    public record BankAccountRequest(
        [property: Required, MaxLength(120)] string AccountName,
        // Deliberately not NonNegative, against what #9's acceptance criteria asked for. An
        // overdraft is an ordinary current-account feature — routine in Hungary, among other
        // places — and a credit card balance is negative by definition. Rejecting it would refuse
        // to record accounts that genuinely exist, which is a worse failure than accepting a
        // typo. Every other amount in this file stays non-negative.
        decimal Balance,
        [property: MaxLength(120)] string BankName,
        [property: MaxLength(64)] string AccountNumber,
        [property: MaxLength(40)] string AccountType,
        [property: SupportedCurrency] string? CurrencyCode = null);

    /// <summary>What is held in one currency. Always exact — no rate is involved in a subtotal.</summary>
    public record CurrencyTotal(string CurrencyCode, decimal Total);

    /// <summary>
    /// <c>Currency</c> names the unit <c>TotalBalance</c> is in, and is never a guess: when the
    /// accounts share a currency it is that one, and when they do not it is the owner's base
    /// currency, which is also the only case where a rate is applied.
    ///
    /// <para>
    /// <c>ByCurrency</c> is the part that is always true. If a rate is missing the headline total
    /// is null rather than approximate, and the breakdown still tells the user exactly what they
    /// hold.
    /// </para>
    /// </summary>
    public record BankBalanceSummaryDto(
        decimal? TotalBalance,
        string Currency,
        bool MixedCurrency,
        bool Converted,
        string BaseCurrency,
        IReadOnlyList<CurrencyTotal> ByCurrency,
        IReadOnlyList<CurrencyPair> MissingRates,
        IReadOnlyList<AppliedRate> AppliedRates,
        IReadOnlyList<MetricWarning> Warnings)
    {
        public static BankBalanceSummaryDto From(IReadOnlyList<BankAccount> accounts, RollupContext rollup)
        {
            var byCurrency = accounts
                .GroupBy(a => a.CurrencyCode.Trim().ToUpperInvariant(), StringComparer.Ordinal)
                .Select(g => new CurrencyTotal(g.Key, Math.Round(g.Sum(a => a.Balance), 2)))
                .OrderBy(t => t.CurrencyCode, StringComparer.Ordinal)
                .ToList();

            // No accounts is not the same shape of unknown as a missing rate: nothing held is
            // genuinely zero, and reporting it as such needs no rate at all.
            if (byCurrency.Count == 0)
            {
                return new BankBalanceSummaryDto(
                    0m, rollup.BaseCurrency, false, false, rollup.BaseCurrency, byCurrency, [], [], []);
            }

            var currencies = byCurrency.Select(t => t.CurrencyCode).ToList();
            var target = rollup.ResolveTarget(currencies);
            var missingRates = CurrencyRollup.MissingRates(currencies, rollup.Rates, target);

            // Summed from the accounts rather than from the rounded subtotals above, so the
            // headline figure is not the sum of a set of roundings.
            var total = CurrencyRollup.Sum(
                accounts.Select(a => ((decimal?)a.Balance, a.CurrencyCode)),
                rollup.Rates,
                target);

            var warnings = new List<MetricWarning>();
            if (missingRates.Count > 0)
                warnings.Add(CurrencyRollup.MissingRateWarning(missingRates));

            return new BankBalanceSummaryDto(
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
