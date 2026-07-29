namespace MoneyManager.Api.Services.Rent
{
    /// <summary>One period of rent owed, before it is persisted.</summary>
    public readonly record struct RentPeriod(
        DateTime PeriodStart,
        DateTime PeriodEnd,
        DateTime DueDate,
        decimal AmountDue,
        bool IsProrated);

    /// <summary>
    /// Works out which rent periods a tenancy owes.
    ///
    /// Pure, like the analytics calculator, because the interesting cases are all calendar
    /// arithmetic: a tenancy that starts on the 15th, a due day of 31 in February, a lease
    /// that ends mid-month, and a generator that must be safe to run twice a day forever.
    /// </summary>
    public static class RentScheduleCalculator
    {
        /// <summary>
        /// Periods from <paramref name="leaseStart"/> up to and including the period
        /// containing <paramref name="upTo"/>, excluding any whose start is already in
        /// <paramref name="existingPeriodStarts"/>.
        ///
        /// Idempotency is the caller's existing periods, not a flag: re-running with the
        /// same stored charges yields an empty list, which is what stops a nightly job from
        /// charging a tenant twice.
        /// </summary>
        public static IReadOnlyList<RentPeriod> Generate(
            DateTime leaseStart,
            DateTime? leaseEnd,
            decimal monthlyRent,
            int dueDayOfMonth,
            DateTime upTo,
            IReadOnlySet<DateTime>? existingPeriodStarts = null)
        {
            var periods = new List<RentPeriod>();

            if (monthlyRent <= 0m)
                return periods;

            var start = leaseStart.Date;
            var end = leaseEnd?.Date;
            var horizon = upTo.Date;

            if (end is not null && end < start)
                return periods;

            // A tenancy that has not started yet owes nothing.
            if (start > horizon)
                return periods;

            var existing = existingPeriodStarts ?? new HashSet<DateTime>();
            var cursor = start;

            while (cursor <= horizon)
            {
                var monthStart = new DateTime(cursor.Year, cursor.Month, 1);
                var daysInMonth = DateTime.DaysInMonth(cursor.Year, cursor.Month);
                var monthEnd = new DateTime(cursor.Year, cursor.Month, daysInMonth);

                // The period is the intersection of this calendar month with the tenancy.
                var periodStart = cursor;
                var periodEnd = end is not null && end < monthEnd ? end.Value : monthEnd;

                if (periodEnd < periodStart)
                    break;

                var daysCovered = (periodEnd - periodStart).Days + 1;
                var isProrated = daysCovered < daysInMonth;

                var amount = isProrated
                    ? Math.Round(monthlyRent * daysCovered / daysInMonth, 2)
                    : monthlyRent;

                if (!existing.Contains(periodStart))
                {
                    periods.Add(new RentPeriod(
                        periodStart,
                        periodEnd,
                        DueDateFor(monthStart, daysInMonth, dueDayOfMonth, periodStart),
                        amount,
                        isProrated));
                }

                // Stop once the tenancy has ended, rather than running to the horizon.
                if (end is not null && periodEnd >= end)
                    break;

                cursor = monthStart.AddMonths(1);
            }

            return periods;
        }

        /// <summary>
        /// The due day within the period's month, clamped to months that are too short —
        /// a due day of 31 has to mean the 28th in February, not roll into March.
        /// A tenancy starting after its own due day is due on the day it starts.
        /// </summary>
        private static DateTime DueDateFor(
            DateTime monthStart, int daysInMonth, int dueDayOfMonth, DateTime periodStart)
        {
            var day = Math.Clamp(dueDayOfMonth, 1, daysInMonth);
            var due = new DateTime(monthStart.Year, monthStart.Month, day);

            return due < periodStart ? periodStart : due;
        }
    }
}
