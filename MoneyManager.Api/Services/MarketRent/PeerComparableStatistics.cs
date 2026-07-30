namespace MoneyManager.Api.Services.MarketRent
{
    /// <summary>
    /// One let property used as evidence.
    ///
    /// <c>OwnerKey</c> is an opaque grouping token. It exists solely to enforce the
    /// distinct-owner minimum below and never leaves this class — no field of a
    /// <see cref="ComparableEstimate"/> is derived from it.
    /// </summary>
    public readonly record struct RentComparable(int OwnerKey, decimal MonthlyRent, decimal? SizeSqm);

    public sealed record ComparableEstimate(
        decimal Monthly,
        decimal Low,
        decimal High,
        int SampleSize,
        MarketRentConfidence Confidence,
        bool PerSquareMetre);

    /// <summary>
    /// The maths behind a peer-comparable estimate, separated from the query that gathers
    /// the evidence so it can be tested with literals.
    ///
    /// Uses the median rather than the mean throughout: one mispriced or mistyped property
    /// in a small sample would drag an average badly, and small samples are the normal case.
    /// </summary>
    public static class PeerComparableStatistics
    {
        /// <summary>
        /// Below this, an estimate says more about one landlord's pricing than about the
        /// market.
        /// </summary>
        public const int MinimumSampleSize = 3;

        /// <summary>
        /// The k in k-anonymity: how many *different* landlords must stand behind a figure
        /// before it may be published.
        ///
        /// Counting rows instead of owners is what makes a threshold like this fail. Three
        /// flats owned by one landlord, or three overlapping leases on a single flat, are
        /// not a market — and a median over them restates one person's rent. The caller is
        /// additionally excluded from its own sample upstream, so this counts unrelated
        /// third parties only.
        /// </summary>
        public const int MinimumDistinctOwners = 3;

        public static ComparableEstimate? Estimate(
            IReadOnlyList<RentComparable> comparables,
            decimal? targetSizeSqm,
            int minimumSampleSize = MinimumSampleSize,
            int minimumDistinctOwners = MinimumDistinctOwners)
        {
            if (!IsPublishable(comparables, minimumSampleSize, minimumDistinctOwners))
                return null;

            // Comparing rent per square metre is far more meaningful than comparing rents,
            // but only when both the target and enough of the evidence have a size. The
            // sized subset has to clear the disclosure thresholds in its own right —
            // filtering by size can collapse a broad sample down to a single landlord.
            var sized = comparables.Where(c => c.SizeSqm is > 0).ToList();

            if (targetSizeSqm is > 0 && IsPublishable(sized, minimumSampleSize, minimumDistinctOwners))
            {
                var perSquareMetre = sized
                    .Select(c => c.MonthlyRent / c.SizeSqm!.Value)
                    .OrderBy(v => v)
                    .ToList();

                return Build(
                    Median(perSquareMetre) * targetSizeSqm.Value,
                    Percentile(perSquareMetre, 0.25m) * targetSizeSqm.Value,
                    Percentile(perSquareMetre, 0.75m) * targetSizeSqm.Value,
                    perSquareMetre.Count,
                    perSquareMetre: true);
            }

            var absolute = comparables.Select(c => c.MonthlyRent).OrderBy(v => v).ToList();

            return Build(
                Median(absolute),
                Percentile(absolute, 0.25m),
                Percentile(absolute, 0.75m),
                absolute.Count,
                perSquareMetre: false);
        }

        /// <summary>
        /// Both thresholds have to hold on whichever set actually produces the numbers,
        /// which is why this is a function rather than a check done once at the top.
        /// </summary>
        private static bool IsPublishable(
            IReadOnlyList<RentComparable> comparables, int minimumSampleSize, int minimumDistinctOwners) =>
            comparables.Count >= minimumSampleSize
            && comparables.Select(c => c.OwnerKey).Distinct().Count() >= minimumDistinctOwners;

        private static ComparableEstimate Build(
            decimal monthly, decimal low, decimal high, int sampleSize, bool perSquareMetre) =>
            new(
                Math.Round(monthly, 2),
                Math.Round(low, 2),
                Math.Round(high, 2),
                sampleSize,
                ConfidenceFor(sampleSize),
                perSquareMetre);

        /// <summary>
        /// Confidence tracks sample size only. It is not a claim about accuracy — it is a
        /// statement about how much evidence there is, which is what a user needs to weigh
        /// the number.
        /// </summary>
        private static MarketRentConfidence ConfidenceFor(int sampleSize) => sampleSize switch
        {
            >= 10 => MarketRentConfidence.High,
            >= 5 => MarketRentConfidence.Medium,
            >= MinimumSampleSize => MarketRentConfidence.Low,
            _ => MarketRentConfidence.Insufficient,
        };

        /// <summary>Assumes <paramref name="sorted"/> is ascending.</summary>
        private static decimal Median(IReadOnlyList<decimal> sorted)
        {
            var middle = sorted.Count / 2;

            return sorted.Count % 2 == 1
                ? sorted[middle]
                : (sorted[middle - 1] + sorted[middle]) / 2m;
        }

        /// <summary>
        /// Linear-interpolated percentile. Assumes <paramref name="sorted"/> is ascending.
        ///
        /// Interpolating rather than picking the nearest rank is what keeps the range from
        /// being a disclosure: over three values, nearest-rank quartiles return the lowest
        /// and highest of them verbatim, so a "range" would republish two individual rents
        /// exactly. Blending adjacent values means no published bound is any one landlord's
        /// figure.
        /// </summary>
        private static decimal Percentile(IReadOnlyList<decimal> sorted, decimal fraction)
        {
            if (sorted.Count == 1)
                return sorted[0];

            var position = fraction * (sorted.Count - 1);
            var lower = (int)Math.Floor(position);
            var upper = (int)Math.Ceiling(position);

            if (lower == upper)
                return sorted[lower];

            return sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower));
        }
    }
}
