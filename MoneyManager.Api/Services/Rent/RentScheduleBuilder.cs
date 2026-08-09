using System.Globalization;

namespace MoneyManager.Api.Services.Rent
{
    /// <summary>
    /// Turns tenancies plus recorded rent into a month-by-month collection record: what was owed,
    /// what arrived, and what is still outstanding.
    ///
    /// <para>
    /// Pure by design — no database, no clock, no configuration — for the same reason
    /// <c>PropertyAnalyticsCalculator</c> is: every case below can be written as a literal in a
    /// test, which is the only way the awkward months (a tenancy starting mid-month, a rent
    /// change part-way through a year, a gap between tenants) get checked at all.
    /// </para>
    /// </summary>
    public static class RentScheduleBuilder
    {
        /// <summary>
        /// A guard, not a business rule. A mistyped tenancy start of 1899 would otherwise generate
        /// fifteen hundred rows nobody asked for; this keeps the response bounded and recent.
        /// </summary>
        private const int MaxPeriods = 600;

        /// <summary>
        /// The one place a payment is matched to a month.
        ///
        /// <para>
        /// A payment belongs to the calendar month its date falls in. Where it carries a
        /// <c>LeaseId</c> it is only credited to a month billed to that same tenancy, so a payment
        /// tagged to the outgoing tenant cannot pay off the incoming one's first month. An
        /// untagged payment is credited to whichever tenancy was running.
        /// </para>
        ///
        /// <para>
        /// The known limitation is late payment across a month boundary: rent due on the 5th and
        /// paid on the 2nd of the following month credits the following month, leaving the first
        /// short. That is visible and correctable in the ledger rather than silently smoothed
        /// over, which is the trade this codebase makes everywhere else — a wrong number that
        /// looks right is worse than a right number that looks odd.
        /// </para>
        /// </summary>
        public static RentSchedule Build(RentScheduleInput input)
        {
            var asOf = input.AsOf.Date;

            // Newest start first, so overlapping tenancies resolve to the most recent one —
            // the same tie-break RentalPropertiesController.ActiveLeaseFor applies.
            var tenancies = input.Tenancies
                .OrderByDescending(t => t.StartDate.Date)
                .ThenByDescending(t => t.LeaseId)
                .ToList();

            // Never past today: a schedule of months that have not happened yet is not a
            // collection record, and every unbilled future month would read as arrears-in-waiting.
            var requestedTo = input.To?.Date ?? asOf;
            var to = requestedTo > asOf ? asOf : requestedTo;

            var from = input.From?.Date
                       ?? (tenancies.Count == 0 ? to : tenancies.Min(t => t.StartDate.Date));

            var firstMonth = FirstOfMonth(from);
            var lastMonth = FirstOfMonth(to);

            if (firstMonth > lastMonth)
                return Empty(input, asOf);

            if (MonthsBetween(firstMonth, lastMonth) + 1 > MaxPeriods)
                firstMonth = lastMonth.AddMonths(-(MaxPeriods - 1));

            var periods = new List<RentPeriod>();

            for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
            {
                periods.Add(BuildPeriod(month, tenancies, input.Payments, asOf));
            }

            var overdue = periods.Where(p => p.IsOverdue).ToList();

            return new RentSchedule
            {
                PropertyId = input.PropertyId,
                CurrencyCode = input.CurrencyCode,
                AsOf = asOf,
                Periods = periods,
                TotalExpected = periods.Sum(p => p.ExpectedAmount ?? 0m),
                TotalReceived = periods.Sum(p => p.ReceivedAmount),
                Arrears = overdue.Sum(p => p.Shortfall ?? 0m),
                OverduePeriodCount = overdue.Count,
                OldestOverduePeriod = overdue.FirstOrDefault()?.Period,
            };
        }

        private static RentPeriod BuildPeriod(
            DateTime month,
            List<ScheduledTenancy> tenancies,
            IReadOnlyList<RecordedRent> payments,
            DateTime asOf)
        {
            var monthEnd = month.AddMonths(1).AddDays(-1);

            var candidate = TenancyOverlapping(tenancies, month, monthEnd);

            // Due day 31 in a 30-day month falls on the 30th. Clamping rather than rolling into
            // the next month keeps every month's rent inside the month it belongs to.
            var dueDay = candidate?.RentDueDayOfMonth ?? 1;
            var dueDate = month.AddDays(Math.Clamp(dueDay, 1, DateTime.DaysInMonth(month.Year, month.Month)) - 1);

            // A tenancy that begins after its own due day owes nothing for that month: there is no
            // proration in this model, and billing a full month for a few days of occupancy would
            // invent arrears. The month reads as vacant, and the next one bills normally.
            ScheduledTenancy? billed = candidate is { } running && IsActiveOn(running, dueDate)
                ? running
                : null;

            var matched = payments
                .Where(p => p.Date.Date >= month && p.Date.Date <= monthEnd)
                .Where(p => p.LeaseId is null || billed is null || p.LeaseId == billed.Value.LeaseId)
                .ToList();

            var received = matched.Sum(p => p.Amount);
            var expected = billed?.MonthlyRent;

            decimal? shortfall = null;
            var status = RentPeriodStatus.Vacant;

            if (expected is { } owed)
            {
                shortfall = Math.Max(0m, owed - received);
                status = received >= owed
                    ? RentPeriodStatus.Paid
                    : received > 0m
                        ? RentPeriodStatus.Partial
                        : RentPeriodStatus.Unpaid;
            }

            return new RentPeriod
            {
                Period = PeriodKey(month),
                DueDate = dueDate,
                Status = status,
                ExpectedAmount = expected,
                ReceivedAmount = received,
                Shortfall = shortfall,

                // Due today is not late. Only a due date already in the past counts against the
                // landlord, which is what keeps the current month out of the arrears figure.
                IsOverdue = shortfall > 0m && dueDate < asOf,

                LeaseId = billed?.LeaseId,
                TenantName = billed?.TenantName,
                PaymentIds = matched.Select(p => p.TransactionId).ToList(),
            };
        }

        /// <summary>
        /// Tenancies are pre-sorted newest-start-first, so the first overlap is the winner.
        /// Returns null rather than <c>default</c> — a zeroed struct would read as a real tenancy
        /// charging nothing.
        /// </summary>
        private static ScheduledTenancy? TenancyOverlapping(
            List<ScheduledTenancy> tenancies, DateTime monthStart, DateTime monthEnd)
        {
            foreach (var tenancy in tenancies)
            {
                var startsInTime = tenancy.StartDate.Date <= monthEnd;
                var hasNotEnded = tenancy.EndDate is null || tenancy.EndDate.Value.Date >= monthStart;

                if (startsInTime && hasNotEnded)
                    return tenancy;
            }

            return null;
        }

        private static bool IsActiveOn(ScheduledTenancy tenancy, DateTime date) =>
            tenancy.StartDate.Date <= date && (tenancy.EndDate is null || tenancy.EndDate.Value.Date >= date);

        private static RentSchedule Empty(RentScheduleInput input, DateTime asOf) => new()
        {
            PropertyId = input.PropertyId,
            CurrencyCode = input.CurrencyCode,
            AsOf = asOf,
        };

        private static DateTime FirstOfMonth(DateTime date) => new(date.Year, date.Month, 1);

        private static int MonthsBetween(DateTime from, DateTime to) =>
            ((to.Year - from.Year) * 12) + to.Month - from.Month;

        /// <summary>The <c>yyyy-MM</c> key a month is identified by, everywhere.</summary>
        public static string PeriodKey(DateTime date) =>
            date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        /// <summary>
        /// Parses a <c>yyyy-MM</c> key back to the first of that month. Invariant and exact, so a
        /// server in a different locale cannot read the same URL as a different month.
        /// </summary>
        public static bool TryParsePeriod(string? period, out DateTime monthStart)
        {
            monthStart = default;

            if (string.IsNullOrWhiteSpace(period))
                return false;

            if (!DateTime.TryParseExact(
                    period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return false;
            }

            monthStart = new DateTime(parsed.Year, parsed.Month, 1);
            return true;
        }
    }
}
