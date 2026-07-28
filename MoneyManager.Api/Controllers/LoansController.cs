using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
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
            loan.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? loan.CurrencyCode
                : request.CurrencyCode.ToUpperInvariant();
        }
    }

    public record LoanRequest(
        string LoanName,
        decimal LoanAmount,
        decimal RemainingBalance,
        decimal InterestRate,
        DateTime DueDate,
        bool IsPaidOff,
        string? CurrencyCode = null);
}
