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
    [FeatureGate(Feature.Loans)]
    [Route("api/[controller]")]
    public class LoansController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly CurrencyRollupService _rollups;

        public LoansController(MoneyManagerDbContext context, CurrencyRollupService rollups)
        {
            _context = context;
            _rollups = rollups;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Loan>>> GetLoans()
        {
            return await _context.Loans.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Loan>> GetLoan(int id)
        {
            var loan = await _context.Loans.FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
                return NotFound();

            return loan;
        }

        /// <summary>
        /// Total owed across every loan's original principal, plus the per-currency breakdown it
        /// was built from — the loans equivalent of <c>BankAccountsController.GetTotalBalance</c>.
        ///
        /// <para>
        /// This used to be summed client-side across <c>LoanAmount</c> while ignoring
        /// <c>CurrencyCode</c> (and labelled with a hardcoded HUF suffix regardless), so a EUR
        /// mortgage beside a HUF one produced a confident nonsense number. Loans in different
        /// currencies are now converted at the owner's own rates, and if a rate is missing the
        /// total is null with the pair named — the breakdown below is still exact either way.
        /// </para>
        /// </summary>
        [HttpGet("summary/total-amount")]
        public async Task<ActionResult<LoanAmountSummaryDto>> GetTotalAmount()
        {
            // Materialized before summing on purpose: SQLite has no native decimal type, so
            // aggregating decimals in SQL either fails or loses precision.
            var loans = await _context.Loans.ToListAsync();
            var rollup = await _rollups.LoadAsync();

            return LoanAmountSummaryDto.From(loans, rollup);
        }

        [HttpPost]
        public async Task<ActionResult<Loan>> CreateLoan([FromBody] LoanRequest request)
        {
            if (!await PropertyLinkIsValid(request))
                return ValidationProblem(detail: "The property this loan is secured on was not found.");

            var loan = new Loan();
            Apply(request, loan);

            _context.Loans.Add(loan);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLoan), new { id = loan.Id }, loan);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLoan(int id, [FromBody] LoanRequest request)
        {
            var loan = await _context.Loans.FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
                return NotFound();

            if (!await PropertyLinkIsValid(request))
                return ValidationProblem(detail: "The property this loan is secured on was not found.");

            Apply(request, loan);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLoan(int id)
        {
            var loan = await _context.Loans.FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
                return NotFound();

            _context.Loans.Remove(loan);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static void Apply(LoanRequest request, Loan loan)
        {
            loan.LoanName = request.LoanName;
            loan.LoanAmount = request.LoanAmount;
            loan.RemainingBalance = request.RemainingBalance;
            loan.InterestRate = request.InterestRate;
            loan.DueDate = request.DueDate;
            loan.IsPaidOff = request.IsPaidOff;
            loan.LoanType = request.LoanType;
            loan.RentalPropertyId = request.RentalPropertyId;
            loan.MonthlyPayment = request.MonthlyPayment;
            loan.StartDate = request.StartDate;
            loan.TermMonths = request.TermMonths;
            loan.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? loan.CurrencyCode
                : request.CurrencyCode.ToUpperInvariant();
        }

        /// <summary>
        /// Guards the mortgage link. The tenant query filter means an id belonging to
        /// another user simply is not found, so this both validates and isolates.
        /// </summary>
        private async Task<bool> PropertyLinkIsValid(LoanRequest request)
        {
            if (request.RentalPropertyId is not { } propertyId)
                return true;

            return await _context.RentalProperties.AnyAsync(p => p.Id == propertyId);
        }
    }

    public record LoanRequest(
        [Required, MaxLength(120)] string LoanName,
        [NonNegative] decimal LoanAmount,
        [NonNegative] decimal RemainingBalance,
        [NonNegative] decimal InterestRate,
        DateTime DueDate,
        bool IsPaidOff,
        [SupportedCurrency] string? CurrencyCode = null,
        LoanType LoanType = LoanType.Personal,
        int? RentalPropertyId = null,
        [NonNegative] decimal? MonthlyPayment = null,
        DateTime? StartDate = null,
        [NonNegative] int? TermMonths = null) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Owing more than was ever borrowed is not a loan, it is a typo — and it feeds
            // straight into equity and cash-on-cash, where it would read as a plausible number.
            if (RemainingBalance > LoanAmount)
            {
                yield return new ValidationResult(
                    "Remaining balance cannot exceed the original loan amount.",
                    [nameof(RemainingBalance)]);
            }
        }
    }

    /// <summary>
    /// <c>Currency</c> names the unit <c>TotalAmount</c> is in, and is never a guess: when the
    /// loans share a currency it is that one, and when they do not it is the owner's base
    /// currency, which is also the only case where a rate is applied.
    ///
    /// <para>
    /// <c>ByCurrency</c> is the part that is always true. If a rate is missing the headline total
    /// is null rather than approximate, and the breakdown still tells the user exactly what they
    /// owe.
    /// </para>
    /// </summary>
    public record LoanAmountSummaryDto(
        decimal? TotalAmount,
        string Currency,
        bool MixedCurrency,
        bool Converted,
        string BaseCurrency,
        IReadOnlyList<CurrencyTotal> ByCurrency,
        IReadOnlyList<CurrencyPair> MissingRates,
        IReadOnlyList<AppliedRate> AppliedRates,
        IReadOnlyList<MetricWarning> Warnings)
    {
        public static LoanAmountSummaryDto From(IReadOnlyList<Loan> loans, RollupContext rollup)
        {
            var byCurrency = loans
                .GroupBy(l => l.CurrencyCode.Trim().ToUpperInvariant(), StringComparer.Ordinal)
                .Select(g => new CurrencyTotal(g.Key, Math.Round(g.Sum(l => l.LoanAmount), 2)))
                .OrderBy(t => t.CurrencyCode, StringComparer.Ordinal)
                .ToList();

            // No loans is not the same shape of unknown as a missing rate: owing nothing is
            // genuinely zero, and reporting it as such needs no rate at all.
            if (byCurrency.Count == 0)
            {
                return new LoanAmountSummaryDto(
                    0m, rollup.BaseCurrency, false, false, rollup.BaseCurrency, byCurrency, [], [], []);
            }

            var currencies = byCurrency.Select(t => t.CurrencyCode).ToList();
            var target = rollup.ResolveTarget(currencies);
            var missingRates = CurrencyRollup.MissingRates(currencies, rollup.Rates, target);

            // Summed from the loans rather than from the rounded subtotals above, so the
            // headline figure is not the sum of a set of roundings.
            var total = CurrencyRollup.Sum(
                loans.Select(l => ((decimal?)l.LoanAmount, l.CurrencyCode)),
                rollup.Rates,
                target);

            var warnings = new List<MetricWarning>();
            if (missingRates.Count > 0)
                warnings.Add(CurrencyRollup.MissingRateWarning(missingRates));

            return new LoanAmountSummaryDto(
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
