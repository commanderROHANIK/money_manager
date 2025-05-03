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
        public async Task<ActionResult<IEnumerable<RentalProperty>>> GetAll()
        {
            return await _context.RentalProperties.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RentalProperty>> GetById(int id)
        {
            var property = await _context.RentalProperties.FindAsync(id);
            if (property == null)
                return NotFound();
            return property;
        }

        [HttpPost]
        public async Task<ActionResult<RentalProperty>> Create(RentalProperty property)
        {
            property.RentDueDate= DateTime.SpecifyKind(property.RentDueDate, DateTimeKind.Utc);

            _context.RentalProperties.Add(property);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = property.Id }, property);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, RentalProperty property)
        {
            if (id != property.Id)
                return BadRequest();

            _context.Entry(property).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var property = await _context.RentalProperties.FindAsync(id);
            if (property == null)
                return NotFound();

            _context.RentalProperties.Remove(property);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
