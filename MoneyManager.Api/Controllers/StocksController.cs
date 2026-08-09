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
    public class StocksController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public StocksController(MoneyManagerDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Stock>>> GetAll()
        {
            return await _context.Stocks.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Stock>> GetById(int id)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id);
            if (stock == null)
                return NotFound();
            return stock;
        }

        [HttpPost]
        public async Task<ActionResult<Stock>> Create([FromBody] StockRequest request)
        {
            var stock = new Stock();
            Apply(request, stock);

            _context.Stocks.Add(stock);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = stock.Id }, stock);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] StockRequest request)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id);
            if (stock == null)
                return NotFound();

            Apply(request, stock);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var stock = await _context.Stocks.FirstOrDefaultAsync(s => s.Id == id);
            if (stock == null)
                return NotFound();

            _context.Stocks.Remove(stock);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static void Apply(StockRequest request, Stock stock)
        {
            stock.Ticker = request.Ticker.ToUpperInvariant();
            stock.SharesOwned = request.SharesOwned;
            stock.PurchasePrice = request.PurchasePrice;
            stock.CurrentPrice = request.CurrentPrice;
            stock.PurchaseDate = request.PurchaseDate;
            stock.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? stock.CurrencyCode
                : request.CurrencyCode.ToUpperInvariant();
        }
    }

    public record StockRequest(
        [Required, MaxLength(16)] string Ticker,
        [NonNegative] int SharesOwned,
        [NonNegative] decimal PurchasePrice,
        [NonNegative] decimal CurrentPrice,
        DateTime PurchaseDate,
        [SupportedCurrency] string? CurrencyCode = null);
}
