using MoneyManager.Api.Controllers;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Analytics;
using MoneyManager.Api.Services.Currency;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// The consolidation boundary is the only place in the product where amounts in different
/// currencies meet, so it is the only place the "never add unlike amounts" rule can be
/// broken. These tests hold that line.
/// </summary>
public class PortfolioTotalsTests
{
    private static readonly DateOnly RateDate = new(2026, 7, 28);

    private static PropertyMetrics Metrics(
        int id, string currency, decimal cashInvested, decimal equity, decimal monthlyCashFlow) =>
        new()
        {
            PropertyId = id,
            PropertyName = $"Property {id}",
            CurrencyCode = currency,
            AsOf = new DateTime(2026, 7, 28),
            CashInvested = cashInvested,
            Equity = equity,
            MonthlyCashFlow = monthlyCashFlow,
            CurrentValue = equity,
            CumulativeNetCashFlow = 0m,
        };

    private static CurrencyConverter EurHuf(decimal rate = 400m) =>
        CurrencyConverter.FromRates([
            new ExchangeRate { FromCurrency = "EUR", ToCurrency = "HUF", Rate = rate, AsOf = RateDate },
        ]);

    [Fact]
    public void Single_currency_portfolio_totals_without_needing_any_rate()
    {
        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "EUR", 70_000m, 90_000m, 200m), Metrics(2, "EUR", 30_000m, 40_000m, 100m)],
            CurrencyConverter.Empty(),
            "EUR");

        Assert.Equal(100_000m, dto.TotalInvested);
        Assert.Equal(130_000m, dto.TotalEquity);
        Assert.Equal(300m, dto.TotalMonthlyCashFlow);
        Assert.False(dto.MixedCurrency);
        Assert.Null(dto.FxAsOf);          // no rate was applied
        Assert.Empty(dto.UnconvertedCurrencies);
    }

    [Fact]
    public void Mixed_currency_portfolio_totals_in_the_base_currency()
    {
        var dto = PortfolioAnalyticsDto.From(
            [
                Metrics(1, "EUR", 70_000m, 90_000m, 200m),
                Metrics(2, "HUF", 8_000_000m, 12_000_000m, 40_000m),
            ],
            EurHuf(),
            "EUR");

        // 8,000,000 HUF / 400 = 20,000 EUR
        Assert.Equal(90_000m, dto.TotalInvested);
        Assert.Equal(120_000m, dto.TotalEquity);   // 90,000 + 30,000
        Assert.Equal(300m, dto.TotalMonthlyCashFlow);
        Assert.True(dto.MixedCurrency);
        Assert.Equal("EUR", dto.Currency);
        Assert.Equal(RateDate, dto.FxAsOf);
    }

    [Fact]
    public void Totals_follow_the_users_chosen_base_currency()
    {
        var dto = PortfolioAnalyticsDto.From(
            [
                Metrics(1, "EUR", 70_000m, 90_000m, 200m),
                Metrics(2, "HUF", 8_000_000m, 12_000_000m, 40_000m),
            ],
            EurHuf(),
            "HUF");

        // 70,000 EUR x 400 = 28,000,000 HUF, plus 8,000,000
        Assert.Equal(36_000_000m, dto.TotalInvested);
        Assert.Equal("HUF", dto.Currency);
    }

    [Fact]
    public void A_missing_rate_withholds_the_total_and_names_the_currency()
    {
        var dto = PortfolioAnalyticsDto.From(
            [
                Metrics(1, "EUR", 70_000m, 90_000m, 200m),
                Metrics(2, "GBP", 50_000m, 60_000m, 150m),
            ],
            EurHuf(),          // knows nothing about GBP
            "EUR");

        // Totalling only the EUR property would read as a portfolio total without being one.
        Assert.Null(dto.TotalInvested);
        Assert.Null(dto.TotalEquity);
        Assert.Null(dto.PortfolioRoi);
        Assert.Equal(["GBP"], dto.UnconvertedCurrencies);

        // The per-property figures are unaffected — those never needed conversion.
        Assert.Equal(2, dto.Properties.Count);
    }

    [Fact]
    public void Portfolio_roi_is_computed_after_conversion_not_before()
    {
        var dto = PortfolioAnalyticsDto.From(
            [
                Metrics(1, "EUR", 50_000m, 75_000m, 200m),
                Metrics(2, "HUF", 20_000_000m, 30_000_000m, 40_000m),
            ],
            EurHuf(),
            "EUR");

        // Invested 50,000 + 50,000 = 100,000; equity 75,000 + 75,000 = 150,000.
        Assert.Equal(100_000m, dto.TotalInvested);
        Assert.Equal(150_000m, dto.TotalEquity);
        Assert.Equal(0.5m, dto.PortfolioRoi);
    }

    [Fact]
    public void An_empty_portfolio_reports_the_base_currency_and_no_totals()
    {
        var dto = PortfolioAnalyticsDto.From([], CurrencyConverter.Empty(), "EUR");

        Assert.Equal(0, dto.PropertyCount);
        Assert.Equal("EUR", dto.Currency);
        Assert.Null(dto.TotalInvested);
        Assert.False(dto.MixedCurrency);
    }

    [Fact]
    public void Properties_with_unknown_metrics_are_skipped_rather_than_counted_as_zero()
    {
        var withoutEquity = Metrics(2, "EUR", 30_000m, 0m, 100m) with { Equity = null };

        var dto = PortfolioAnalyticsDto.From(
            [Metrics(1, "EUR", 70_000m, 90_000m, 200m), withoutEquity],
            CurrencyConverter.Empty(),
            "EUR");

        Assert.Equal(100_000m, dto.TotalInvested);
        Assert.Equal(90_000m, dto.TotalEquity);   // not 90,000 + 0 from a property we cannot value
    }
}
