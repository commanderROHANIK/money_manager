using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Rent;

namespace MoneyManager.Api.Controllers
{
    /// <summary>
    /// Rent collection: what each month owed, what arrived, and one call to record a payment that
    /// would otherwise be typed in by hand twelve times a year per tenancy.
    ///
    /// <para>
    /// That typing is why this exists. Every return metric is computed from the ledger, so an
    /// empty ledger means null yield, null cap rate and null cash-on-cash — and a ledger that
    /// costs thirty-six manual entries a year to keep current stays empty, which leaves the
    /// product unable to answer its own question.
    /// </para>
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/RentalProperties")]
    public class RentScheduleController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly RentScheduleService _schedules;

        public RentScheduleController(MoneyManagerDbContext context, RentScheduleService schedules)
        {
            _context = context;
            _schedules = schedules;
        }

        [HttpGet("{propertyId:int}/rent-schedule")]
        public async Task<ActionResult<RentSchedule>> GetSchedule(
            int propertyId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var schedule = await _schedules.GetForPropertyAsync(propertyId, from, to);

            return schedule is null ? NotFound() : Ok(schedule);
        }

        /// <summary>Which properties are behind on rent, worst first. Empty when everything is square.</summary>
        [HttpGet("rent-schedule/arrears")]
        public async Task<ActionResult<IEnumerable<PropertyArrears>>> GetArrears() =>
            Ok(await _schedules.GetArrearsAsync());

        /// <summary>
        /// Records the rent for one month, pre-filled from the tenancy that was running.
        /// <paramref name="period"/> is the <c>yyyy-MM</c> key the schedule reports.
        /// </summary>
        [HttpPost("{propertyId:int}/rent-schedule/{period}/record")]
        public async Task<ActionResult<RentPeriod>> Record(
            int propertyId, string period, [FromBody] RecordRentRequest? request = null)
        {
            var property = await _context.RentalProperties.FirstOrDefaultAsync(p => p.Id == propertyId);
            if (property is null)
                return NotFound();

            if (!RentScheduleBuilder.TryParsePeriod(period, out var monthStart))
                return ValidationProblem(detail: "Period must be a month in yyyy-MM form, for example 2026-08.");

            var schedule = await _schedules.GetForPropertyAsync(propertyId, monthStart, monthStart);
            var month = schedule?.Periods.FirstOrDefault(p => p.Period == period);

            if (month is null)
            {
                return ValidationProblem(detail: $"{period} has not started yet, so there is no rent to record against it.");
            }

            if (month.Status == RentPeriodStatus.Vacant || month.ExpectedAmount is not { } expected)
            {
                return ValidationProblem(detail: $"No tenancy was running when rent fell due in {period}, so nothing was owed.");
            }

            // The point of the 409 is that this endpoint is a button. A double click, a retried
            // request, or two devices open on the same page must not book the rent twice.
            if (month.ReceivedAmount > 0m)
            {
                return Problem(
                    detail: $"Rent for {period} is already recorded. Edit the existing entry in the "
                              + "ledger rather than adding a second one.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var amount = request?.Amount ?? expected;
            if (amount <= 0m)
                return ValidationProblem(detail: "Amount must be positive.");

            var date = (request?.Date ?? month.DueDate).Date;
            var note = request?.Description;

            // A date outside the month would be filed against a different period, so the call
            // would silently not do what it says. Refusing is more honest than recording a row
            // that leaves the month it was meant to settle still showing as unpaid.
            if (date < monthStart || date > monthStart.AddMonths(1).AddDays(-1))
            {
                return ValidationProblem(detail: $"A payment dated {date:yyyy-MM-dd} falls outside {period}, so it would not "
                              + "settle that month. Record it against the month it belongs to.");
            }

            _context.PropertyTransactions.Add(new PropertyTransaction
            {
                RentalPropertyId = propertyId,
                LeaseId = month.LeaseId,
                Date = date,
                Amount = amount,
                CurrencyCode = property.CurrencyCode,
                Category = TransactionCategory.RentIncome,
                Description = string.IsNullOrWhiteSpace(note) ? $"Rent for {period}" : note,
            });

            await _context.SaveChangesAsync();

            // Recomputed rather than patched, so the row the caller renders is the row a reload
            // would produce.
            var updated = await _schedules.GetForPropertyAsync(propertyId, monthStart, monthStart);
            var settled = updated?.Periods.FirstOrDefault(p => p.Period == period);

            return settled is null ? NoContent() : Ok(settled);
        }
    }

    /// <summary>
    /// Every field is optional: the whole point is that the tenancy already knows the amount and
    /// the due date. They are overridable for the case where the tenant paid something else, or
    /// paid on a different day.
    /// </summary>
    public record RecordRentRequest(decimal? Amount = null, DateTime? Date = null, string? Description = null);
}
