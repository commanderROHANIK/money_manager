using MoneyManager.Api.Controllers;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Currency;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// The endpoint behind these used to add <c>Balance</c> across accounts while ignoring
/// <c>CurrencyCode</c>, so a EUR account beside a HUF one produced a confident nonsense number.
/// These tests pin the replacement: an exact per-currency breakdown always, and a single headline
/// figure only when a rate exists to justify it.
/// </summary>
public sealed class BankBalanceSummaryTests
{
    private static readonly DateTime Quoted = new(2026, 7, 1);

    private static RollupContext Rollup(
        string baseCurrency = "EUR",
        bool alwaysConvert = false,
        params ExchangeRateSnapshot[] rates) =>
        new(new CurrencyConverter(rates), baseCurrency, alwaysConvert);

    private static ExchangeRateSnapshot EurHuf => new("EUR", "HUF", 400m, Quoted);

    private static BankAccount Account(decimal balance, string currency) =>
        new() { AccountName = "Account", Balance = balance, CurrencyCode = currency };

    [Fact]
    public void Accounts_sharing_a_currency_total_in_it_without_needing_a_rate()
    {
        var summary = BankBalanceSummaryDto.From(
            [Account(412_350m, "HUF"), Account(1_850_000m, "HUF")],
            Rollup(baseCurrency: "EUR"));

        Assert.Equal(2_262_350m, summary.TotalBalance);
        Assert.Equal("HUF", summary.Currency);
        Assert.False(summary.MixedCurrency);
        Assert.False(summary.Converted);
        Assert.Empty(summary.MissingRates);
    }

    [Fact]
    public void Accounts_in_different_currencies_are_converted_rather_than_added_blind()
    {
        // The bug this replaces would have answered 1,000,400 of nothing in particular.
        var summary = BankBalanceSummaryDto.From(
            [Account(1_000_000m, "HUF"), Account(400m, "EUR")],
            Rollup("EUR", false, EurHuf));

        Assert.True(summary.MixedCurrency);
        Assert.True(summary.Converted);
        Assert.Equal("EUR", summary.Currency);
        Assert.Equal(2_900m, summary.TotalBalance);
    }

    [Fact]
    public void Without_a_rate_the_headline_is_unknown_but_the_breakdown_is_still_exact()
    {
        var summary = BankBalanceSummaryDto.From(
            [Account(1_000_000m, "HUF"), Account(400m, "EUR")],
            Rollup("EUR"));

        Assert.Null(summary.TotalBalance);

        // The part that is always true, and the reason a missing rate is not a blank screen.
        Assert.Collection(
            summary.ByCurrency,
            eur => { Assert.Equal("EUR", eur.CurrencyCode); Assert.Equal(400m, eur.Total); },
            huf => { Assert.Equal("HUF", huf.CurrencyCode); Assert.Equal(1_000_000m, huf.Total); });

        var missing = Assert.Single(summary.MissingRates);
        Assert.Equal("HUF", missing.From);
        Assert.Equal("EUR", missing.To);
        Assert.Single(summary.Warnings);
    }

    [Fact]
    public void Currency_codes_are_grouped_regardless_of_how_they_were_typed()
    {
        var summary = BankBalanceSummaryDto.From(
            [Account(100m, "eur"), Account(200m, "EUR")],
            Rollup("EUR"));

        var only = Assert.Single(summary.ByCurrency);
        Assert.Equal("EUR", only.CurrencyCode);
        Assert.Equal(300m, only.Total);
    }

    [Fact]
    public void No_accounts_is_zero_held_rather_than_unknown()
    {
        // A genuine zero, not a missing figure: nothing is held, and no rate is needed to say so.
        var summary = BankBalanceSummaryDto.From([], Rollup("EUR"));

        Assert.Equal(0m, summary.TotalBalance);
        Assert.Equal("EUR", summary.Currency);
        Assert.Empty(summary.ByCurrency);
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void Always_convert_reports_a_single_currency_balance_in_the_base_currency()
    {
        var summary = BankBalanceSummaryDto.From(
            [Account(1_000_000m, "HUF")],
            Rollup("EUR", true, EurHuf));

        Assert.False(summary.MixedCurrency);
        Assert.True(summary.Converted);
        Assert.Equal("EUR", summary.Currency);
        Assert.Equal(2_500m, summary.TotalBalance);
    }
}
