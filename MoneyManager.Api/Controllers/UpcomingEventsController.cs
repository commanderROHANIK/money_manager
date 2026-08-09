using System.ComponentModel.DataAnnotations;
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

        // The ILogger this used to take existed solely for the try/catch around CreateEvent.
        // UseExceptionHandler logs unhandled exceptions now, so keeping the dependency would
        // leave a field that is assigned and never read.
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
            var ev = await _context.UpcomingEvents.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound();
            return ev;
        }

        [HttpPost]
        public async Task<ActionResult<UpcomingEvent>> CreateEvent([FromBody] UpcomingEventRequest request)
        {
            // The title and date checks that used to live here are now DataAnnotations on the
            // request record, so they fail as ValidationProblemDetails naming the offending field
            // rather than as a bare string the UI could only show as a toast.
            //
            // The try/catch that used to wrap the save is gone too. It logged and returned a
            // string with status 500, which was the only endpoint in the app to answer that
            // shape; UseExceptionHandler now does both, uniformly and without a stack trace.
            var ev = new UpcomingEvent();
            Apply(request, ev);

            _context.UpcomingEvents.Add(ev);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEvent), new { id = ev.Id }, ev);
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
        [property: Required, MaxLength(200)] string Title,
        [property: MaxLength(2000)] string? Description,
        DateTime EventDate,
        bool IsRecurring,
        bool IsNotified,
        int? RentalPropertyId = null,
        int? LoanId = null) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // [Required] cannot catch this: EventDate is a non-nullable DateTime, so an absent or
            // unparseable value binds to default(DateTime) and looks like a supplied one.
            if (EventDate == default)
            {
                yield return new ValidationResult("A valid event date is required.", [nameof(EventDate)]);
            }
        }
    }
}
