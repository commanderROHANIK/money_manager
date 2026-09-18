using MoneyManager.Api.Models;

namespace MoneyManager.Api.Services.Analytics
{
    /// <summary>
    /// Turns a property's ledger into the return metrics a landlord actually decides on.
    ///
    /// Pure by design: no database, no clock, no configuration. Every input arrives in
    /// <see cref="PropertyAnalyticsInput"/>, which is what makes each formula checkable
    /// against a worked example.
    ///
    /// <para>
    /// Worked example for <c>Irr</c> — a property bought outright, with no interim cash flows,
    /// so the answer is checkable with a calculator rather than a spreadsheet:
    /// </para>
    /// <para>
    ///   Studio bought 2022-01-01 for 100,000, paid entirely in cash (no mortgage).<br/>
    ///   No rent, no expenses, no capital spend recorded.<br/>
    ///   Valued at 125,440 on 2024-01-01. Evaluated as of 2024-01-01.
    /// </para>
    /// <para>
    ///   Two dated cash flows: -100,000 at 2022-01-01 (the down payment, which here is the
    ///   whole price) and +125,440 at 2024-01-01 (terminal equity — no mortgage balance to
    ///   subtract). 2022 and 2023 are both 365-day years, so the gap is exactly 730 days, i.e.
    ///   2.0000 years at the calculator's day-count convention.
    /// </para>
    /// <para>
    ///   IRR solves (1 + r)^2.0000 = 125,440 / 100,000 = 1.2544, so
    ///   r = 1.2544^(1/2) - 1 = 1.12 - 1 = 0.1200 (12.00%).
    /// </para>
    /// </summary>
    public static class PropertyAnalyticsCalculator
    {
        private const int DaysPerYear = 365;

        public static PropertyMetrics Compute(PropertyAnalyticsInput input)
        {
            var warnings = new List<MetricWarning>();
            var asOf = input.AsOf.Date;

            // ---------------------------------------------------------------
            // Capital invested
            // ---------------------------------------------------------------
            var acquisitionAndCapital = input.Transactions
                .Where(t => TransactionCategoryInfo.IsCapital(t.Category))
                .Sum(t => t.Amount);

            decimal? totalInvested = input.PurchasePrice is null
                ? (acquisitionAndCapital > 0 ? acquisitionAndCapital : null)
                : input.PurchasePrice + acquisitionAndCapital;

            if (input.PurchasePrice is null)
            {
                warnings.Add(new MetricWarning(
                    "NoPurchasePrice",
                    "No purchase price recorded, so invested capital and return on it cannot be calculated."));
            }

            decimal? cashInvested = totalInvested is null
                ? null
                : totalInvested - (input.MortgageOriginalAmount ?? 0m);

            // Buying entirely with debt, or mis-entered figures, can drive this to zero or
            // below. Returns divided by it would be meaningless, so drop it instead.
            if (cashInvested is <= 0m)
            {
                warnings.Add(new MetricWarning(
                    "NonPositiveCashInvested",
                    "Borrowing covers the full cost recorded, so returns on cash invested cannot be calculated."));
                cashInvested = null;
            }

            // ---------------------------------------------------------------
            // Trailing-twelve-month income and costs
            // ---------------------------------------------------------------
            var windowStart = asOf.AddDays(-DaysPerYear);
            var observedFrom = input.PurchaseDate is { } purchased && purchased.Date > windowStart
                ? purchased.Date
                : windowStart;

            var observedDays = Math.Max(1, (asOf - observedFrom).Days);
            var annualisation = (decimal)DaysPerYear / observedDays;
            var isExtrapolated = observedDays < DaysPerYear;

            var inWindow = input.Transactions
                .Where(t => t.Date.Date > windowStart && t.Date.Date <= asOf)
                .ToList();

            var rentReceived = inWindow
                .Where(t => TransactionCategoryInfo.IsRentalIncome(t.Category))
                .Sum(t => t.Amount);

            var operatingCosts = inWindow
                .Where(t => TransactionCategoryInfo.IsOperatingExpense(t.Category))
                .Sum(t => t.Amount);

            var financingPaid = inWindow
                .Where(t => TransactionCategoryInfo.IsFinancing(t.Category))
                .Sum(t => t.Amount);

            decimal? annualRentalIncome;
            if (rentReceived > 0)
            {
                annualRentalIncome = Round(rentReceived * annualisation);

                if (isExtrapolated)
                {
                    warnings.Add(new MetricWarning(
                        "ExtrapolatedIncome",
                        $"Only {observedDays} days of history — annual figures are scaled up from that period."));
                }
            }
            else if (input.ActiveMonthlyRent is { } scheduled and > 0)
            {
                // No payments recorded, but there is a tenancy: fall back to what is
                // contracted and say so, rather than reporting a zero-income property.
                annualRentalIncome = scheduled * 12;
                warnings.Add(new MetricWarning(
                    "ScheduledRentUsed",
                    "No rent payments recorded, so contracted rent is used instead of collected rent."));
            }
            else
            {
                annualRentalIncome = null;
                warnings.Add(new MetricWarning(
                    "NoIncomeData",
                    "No rent payments recorded and no active tenancy, so income-based metrics are unavailable."));
            }

            decimal? annualOperatingExpenses = operatingCosts > 0
                ? Round(operatingCosts * annualisation)
                : null;

            if (annualOperatingExpenses is null && annualRentalIncome is not null)
            {
                warnings.Add(new MetricWarning(
                    "NoExpenseData",
                    "No operating expenses recorded — net figures will look better than reality."));
            }

            var netOperatingIncome = annualRentalIncome is null
                ? (decimal?)null
                : annualRentalIncome - (annualOperatingExpenses ?? 0m);

            // Prefer what was actually paid; fall back to the contractual repayment.
            decimal? annualDebtService = financingPaid > 0
                ? Round(financingPaid * annualisation)
                : input.MortgageMonthlyPayment * 12;

            var hasMortgage = (input.MortgageBalance ?? 0m) > 0 || (input.MortgageOriginalAmount ?? 0m) > 0;
            if (!hasMortgage)
            {
                warnings.Add(new MetricWarning(
                    "NoMortgage",
                    "No mortgage linked to this property, so leveraged returns match unleveraged ones."));
            }

            var monthlyCashFlow = netOperatingIncome is null
                ? (decimal?)null
                : Round((netOperatingIncome.Value - (annualDebtService ?? 0m)) / 12);

            // ---------------------------------------------------------------
            // Position: value, equity, appreciation
            // ---------------------------------------------------------------
            decimal? currentValue = input.Status == PropertyStatus.Sold && input.SalePrice is not null
                ? input.SalePrice
                : input.CurrentValuation ?? input.PurchasePrice;

            if (input.CurrentValuation is null && input.Status != PropertyStatus.Sold)
            {
                warnings.Add(new MetricWarning(
                    "NoValuation",
                    "No valuation recorded, so the purchase price is used as the current value and appreciation reads as zero."));
            }

            var equity = currentValue is null ? null : currentValue - (input.MortgageBalance ?? 0m);

            decimal? appreciation = currentValue is null || input.PurchasePrice is null
                ? null
                : currentValue - input.PurchasePrice;

            var appreciationPercent = Divide(appreciation, input.PurchasePrice);

            // ---------------------------------------------------------------
            // Lifetime cash flow and return
            // ---------------------------------------------------------------
            // Capital spend is excluded here because it is counted as invested capital, and
            // deposits are excluded because they are held on behalf of the tenant.
            var cumulativeNetCashFlow = input.Transactions
                .Where(t => !TransactionCategoryInfo.IsCapital(t.Category)
                            && t.Category != TransactionCategory.DepositReceived
                            && t.Date.Date <= asOf)
                .Sum(t => t.Category is TransactionCategory.RentIncome or TransactionCategory.OtherIncome
                    ? t.Amount
                    : -t.Amount);

            var endDate = input.Status == PropertyStatus.Sold && input.SaleDate is { } sold ? sold.Date : asOf;

            decimal? yearsHeld = input.PurchaseDate is { } start && endDate > start.Date
                ? Math.Round((decimal)(endDate - start.Date).Days / DaysPerYear, 4)
                : null;

            // What selling today would leave, plus what has already been collected, less
            // what was put in.
            decimal? totalReturn = equity is null || cashInvested is null
                ? null
                : equity + cumulativeNetCashFlow - cashInvested;

            var totalRoi = Divide(totalReturn, cashInvested);

            decimal? annualizedRoi = null;
            if (totalRoi is { } roi && yearsHeld is { } years && years > 0 && roi > -1m)
            {
                annualizedRoi = Math.Round(
                    (decimal)Math.Pow((double)(1m + roi), 1.0 / (double)years) - 1m, 4);
            }

            // ---------------------------------------------------------------
            // Internal rate of return
            // ---------------------------------------------------------------
            // Unlike AnnualizedRoi, which only looks at the start and end balances, this
            // discounts every dated cash flow individually, so two properties that reached the
            // same total return on a different capital-injection schedule no longer look alike.
            decimal? irr = null;

            var hasRealTerminalValue = (input.Status == PropertyStatus.Sold && input.SalePrice is not null)
                                        || input.CurrentValuation is not null;

            if (input.PurchasePrice is not { } purchasePrice)
            {
                // Already warned above via NoPurchasePrice: without it there is no dated
                // outflow to anchor the schedule to.
            }
            else if (input.PurchaseDate is not { } irrStart)
            {
                warnings.Add(new MetricWarning(
                    "IrrNoPurchaseDate",
                    "No purchase date recorded, so cash flows cannot be dated and IRR cannot be calculated."));
            }
            else if (!hasRealTerminalValue || currentValue is not { } terminalValue)
            {
                warnings.Add(new MetricWarning(
                    "IrrNoValuation",
                    "No valuation recorded to anchor a terminal value, so IRR cannot be calculated."));
            }
            else
            {
                var cashFlows = new List<(DateTime Date, decimal Amount)>
                {
                    // The down payment: the mortgage covers the rest, and its proceeds never
                    // pass through the investor's hands.
                    (irrStart, -(purchasePrice - (input.MortgageOriginalAmount ?? 0m))),
                };

                cashFlows.AddRange(input.Transactions
                    .Where(t => t.Category != TransactionCategory.DepositReceived && t.Date.Date <= endDate)
                    .Select(t => (
                        t.Date.Date,
                        TransactionCategoryInfo.DirectionOf(t.Category) == CashFlowDirection.Income
                            ? t.Amount
                            : -t.Amount)));

                cashFlows.Add((endDate, terminalValue - (input.MortgageBalance ?? 0m)));

                if (cashFlows.Select(c => c.Date).Distinct().Count() < 2)
                {
                    warnings.Add(new MetricWarning(
                        "IrrTooFewCashFlows",
                        "Fewer than two dated cash flows, so IRR cannot be calculated."));
                }
                else
                {
                    irr = SolveIrr(cashFlows, irrStart);
                    if (irr is null)
                    {
                        warnings.Add(new MetricWarning(
                            "IrrDidNotConverge",
                            "The recorded cash flows do not resolve to a single rate of return, so IRR cannot be calculated."));
                    }
                }
            }

            // ---------------------------------------------------------------
            // Occupancy
            // ---------------------------------------------------------------
            decimal? occupancyRate = null;
            if (input.PurchaseDate is { } owned && endDate > owned.Date)
            {
                var ownedDays = (endDate - owned.Date).Days;
                var occupiedDays = CountOccupiedDays(input.Occupancy, owned.Date, endDate);
                occupancyRate = ownedDays > 0
                    ? Math.Round((decimal)occupiedDays / ownedDays, 4)
                    : null;
            }

            // ---------------------------------------------------------------
            // Rent versus market
            // ---------------------------------------------------------------
            decimal? rentGapPercent = null;
            decimal? annualRentUplift = null;

            if (input.MarketMonthlyRent is { } market and > 0 && input.ActiveMonthlyRent is { } contracted)
            {
                rentGapPercent = Math.Round((market - contracted) / market, 4);
                annualRentUplift = Round((market - contracted) * 12);
            }

            return new PropertyMetrics
            {
                PropertyId = input.PropertyId,
                PropertyName = input.PropertyName,
                CurrencyCode = input.CurrencyCode,
                AsOf = asOf,

                TotalInvested = Round(totalInvested),
                CashInvested = Round(cashInvested),

                AnnualRentalIncome = annualRentalIncome,
                AnnualOperatingExpenses = annualOperatingExpenses,
                NetOperatingIncome = Round(netOperatingIncome),
                AnnualDebtService = Round(annualDebtService),
                MonthlyCashFlow = monthlyCashFlow,

                GrossYield = Divide(annualRentalIncome, totalInvested),
                NetYield = Divide(netOperatingIncome, totalInvested),
                CapRate = Divide(netOperatingIncome, currentValue),
                CashOnCashReturn = netOperatingIncome is null
                    ? null
                    : Divide(netOperatingIncome - (annualDebtService ?? 0m), cashInvested),

                CurrentValue = Round(currentValue),
                Equity = Round(equity),
                Appreciation = Round(appreciation),
                AppreciationPercent = appreciationPercent,
                CumulativeNetCashFlow = Round(cumulativeNetCashFlow),
                TotalReturn = Round(totalReturn),
                TotalRoi = totalRoi,
                AnnualizedRoi = annualizedRoi,
                Irr = irr,
                YearsHeld = yearsHeld,
                OccupancyRate = occupancyRate,

                MarketMonthlyRent = Round(input.MarketMonthlyRent),
                ContractedMonthlyRent = Round(input.ActiveMonthlyRent),
                RentGapPercent = rentGapPercent,
                AnnualRentUplift = annualRentUplift,

                Warnings = warnings,
            };
        }

        /// <summary>
        /// Total days let, merging overlapping tenancies so a handover that overlaps by a
        /// day cannot push occupancy above 100%.
        /// </summary>
        private static int CountOccupiedDays(
            IReadOnlyList<OccupancyWindow> windows, DateTime from, DateTime to)
        {
            var clipped = windows
                .Select(w => (
                    Start: w.Start.Date < from ? from : w.Start.Date,
                    End: (w.End?.Date ?? to) > to ? to : (w.End?.Date ?? to)))
                .Where(w => w.End > w.Start)
                .OrderBy(w => w.Start)
                .ToList();

            var total = 0;
            DateTime? cursorEnd = null;
            DateTime cursorStart = default;

            foreach (var window in clipped)
            {
                if (cursorEnd is null)
                {
                    cursorStart = window.Start;
                    cursorEnd = window.End;
                    continue;
                }

                if (window.Start <= cursorEnd)
                {
                    if (window.End > cursorEnd) cursorEnd = window.End;
                }
                else
                {
                    total += (cursorEnd.Value - cursorStart).Days;
                    cursorStart = window.Start;
                    cursorEnd = window.End;
                }
            }

            if (cursorEnd is not null)
                total += (cursorEnd.Value - cursorStart).Days;

            return total;
        }

        private const int IrrMaxIterations = 100;
        private const double IrrToleranceCurrencyUnits = 0.01;

        /// <summary>
        /// Solves for the annualised rate at which <paramref name="cashFlows"/>, discounted
        /// back to <paramref name="baseline"/> at day-count fractions of a 365-day year, net to
        /// zero. Newton-Raphson, the same closed-form-free approach a spreadsheet's XIRR uses,
        /// because cash flows land on irregular dates rather than evenly spaced periods.
        ///
        /// Returns null rather than throwing when the flows do not resolve to a rate — always
        /// positive, always negative, or a search that runs away — because the correct answer
        /// to "cannot be solved" is unknown, not a number that happens to fall out of a capped
        /// iteration count.
        /// </summary>
        private static decimal? SolveIrr(IReadOnlyList<(DateTime Date, decimal Amount)> cashFlows, DateTime baseline)
        {
            if (!cashFlows.Any(c => c.Amount > 0m) || !cashFlows.Any(c => c.Amount < 0m))
                return null;

            double Npv(double rate)
            {
                var total = 0.0;
                foreach (var (date, amount) in cashFlows)
                {
                    var years = (date - baseline).Days / (double)DaysPerYear;
                    total += (double)amount / Math.Pow(1 + rate, years);
                }

                return total;
            }

            double NpvDerivative(double rate)
            {
                var total = 0.0;
                foreach (var (date, amount) in cashFlows)
                {
                    var years = (date - baseline).Days / (double)DaysPerYear;
                    if (years == 0) continue;
                    total += (double)amount * -years * Math.Pow(1 + rate, -years - 1);
                }

                return total;
            }

            var rate = 0.1;
            for (var i = 0; i < IrrMaxIterations; i++)
            {
                var npv = Npv(rate);
                if (Math.Abs(npv) < IrrToleranceCurrencyUnits)
                    return Math.Round((decimal)rate, 4);

                var derivative = NpvDerivative(rate);
                if (derivative == 0) return null;

                var next = rate - npv / derivative;

                // Newton's method can step past -100%, where (1+rate) turns negative and the
                // discount factor stops meaning anything. Halve the step towards -100% instead
                // of following it there.
                if (next <= -1) next = (rate - 1) / 2;
                if (double.IsNaN(next) || double.IsInfinity(next)) return null;

                rate = next;
            }

            return null;
        }

        /// <summary>Ratio that yields null rather than a divide-by-zero or a nonsense figure.</summary>
        private static decimal? Divide(decimal? numerator, decimal? denominator) =>
            numerator is null || denominator is null || denominator == 0m
                ? null
                : Math.Round(numerator.Value / denominator.Value, 4);

        private static decimal? Round(decimal? value) =>
            value is null ? null : Math.Round(value.Value, 2);

        private static decimal Round(decimal value) => Math.Round(value, 2);
    }
}
