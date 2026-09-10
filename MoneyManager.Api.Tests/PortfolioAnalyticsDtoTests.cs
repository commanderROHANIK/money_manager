using MoneyManager.Api.Controllers;
using MoneyManager.Api.Services.Analytics;
using MoneyManager.Api.Services.Currency;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// Where consolidation actually happens. Conversion lives here, at the rollup, and nowhere else —
/// which is what lets the per-property calculator stay pure and FX-free.
///
/// <para>
/// Worked example used throughout: 1 EUR = 400 HUF, recorded 1 July 2026. A HUF property with
/// 18,600,000 invested and 49,200,000 equity, and a EUR property with 60,000 invested and
/// 150,000 equity. In EUR the portfolio is 18,600,000/400 + 60,000 = 106,500 invested and
/// 49,200,000/400 + 150,000 = 273,000 equity.
/// </para>
/// </summary>
public sealed class PortfolioAnalyticsDtoTests
{
    private static readonly DateTime AsOf = new(2026, 8, 1);
    private static readonly DateTime Quoted = new(2026, 7, 1);

    private static RollupContext Rollup(
        string baseCurrency = "EUR",
        bool alwaysConvert = false,
        params ExchangeRateSnapshot[] rates) =>
        new(new CurrencyConverter(rates), baseCurrency, alwaysConvert);

    private static ExchangeRateSnapshot EurHuf => new("EUR", "HUF", 400m, Quoted);

    private static PropertyMetrics Metrics(
        int id,
        string currency,
        decimal? cashInvested = null,
        decimal? equity = null,
        decimal? monthlyCashFlow = null,
        decimal? cumulativeNetCashFlow = null,
        decimal? annualRentUplift = null,
        decimal? contractedMonthlyRent = null) =>
        new()
        {
            PropertyId = id,
            PropertyName = $"Property {id}",
            CurrencyCode = currency,
            AsOf = AsOf,
            CashInvested = cashInvested,
            Equity = equity,
            MonthlyCashFlow = monthlyCashFlow,
            CumulativeNetCashFlow = cumulativeNetCashFlow,
            AnnualRentUplift = annualRentUplift,
            ContractedMonthlyRent = contractedMonthlyRent,
        };

    [Fact]
    public void A_single_currency_portfolio_totals_in_its_own_currency_with_no_rate_on_record()
    {
        // The default. A landlord whose properties are all in HUF must not need an exchange rate
        // to see a total, whatever their base currency happens to say.
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "HUF", cashInvested: 18_600_000m, equity: 49_200_000m),
             Metrics(2, "HUF", cashInvested: 10_000_000m, equity: 20_000_000m)],
            Rollup(baseCurrency: "EUR"));

        Assert.Equal("HUF", dto.Currency);
        Assert.False(dto.MixedCurrency);
        Assert.False(dto.Converted);
        Assert.Equal(28_600_000m, dto.TotalInvested);
        Assert.Equal(69_200_000m, dto.TotalEquity);
        Assert.Empty(dto.MissingRates);
        Assert.Empty(dto.Warnings);
    }

    [Fact]
    public void A_mixed_portfolio_with_a_rate_totals_in_the_base_currency_and_says_so()
    {
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "HUF", cashInvested: 18_600_000m, equity: 49_200_000m),
             Metrics(2, "EUR", cashInvested: 60_000m, equity: 150_000m)],
            Rollup("EUR", false, EurHuf));

        Assert.True(dto.MixedCurrency);
        Assert.True(dto.Converted);
        Assert.Equal("EUR", dto.Currency);
        Assert.Equal(106_500m, dto.TotalInvested);
        Assert.Equal(273_000m, dto.TotalEquity);
        Assert.Empty(dto.MissingRates);

        // The UI has to be able to show its working, so the rate that produced these is returned.
        var applied = Assert.Single(dto.AppliedRates);
        Assert.Equal("HUF", applied.From);
        Assert.Equal("EUR", applied.To);
        Assert.Equal<DateTime?>(Quoted, applied.AsOf);
    }

    [Fact]
    public void A_mixed_portfolio_with_no_rate_reports_nulls_and_names_the_missing_pair()
    {
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "HUF", cashInvested: 18_600_000m, equity: 49_200_000m),
             Metrics(2, "EUR", cashInvested: 60_000m, equity: 150_000m)],
            Rollup("EUR"));

        // Not a partial sum of the EUR half. Half a portfolio is not a portfolio total.
        Assert.Null(dto.TotalInvested);
        Assert.Null(dto.TotalEquity);
        Assert.Null(dto.PortfolioRoi);

        var missing = Assert.Single(dto.MissingRates);
        Assert.Equal("HUF", missing.From);
        Assert.Equal("EUR", missing.To);

        var warning = Assert.Single(dto.Warnings);
        Assert.Equal(CurrencyRollup.MissingRateWarningCode, warning.Code);
        Assert.Contains("HUF", warning.Message);
    }

    [Fact]
    public void Turning_on_always_convert_reports_a_single_currency_portfolio_in_the_base_currency()
    {
        var metrics = new[] { Metrics(1, "HUF", cashInvested: 18_600_000m, equity: 49_200_000m) };

        var dto = PortfolioAnalyticsDto.From(metrics, Rollup("EUR", true, EurHuf));

        Assert.False(dto.MixedCurrency);
        Assert.True(dto.Converted);
        Assert.Equal("EUR", dto.Currency);
        Assert.Equal(46_500m, dto.TotalInvested);
        Assert.Equal(123_000m, dto.TotalEquity);
    }

    [Fact]
    public void Turning_on_always_convert_without_a_rate_reports_unknown_rather_than_the_raw_figure()
    {
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "HUF", cashInvested: 18_600_000m)],
            Rollup("EUR", true));

        // The dangerous alternative is 18,600,000 rendered under a EUR label.
        Assert.Null(dto.TotalInvested);
        Assert.Single(dto.MissingRates);
    }

    [Fact]
    public void A_metric_nobody_recorded_is_still_skipped_rather_than_blocking_the_total()
    {
        // Unchanged from before conversion existed: a property with nothing to say about a
        // metric does not veto the others. Only an unconvertible figure does.
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "EUR", cashInvested: 60_000m),
             Metrics(2, "EUR", cashInvested: null)],
            Rollup("EUR"));

        Assert.Equal(60_000m, dto.TotalInvested);
        Assert.Null(dto.TotalMonthlyCashFlow);
    }

    [Fact]
    public void Roi_is_recomputed_from_the_converted_components()
    {
        // A ratio is never multiplied by an exchange rate. (273,000 + 1,000 - 106,500) / 106,500.
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "HUF", cashInvested: 18_600_000m, equity: 49_200_000m, cumulativeNetCashFlow: 400_000m),
             Metrics(2, "EUR", cashInvested: 60_000m, equity: 150_000m, cumulativeNetCashFlow: 0m)],
            Rollup("EUR", false, EurHuf));

        Assert.Equal(Math.Round((273_000m + 1_000m - 106_500m) / 106_500m, 4), dto.PortfolioRoi);
    }

    [Fact]
    public void Roi_is_unknown_when_a_component_could_not_be_converted()
    {
        // The trap this guards: an unconvertible cash-flow leg defaulting to zero and producing a
        // confident, wrong ROI.
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "EUR", cashInvested: 60_000m, equity: 150_000m, cumulativeNetCashFlow: 1_000m),
             Metrics(2, "HUF", cumulativeNetCashFlow: 400_000m)],
            Rollup("EUR"));

        Assert.Null(dto.PortfolioRoi);
    }

    [Fact]
    public void Per_property_metrics_are_passed_through_untouched_whether_or_not_a_rate_exists()
    {
        // Conversion happens at the rollup and nowhere else, so adding a rate must not move a
        // single figure on an individual property.
        var metrics = new[]
        {
            Metrics(1, "HUF", cashInvested: 18_600_000m, equity: 49_200_000m),
            Metrics(2, "EUR", cashInvested: 60_000m, equity: 150_000m),
        };

        var without = PortfolioAnalyticsDto.From(metrics, Rollup("EUR"));
        var with = PortfolioAnalyticsDto.From(metrics, Rollup("EUR", false, EurHuf));

        Assert.Equal(without.Properties, with.Properties);
        Assert.Equal("HUF", with.Properties[0].CurrencyCode);
        Assert.Equal(18_600_000m, with.Properties[0].CashInvested);
    }

    [Fact]
    public void TotalAnnualRentUplift_sums_only_underpriced_properties()
    {
        // One underpriced (+1,200/yr), one right at market (0), one overpriced (-600/yr, already
        // let above the market estimate) and one with no market estimate at all (null, skipped
        // like any other unrecorded metric). The total is 1,200 — the overpriced property's -600
        // must not net against it.
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "EUR", annualRentUplift: 1_200m),
             Metrics(2, "EUR", annualRentUplift: 0m),
             Metrics(3, "EUR", annualRentUplift: -600m),
             Metrics(4, "EUR", annualRentUplift: null)],
            Rollup("EUR"));

        Assert.Equal(1_200m, dto.TotalAnnualRentUplift);
    }

    [Fact]
    public void TotalAnnualRentUplift_is_null_when_nobody_is_underpriced()
    {
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "EUR", annualRentUplift: 0m), Metrics(2, "EUR", annualRentUplift: -600m)],
            Rollup("EUR"));

        // No property contributes, so this reads as "cannot be known" via CurrencyRollup.Sum's
        // own rule — the same "nobody said anything" case as `cashInvested` never being entered,
        // not a confident zero.
        Assert.Null(dto.TotalAnnualRentUplift);
    }

    [Fact]
    public void TotalMonthlyRent_sums_contracted_rent_and_skips_a_vacant_property()
    {
        // A vacant property carries no active lease, so ContractedMonthlyRent is null there — not
        // zero, the same "skipped rather than counted" rule every other rollup total follows for
        // a metric nobody has an answer for.
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "EUR", contractedMonthlyRent: 1_200m),
             Metrics(2, "EUR", contractedMonthlyRent: 800m),
             Metrics(3, "EUR", contractedMonthlyRent: null)],
            Rollup("EUR"));

        Assert.Equal(2_000m, dto.TotalMonthlyRent);
    }

    [Fact]
    public void TotalMonthlyRent_converts_across_currencies_like_every_other_total()
    {
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "HUF", contractedMonthlyRent: 400_000m),
             Metrics(2, "EUR", contractedMonthlyRent: 500m)],
            Rollup("EUR", false, EurHuf));

        Assert.Equal(1_500m, dto.TotalMonthlyRent);
    }

    [Fact]
    public void An_empty_portfolio_reports_no_totals_and_no_missing_rates()
    {
        var dto = PortfolioAnalyticsDto.From([], Rollup("EUR"));

        Assert.Equal(0, dto.PropertyCount);
        Assert.Null(dto.Currency);
        Assert.False(dto.MixedCurrency);
        Assert.False(dto.Converted);
        Assert.Null(dto.TotalInvested);
        Assert.Empty(dto.MissingRates);
        Assert.Empty(dto.Warnings);
    }
}
