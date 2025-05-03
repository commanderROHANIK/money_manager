using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
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
            var loan = await _context.Loans.FindAsync(id);

            if (loan == null)
                return NotFound();

            return loan;
        }

        [HttpPost]
        public async Task<ActionResult<Loan>> CreateLoan(Loan loan)
        {
            loan.DueDate= DateTime.SpecifyKind(loan.DueDate, DateTimeKind.Utc);

            _context.Loans.Add(loan);

            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetLoan), new { id = loan.Id }, loan);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLoan(int id, Loan loan)
        {
            if (id != loan.Id)
                return BadRequest();

            _context.Entry(loan).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLoan(int id)
        {
            var loan = await _context.Loans.FindAsync(id);

            if (loan == null)
                return NotFound();

            _context.Loans.Remove(loan);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
