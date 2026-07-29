using System.Text.Json.Serialization;

namespace MoneyManager.Api.Models
{
    /// <summary>
    /// What the property was worth on a given date. Appreciation, equity and cap rate are
    /// all undefined without a value timeline, so this is recorded rather than assumed.
    /// </summary>
    public class PropertyValuation : IOwnedByUser
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public int RentalPropertyId { get; set; }

        [JsonIgnore]
        public RentalProperty? RentalProperty { get; set; }

        public DateTime ValuedOn { get; set; }

        public decimal Value { get; set; }
        public string CurrencyCode { get; set; } = "EUR";

        public ValuationSource Source { get; set; }

        public string? Notes { get; set; }
    }
}
