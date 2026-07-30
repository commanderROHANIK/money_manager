using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.MarketRent
{
    public enum MarketRentConfidence
    {
        /// <summary>Not enough evidence to say anything. Always paired with a null estimate.</summary>
        Insufficient = 0,
        Low = 1,
        Medium = 2,
        High = 3,
    }

    /// <summary>
    /// What is known about the property we want an estimate for. Carries no identity — a
    /// provider gets the characteristics of a home, never whose it is.
    /// </summary>
    public sealed record MarketRentQuery
    {
        public required string? City { get; init; }
        public required string? CountryCode { get; init; }
        public required PropertyType PropertyType { get; init; }
        public required string CurrencyCode { get; init; }
        public decimal? SizeSqm { get; init; }
        public int? Bedrooms { get; init; }

        /// <summary>
        /// The property being valued, so it cannot become its own comparable and quietly
        /// confirm whatever rent is already being charged.
        /// </summary>
        public required int ExcludePropertyId { get; init; }

        /// <summary>
        /// The owner of the property being valued. Their whole portfolio is excluded from
        /// the evidence, not just the one property: a caller who can add properties could
        /// otherwise stack the sample with decoy rents either side of a single real
        /// neighbour and read that neighbour's exact figure back out of the median.
        /// </summary>
        public required int ExcludeUserId { get; init; }
    }

    /// <summary>
    /// An estimate, always carrying its provenance. Nothing renders a market figure without
    /// showing where it came from and how confident it is — an authoritative-looking wrong
    /// rent is worse than no rent at all.
    /// </summary>
    public sealed record MarketRentEstimate
    {
        public required decimal Monthly { get; init; }
        public required string CurrencyCode { get; init; }
        public required string ProviderKey { get; init; }
        public required MarketRentConfidence Confidence { get; init; }
        public decimal? Low { get; init; }
        public decimal? High { get; init; }
        public int? SampleSize { get; init; }
        public string? Notes { get; init; }
    }

    public interface IMarketRentProvider
    {
        /// <summary>Stored on the resulting price point, so an estimate's origin is auditable.</summary>
        string Key { get; }

        /// <summary>Lower runs first. The first provider to return a value wins.</summary>
        int Priority { get; }

        /// <summary>Null when this provider has nothing trustworthy to say.</summary>
        Task<MarketRentEstimate?> GetEstimateAsync(MarketRentQuery query, CancellationToken ct);
    }
}
