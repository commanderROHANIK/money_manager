using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BankAccountsController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public BankAccountsController(MoneyManagerDbContext context)
        {
            _context = context;
        }

        // Get all bank accounts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BankAccount>>> GetBankAccounts()
        {
            return await _context.BankAccounts.ToListAsync();
        }

        // Get a specific bank account by ID
        [HttpGet("{id}")]
        public async Task<ActionResult<BankAccount>> GetBankAccount(int id)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(id);

            if (bankAccount == null)
            {
                return NotFound();
            }

            return bankAccount;
        }

        [HttpGet("summary/total-balance")]
        public async Task<IActionResult> GetTotalBalance()
        {
            var accounts = await _context.BankAccounts.ToListAsync();

            decimal accountTotal = accounts.Sum(a => a.Balance);
            
            return Ok(new { totalBalance = accountTotal});
        }

        // Create a new bank account
        [HttpPost]
        public async Task<ActionResult<BankAccount>> CreateBankAccount([FromBody] BankAccount bankAccount)
        {
            if (bankAccount == null)
            {
                return BadRequest("Bank account data is required.");
            }

            _context.BankAccounts.Add(bankAccount);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBankAccount), new { id = bankAccount.Id }, bankAccount);
        }

        // Update an existing bank account
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBankAccount(int id, [FromBody] BankAccount bankAccount)
        {
            if (id != bankAccount.Id)
            {
                return BadRequest();
            }

            _context.Entry(bankAccount).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Delete a bank account
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBankAccount(int id)
        {
            var bankAccount = await _context.BankAccounts.FindAsync(id);

            if (bankAccount == null)
            {
                return NotFound();
            }

            _context.BankAccounts.Remove(bankAccount);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
