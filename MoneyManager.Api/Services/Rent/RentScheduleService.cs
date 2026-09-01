using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Rent
{
    /// <summary>
    /// Assembles <see cref="RentScheduleBuilder"/>'s input from stored entities and nothing more.
    /// Every row is materialised before it is aggregated: SQLite has no decimal type, so summing
    /// a money column in SQL loses precision.
    /// </summary>
    public sealed class RentScheduleService(MoneyManagerDbContext context)
    {
        public async Task<RentSchedule?> GetForPropertyAsync(
            int propertyId, DateTime? from = null, DateTime? to = null, DateTime? asOf = null)
        {
            var property = await context.RentalProperties.FirstOrDefaultAsync(p => p.Id == propertyId);
            if (property is null)
                return null;

            var leases = await context.Leases
                .Where(l => l.RentalPropertyId == propertyId)
                .ToListAsync();

            // Deposits are the tenant's money held on their behalf and other income is not rent,
            // so only RentIncome is ever a candidate for paying a month off.
            var payments = await context.PropertyTransactions
                .Where(t => t.RentalPropertyId == propertyId && t.Category == TransactionCategory.RentIncome)
                .ToListAsync();

            return RentScheduleBuilder.Build(new RentScheduleInput
            {
                PropertyId = property.Id,
                CurrencyCode = property.CurrencyCode,
                Tenancies = ToTenancies(leases),
                Payments = ToPayments(payments),
                From = from,
                To = to,
                AsOf = (asOf ?? DateTime.UtcNow).Date,
            });
        }

        /// <summary>
        /// Only properties actually owing something come back. A caller wanting "how many are in
        /// arrears" counts the list; one wanting to badge a row looks its property up and finds
        /// nothing when it is square. Returning a zero row for every healthy property would make
        /// both of those read worse.
        /// </summary>
        public async Task<IReadOnlyList<PropertyArrears>> GetArrearsAsync(DateTime? asOf = null)
        {
            var effectiveAsOf = (asOf ?? DateTime.UtcNow).Date;

            var properties = await context.RentalProperties.ToListAsync();
            if (properties.Count == 0)
                return [];

            // Three queries regardless of portfolio size, rather than three per property.
            var ids = properties.Select(p => p.Id).ToList();

            var leases = await context.Leases
                .Where(l => ids.Contains(l.RentalPropertyId))
                .ToListAsync();

            var payments = await context.PropertyTransactions
                .Where(t => ids.Contains(t.RentalPropertyId) && t.Category == TransactionCategory.RentIncome)
                .ToListAsync();

            var arrears = new List<PropertyArrears>();

            foreach (var property in properties)
            {
                var schedule = RentScheduleBuilder.Build(new RentScheduleInput
                {
                    PropertyId = property.Id,
                    CurrencyCode = property.CurrencyCode,
                    Tenancies = ToTenancies(leases.Where(l => l.RentalPropertyId == property.Id)),
                    Payments = ToPayments(payments.Where(t => t.RentalPropertyId == property.Id)),
                    AsOf = effectiveAsOf,
                });

                if (schedule.OverduePeriodCount == 0)
                    continue;

                arrears.Add(new PropertyArrears
                {
                    PropertyId = property.Id,
                    PropertyName = property.PropertyName,
                    CurrencyCode = property.CurrencyCode,
                    Arrears = schedule.Arrears,
                    OverduePeriodCount = schedule.OverduePeriodCount,
                    OldestOverduePeriod = schedule.OldestOverduePeriod,
                });
            }

            // Worst first: the point of the list is to be acted on from the top.
            return arrears.OrderByDescending(a => a.OverduePeriodCount).ThenBy(a => a.PropertyName).ToList();
        }

        /// <summary>
        /// The current month's rent for every let property, for the due-date agenda.
        ///
        /// <para>
        /// One row per active lease, not one row per property in arrears: unlike
        /// <see cref="GetArrearsAsync"/>, which sums every overdue month a tenancy has ever
        /// missed, the agenda only ever asks "what does this lease owe for the period running
        /// right now" — a landlord already sees the full arrears history on the property's own
        /// rent-schedule page, and duplicating months of back-rent as separate agenda rows would
        /// make the reminder list unreadable rather than more complete.
        /// </para>
        ///
        /// <para>
        /// A property comes back only when the current period actually billed something and some
        /// of it is still unpaid — a vacant month, a month billed but already paid in full, and a
        /// month where the tenancy starts after its own due day (no proration, see
        /// <see cref="RentScheduleBuilder"/>) all produce nothing here, the same as they produce no
        /// arrears.
        /// </para>
        /// </summary>
        public async Task<IReadOnlyList<PropertyRentDue>> GetCurrentDueAsync(DateTime? asOf = null)
        {
            var effectiveAsOf = (asOf ?? DateTime.UtcNow).Date;
            var monthStart = new DateTime(effectiveAsOf.Year, effectiveAsOf.Month, 1);

            var properties = await context.RentalProperties.ToListAsync();
            if (properties.Count == 0)
                return [];

            var ids = properties.Select(p => p.Id).ToList();

            var leases = await context.Leases
                .Where(l => ids.Contains(l.RentalPropertyId))
                .ToListAsync();

            var payments = await context.PropertyTransactions
                .Where(t => ids.Contains(t.RentalPropertyId) && t.Category == TransactionCategory.RentIncome)
                .ToListAsync();

            var due = new List<PropertyRentDue>();

            foreach (var property in properties)
            {
                var schedule = RentScheduleBuilder.Build(new RentScheduleInput
                {
                    PropertyId = property.Id,
                    CurrencyCode = property.CurrencyCode,
                    Tenancies = ToTenancies(leases.Where(l => l.RentalPropertyId == property.Id)),
                    Payments = ToPayments(payments.Where(t => t.RentalPropertyId == property.Id)),
                    From = monthStart,
                    To = monthStart,
                    AsOf = effectiveAsOf,
                });

                var current = schedule.Periods.SingleOrDefault(p => p.Period == RentScheduleBuilder.PeriodKey(monthStart));

                if (current is not { LeaseId: { } leaseId, TenantName: { } tenantName, Shortfall: > 0m })
                    continue;

                due.Add(new PropertyRentDue
                {
                    PropertyId = property.Id,
                    PropertyName = property.PropertyName,
                    CurrencyCode = property.CurrencyCode,
                    LeaseId = leaseId,
                    TenantName = tenantName,
                    DueDate = current.DueDate,
                    AmountDue = current.Shortfall.Value,
                    IsOverdue = current.IsOverdue,
                });
            }

            return due;
        }

        private static List<ScheduledTenancy> ToTenancies(IEnumerable<Lease> leases) =>
            leases
                .Select(l => new ScheduledTenancy(
                    l.Id, l.TenantName, l.StartDate, l.EndDate, l.MonthlyRent, l.RentDueDayOfMonth))
                .ToList();

        private static List<RecordedRent> ToPayments(IEnumerable<PropertyTransaction> transactions) =>
            transactions
                .Select(t => new RecordedRent(t.Id, t.Date, t.Amount, t.LeaseId))
                .ToList();
    }
}
