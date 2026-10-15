using MoneyManager.Api.Controllers;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Currency;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// The loans equivalent of <see cref="BankBalanceSummaryTests"/>: before this endpoint existed,
/// <c>TotalLoanAmountWidget</c> summed <c>LoanAmount</c> across loans client-side while ignoring
/// <c>CurrencyCode</c> (and labelled the result with a hardcoded HUF suffix regardless), so a EUR
/// mortgage beside a HUF one produced a confident nonsense number. These tests pin the
/// replacement: an exact per-currency breakdown always, and a single headline figure only when a
/// rate exists to justify it.
/// </summary>
public sealed class LoanAmountSummaryTests
{
    private static readonly DateTime Quoted = new(2026, 7, 1);

    private static RollupContext Rollup(
        string baseCurrency = "EUR",
        bool alwaysConvert = false,
        params ExchangeRateSnapshot[] rates) =>
        new(new CurrencyConverter(rates), baseCurrency, alwaysConvert);

    private static ExchangeRateSnapshot EurHuf => new("EUR", "HUF", 400m, Quoted);

    private static Loan Debt(decimal amount, string currency) =>
        new() { LoanName = "Test mortgage", LoanAmount = amount, CurrencyCode = currency };

    [Fact]
    public void Loans_sharing_a_currency_total_in_it_without_needing_a_rate()
    {
        var summary = LoanAmountSummaryDto.From(
            [Debt(164_500m, "HUF"), Debt(21_850m, "HUF")],
            Rollup(baseCurrency: "EUR"));

        Assert.Equal(164_500m + 21_850m, summary.TotalAmount);
        Assert.Equal("HUF", summary.Currency);
        Assert.False(summary.MixedCurrency);
        Assert.False(summary.Converted);
        Assert.Empty(summary.MissingRates);
    }

    [Fact]
    public void Loans_in_different_currencies_are_converted_rather_than_added_blind()
    {
        // The bug this replaces would have answered a raw sum of HUF and EUR figures, labelled Ft.
        var summary = LoanAmountSummaryDto.From(
            [Debt(100_000m, "HUF"), Debt(400m, "EUR")],
            Rollup("EUR", false, EurHuf));

        Assert.True(summary.MixedCurrency);
        Assert.True(summary.Converted);
        Assert.Equal("EUR", summary.Currency);
        Assert.Equal(250m + 400m, summary.TotalAmount);
    }

    [Fact]
    public void Without_a_rate_the_headline_is_unknown_but_the_breakdown_is_still_exact()
    {
        var summary = LoanAmountSummaryDto.From(
            [Debt(100_000m, "HUF"), Debt(400m, "EUR")],
            Rollup("EUR"));

        Assert.Null(summary.TotalAmount);

        Assert.Collection(
            summary.ByCurrency,
            eur => { Assert.Equal("EUR", eur.CurrencyCode); Assert.Equal(400m, eur.Total); },
            huf => { Assert.Equal("HUF", huf.CurrencyCode); Assert.Equal(100_000m, huf.Total); });

        var missing = Assert.Single(summary.MissingRates);
        Assert.Equal("HUF", missing.From);
        Assert.Equal("EUR", missing.To);
        Assert.Single(summary.Warnings);
    }

    [Fact]
    public void No_loans_is_zero_owed_rather_than_unknown()
    {
        var summary = LoanAmountSummaryDto.From([], Rollup("EUR"));

        Assert.Equal(0m, summary.TotalAmount);
        Assert.Equal("EUR", summary.Currency);
        Assert.Empty(summary.ByCurrency);
        Assert.Empty(summary.Warnings);
    }

    [Fact]
    public void Always_convert_reports_a_single_currency_value_in_the_base_currency()
    {
        var summary = LoanAmountSummaryDto.From(
            [Debt(100_000m, "HUF")],
            Rollup("EUR", true, EurHuf));

        Assert.False(summary.MixedCurrency);
        Assert.True(summary.Converted);
        Assert.Equal("EUR", summary.Currency);
        Assert.Equal(250m, summary.TotalAmount);
    }
}
