using MoneyManager.Api.Services.Agenda;
using MoneyManager.Api.Services.Rent;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// <see cref="AgendaBuilder"/> in isolation: no database, so every case here is a literal and the
/// due-date math it depends on (day-31 clamping, what counts as overdue) is proven once in
/// <c>RentScheduleBuilderTests</c> rather than re-derived here — this suite only checks what the
/// merge, the window and acknowledgement do with whatever <c>RentScheduleService</c> handed it.
/// </summary>
public sealed class AgendaBuilderTests
{
    private static readonly DateTime Today = new(2026, 3, 15);

    private static PropertyRentDue Rent(
        DateTime dueDate, bool isOverdue, int leaseId = 1, int propertyId = 1, decimal amount = 1_000m) => new()
    {
        PropertyId = propertyId,
        PropertyName = "Kerkstraat 8",
        CurrencyCode = "EUR",
        LeaseId = leaseId,
        TenantName = "Anna",
        DueDate = dueDate,
        AmountDue = amount,
        IsOverdue = isOverdue,
    };

    private static AgendaLoan Loan(DateTime dueDate, bool isPaidOff = false, int loanId = 1) => new(
        loanId, "Mortgage", "EUR", dueDate, 850m, isPaidOff, RentalPropertyId: 1);

    private static AgendaManualEvent Manual(DateTime eventDate, int id = 1) => new(
        id, "Insurance renewal", eventDate, RentalPropertyId: null, LoanId: null);

    private static AgendaInput InputWith(
        IReadOnlyList<PropertyRentDue>? rent = null,
        IReadOnlyList<AgendaLoan>? loans = null,
        IReadOnlyList<AgendaManualEvent>? manual = null,
        IReadOnlySet<string>? acknowledged = null,
        int days = 30) => new()
    {
        RentDue = rent ?? [],
        Loans = loans ?? [],
        ManualEvents = manual ?? [],
        AcknowledgedKeys = acknowledged ?? new HashSet<string>(),
        Days = days,
        Today = Today,
    };

    // ------------------------------------------------------------------
    // Rent
    // ------------------------------------------------------------------

    [Fact]
    public void A_lease_due_within_the_window_produces_one_entry()
    {
        var dueDate = new DateTime(2026, 3, 20);
        var entries = AgendaBuilder.Build(InputWith(rent: [Rent(dueDate, isOverdue: false)]));

        var only = Assert.Single(entries);
        Assert.Equal(AgendaSource.Rent, only.Source);
        Assert.Equal(dueDate, only.DueDate);
        Assert.Equal(1_000m, only.Amount);
        Assert.Equal("EUR", only.CurrencyCode);
        Assert.False(only.IsOverdue);
        Assert.Equal($"rent:1:{dueDate:yyyy-MM-dd}", only.Key);
        Assert.Equal(1, only.LeaseId);
        Assert.Equal(1, only.RentalPropertyId);
    }

    [Fact]
    public void Overdue_rent_shows_even_with_a_zero_day_window()
    {
        var dueDate = Today.AddDays(-40);
        var entries = AgendaBuilder.Build(InputWith(rent: [Rent(dueDate, isOverdue: true)], days: 0));

        var only = Assert.Single(entries);
        Assert.True(only.IsOverdue);
    }

    [Fact]
    public void Acknowledging_this_months_rent_does_not_hide_next_months()
    {
        var march = new DateTime(2026, 3, 5);
        var april = new DateTime(2026, 4, 5);

        var input = InputWith(
            rent: [Rent(march, isOverdue: true), Rent(april, isOverdue: false)],
            acknowledged: new HashSet<string> { $"rent:1:{march:yyyy-MM-dd}" },
            days: 60);

        var entries = AgendaBuilder.Build(input);

        // March is acknowledged and gone; April is a different key and still due.
        var only = Assert.Single(entries);
        Assert.Equal(april, only.DueDate);
    }

    // ------------------------------------------------------------------
    // Loans
    // ------------------------------------------------------------------

    [Fact]
    public void A_paid_off_loan_produces_nothing()
    {
        var entries = AgendaBuilder.Build(InputWith(loans: [Loan(Today.AddDays(5), isPaidOff: true)]));

        Assert.Empty(entries);
    }

    [Fact]
    public void An_open_loan_due_within_the_window_produces_one_entry()
    {
        var dueDate = Today.AddDays(10);
        var entries = AgendaBuilder.Build(InputWith(loans: [Loan(dueDate)]));

        var only = Assert.Single(entries);
        Assert.Equal(AgendaSource.Loan, only.Source);
        Assert.Equal("loan:1", only.Key);
        Assert.Equal(850m, only.Amount);
        Assert.False(only.IsOverdue);
    }

    [Fact]
    public void A_loan_due_today_is_included_and_not_overdue()
    {
        var entries = AgendaBuilder.Build(InputWith(loans: [Loan(Today)]));

        var only = Assert.Single(entries);
        Assert.False(only.IsOverdue);
    }

    [Fact]
    public void An_overdue_loan_shows_indefinitely_regardless_of_the_window()
    {
        var longOverdue = Today.AddDays(-400);
        var entries = AgendaBuilder.Build(InputWith(loans: [Loan(longOverdue)], days: 1));

        var only = Assert.Single(entries);
        Assert.True(only.IsOverdue);
    }

    [Fact]
    public void Acknowledging_a_loan_hides_it_with_no_date_in_the_key()
    {
        var entries = AgendaBuilder.Build(InputWith(
            loans: [Loan(Today.AddDays(-400))],
            acknowledged: new HashSet<string> { "loan:1" }));

        Assert.Empty(entries);
    }

    // ------------------------------------------------------------------
    // Manual events
    // ------------------------------------------------------------------

    [Fact]
    public void A_manual_event_within_the_window_still_appears()
    {
        var dueDate = Today.AddDays(3);
        var entries = AgendaBuilder.Build(InputWith(manual: [Manual(dueDate)]));

        var only = Assert.Single(entries);
        Assert.Equal(AgendaSource.Manual, only.Source);
        Assert.Equal("Insurance renewal", only.Title);
        Assert.Equal("manual:1", only.Key);
        Assert.Equal(1, only.UpcomingEventId);
        Assert.Null(only.Amount);
        Assert.Null(only.CurrencyCode);
    }

    [Fact]
    public void A_manual_event_outside_the_window_and_not_overdue_is_left_out()
    {
        var entries = AgendaBuilder.Build(InputWith(manual: [Manual(Today.AddDays(60))], days: 30));

        Assert.Empty(entries);
    }

    [Fact]
    public void An_overdue_manual_event_still_shows_outside_the_window()
    {
        var entries = AgendaBuilder.Build(InputWith(manual: [Manual(Today.AddDays(-90))], days: 7));

        var only = Assert.Single(entries);
        Assert.True(only.IsOverdue);
    }

    [Fact]
    public void Acknowledging_a_manual_event_hides_it()
    {
        var entries = AgendaBuilder.Build(InputWith(
            manual: [Manual(Today.AddDays(3))],
            acknowledged: new HashSet<string> { "manual:1" }));

        Assert.Empty(entries);
    }

    // ------------------------------------------------------------------
    // Merge and ordering
    // ------------------------------------------------------------------

    [Fact]
    public void The_merged_agenda_is_sorted_soonest_first_across_all_three_sources()
    {
        var rentDue = Today.AddDays(20);
        var loanDue = Today.AddDays(2);
        var manualDue = Today.AddDays(10);

        var input = InputWith(
            rent: [Rent(rentDue, isOverdue: false)],
            loans: [Loan(loanDue)],
            manual: [Manual(manualDue)],
            days: 30);

        var entries = AgendaBuilder.Build(input);

        Assert.Equal(
            new[] { AgendaSource.Loan, AgendaSource.Manual, AgendaSource.Rent },
            entries.Select(e => e.Source));
    }

    [Fact]
    public void A_negative_days_window_still_shows_only_what_is_overdue()
    {
        var input = InputWith(
            rent: [Rent(Today.AddDays(-5), isOverdue: true)],
            loans: [Loan(Today.AddDays(5))],
            days: -10);

        var entries = AgendaBuilder.Build(input);

        var only = Assert.Single(entries);
        Assert.Equal(AgendaSource.Rent, only.Source);
    }
}
