using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UpcomingEventsController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public UpcomingEventsController(MoneyManagerDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UpcomingEvent>>> GetEvents()
        {
            return await _context.UpcomingEvents
                .OrderBy(e => e.EventDate)
                .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UpcomingEvent>> GetEvent(int id)
        {
            var ev = await _context.UpcomingEvents.FindAsync(id);
            if (ev == null) return NotFound();
            return ev;
        }

        [HttpPost]
        public async Task<ActionResult<UpcomingEvent>> CreateEvent(UpcomingEvent ev)
        {
            _context.UpcomingEvents.Add(ev);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetEvent), new { id = ev.Id }, ev);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(int id, UpcomingEvent updated)
        {
            if (id != updated.Id) return BadRequest();
            _context.Entry(updated).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.UpcomingEvents.Any(e => e.Id == id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var ev = await _context.UpcomingEvents.FindAsync(id);
            if (ev == null) return NotFound();

            _context.UpcomingEvents.Remove(ev);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
