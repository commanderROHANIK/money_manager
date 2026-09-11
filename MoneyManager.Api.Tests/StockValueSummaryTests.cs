using MoneyManager.Api.Controllers;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Currency;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// The stocks equivalent of <see cref="BankBalanceSummaryTests"/>: before this endpoint existed,
/// widgets summed <c>SharesOwned * CurrentPrice</c> across holdings client-side while ignoring
/// <c>CurrencyCode</c>, so a USD holding beside a HUF one produced a confident nonsense number.
/// These tests pin the replacement: an exact per-currency breakdown always, and a single headline
/// figure only when a rate exists to justify it.
/// </summary>
public sealed class StockValueSummaryTests
{
    private static readonly DateTime Quoted = new(2026, 7, 1);

    private static RollupContext Rollup(
        string baseCurrency = "EUR",
        bool alwaysConvert = false,
        params ExchangeRateSnapshot[] rates) =>
        new(new CurrencyConverter(rates), baseCurrency, alwaysConvert);

    private static ExchangeRateSnapshot EurHuf => new("EUR", "HUF", 400m, Quoted);

    private static Stock Holding(int shares, decimal currentPrice, string currency) =>
        new() { Ticker = "TICK", SharesOwned = shares, CurrentPrice = currentPrice, CurrencyCode = currency };

    [Fact]
    public void Holdings_sharing_a_currency_total_in_it_without_needing_a_rate()
    {
        var summary = StockValueSummaryDto.From(
            [Holding(12, 164_500m, "HUF"), Holding(40, 21_850m, "HUF")],
            Rollup(baseCurrency: "EUR"));

        Assert.Equal(12 * 164_500m + 40 * 21_850m, summary.TotalValue);
        Assert.Equal("HUF", summary.Currency);
        Assert.False(summary.MixedCurrency);
        Assert.False(summary.Converted);
        Assert.Empty(summary.MissingRates);
    }

    [Fact]
    public void Holdings_in_different_currencies_are_converted_rather_than_added_blind()
    {
        // The bug this replaces would have answered a raw sum of HUF and EUR figures.
        var summary = StockValueSummaryDto.From(
            [Holding(10, 100_000m, "HUF"), Holding(5, 400m, "EUR")],
            Rollup("EUR", false, EurHuf));

        Assert.True(summary.MixedCurrency);
        Assert.True(summary.Converted);
        Assert.Equal("EUR", summary.Currency);
        Assert.Equal(2_500m + 2_000m, summary.TotalValue);
    }

    [Fact]
    public void Without_a_rate_the_headline_is_unknown_but_the_breakdown_is_still_exact()
    {
        var summary = StockValueSummaryDto.From(
            [Holding(10, 100_000m, "HUF"), Holding(5, 400m, "EUR")],
            Rollup("EUR"));

        Assert.Null(summary.TotalValue);

        Assert.Collection(
            summary.ByCurrency,
            eur => { Assert.Equal("EUR", eur.CurrencyCode); Assert.Equal(2_000m, eur.Total); },
            huf => { Assert.Equal("HUF", huf.CurrencyCode); Assert.Equal(1_000_000m, huf.Total); });

        var missing = Assert.Single(summary.MissingRates);
        Assert.Equal("HUF", missing.From);
        Assert.Equal("EUR", missing.To);
        Assert.Single(summary.Warnings);
    }

    [Fact]
    public void No_holdings_is_zero_held_rather_than_unknown()
    {
        var summary = StockValueSummaryDto.From([], Rollup("EUR"));

        Assert.Equal(0m, summary.TotalValue);
        Assert.Equal("EUR", summary.Currency);
        Assert.Empty(summary.ByCurrency);
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void Always_convert_reports_a_single_currency_value_in_the_base_currency()
    {
        var summary = StockValueSummaryDto.From(
            [Holding(10, 100_000m, "HUF")],
            Rollup("EUR", true, EurHuf));

        Assert.False(summary.MixedCurrency);
        Assert.True(summary.Converted);
        Assert.Equal("EUR", summary.Currency);
        Assert.Equal(2_500m, summary.TotalValue);
    }
}
