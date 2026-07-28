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
    public class UpcomingEventsController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly ILogger<UpcomingEventsController> _logger;

        public UpcomingEventsController(MoneyManagerDbContext context, ILogger<UpcomingEventsController> logger)
        {
            _context = context;
            _logger = logger;
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
            var ev = await _context.UpcomingEvents.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();
            return ev;
        }

        [HttpPost]
        public async Task<ActionResult<UpcomingEvent>> CreateEvent([FromBody] UpcomingEventRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.EventDate == DateTime.MinValue)
            {
                return BadRequest("Event title and valid date are required.");
            }

            var ev = new UpcomingEvent();
            Apply(request, ev);

            try
            {
                _context.UpcomingEvents.Add(ev);
                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetEvent), new { id = ev.Id }, ev);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating event.");
                return StatusCode(500, "An error occurred while creating the event.");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] UpcomingEventRequest request)
        {
            var ev = await _context.UpcomingEvents.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();

            Apply(request, ev);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var ev = await _context.UpcomingEvents.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();

            _context.UpcomingEvents.Remove(ev);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static void Apply(UpcomingEventRequest request, UpcomingEvent ev)
        {
            ev.Title = request.Title;
            ev.Description = request.Description ?? string.Empty;
            ev.EventDate = request.EventDate;
            ev.IsRecurring = request.IsRecurring;
            ev.IsNotified = request.IsNotified;
            ev.RentalPropertyId = request.RentalPropertyId;
            ev.LoanId = request.LoanId;
        }
    }

    public record UpcomingEventRequest(
        string Title,
        string? Description,
        DateTime EventDate,
        bool IsRecurring,
        bool IsNotified,
        int? RentalPropertyId = null,
        int? LoanId = null);
}
