using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RentalPropertiesController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public RentalPropertiesController(MoneyManagerDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RentalProperty>>> GetRentalProperties()
        {
            return await _context.RentalProperties.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RentalProperty>> GetRentalProperty(int id)
        {
            var property = await _context.RentalProperties.FindAsync(id);
            if (property == null) return NotFound();
            return property;
        }

        [HttpPost]
        public async Task<ActionResult<RentalProperty>> CreateRentalProperty(RentalProperty property)
        {
            _context.RentalProperties.Add(property);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetRentalProperty), new { id = property.Id }, property);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRentalProperty(int id, RentalProperty updated)
        {
            if (id != updated.Id) return BadRequest();
            _context.Entry(updated).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.RentalProperties.Any(e => e.Id == id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRentalProperty(int id)
        {
            var property = await _context.RentalProperties.FindAsync(id);
            if (property == null) return NotFound();

            _context.RentalProperties.Remove(property);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
