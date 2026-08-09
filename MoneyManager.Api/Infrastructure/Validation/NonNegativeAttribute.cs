using System.ComponentModel.DataAnnotations;

namespace MoneyManager.Api.Infrastructure.Validation
{
    /// <summary>
    /// Rejects a negative money amount or quantity. Null passes, so an optional figure stays
    /// optional — "not recorded" is a different fact from "recorded as zero", which is the same
    /// distinction the analytics metrics are built on.
    ///
    /// <para>
    /// Its own attribute rather than <c>[Range(0, double.MaxValue)]</c> because every amount here
    /// is a <c>decimal</c>, and <c>RangeAttribute</c>'s double-typed bounds convert through
    /// floating point to compare them. Money should not take a detour through a type that cannot
    /// represent it exactly, however wide the bound.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class NonNegativeAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value) => value switch
        {
            null => true,
            decimal amount => amount >= 0m,
            int count => count >= 0,
            long count => count >= 0,
            _ => false,
        };

        public override string FormatErrorMessage(string name) => $"{name} cannot be negative.";
    }
}
