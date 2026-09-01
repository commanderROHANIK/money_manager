using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Agenda;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Authorize]
    [FeatureGate(Feature.Events)]
    [Route("api/[controller]")]
    public class UpcomingEventsController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly AgendaService _agenda;

        // The ILogger this used to take existed solely for the try/catch around CreateEvent.
        // UseExceptionHandler logs unhandled exceptions now, so keeping the dependency would
        // leave a field that is assigned and never read.
        public UpcomingEventsController(MoneyManagerDbContext context, AgendaService agenda)
        {
            _context = context;
            _agenda = agenda;
        }

        /// <summary>
        /// Manual reminders merged with rent and loan due dates derived from the ledger, sorted
        /// by due date. <paramref name="days"/> bounds only entries that are not yet due — an
        /// overdue one shows regardless, because a rent six months overdue must not vanish for
        /// falling outside a 30-day window.
        ///
        /// <para>
        /// Routed ahead of <see cref="GetEvent"/>'s <c>{id}</c> template on purpose: attribute
        /// routing prefers the more specific literal segment, the same as <c>/users/me</c> would
        /// beat <c>/users/{id}</c>, so a request for <c>/agenda</c> never falls through to a
        /// lookup for an event literally named "agenda".
        /// </para>
        /// </summary>
        [HttpGet("agenda")]
        public async Task<ActionResult<IEnumerable<AgendaEntry>>> GetAgenda([FromQuery] int days = 30)
        {
            if (days < 0)
                return ValidationProblem(detail: "days must not be negative.");

            return Ok(await _agenda.GetAgendaAsync(days));
        }

        /// <summary>
        /// Dismisses one agenda entry by the stable key <see cref="GetAgenda"/> reported it
        /// under. Works uniformly for a manual event and a derived rent or loan entry — there is
        /// no row to delete for the derived ones, only a key to remember having seen.
        /// </summary>
        [HttpPost("agenda/{key}/ack")]
        public async Task<IActionResult> AcknowledgeAgendaEntry(string key)
        {
            await _agenda.AcknowledgeAsync(key);
            return NoContent();
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

            // The agenda entry this event would have produced is gone with it, so an
            // acknowledgement of it is now a row about nothing. Left behind it is harmless — the
            // key can never be produced again — but cleaning it up keeps the table from
            // accumulating rows nothing will ever look up again.
            var key = $"manual:{id}";
            var ack = await _context.AgendaAcknowledgements.FirstOrDefaultAsync(a => a.Key == key);
            if (ack is not null)
                _context.AgendaAcknowledgements.Remove(ack);

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
        [Required, MaxLength(200)] string Title,
        [MaxLength(2000)] string? Description,
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
