using MoneyManager.Api.Services.Rent;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// The worked example below is computed by hand so the expected values are verifiable without
/// re-running the code under test.
///
///   Property 1, EUR. Evaluated as of 2025-12-15.
///
///   Tenancy 1  Anna   2025-01-01 .. 2025-06-30   1,000/month, due on the 5th
///   Tenancy 2  Béla   2025-08-15 .. open-ended   1,200/month, due on the 10th
///
///   Month     Billed to   Due       Expected   Received   Status    Shortfall
///   2025-01   Anna        Jan 5        1,000      1,000   Paid              0
///   2025-02   Anna        Feb 5        1,000        600   Partial         400
///   2025-03   Anna        Mar 5        1,000          0   Unpaid        1,000
///   2025-04   Anna        Apr 5        1,000      1,000   Paid              0
///   2025-05   Anna        May 5        1,000      1,000   Paid              0
///   2025-06   Anna        Jun 5        1,000      1,000   Paid              0
///   2025-07   —           Jul 1            —          0   Vacant            —
///   2025-08   —           Aug 10           —          0   Vacant            —
///   2025-09   Béla        Sep 10       1,200      1,200   Paid              0
///   2025-10   Béla        Oct 10       1,200          0   Unpaid        1,200
///   2025-11   Béla        Nov 10       1,200      1,200   Paid              0
///   2025-12   Béla        Dec 10       1,200      1,200   Paid              0
///
///   July is vacant because neither tenancy was running at all. August is vacant for a subtler
///   reason: Béla's tenancy begins on the 15th, after the 10th on which his rent falls due, and
///   there is no proration in this model — so that month bills nothing rather than inventing a
///   part-month charge or a full one.
///
///   Expected  6 x 1,000 + 4 x 1,200 = 10,800
///   Received  1,000 + 600 + 1,000 + 1,000 + 1,000 + 1,200 + 1,200 + 1,200 = 8,200
///   Arrears   400 (Feb) + 1,000 (Mar) + 1,200 (Oct) = 2,600, all past their due date
/// </summary>
public class RentScheduleBuilderTests
{
    private static readonly DateTime AsOf = new(2025, 12, 15);

    private const int Anna = 1;
    private const int Bela = 2;

    private static RentScheduleInput BaselineInput() => new()
    {
        PropertyId = 1,
        CurrencyCode = "EUR",
        AsOf = AsOf,
        Tenancies =
        [
            new ScheduledTenancy(Anna, "Anna", new DateTime(2025, 1, 1), new DateTime(2025, 6, 30), 1_000m, 5),
            new ScheduledTenancy(Bela, "Béla", new DateTime(2025, 8, 15), null, 1_200m, 10),
        ],
        Payments =
        [
            new RecordedRent(101, new DateTime(2025, 1, 5), 1_000m, Anna),
            new RecordedRent(102, new DateTime(2025, 2, 5), 600m, Anna),
            // April's arrives untagged, which is the common case for a payment typed straight
            // into the ledger rather than recorded against a month.
            new RecordedRent(103, new DateTime(2025, 4, 3), 1_000m, null),
            new RecordedRent(104, new DateTime(2025, 5, 5), 1_000m, Anna),
            new RecordedRent(105, new DateTime(2025, 6, 5), 1_000m, Anna),
            new RecordedRent(106, new DateTime(2025, 9, 10), 1_200m, Bela),
            new RecordedRent(107, new DateTime(2025, 11, 10), 1_200m, Bela),
            new RecordedRent(108, new DateTime(2025, 12, 10), 1_200m, Bela),
        ],
    };

    private static RentPeriod Month(RentSchedule schedule, string period) =>
        schedule.Periods.Single(p => p.Period == period);

    // ------------------------------------------------------------------
    // Shape of the schedule
    // ------------------------------------------------------------------

    [Fact]
    public void Runs_one_row_per_month_from_the_first_tenancy_to_today()
    {
        var schedule = RentScheduleBuilder.Build(BaselineInput());

        Assert.Equal(12, schedule.Periods.Count);
        Assert.Equal("2025-01", schedule.Periods[0].Period);
        Assert.Equal("2025-12", schedule.Periods[^1].Period);
    }

    [Fact]
    public void Never_runs_past_today_even_when_asked_to()
    {
        var input = BaselineInput() with { To = new DateTime(2027, 6, 1) };

        var schedule = RentScheduleBuilder.Build(input);

        // Months that have not happened are not a collection record, and every one of them would
        // otherwise read as rent about to go missing.
        Assert.Equal("2025-12", schedule.Periods[^1].Period);
    }

    [Fact]
    public void An_explicit_range_narrows_the_schedule()
    {
        var input = BaselineInput() with
        {
            From = new DateTime(2025, 3, 10),
            To = new DateTime(2025, 5, 20),
        };

        var schedule = RentScheduleBuilder.Build(input);

        Assert.Equal(new[] { "2025-03", "2025-04", "2025-05" }, schedule.Periods.Select(p => p.Period));
    }

    [Fact]
    public void A_property_with_no_tenancy_reports_the_current_month_as_vacant()
    {
        var input = new RentScheduleInput { PropertyId = 9, CurrencyCode = "EUR", AsOf = AsOf };

        var schedule = RentScheduleBuilder.Build(input);

        var only = Assert.Single(schedule.Periods);
        Assert.Equal("2025-12", only.Period);
        Assert.Equal(RentPeriodStatus.Vacant, only.Status);
    }

    // ------------------------------------------------------------------
    // Status of a single month
    // ------------------------------------------------------------------

    [Fact]
    public void A_month_paid_in_full_is_paid()
    {
        var january = Month(RentScheduleBuilder.Build(BaselineInput()), "2025-01");

        Assert.Equal(RentPeriodStatus.Paid, january.Status);
        Assert.Equal(1_000m, january.ExpectedAmount);
        Assert.Equal(1_000m, january.ReceivedAmount);
        Assert.Equal(0m, january.Shortfall);
        Assert.False(january.IsOverdue);
        Assert.Equal(new DateTime(2025, 1, 5), january.DueDate);
        Assert.Equal("Anna", january.TenantName);
    }

    [Fact]
    public void A_month_paid_short_is_partial_and_names_the_shortfall()
    {
        var february = Month(RentScheduleBuilder.Build(BaselineInput()), "2025-02");

        Assert.Equal(RentPeriodStatus.Partial, february.Status);
        Assert.Equal(600m, february.ReceivedAmount);
        Assert.Equal(400m, february.Shortfall);
        Assert.True(february.IsOverdue);
    }

    [Fact]
    public void A_month_with_nothing_against_it_is_unpaid_for_the_whole_rent()
    {
        var march = Month(RentScheduleBuilder.Build(BaselineInput()), "2025-03");

        Assert.Equal(RentPeriodStatus.Unpaid, march.Status);
        Assert.Equal(0m, march.ReceivedAmount);
        Assert.Equal(1_000m, march.Shortfall);
        Assert.Empty(march.PaymentIds);
    }

    [Fact]
    public void A_month_between_tenancies_is_vacant_rather_than_unpaid()
    {
        var july = Month(RentScheduleBuilder.Build(BaselineInput()), "2025-07");

        Assert.Equal(RentPeriodStatus.Vacant, july.Status);

        // Null, not zero. An empty property owing nothing and a let property owing nothing are
        // different facts, and a 0 here would make the first look like the second.
        Assert.Null(july.ExpectedAmount);
        Assert.Null(july.Shortfall);
        Assert.Null(july.LeaseId);
        Assert.False(july.IsOverdue);
    }

    [Fact]
    public void A_tenancy_beginning_after_its_own_due_day_bills_nothing_that_month()
    {
        var schedule = RentScheduleBuilder.Build(BaselineInput());

        // Béla moves in on 2025-08-15; his rent falls due on the 10th.
        Assert.Equal(RentPeriodStatus.Vacant, Month(schedule, "2025-08").Status);

        // ...and the following month bills normally, at his rent rather than Anna's.
        var september = Month(schedule, "2025-09");
        Assert.Equal(1_200m, september.ExpectedAmount);
        Assert.Equal("Béla", september.TenantName);
    }

    [Fact]
    public void Each_month_is_billed_at_the_rent_of_the_tenancy_running_then()
    {
        var schedule = RentScheduleBuilder.Build(BaselineInput());

        // The rent changed because the tenancy did, part-way through the year.
        Assert.Equal(1_000m, Month(schedule, "2025-06").ExpectedAmount);
        Assert.Equal(1_200m, Month(schedule, "2025-09").ExpectedAmount);
    }

    // ------------------------------------------------------------------
    // Matching payments to months
    // ------------------------------------------------------------------

    [Fact]
    public void An_untagged_payment_settles_whichever_tenancy_was_running()
    {
        var april = Month(RentScheduleBuilder.Build(BaselineInput()), "2025-04");

        Assert.Equal(RentPeriodStatus.Paid, april.Status);
        Assert.Equal(new[] { 103 }, april.PaymentIds);
    }

    [Fact]
    public void A_payment_tagged_to_another_tenancy_does_not_settle_this_month()
    {
        // Anna's tenancy has ended; a payment still tagged to her cannot pay off Béla's month.
        // Without this the two tenancies would launder rent between each other and an unpaid
        // month would quietly disappear.
        var input = BaselineInput() with
        {
            Payments = [.. BaselineInput().Payments, new RecordedRent(109, new DateTime(2025, 10, 10), 1_200m, Anna)],
        };

        var october = Month(RentScheduleBuilder.Build(input), "2025-10");

        Assert.Equal(RentPeriodStatus.Unpaid, october.Status);
        Assert.Equal(0m, october.ReceivedAmount);
    }

    [Fact]
    public void A_settled_month_names_the_ledger_rows_that_settled_it()
    {
        // RecordedRent has no category, so the builder cannot mistake a deposit for rent — the
        // narrowing happens in RentScheduleService, and RentScheduleServiceTests is what proves
        // it. If RecordedRent ever grows a category, that guarantee moves here.
        var settled = Month(RentScheduleBuilder.Build(BaselineInput()), "2025-01");

        Assert.Equal(new[] { 101 }, settled.PaymentIds);
    }

    [Fact]
    public void Overlapping_tenancies_resolve_to_the_newest_start()
    {
        // A handover entered as two overlapping tenancies, or a plain data-entry error. Either
        // way the newest one wins, matching RentalPropertiesController.ActiveLeaseFor.
        var input = BaselineInput() with
        {
            Tenancies =
            [
                new ScheduledTenancy(Anna, "Anna", new DateTime(2025, 1, 1), null, 1_000m, 5),
                new ScheduledTenancy(Bela, "Béla", new DateTime(2025, 3, 1), null, 1_500m, 5),
            ],
        };

        var april = Month(RentScheduleBuilder.Build(input), "2025-04");

        Assert.Equal(1_500m, april.ExpectedAmount);
        Assert.Equal("Béla", april.TenantName);
    }

    [Fact]
    public void A_due_day_past_the_end_of_a_short_month_is_clamped_into_it()
    {
        var input = BaselineInput() with
        {
            Tenancies = [new ScheduledTenancy(Anna, "Anna", new DateTime(2025, 1, 1), null, 1_000m, 31)],
            From = new DateTime(2025, 2, 1),
            To = new DateTime(2025, 2, 28),
        };

        var february = Month(RentScheduleBuilder.Build(input), "2025-02");

        // Clamped rather than rolled forward, so February's rent stays inside February.
        Assert.Equal(new DateTime(2025, 2, 28), february.DueDate);
    }

    // ------------------------------------------------------------------
    // Totals
    // ------------------------------------------------------------------

    [Fact]
    public void Totals_add_up_to_the_worked_example()
    {
        var schedule = RentScheduleBuilder.Build(BaselineInput());

        Assert.Equal(10_800m, schedule.TotalExpected);
        Assert.Equal(8_200m, schedule.TotalReceived);
    }

    [Fact]
    public void Arrears_count_only_months_already_past_their_due_date()
    {
        var schedule = RentScheduleBuilder.Build(BaselineInput());

        Assert.Equal(2_600m, schedule.Arrears);
        Assert.Equal(3, schedule.OverduePeriodCount);
        Assert.Equal("2025-02", schedule.OldestOverduePeriod);
    }

    [Fact]
    public void Rent_that_is_not_yet_due_is_not_arrears()
    {
        // Same data, read five days earlier: December's rent is unpaid but does not fall due
        // until the 10th, so it is not yet a debt.
        var input = BaselineInput() with
        {
            AsOf = new DateTime(2025, 12, 5),
            Payments = [.. BaselineInput().Payments.Where(p => p.TransactionId != 108)],
        };

        var schedule = RentScheduleBuilder.Build(input);

        Assert.Equal(RentPeriodStatus.Unpaid, Month(schedule, "2025-12").Status);
        Assert.False(Month(schedule, "2025-12").IsOverdue);
        Assert.Equal(2_600m, schedule.Arrears);
    }

    [Fact]
    public void A_month_due_today_is_not_yet_late()
    {
        var input = BaselineInput() with
        {
            AsOf = new DateTime(2025, 12, 10),
            Payments = [.. BaselineInput().Payments.Where(p => p.TransactionId != 108)],
        };

        Assert.False(Month(RentScheduleBuilder.Build(input), "2025-12").IsOverdue);
    }

    // ------------------------------------------------------------------
    // Period keys
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("2026-08", 2026, 8)]
    [InlineData("2025-01", 2025, 1)]
    public void A_period_key_round_trips(string period, int year, int month)
    {
        Assert.True(RentScheduleBuilder.TryParsePeriod(period, out var parsed));
        Assert.Equal(new DateTime(year, month, 1), parsed);
        Assert.Equal(period, RentScheduleBuilder.PeriodKey(parsed));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026")]
    [InlineData("2026-13")]
    [InlineData("August 2026")]
    [InlineData("2026-08-01")]
    public void A_malformed_period_key_is_rejected(string period)
    {
        Assert.False(RentScheduleBuilder.TryParsePeriod(period, out _));
    }
}
