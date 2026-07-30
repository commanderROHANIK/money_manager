using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Currency;
using Xunit;

namespace MoneyManager.Api.Tests;

public class CurrencyConverterTests
{
    private static readonly DateOnly Today = new(2026, 7, 28);
    private static readonly DateOnly Yesterday = new(2026, 7, 27);

    private static ExchangeRate Rate(string from, string to, decimal rate, DateOnly? asOf = null) =>
        new() { FromCurrency = from, ToCurrency = to, Rate = rate, AsOf = asOf ?? Today };

    [Fact]
    public void Converting_a_currency_to_itself_needs_no_rate()
    {
        var converter = CurrencyConverter.Empty();

        var result = converter.Convert(1_000m, "EUR", "EUR");

        Assert.NotNull(result);
        Assert.Equal(1_000m, result!.Value.Amount);

        // No rate was involved, so this must not date-stamp a total that used it.
        Assert.Null(result.Value.RateAsOf);
    }

    [Fact]
    public void Direct_rate_is_applied()
    {
        var converter = CurrencyConverter.FromRates([Rate("EUR", "HUF", 400m)]);

        var result = converter.Convert(1_200m, "EUR", "HUF");

        Assert.Equal(480_000m, result!.Value.Amount);
        Assert.Equal(Today, result.Value.RateAsOf);
    }

    [Fact]
    public void Inverse_is_derived_so_only_one_direction_needs_storing()
    {
        var converter = CurrencyConverter.FromRates([Rate("EUR", "HUF", 400m)]);

        var result = converter.Convert(400_000m, "HUF", "EUR");

        Assert.Equal(1_000m, result!.Value.Amount);
    }

    [Fact]
    public void Round_trip_returns_the_original_amount()
    {
        var converter = CurrencyConverter.FromRates([Rate("EUR", "HUF", 397.5m)]);

        var toHuf = converter.Convert(1_000m, "EUR", "HUF")!.Value.Amount;
        var backToEur = converter.Convert(toHuf, "HUF", "EUR")!.Value.Amount;

        Assert.Equal(1_000m, Math.Round(backToEur, 6));
    }

    [Fact]
    public void Unrelated_pair_crosses_through_the_pivot()
    {
        // Rates are quoted against EUR, as a real rate table is: HUF to GBP is not stored.
        var converter = CurrencyConverter.FromRates([
            Rate("EUR", "HUF", 400m),
            Rate("EUR", "GBP", 0.85m),
        ]);

        var result = converter.Convert(400_000m, "HUF", "GBP");

        // 400,000 HUF -> 1,000 EUR -> 850 GBP
        Assert.Equal(850m, Math.Round(result!.Value.Amount, 6));
    }

    [Fact]
    public void A_crossed_rate_is_only_as_current_as_its_stalest_leg()
    {
        var converter = CurrencyConverter.FromRates([
            Rate("EUR", "HUF", 400m, Today),
            Rate("EUR", "GBP", 0.85m, Yesterday),
        ]);

        var result = converter.Convert(400_000m, "HUF", "GBP");

        Assert.Equal(Yesterday, result!.Value.RateAsOf);
    }

    [Fact]
    public void Unknown_pair_returns_null_rather_than_assuming_parity()
    {
        var converter = CurrencyConverter.FromRates([Rate("EUR", "HUF", 400m)]);

        // Treating an unknown rate as 1:1 would report 1,000 JPY as 1,000 EUR.
        Assert.Null(converter.Convert(1_000m, "JPY", "EUR"));
        Assert.False(converter.CanConvert("JPY", "EUR"));
    }

    [Fact]
    public void Empty_rate_table_still_converts_within_one_currency()
    {
        var converter = CurrencyConverter.Empty();

        Assert.NotNull(converter.Convert(500m, "HUF", "HUF"));
        Assert.Null(converter.Convert(500m, "HUF", "EUR"));
    }

    [Fact]
    public void The_most_recent_rate_for_a_pair_wins()
    {
        var converter = CurrencyConverter.FromRates([
            Rate("EUR", "HUF", 350m, Yesterday),
            Rate("EUR", "HUF", 400m, Today),
        ]);

        Assert.Equal(400m, converter.Convert(1m, "EUR", "HUF")!.Value.Amount);
    }

    [Fact]
    public void Currency_codes_are_matched_case_and_whitespace_insensitively()
    {
        var converter = CurrencyConverter.FromRates([Rate(" eur ", "huf", 400m)]);

        Assert.Equal(400m, converter.Convert(1m, "EUR", "HUF")!.Value.Amount);
    }

    [Fact]
    public void Non_positive_rates_are_discarded_because_they_cannot_be_inverted()
    {
        var converter = CurrencyConverter.FromRates([
            Rate("EUR", "HUF", 0m),
            Rate("EUR", "GBP", -1m),
        ]);

        Assert.Null(converter.Convert(1m, "EUR", "HUF"));
        Assert.Null(converter.Convert(1m, "EUR", "GBP"));
    }
}
