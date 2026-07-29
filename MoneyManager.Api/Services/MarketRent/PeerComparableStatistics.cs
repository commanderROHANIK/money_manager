namespace MoneyManager.Api.Services.MarketRent
{
    /// <summary>One let property used as evidence. Deliberately carries no identity.</summary>
    public readonly record struct RentComparable(decimal MonthlyRent, decimal? SizeSqm);

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
        /// market, and could let a user infer a specific neighbour's rent.
        /// </summary>
        public const int MinimumSampleSize = 3;

        public static ComparableEstimate? Estimate(
            IReadOnlyList<RentComparable> comparables,
            decimal? targetSizeSqm,
            int minimumSampleSize = MinimumSampleSize)
        {
            if (comparables.Count < minimumSampleSize)
                return null;

            // Comparing rent per square metre is far more meaningful than comparing rents,
            // but only when both the target and enough of the evidence have a size.
            var sized = comparables
                .Where(c => c.SizeSqm is > 0)
                .Select(c => c.MonthlyRent / c.SizeSqm!.Value)
                .OrderBy(v => v)
                .ToList();

            if (targetSizeSqm is > 0 && sized.Count >= minimumSampleSize)
            {
                return Build(
                    Median(sized) * targetSizeSqm.Value,
                    Percentile(sized, 0.25m) * targetSizeSqm.Value,
                    Percentile(sized, 0.75m) * targetSizeSqm.Value,
                    sized.Count,
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

        /// <summary>Nearest-rank percentile. Assumes <paramref name="sorted"/> is ascending.</summary>
        private static decimal Percentile(IReadOnlyList<decimal> sorted, decimal fraction)
        {
            var rank = (int)Math.Ceiling(fraction * sorted.Count) - 1;
            return sorted[Math.Clamp(rank, 0, sorted.Count - 1)];
        }
    }
}
