using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure.Validation;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class LoansController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public LoansController(MoneyManagerDbContext context)
        {
            _context = context;
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
}
