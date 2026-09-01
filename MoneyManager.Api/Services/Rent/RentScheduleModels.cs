namespace MoneyManager.Api.Services.Rent
{
    /// <summary>A tenancy as the schedule needs it: when it ran, what it charged, and when it fell due.</summary>
    public readonly record struct ScheduledTenancy(
        int LeaseId,
        string TenantName,
        DateTime StartDate,
        DateTime? EndDate,
        decimal MonthlyRent,
        int RentDueDayOfMonth);

    /// <summary>
    /// A rent payment that is actually on the ledger. Only <c>RentIncome</c> ever becomes one of
    /// these — a deposit is the tenant's money held on their behalf, not rent, and counting it
    /// would make the month it arrived in look paid and the tenancy look more profitable than it is.
    /// </summary>
    public readonly record struct RecordedRent(int TransactionId, DateTime Date, decimal Amount, int? LeaseId);

    /// <summary>
    /// Everything the builder needs and nothing it does not — no DbContext, no clock. Time enters
    /// through <see cref="AsOf"/>, the same arrangement that makes
    /// <c>PropertyAnalyticsCalculator</c> checkable against a worked example.
    /// </summary>
    public sealed record RentScheduleInput
    {
        public required int PropertyId { get; init; }
        public required string CurrencyCode { get; init; }

        public IReadOnlyList<ScheduledTenancy> Tenancies { get; init; } = [];
        public IReadOnlyList<RecordedRent> Payments { get; init; } = [];

        /// <summary>Defaults to the month the earliest tenancy began.</summary>
        public DateTime? From { get; init; }

        /// <summary>Defaults to <see cref="AsOf"/>. Never runs past it — unbilled future months are noise.</summary>
        public DateTime? To { get; init; }

        public DateTime AsOf { get; init; } = DateTime.UtcNow.Date;
    }

    /// <summary>
    /// Ordered so a larger value means "further along": a caller wanting the worst months first
    /// can sort ascending without a lookup table.
    /// </summary>
    public enum RentPeriodStatus
    {
        /// <summary>No tenancy was running when rent would have fallen due. Nothing was owed.</summary>
        Vacant = 0,

        /// <summary>Rent was owed and nothing arrived.</summary>
        Unpaid = 1,

        /// <summary>Something arrived, but less than was owed.</summary>
        Partial = 2,

        /// <summary>The full amount arrived, or more.</summary>
        Paid = 3,
    }

    /// <summary>
    /// One month of one property's rent.
    ///
    /// <para>
    /// <see cref="ExpectedAmount"/> and <see cref="Shortfall"/> are null for a vacant month
    /// rather than zero, for the reason the whole codebase treats null that way: nothing was
    /// owed, which is a different fact from "nothing was owed and it was all paid". A vacant
    /// month showing a 0 shortfall alongside a let month showing a 0 shortfall would make an
    /// empty property look like a performing one.
    /// </para>
    /// </summary>
    public sealed record RentPeriod
    {
        /// <summary>The month, as <c>yyyy-MM</c>. Stable, sortable, and what the record endpoint takes.</summary>
        public required string Period { get; init; }

        /// <summary>The day rent fell due, with the tenancy's due day clamped into the month's length.</summary>
        public required DateTime DueDate { get; init; }

        public required RentPeriodStatus Status { get; init; }

        public decimal? ExpectedAmount { get; init; }

        /// <summary>Rent credited to this month. Reported even for a vacant month, so money is never hidden.</summary>
        public decimal ReceivedAmount { get; init; }

        /// <summary>What is still owed: expected less received, floored at zero. Null when nothing was owed.</summary>
        public decimal? Shortfall { get; init; }

        /// <summary>Past its due date and still short. A month due today is not yet late.</summary>
        public bool IsOverdue { get; init; }

        public int? LeaseId { get; init; }
        public string? TenantName { get; init; }

        /// <summary>The ledger rows credited here, so the UI can link a month to its transactions.</summary>
        public IReadOnlyList<int> PaymentIds { get; init; } = [];
    }

    /// <summary>
    /// <see cref="TotalExpected"/> and <see cref="TotalReceived"/> cover every month in range.
    /// <see cref="Arrears"/> deliberately does not: it counts only months already past their due
    /// date, so rent that is merely not yet due never reads as debt.
    /// </summary>
    public sealed record RentSchedule
    {
        public required int PropertyId { get; init; }
        public required string CurrencyCode { get; init; }
        public required DateTime AsOf { get; init; }

        public IReadOnlyList<RentPeriod> Periods { get; init; } = [];

        public decimal TotalExpected { get; init; }
        public decimal TotalReceived { get; init; }

        public decimal Arrears { get; init; }
        public int OverduePeriodCount { get; init; }

        /// <summary>The earliest month still owing, as <c>yyyy-MM</c>. Null when nothing is overdue.</summary>
        public string? OldestOverduePeriod { get; init; }
    }

    /// <summary>What one property owes, for the list and dashboard rollups.</summary>
    public sealed record PropertyArrears
    {
        public required int PropertyId { get; init; }
        public required string PropertyName { get; init; }
        public required string CurrencyCode { get; init; }
        public required decimal Arrears { get; init; }
        public required int OverduePeriodCount { get; init; }
        public string? OldestOverduePeriod { get; init; }
    }

    /// <summary>
    /// The current month's rent for one let property — the one row the due-date agenda
    /// (<c>Services.Agenda.AgendaBuilder</c>) needs per active lease.
    ///
    /// <para>
    /// Only produced for a month something was actually billed: a vacant month, or one where the
    /// tenancy running started after its own due day, has no <see cref="RentSchedule.Periods"/>
    /// entry worth turning into a reminder, which is why <c>RentScheduleService</c> only ever
    /// returns one of these when <see cref="AmountDue"/> would be positive-or-already-settled — see
    /// its docblock for the exact rule.
    /// </para>
    ///
    /// <para>
    /// <see cref="AmountDue"/> is what is still owed for the month — <c>RentPeriod.Shortfall</c> —
    /// rather than the full monthly rent, so a partly-paid month reads as the remainder still due
    /// and a fully-paid one produces no row at all rather than a reminder for money already in.
    /// </para>
    /// </summary>
    public sealed record PropertyRentDue
    {
        public required int PropertyId { get; init; }
        public required string PropertyName { get; init; }
        public required string CurrencyCode { get; init; }
        public required int LeaseId { get; init; }
        public required string TenantName { get; init; }
        public required DateTime DueDate { get; init; }
        public required decimal AmountDue { get; init; }
        public required bool IsOverdue { get; init; }
    }
}
