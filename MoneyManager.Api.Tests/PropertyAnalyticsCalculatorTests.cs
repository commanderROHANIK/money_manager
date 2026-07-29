using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Analytics;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// The worked example below is computed by hand in the comments so the expected values are
/// verifiable without re-running the code they are testing.
///
///   Apartment bought 2024-01-01 for 200,000 EUR
///   Acquisition costs                    8,000
///   Capital improvement (2024-02-01)    12,000
///   Mortgage: 150,000 original, 140,000 outstanding, 800/month
///   Let at 1,200/month from 2024-01-01, still occupied
///   Valued at 230,000 on 2025-12-01
///   Market rent estimated at 1,400/month
///   Evaluated as of 2026-01-01
///
///   Trailing twelve months (2025-01-02 .. 2026-01-01):
///     rent received     12 x 1,200 = 14,400
///     operating costs   600 + 1,200 + 800 = 2,600
///     mortgage paid     12 x 800  =  9,600
/// </summary>
public class PropertyAnalyticsCalculatorTests
{
    private static readonly DateTime AsOf = new(2026, 1, 1);
    private static readonly DateTime Purchased = new(2024, 1, 1);

    private static PropertyAnalyticsInput BaselineInput()
    {
        var transactions = new List<LedgerEntry>
        {
            new(Purchased, 8_000m, TransactionCategory.AcquisitionCost),
            new(new DateTime(2024, 2, 1), 12_000m, TransactionCategory.CapitalImprovement),
            new(new DateTime(2025, 6, 1), 600m, TransactionCategory.Insurance),
            new(new DateTime(2025, 6, 1), 1_200m, TransactionCategory.PropertyTax),
            new(new DateTime(2025, 6, 1), 800m, TransactionCategory.Maintenance),
        };

        // Twelve rent and twelve mortgage payments, all strictly inside the trailing year.
        for (var month = 1; month <= 12; month++)
        {
            var date = new DateTime(2025, 1, 1).AddMonths(month);
            transactions.Add(new LedgerEntry(date, 1_200m, TransactionCategory.RentIncome));
            transactions.Add(new LedgerEntry(date, 800m, TransactionCategory.MortgagePayment));
        }

        return new PropertyAnalyticsInput
        {
            PropertyId = 1,
            PropertyName = "Test Apartment",
            CurrencyCode = "EUR",
            PurchasePrice = 200_000m,
            PurchaseDate = Purchased,
            CurrentValuation = 230_000m,
            ValuationDate = new DateTime(2025, 12, 1),
            ActiveMonthlyRent = 1_200m,
            MarketMonthlyRent = 1_400m,
            MortgageOriginalAmount = 150_000m,
            MortgageBalance = 140_000m,
            MortgageMonthlyPayment = 800m,
            Transactions = transactions,
            Occupancy = [new OccupancyWindow(Purchased, null)],
            AsOf = AsOf,
        };
    }

    // 200,000 + 8,000 + 12,000
    [Fact]
    public void TotalInvested_is_purchase_price_plus_acquisition_costs_and_capital_spend()
    {
        Assert.Equal(220_000m, PropertyAnalyticsCalculator.Compute(BaselineInput()).TotalInvested);
    }

    // 220,000 - 150,000 borrowed
    [Fact]
    public void CashInvested_excludes_the_borrowed_portion()
    {
        Assert.Equal(70_000m, PropertyAnalyticsCalculator.Compute(BaselineInput()).CashInvested);
    }

    [Fact]
    public void Income_and_running_costs_are_taken_from_the_trailing_year()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput());

        Assert.Equal(14_400m, metrics.AnnualRentalIncome);
        Assert.Equal(2_600m, metrics.AnnualOperatingExpenses);
        Assert.Equal(11_800m, metrics.NetOperatingIncome);   // 14,400 - 2,600
        Assert.Equal(9_600m, metrics.AnnualDebtService);
    }

    // (11,800 - 9,600) / 12
    [Fact]
    public void MonthlyCashFlow_is_operating_income_after_debt_service()
    {
        Assert.Equal(183.33m, PropertyAnalyticsCalculator.Compute(BaselineInput()).MonthlyCashFlow);
    }

    [Fact]
    public void Yields_use_invested_capital_and_cap_rate_uses_market_value()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput());

        Assert.Equal(0.0655m, metrics.GrossYield);   // 14,400 / 220,000
        Assert.Equal(0.0536m, metrics.NetYield);     // 11,800 / 220,000
        Assert.Equal(0.0513m, metrics.CapRate);      // 11,800 / 230,000
        Assert.Equal(0.0314m, metrics.CashOnCashReturn); // 2,200 / 70,000
    }

    [Fact]
    public void Cap_rate_ignores_financing_so_it_stays_comparable_between_properties()
    {
        var geared = BaselineInput();
        var ungeared = geared with
        {
            MortgageOriginalAmount = null,
            MortgageBalance = null,
            MortgageMonthlyPayment = null,
            Transactions = geared.Transactions
                .Where(t => t.Category != TransactionCategory.MortgagePayment)
                .ToList(),
        };

        Assert.Equal(
            PropertyAnalyticsCalculator.Compute(geared).CapRate,
            PropertyAnalyticsCalculator.Compute(ungeared).CapRate);
    }

    [Fact]
    public void Equity_and_appreciation_come_from_the_latest_valuation()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput());

        Assert.Equal(230_000m, metrics.CurrentValue);
        Assert.Equal(90_000m, metrics.Equity);            // 230,000 - 140,000
        Assert.Equal(30_000m, metrics.Appreciation);      // 230,000 - 200,000
        Assert.Equal(0.15m, metrics.AppreciationPercent);
    }

    [Fact]
    public void Total_return_combines_equity_gain_with_cash_collected()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput());

        // 14,400 - 2,600 - 9,600
        Assert.Equal(2_200m, metrics.CumulativeNetCashFlow);

        // equity 90,000 + cash 2,200 - cash invested 70,000
        Assert.Equal(22_200m, metrics.TotalReturn);
        Assert.Equal(0.3171m, metrics.TotalRoi);          // 22,200 / 70,000
    }

    [Fact]
    public void Annualized_return_unwinds_the_holding_period()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput());

        // 731 days held / 365 — 2024 was a leap year.
        Assert.Equal(2.0027m, metrics.YearsHeld);

        // 1.3171 ^ (1 / 2.0027) - 1
        Assert.NotNull(metrics.AnnualizedRoi);
        Assert.InRange(metrics.AnnualizedRoi!.Value, 0.146m, 0.149m);
    }

    [Fact]
    public void Continuously_let_property_reports_full_occupancy()
    {
        Assert.Equal(1m, PropertyAnalyticsCalculator.Compute(BaselineInput()).OccupancyRate);
    }

    [Fact]
    public void Rent_gap_against_market_is_reported_with_the_annual_uplift()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput());

        Assert.Equal(0.1429m, metrics.RentGapPercent);   // (1,400 - 1,200) / 1,400
        Assert.Equal(2_400m, metrics.AnnualRentUplift);  // 200 x 12
    }

    // ---------------------------------------------------------------
    // Degenerate inputs. A real user's first property has most of these gaps.
    // ---------------------------------------------------------------

    [Fact]
    public void Missing_purchase_price_yields_null_rather_than_zero_and_warns()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(
            BaselineInput() with { PurchasePrice = null });

        Assert.Null(metrics.Appreciation);
        Assert.Null(metrics.TotalRoi);
        Assert.Contains(metrics.Warnings, w => w.Code == "NoPurchasePrice");
    }

    [Fact]
    public void Property_bought_outright_still_reports_returns_and_notes_the_absent_mortgage()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput() with
        {
            MortgageOriginalAmount = null,
            MortgageBalance = null,
            MortgageMonthlyPayment = null,
            Transactions = BaselineInput().Transactions
                .Where(t => t.Category != TransactionCategory.MortgagePayment)
                .ToList(),
        });

        Assert.Equal(220_000m, metrics.CashInvested);
        Assert.Equal(230_000m, metrics.Equity);
        Assert.Contains(metrics.Warnings, w => w.Code == "NoMortgage");
    }

    [Fact]
    public void Fully_financed_purchase_does_not_divide_by_zero()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(
            BaselineInput() with { MortgageOriginalAmount = 220_000m });

        Assert.Null(metrics.CashInvested);
        Assert.Null(metrics.CashOnCashReturn);
        Assert.Null(metrics.TotalRoi);
        Assert.Contains(metrics.Warnings, w => w.Code == "NonPositiveCashInvested");
    }

    [Fact]
    public void Absent_valuation_falls_back_to_purchase_price_and_says_so()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(
            BaselineInput() with { CurrentValuation = null });

        Assert.Equal(200_000m, metrics.CurrentValue);
        Assert.Equal(0m, metrics.Appreciation);
        Assert.Contains(metrics.Warnings, w => w.Code == "NoValuation");
    }

    [Fact]
    public void Short_history_is_scaled_to_a_year_and_flagged_as_extrapolated()
    {
        // Bought three months ago with a single month's rent recorded.
        var recent = new DateTime(2025, 10, 1);
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput() with
        {
            PurchaseDate = recent,
            Transactions =
            [
                new LedgerEntry(new DateTime(2025, 11, 1), 1_200m, TransactionCategory.RentIncome),
            ],
        });

        Assert.Contains(metrics.Warnings, w => w.Code == "ExtrapolatedIncome");
        Assert.NotNull(metrics.AnnualRentalIncome);
        Assert.True(metrics.AnnualRentalIncome > 1_200m,
            "one month of rent should be scaled up towards an annual figure");
    }

    [Fact]
    public void With_no_payments_recorded_contracted_rent_is_used_and_disclosed()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(
            BaselineInput() with { Transactions = [] });

        Assert.Equal(14_400m, metrics.AnnualRentalIncome);  // 1,200 x 12 contracted
        Assert.Contains(metrics.Warnings, w => w.Code == "ScheduledRentUsed");
    }

    [Fact]
    public void Vacant_property_with_no_history_reports_no_income_rather_than_zero_yield()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput() with
        {
            Transactions = [],
            ActiveMonthlyRent = null,
        });

        Assert.Null(metrics.AnnualRentalIncome);
        Assert.Null(metrics.GrossYield);
        Assert.Null(metrics.CapRate);
        Assert.Contains(metrics.Warnings, w => w.Code == "NoIncomeData");
    }

    [Fact]
    public void Sold_property_is_valued_at_its_sale_price_and_stops_accruing_time()
    {
        var soldOn = new DateTime(2025, 1, 1);
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput() with
        {
            Status = PropertyStatus.Sold,
            SalePrice = 250_000m,
            SaleDate = soldOn,
        });

        Assert.Equal(250_000m, metrics.CurrentValue);

        // 2024-01-01 to 2025-01-01 is 366 days because 2024 was a leap year: 366 / 365.
        // The clock stops at the sale rather than running on to AsOf.
        Assert.Equal(1.0027m, metrics.YearsHeld);
    }

    [Fact]
    public void Deposits_are_not_counted_as_income_because_they_are_repayable()
    {
        var withDeposit = BaselineInput();
        var metrics = PropertyAnalyticsCalculator.Compute(withDeposit with
        {
            Transactions = withDeposit.Transactions
                .Append(new LedgerEntry(new DateTime(2025, 3, 1), 2_400m, TransactionCategory.DepositReceived))
                .ToList(),
        });

        Assert.Equal(14_400m, metrics.AnnualRentalIncome);
        Assert.Equal(2_200m, metrics.CumulativeNetCashFlow);
    }

    [Fact]
    public void Overlapping_tenancies_cannot_push_occupancy_above_one()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput() with
        {
            Occupancy =
            [
                new OccupancyWindow(Purchased, new DateTime(2025, 6, 30)),
                new OccupancyWindow(new DateTime(2025, 6, 1), null),
            ],
        });

        Assert.Equal(1m, metrics.OccupancyRate);
    }

    [Fact]
    public void Vacancy_between_tenancies_reduces_occupancy()
    {
        var metrics = PropertyAnalyticsCalculator.Compute(BaselineInput() with
        {
            Occupancy =
            [
                // Let for one of the two years held.
                new OccupancyWindow(Purchased, new DateTime(2025, 1, 1)),
            ],
        });

        Assert.NotNull(metrics.OccupancyRate);
        Assert.InRange(metrics.OccupancyRate!.Value, 0.49m, 0.51m);
    }

    [Fact]
    public void Capital_spend_raises_invested_capital_but_not_running_costs()
    {
        var baseline = PropertyAnalyticsCalculator.Compute(BaselineInput());

        var input = BaselineInput();
        var improved = PropertyAnalyticsCalculator.Compute(input with
        {
            Transactions = input.Transactions
                .Append(new LedgerEntry(new DateTime(2025, 5, 1), 10_000m, TransactionCategory.CapitalImprovement))
                .ToList(),
        });

        Assert.Equal(baseline.TotalInvested + 10_000m, improved.TotalInvested);
        Assert.Equal(baseline.AnnualOperatingExpenses, improved.AnnualOperatingExpenses);
        Assert.Equal(baseline.NetOperatingIncome, improved.NetOperatingIncome);
    }
}
