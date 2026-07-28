using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Analytics
{
    /// <summary>A single ledger line, stripped of persistence concerns.</summary>
    public readonly record struct LedgerEntry(DateTime Date, decimal Amount, TransactionCategory Category);

    /// <summary>An occupancy window, used to work out how much of the holding period was let.</summary>
    public readonly record struct OccupancyWindow(DateTime Start, DateTime? End);

    /// <summary>
    /// Everything the calculator needs, and nothing it does not. Keeping this free of EF
    /// types is what lets every formula be tested with a literal, without a database.
    /// </summary>
    public sealed record PropertyAnalyticsInput
    {
        public required int PropertyId { get; init; }
        public required string PropertyName { get; init; }
        public required string CurrencyCode { get; init; }

        public decimal? PurchasePrice { get; init; }
        public DateTime? PurchaseDate { get; init; }

        /// <summary>Most recent recorded valuation, if any.</summary>
        public decimal? CurrentValuation { get; init; }
        public DateTime? ValuationDate { get; init; }

        public PropertyStatus Status { get; init; } = PropertyStatus.Active;
        public decimal? SalePrice { get; init; }
        public DateTime? SaleDate { get; init; }

        /// <summary>Contracted rent under the tenancy running on <see cref="AsOf"/>.</summary>
        public decimal? ActiveMonthlyRent { get; init; }

        /// <summary>Latest market estimate, when a market rent provider has produced one.</summary>
        public decimal? MarketMonthlyRent { get; init; }

        public decimal? MortgageOriginalAmount { get; init; }
        public decimal? MortgageBalance { get; init; }
        public decimal? MortgageMonthlyPayment { get; init; }

        public IReadOnlyList<LedgerEntry> Transactions { get; init; } = [];
        public IReadOnlyList<OccupancyWindow> Occupancy { get; init; } = [];

        public DateTime AsOf { get; init; } = DateTime.UtcNow.Date;
    }

    /// <summary>
    /// Why a figure should not be read at face value. Surfacing these is deliberate: a
    /// spreadsheet gives a confident wrong number, and being told which inputs are missing
    /// is most of what makes the output trustworthy.
    /// </summary>
    public sealed record MetricWarning(string Code, string Message);

    /// <summary>
    /// Every metric is nullable, because every one of them has inputs that a real user may
    /// not have entered yet. A null means "cannot be known", never "zero".
    /// </summary>
    public sealed record PropertyMetrics
    {
        public required int PropertyId { get; init; }
        public required string PropertyName { get; init; }
        public required string CurrencyCode { get; init; }
        public required DateTime AsOf { get; init; }

        // --- Capital ---
        /// <summary>Purchase price plus acquisition costs plus capital improvements.</summary>
        public decimal? TotalInvested { get; init; }

        /// <summary>Out of pocket: total invested less the amount that was borrowed.</summary>
        public decimal? CashInvested { get; init; }

        // --- Income and running costs, annualised ---
        public decimal? AnnualRentalIncome { get; init; }
        public decimal? AnnualOperatingExpenses { get; init; }
        public decimal? NetOperatingIncome { get; init; }
        public decimal? AnnualDebtService { get; init; }
        public decimal? MonthlyCashFlow { get; init; }

        // --- Returns, as fractions (0.075 = 7.5%) ---
        public decimal? GrossYield { get; init; }
        public decimal? NetYield { get; init; }
        public decimal? CapRate { get; init; }
        public decimal? CashOnCashReturn { get; init; }

        // --- Position ---
        public decimal? CurrentValue { get; init; }
        public decimal? Equity { get; init; }
        public decimal? Appreciation { get; init; }
        public decimal? AppreciationPercent { get; init; }
        public decimal? CumulativeNetCashFlow { get; init; }
        public decimal? TotalReturn { get; init; }
        public decimal? TotalRoi { get; init; }
        public decimal? AnnualizedRoi { get; init; }
        public decimal? YearsHeld { get; init; }
        public decimal? OccupancyRate { get; init; }

        // --- The commercial hook: rent versus what the market pays ---
        public decimal? MarketMonthlyRent { get; init; }
        public decimal? ContractedMonthlyRent { get; init; }
        public decimal? RentGapPercent { get; init; }
        public decimal? AnnualRentUplift { get; init; }

        public IReadOnlyList<MetricWarning> Warnings { get; init; } = [];
    }
}
