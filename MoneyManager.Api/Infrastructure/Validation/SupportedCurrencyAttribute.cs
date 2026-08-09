using System.ComponentModel.DataAnnotations;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Infrastructure.Validation
{
    /// <summary>
    /// Accepts one of the codes in <see cref="SupportedCurrencies"/>, case-insensitively, and
    /// treats null or blank as valid so an optional currency can fall back to a default.
    ///
    /// <para>
    /// Deliberately over <see cref="SupportedCurrencies"/> rather than a regular expression or an
    /// ISO 4217 list. Every figure in this product is denominated in a currency fixed at creation
    /// and portfolio totals refuse to add unlike ones, so an unrecognised code is not a cosmetic
    /// problem — it produces a property whose totals can never be combined with anything. A
    /// three-letter pattern would happily accept <c>XYZ</c>.
    /// </para>
    ///
    /// <para>
    /// Pair with <c>[Required]</c> where the code is mandatory: blank passes here on purpose.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class SupportedCurrencyAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is null)
                return true;

            if (value is not string code)
                return false;

            return string.IsNullOrWhiteSpace(code) || SupportedCurrencies.IsSupported(code);
        }

        public override string FormatErrorMessage(string name) =>
            $"{name} must be one of {string.Join(", ", SupportedCurrencies.All)}.";
    }
}
