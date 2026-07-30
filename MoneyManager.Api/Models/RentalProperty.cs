namespace MoneyManager.Api.Models
{
    public class RentalProperty : IOwnedByUser
    {
        public int Id { get; set; }  // Primary Key
        public int UserId { get; set; }

        public string PropertyName { get; set; } = string.Empty;

        // --- Location. Kept granular enough to find comparable properties. ---
        public string Address { get; set; } = string.Empty;

        private string? _city;

        /// <summary>The city as the landlord typed it. Displayed; never matched on.</summary>
        public string? City
        {
            get => _city;
            set
            {
                _city = value;
                NormalizedCity = NormalizeCity(value);
            }
        }

        /// <summary>
        /// Match key for comparable lookups, maintained by the <see cref="City"/> setter.
        ///
        /// Stored rather than computed in the query because SQLite's <c>UPPER()</c> is
        /// ASCII-only: comparing on <see cref="City"/> would leave "Győr" and "GYŐR" in
        /// separate markets, which for this app's users is the common case rather than an
        /// edge case — and splitting a market both loses evidence and drives samples down
        /// towards the disclosure threshold. Mirrors how <see cref="User.NormalizedUsername"/>
        /// keeps lookups off the database's collation.
        /// </summary>
        public string? NormalizedCity { get; private set; }

        public static string? NormalizeCity(string? city) =>
            string.IsNullOrWhiteSpace(city) ? null : city.Trim().ToUpperInvariant();

        public string? PostalCode { get; set; }
        public string? CountryCode { get; set; }

        // --- Physical characteristics, used for market rent comparables. ---
        public PropertyType PropertyType { get; set; } = PropertyType.Apartment;
        public decimal? SizeSqm { get; set; }
        public int? Bedrooms { get; set; }

        // --- Acquisition. Without these there is no denominator for any return metric. ---
        public decimal? PurchasePrice { get; set; }
        public DateTime? PurchaseDate { get; set; }

        // --- Lifecycle. ---
        public PropertyStatus Status { get; set; } = PropertyStatus.Active;
        public decimal? SalePrice { get; set; }
        public DateTime? SaleDate { get; set; }

        public string? Notes { get; set; }

        /// <summary>
        /// The currency every figure on this property is denominated in. Fixed at creation:
        /// per-property analytics then run with no FX conversion at all, and conversion is
        /// confined to portfolio-level rollups.
        /// </summary>
        public string CurrencyCode { get; set; } = "EUR";

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // --- Navigation ---
        public ICollection<Lease> Leases { get; set; } = new List<Lease>();
        public ICollection<PropertyTransaction> Transactions { get; set; } = new List<PropertyTransaction>();
        public ICollection<PropertyValuation> Valuations { get; set; } = new List<PropertyValuation>();
        public ICollection<RentPricePoint> RentPricePoints { get; set; } = new List<RentPricePoint>();
        public ICollection<PropertyEvent> Events { get; set; } = new List<PropertyEvent>();
    }
}
