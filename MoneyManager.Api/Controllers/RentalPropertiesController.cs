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
    public class RentalPropertiesController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public RentalPropertiesController(MoneyManagerDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RentalProperty>>> GetAll()
        {
            return await _context.RentalProperties.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RentalProperty>> GetById(int id)
        {
            var property = await _context.RentalProperties.FirstOrDefaultAsync(p => p.Id == id);
            if (property == null)
                return NotFound();
            return property;
        }

        [HttpPost]
        public async Task<ActionResult<RentalProperty>> Create([FromBody] RentalPropertyRequest request)
        {
            var property = new RentalProperty();
            Apply(request, property);

            _context.RentalProperties.Add(property);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = property.Id }, property);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] RentalPropertyRequest request)
        {
            var property = await _context.RentalProperties.FirstOrDefaultAsync(p => p.Id == id);
            if (property == null)
                return NotFound();

            Apply(request, property);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var property = await _context.RentalProperties.FirstOrDefaultAsync(p => p.Id == id);
            if (property == null)
                return NotFound();

            _context.RentalProperties.Remove(property);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static void Apply(RentalPropertyRequest request, RentalProperty property)
        {
            property.PropertyName = request.PropertyName;
            property.Address = request.Address;
            property.RentAmount = request.RentAmount;
            property.RentDueDate = request.RentDueDate;
            property.IsRented = request.IsRented;
            property.CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? property.CurrencyCode
                : request.CurrencyCode.ToUpperInvariant();
        }
    }

    public record RentalPropertyRequest(
        string PropertyName,
        string Address,
        decimal RentAmount,
        DateTime RentDueDate,
        bool IsRented,
        string? CurrencyCode = null);
}
