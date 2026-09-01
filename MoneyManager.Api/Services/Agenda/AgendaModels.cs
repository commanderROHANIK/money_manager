using MoneyManager.Api.Services.Rent;

namespace MoneyManager.Api.Services.Agenda
{
    /// <summary>Where one agenda entry came from — what kind of row produced it, not who owns it.</summary>
    public enum AgendaSource
    {
        /// <summary>A hand-typed <c>UpcomingEvent</c>.</summary>
        Manual,

        /// <summary>Derived from the lease running today and the rent schedule it produces.</summary>
        Rent,

        /// <summary>Derived from an open <c>Loan</c>.</summary>
        Loan,
    }

    /// <summary>
    /// One row on the due-date agenda. Never stored — built fresh on every request from whatever
    /// is true right now, the same as <c>RentSchedule</c> and <c>PropertyAnalyticsCalculator</c>'s
    /// output.
    ///
    /// <para>
    /// <see cref="Key"/> is the one field callers depend on being stable across requests: it is
    /// what <c>Services.Agenda.AgendaService.AcknowledgeAsync</c> stores, and what a future
    /// request compares its freshly-derived entries against to decide which ones to leave out.
    /// </para>
    ///
    /// <para>
    /// <see cref="Amount"/> and <see cref="CurrencyCode"/> are nullable together — a manual event
    /// carries no amount at all, and never sums across currencies, so each entry states its own
    /// currency rather than assuming the caller's.
    /// </para>
    /// </summary>
    public sealed record AgendaEntry
    {
        public required string Key { get; init; }
        public required AgendaSource Source { get; init; }
        public required string Title { get; init; }
        public required DateTime DueDate { get; init; }

        public decimal? Amount { get; init; }
        public string? CurrencyCode { get; init; }

        /// <summary>Past its due date. Always shown, regardless of the requested window.</summary>
        public required bool IsOverdue { get; init; }

        public int? RentalPropertyId { get; init; }
        public string? PropertyName { get; init; }
        public int? LeaseId { get; init; }
        public int? LoanId { get; init; }
        public int? UpcomingEventId { get; init; }
    }

    /// <summary>An open loan as the agenda needs it — the fields <c>AgendaBuilder</c> derives a due-date entry from.</summary>
    public sealed record AgendaLoan(
        int LoanId,
        string LoanName,
        string CurrencyCode,
        DateTime DueDate,
        decimal? MonthlyPayment,
        bool IsPaidOff,
        int? RentalPropertyId);

    /// <summary>A manual reminder as the agenda needs it.</summary>
    public sealed record AgendaManualEvent(
        int Id,
        string Title,
        DateTime EventDate,
        int? RentalPropertyId,
        int? LoanId);

    /// <summary>
    /// Everything <see cref="AgendaBuilder"/> needs and nothing it does not — no DbContext, no
    /// clock. Time enters through <see cref="Today"/>, the same arrangement that makes
    /// <c>RentScheduleBuilder</c> and <c>PropertyAnalyticsCalculator</c> checkable against literals.
    /// </summary>
    public sealed record AgendaInput
    {
        /// <summary>One row per active lease, already resolved to the period running today — see <see cref="PropertyRentDue"/>.</summary>
        public IReadOnlyList<PropertyRentDue> RentDue { get; init; } = [];

        public IReadOnlyList<AgendaLoan> Loans { get; init; } = [];

        public IReadOnlyList<AgendaManualEvent> ManualEvents { get; init; } = [];

        /// <summary>Keys already acknowledged. An entry whose key is in here is left out entirely.</summary>
        public IReadOnlySet<string> AcknowledgedKeys { get; init; } = new HashSet<string>();

        /// <summary>How far ahead to look for entries that are not yet due. Never bounds an overdue one.</summary>
        public int Days { get; init; } = 30;

        public DateTime Today { get; init; } = DateTime.UtcNow.Date;
    }
}
