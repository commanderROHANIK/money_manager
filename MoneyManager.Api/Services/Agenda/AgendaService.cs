using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Rent;

namespace MoneyManager.Api.Services.Agenda
{
    /// <summary>
    /// Assembles <see cref="AgendaBuilder"/>'s input from stored entities, and persists an
    /// acknowledgement. All the impure work the pure builder is not allowed to do.
    /// </summary>
    public sealed class AgendaService(MoneyManagerDbContext context, RentScheduleService rentSchedules)
    {
        public async Task<IReadOnlyList<AgendaEntry>> GetAgendaAsync(int days, DateTime? today = null)
        {
            var effectiveToday = (today ?? DateTime.UtcNow).Date;

            var rentDue = await rentSchedules.GetCurrentDueAsync(effectiveToday);
            var loans = await context.Loans.ToListAsync();
            var manualEvents = await context.UpcomingEvents.ToListAsync();

            var acknowledged = await context.AgendaAcknowledgements
                .Select(a => a.Key)
                .ToListAsync();

            return AgendaBuilder.Build(new AgendaInput
            {
                RentDue = rentDue,
                Loans = loans.Select(ToAgendaLoan).ToList(),
                ManualEvents = manualEvents.Select(ToAgendaManualEvent).ToList(),
                AcknowledgedKeys = acknowledged.ToHashSet(StringComparer.Ordinal),
                Days = days,
                Today = effectiveToday,
            });
        }

        /// <summary>
        /// Records an acknowledgement. Idempotent — acknowledging a key twice is a no-op rather
        /// than an error, because a retried request or a second tab clicking the same entry is
        /// not a mistake worth surfacing to the caller.
        /// </summary>
        public async Task AcknowledgeAsync(string key)
        {
            var alreadyAcknowledged = await context.AgendaAcknowledgements.AnyAsync(a => a.Key == key);
            if (alreadyAcknowledged)
                return;

            context.AgendaAcknowledgements.Add(new AgendaAcknowledgement { Key = key });
            await context.SaveChangesAsync();
        }

        private static AgendaLoan ToAgendaLoan(Loan loan) => new(
            loan.Id,
            loan.LoanName,
            loan.CurrencyCode,
            loan.DueDate,
            loan.MonthlyPayment,
            loan.IsPaidOff,
            loan.RentalPropertyId);

        private static AgendaManualEvent ToAgendaManualEvent(UpcomingEvent ev) => new(
            ev.Id,
            ev.Title,
            ev.EventDate,
            ev.RentalPropertyId,
            ev.LoanId);
    }
}
