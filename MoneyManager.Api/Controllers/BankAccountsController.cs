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
    public class BankAccountsController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public BankAccountsController(MoneyManagerDbContext context)
        {
            _context = context;
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

        [HttpGet("summary/total-balance")]
        public async Task<IActionResult> GetTotalBalance()
        {
            // Materialized before summing on purpose: SQLite has no native decimal type, so
            // aggregating decimals in SQL either fails or loses precision.
            var accounts = await _context.BankAccounts.ToListAsync();

            return Ok(new { totalBalance = accounts.Sum(a => a.Balance) });
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

    public record BankAccountRequest(
        string AccountName,
        decimal Balance,
        string BankName,
        string AccountNumber,
        string AccountType,
        string? CurrencyCode = null);
}
