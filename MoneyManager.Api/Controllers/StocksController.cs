using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
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
            var stock = await _context.Stocks.FindAsync(id);
            if (stock == null)
                return NotFound();
            return stock;
        }

        [HttpPost]
        public async Task<ActionResult<Stock>> Create(Stock stock)
        {
            stock.PurchaseDate = DateTime.SpecifyKind(stock.PurchaseDate, DateTimeKind.Utc);

            _context.Stocks.Add(stock);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = stock.Id }, stock);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Stock stock)
        {
            if (id != stock.Id)
                return BadRequest();

            _context.Entry(stock).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock == null)
                return NotFound();

            _context.Stocks.Remove(stock);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
