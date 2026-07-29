using System.Text.Json.Serialization;

namespace MoneyManager.Api.Models
{
    /// <summary>
    /// The narrative of a property: what happened and when. Most rows are written
    /// automatically as leases start and end, capital is spent, or a valuation is added,
    /// because a timeline nobody has to maintain is the only kind that stays accurate.
    /// </summary>
    public class PropertyEvent : IOwnedByUser
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

        public DateTime OccurredOn { get; set; }

        public PropertyEventType Type { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>False when the user wrote the entry by hand.</summary>
        public bool IsSystemGenerated { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
