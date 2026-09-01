using MoneyManager.Api.Services.Rent;

namespace MoneyManager.Api.Services.Agenda
{
    /// <summary>
    /// Merges manual reminders with derived rent and loan due dates into one sorted agenda,
    /// instead of a landlord typing a reminder in for a due date the ledger already knows.
    ///
    /// <para>
    /// Pure by design — no database, no clock — for the same reason <c>RentScheduleBuilder</c> is:
    /// every case below is a literal in a test. The impure half lives in
    /// <c>AgendaService</c>, which fetches <c>PropertyRentDue</c> rows from
    /// <c>RentScheduleService</c> (itself built on <c>RentScheduleBuilder</c>, so due-date math is
    /// never written twice), maps loans and manual events into the plain records this type takes,
    /// and calls <see cref="Build"/>.
    /// </para>
    ///
    /// <para>
    /// Loan recurrence is deliberately not modelled: an open, overdue loan (<c>IsPaidOff</c> false,
    /// <c>DueDate</c> in the past) shows as overdue on every call, indefinitely, rather than
    /// projecting its due date forward by <c>MonthlyPayment</c> cadence. <c>Loan</c> carries one
    /// due date and no recurrence rule to project from, and inventing one would be a guess this
    /// product does not make.
    /// </para>
    /// </summary>
    public static class AgendaBuilder
    {
        public static IReadOnlyList<AgendaEntry> Build(AgendaInput input)
        {
            var today = input.Today.Date;

            // A guard against a negative window, not a business rule: "days" is a query
            // parameter, and a negative value should behave like "show only what's overdue"
            // rather than throw.
            var horizon = today.AddDays(Math.Max(0, input.Days));

            var entries = new List<AgendaEntry>();

            foreach (var rent in input.RentDue)
            {
                // Stable across requests, and stable across an acknowledgement for exactly one
                // month: the due date is baked into the key, so acknowledging August's rent
                // reminder has no bearing on September's — that is a different string, and was
                // never acknowledged.
                var key = $"rent:{rent.LeaseId}:{rent.DueDate:yyyy-MM-dd}";

                if (input.AcknowledgedKeys.Contains(key) || !Include(rent.DueDate, rent.IsOverdue, today, horizon))
                    continue;

                entries.Add(new AgendaEntry
                {
                    Key = key,
                    Source = AgendaSource.Rent,
                    Title = $"Rent due — {rent.PropertyName}",
                    DueDate = rent.DueDate,
                    Amount = rent.AmountDue,
                    CurrencyCode = rent.CurrencyCode,
                    IsOverdue = rent.IsOverdue,
                    RentalPropertyId = rent.PropertyId,
                    PropertyName = rent.PropertyName,
                    LeaseId = rent.LeaseId,
                });
            }

            foreach (var loan in input.Loans)
            {
                if (loan.IsPaidOff)
                    continue;

                // No recurrence to project, so the key carries no date — see the class docblock.
                var key = $"loan:{loan.LoanId}";
                var isOverdue = loan.DueDate.Date < today;

                if (input.AcknowledgedKeys.Contains(key) || !Include(loan.DueDate, isOverdue, today, horizon))
                    continue;

                entries.Add(new AgendaEntry
                {
                    Key = key,
                    Source = AgendaSource.Loan,
                    Title = $"{loan.LoanName} payment due",
                    DueDate = loan.DueDate.Date,
                    Amount = loan.MonthlyPayment,
                    CurrencyCode = loan.CurrencyCode,
                    IsOverdue = isOverdue,
                    RentalPropertyId = loan.RentalPropertyId,
                    LoanId = loan.LoanId,
                });
            }

            foreach (var manual in input.ManualEvents)
            {
                var key = $"manual:{manual.Id}";
                var isOverdue = manual.EventDate.Date < today;

                if (input.AcknowledgedKeys.Contains(key) || !Include(manual.EventDate, isOverdue, today, horizon))
                    continue;

                entries.Add(new AgendaEntry
                {
                    Key = key,
                    Source = AgendaSource.Manual,
                    Title = manual.Title,
                    DueDate = manual.EventDate.Date,
                    IsOverdue = isOverdue,
                    RentalPropertyId = manual.RentalPropertyId,
                    LoanId = manual.LoanId,
                    UpcomingEventId = manual.Id,
                });
            }

            // Soonest first, ties broken by key so the order is deterministic rather than
            // depending on the order entries happened to be added in.
            return entries
                .OrderBy(e => e.DueDate)
                .ThenBy(e => e.Key, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Overdue always shows, regardless of the window — a rent six months overdue must not
        /// vanish because it falls outside <c>days</c>. An upcoming (not-yet-due) entry only
        /// shows inside the window, and is included on the due date itself: a due date is not
        /// overdue on the day it falls, but it has to be visible.
        /// </summary>
        private static bool Include(DateTime dueDate, bool isOverdue, DateTime today, DateTime horizon) =>
            isOverdue || (dueDate.Date >= today && dueDate.Date <= horizon);
    }
}
