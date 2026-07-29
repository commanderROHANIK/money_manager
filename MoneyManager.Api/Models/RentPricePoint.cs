using System.Text.Json.Serialization;

namespace MoneyManager.Api.Models
{
    /// <summary>
    /// A point on a property's rent timeline. One table carries both what the landlord
    /// charges and what the market is estimated to pay, so the gap between the two — the
    /// number that tells someone they are underpricing — is a single query rather than a
    /// join across two separate histories.
    /// </summary>
    public class RentPricePoint : IOwnedByUser
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public int RentalPropertyId { get; set; }
        /// <summary>
        /// Navigation for queries only. Excluded from responses: EF fixes it up when the
        /// parent is tracked in the same context, which would otherwise serialise the whole
        /// property graph back — bloated, and a cycle the serialiser cannot resolve.
        /// </summary>
        [JsonIgnore]
        public RentalProperty? RentalProperty { get; set; }

        /// <summary>Set when this price came from a specific tenancy.</summary>
        public int? LeaseId { get; set; }

        public DateTime EffectiveFrom { get; set; }

        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; } = "EUR";

        public RentPriceSource Source { get; set; }

        /// <summary>Which market rent provider produced this, for estimates.</summary>
        public string? ProviderKey { get; set; }

        public string? Notes { get; set; }
    }
}
