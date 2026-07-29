using MoneyManager.Api.Services.Rent;
using Xunit;

namespace MoneyManager.Api.Tests;

public class RentScheduleCalculatorTests
{
    private static IReadOnlyList<RentPeriod> Generate(
        string start,
        string? end,
        decimal rent = 1_000m,
        int dueDay = 1,
        string upTo = "2026-06-30",
        IEnumerable<string>? existing = null) =>
        RentScheduleCalculator.Generate(
            DateTime.Parse(start),
            end is null ? null : DateTime.Parse(end),
            rent,
            dueDay,
            DateTime.Parse(upTo),
            existing?.Select(DateTime.Parse).ToHashSet());

    [Fact]
    public void A_tenancy_starting_on_the_first_owes_one_whole_month_per_month()
    {
        var periods = Generate("2026-01-01", null, upTo: "2026-03-15");

        Assert.Equal(3, periods.Count);
        Assert.All(periods, p => Assert.Equal(1_000m, p.AmountDue));
        Assert.All(periods, p => Assert.False(p.IsProrated));
    }

    [Fact]
    public void A_mid_month_start_is_prorated_by_days_covered()
    {
        var periods = Generate("2026-03-15", null, upTo: "2026-03-31");

        // 15th to 31st inclusive is 17 of March's 31 days: 1000 * 17 / 31.
        var first = Assert.Single(periods);
        Assert.Equal(548.39m, first.AmountDue);
        Assert.True(first.IsProrated);
        Assert.Equal(new DateTime(2026, 3, 15), first.PeriodStart);
        Assert.Equal(new DateTime(2026, 3, 31), first.PeriodEnd);
    }

    [Fact]
    public void The_month_after_a_prorated_start_is_charged_in_full()
    {
        var periods = Generate("2026-03-15", null, upTo: "2026-04-30");

        Assert.Equal(2, periods.Count);
        Assert.True(periods[0].IsProrated);
        Assert.Equal(1_000m, periods[1].AmountDue);
        Assert.Equal(new DateTime(2026, 4, 1), periods[1].PeriodStart);
    }

    [Fact]
    public void A_mid_month_end_is_prorated_and_nothing_follows_it()
    {
        var periods = Generate("2026-01-01", "2026-02-10", upTo: "2026-06-30");

        Assert.Equal(2, periods.Count);
        Assert.Equal(1_000m, periods[0].AmountDue);

        // 1st to 10th inclusive is 10 of February's 28 days.
        Assert.Equal(357.14m, periods[1].AmountDue);
        Assert.Equal(new DateTime(2026, 2, 10), periods[1].PeriodEnd);
    }

    [Fact]
    public void Regenerating_over_existing_periods_produces_nothing()
    {
        // The whole point: a nightly job must not charge the same month twice.
        var periods = Generate(
            "2026-01-01", null, upTo: "2026-03-15",
            existing: ["2026-01-01", "2026-02-01", "2026-03-01"]);

        Assert.Empty(periods);
    }

    [Fact]
    public void Regenerating_fills_only_the_gap()
    {
        var periods = Generate(
            "2026-01-01", null, upTo: "2026-03-15",
            existing: ["2026-01-01", "2026-03-01"]);

        var missing = Assert.Single(periods);
        Assert.Equal(new DateTime(2026, 2, 1), missing.PeriodStart);
    }

    [Fact]
    public void A_due_day_past_the_end_of_a_short_month_lands_on_its_last_day()
    {
        var periods = Generate("2026-02-01", "2026-02-28", dueDay: 31);

        // The 31st of February must mean the 28th, not the 3rd of March.
        var february = Assert.Single(periods);
        Assert.Equal(new DateTime(2026, 2, 28), february.DueDate);
    }

    [Fact]
    public void A_tenancy_starting_after_its_due_day_is_due_on_the_day_it_starts()
    {
        var periods = Generate("2026-03-15", null, dueDay: 1, upTo: "2026-03-31");

        // Rent cannot have been due on the 1st for a tenancy that began on the 15th.
        Assert.Equal(new DateTime(2026, 3, 15), periods[0].DueDate);
    }

    [Fact]
    public void Due_day_is_honoured_for_ordinary_months()
    {
        var periods = Generate("2026-01-01", null, dueDay: 5, upTo: "2026-02-28");

        Assert.Equal(new DateTime(2026, 1, 5), periods[0].DueDate);
        Assert.Equal(new DateTime(2026, 2, 5), periods[1].DueDate);
    }

    [Fact]
    public void A_leap_february_prorates_over_twenty_nine_days()
    {
        var periods = Generate("2028-02-01", "2028-02-15", upTo: "2028-06-30");

        // 2028 is a leap year: 15 of 29 days.
        var february = Assert.Single(periods);
        Assert.Equal(517.24m, february.AmountDue);
    }

    [Fact]
    public void A_tenancy_that_has_not_started_owes_nothing()
    {
        Assert.Empty(Generate("2027-01-01", null, upTo: "2026-06-30"));
    }

    [Fact]
    public void A_tenancy_ending_before_it_starts_owes_nothing()
    {
        Assert.Empty(Generate("2026-03-01", "2026-02-01"));
    }

    [Fact]
    public void A_zero_or_negative_rent_produces_no_charges()
    {
        Assert.Empty(Generate("2026-01-01", null, rent: 0m));
        Assert.Empty(Generate("2026-01-01", null, rent: -100m));
    }

    [Fact]
    public void A_tenancy_of_a_single_day_is_charged_for_that_day()
    {
        var periods = Generate("2026-03-10", "2026-03-10", upTo: "2026-06-30");

        var single = Assert.Single(periods);
        Assert.Equal(32.26m, single.AmountDue);   // 1000 / 31
        Assert.True(single.IsProrated);
    }

    [Fact]
    public void Generation_stops_at_the_horizon_rather_than_charging_the_future()
    {
        var periods = Generate("2026-01-01", null, upTo: "2026-03-15");

        Assert.Equal(3, periods.Count);
        Assert.DoesNotContain(periods, p => p.PeriodStart > new DateTime(2026, 3, 15));
    }

    [Fact]
    public void An_ended_tenancy_stops_accruing_after_its_last_period()
    {
        var periods = Generate("2026-01-01", "2026-02-28", upTo: "2026-12-31");

        Assert.Equal(2, periods.Count);
        Assert.Equal(new DateTime(2026, 2, 28), periods[^1].PeriodEnd);
    }
}
