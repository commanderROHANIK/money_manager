using MoneyManager.Api.Services.Currency;
using Xunit;

namespace MoneyManager.Api.Tests;

/// <summary>
/// The converter is pure — every rate it will see arrives in its constructor — so each rule is
/// checkable against a literal, with no database and no clock.
///
/// <para>
/// The rule these tests exist to protect: an unknown pair is <c>null</c>. A total that quietly
/// treats 62,000,000 HUF as 62,000,000 EUR is worse than no total, because nothing in the output
/// tells the user it happened.
/// </para>
/// </summary>
public sealed class CurrencyConverterTests
{
    private static readonly DateTime Quoted = new(2026, 7, 1);

    /// <summary>1 EUR = 400 HUF.</summary>
    private static ICurrencyConverter WithEurToHuf() =>
        new CurrencyConverter([new ExchangeRateSnapshot("EUR", "HUF", 400m, Quoted)]);

    [Fact]
    public void A_currency_converts_to_itself_at_one_with_no_rate_on_record()
    {
        var converter = CurrencyConverter.Empty;

        Assert.Equal(1234.56m, converter.Convert(1234.56m, new CurrencyPair("HUF", "HUF")));

        var identity = Assert.IsType<AppliedRate>(converter.RateFor(new CurrencyPair("HUF", "HUF")));
        Assert.Equal(1m, identity.Rate);
        Assert.Null(identity.AsOf);
    }

    [Fact]
    public void A_direct_rate_multiplies()
    {
        Assert.Equal(400_000m, WithEurToHuf().Convert(1_000m, new CurrencyPair("EUR", "HUF")));
    }

    [Fact]
    public void The_reverse_direction_is_read_off_the_same_row()
    {
        // Making the user type both directions would mostly produce two rows that disagree.
        var applied = Assert.IsType<AppliedRate>(WithEurToHuf().RateFor(new CurrencyPair("HUF", "EUR")));

        Assert.True(applied.Inverted);
        Assert.Equal(0.0025m, applied.Rate);
        Assert.Equal<DateTime?>(Quoted, applied.AsOf);
        Assert.Equal(1_000m, WithEurToHuf().Convert(400_000m, new CurrencyPair("HUF", "EUR")));
    }

    [Fact]
    public void A_direct_rate_wins_over_the_reciprocal_of_the_reverse_row()
    {
        var converter = new CurrencyConverter(
        [
            new ExchangeRateSnapshot("EUR", "HUF", 400m, Quoted),
            new ExchangeRateSnapshot("HUF", "EUR", 0.003m, Quoted),
        ]);

        var applied = Assert.IsType<AppliedRate>(converter.RateFor(new CurrencyPair("HUF", "EUR")));

        Assert.False(applied.Inverted);
        Assert.Equal(0.003m, applied.Rate);
    }

    [Fact]
    public void An_unknown_pair_is_null_and_not_zero()
    {
        // The whole contract in one assertion. Zero would read as "this property is worthless".
        Assert.Null(WithEurToHuf().Convert(1_000m, new CurrencyPair("EUR", "GBP")));
        Assert.Null(WithEurToHuf().RateFor(new CurrencyPair("EUR", "GBP")));
    }

    [Fact]
    public void Rates_are_never_chained_through_a_third_currency()
    {
        // EUR->HUF and GBP->HUF are both on record, so EUR->GBP is arithmetically derivable.
        // It is still refused: nobody entered that rate, and the answer would carry the
        // compounded error of two others while looking exactly like a figure the user chose.
        var converter = new CurrencyConverter(
        [
            new ExchangeRateSnapshot("EUR", "HUF", 400m, Quoted),
            new ExchangeRateSnapshot("GBP", "HUF", 460m, Quoted),
        ]);

        Assert.Null(converter.Convert(100m, new CurrencyPair("EUR", "GBP")));
    }

    [Fact]
    public void Codes_match_regardless_of_case_or_padding()
    {
        var converter = new CurrencyConverter([new ExchangeRateSnapshot(" eur ", "huf", 400m, Quoted)]);

        Assert.Equal(400m, converter.Convert(1m, new CurrencyPair("Eur", "HUF")));
    }

    [Fact]
    public void A_non_positive_rate_is_treated_as_no_rate_at_all()
    {
        // Such a row cannot be inverted and cannot describe an exchange. Reporting "unknown" is
        // the honest answer; letting it through is a divide-by-zero or a sign flip.
        var converter = new CurrencyConverter(
        [
            new ExchangeRateSnapshot("EUR", "HUF", 0m, Quoted),
            new ExchangeRateSnapshot("GBP", "HUF", -400m, Quoted),
        ]);

        Assert.Null(converter.Convert(100m, new CurrencyPair("EUR", "HUF")));
        Assert.Null(converter.Convert(100m, new CurrencyPair("HUF", "EUR")));
        Assert.Null(converter.Convert(100m, new CurrencyPair("GBP", "HUF")));
    }

    [Fact]
    public void Conversion_is_left_unrounded_so_the_caller_can_round_the_total_once()
    {
        // Rounding every leg before adding them drifts by a cent per property, which is exactly
        // the sort of quiet wrongness this codebase spends effort avoiding.
        var converter = new CurrencyConverter([new ExchangeRateSnapshot("HUF", "EUR", 0.0026m, Quoted)]);

        Assert.Equal(2600.0026m, converter.Convert(1_000_001m, new CurrencyPair("HUF", "EUR")));
    }

    [Fact]
    public void An_empty_or_blank_code_converts_to_nothing()
    {
        Assert.Null(WithEurToHuf().Convert(100m, new CurrencyPair("", "HUF")));
        Assert.Null(WithEurToHuf().Convert(100m, new CurrencyPair("EUR", "   ")));
    }
}
