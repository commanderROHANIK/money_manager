namespace MoneyManager.Api.Models
{
    public enum PropertyType
    {
        Apartment = 0,
        House = 1,
        Room = 2,
        Commercial = 3,
        Land = 4,
    }

    public enum PropertyStatus
    {
        Active = 0,
        Renovating = 1,
        Sold = 2,
    }

    public enum CashFlowDirection
    {
        Income = 0,
        Expense = 1,
    }

    /// <summary>
    /// What a movement of money against a property was for. The classification in
    /// <see cref="TransactionCategoryInfo"/> is what turns a ledger into yields, cap rate
    /// and invested capital, so adding a category means deciding how it is classified.
    /// </summary>
    public enum TransactionCategory
    {
        // Income
        RentIncome = 0,
        DepositReceived = 1,
        OtherIncome = 2,

        // Operating expenses — these reduce net operating income.
        Insurance = 10,
        PropertyTax = 11,
        Maintenance = 12,
        Repairs = 13,
        ManagementFee = 14,
        Utilities = 15,
        ServiceCharge = 16,
        OtherExpense = 17,

        // Financing — excluded from NOI, included in cash flow.
        MortgagePayment = 30,

        // Capital — raises invested capital rather than reducing operating profit.
        AcquisitionCost = 50,
        CapitalImprovement = 51,
    }

    public enum RentPriceSource
    {
        /// <summary>What the landlord actually charges.</summary>
        Contracted = 0,

        /// <summary>What the market is estimated to pay, from a market rent provider.</summary>
        MarketEstimate = 1,
    }

    public enum ValuationSource
    {
        PurchasePrice = 0,
        OwnerEstimate = 1,
        Appraisal = 2,
        AutomatedModel = 3,
    }

    public enum PropertyEventType
    {
        Purchase = 0,
        TenantMovedIn = 1,
        TenantMovedOut = 2,
        RentChanged = 3,
        Maintenance = 4,
        Inspection = 5,
        Renovation = 6,
        Valuation = 7,
        MortgageLinked = 8,
        Sale = 9,
        Note = 10,
    }

    public enum LoanType
    {
        Mortgage = 0,
        Personal = 1,
        Other = 2,
    }

    /// <summary>
    /// How each category is treated by the analytics engine. Kept as one table of facts so
    /// the accounting rules live in a single place instead of being re-derived per metric.
    /// </summary>
    public static class TransactionCategoryInfo
    {
        public static CashFlowDirection DirectionOf(TransactionCategory category) => category switch
        {
            TransactionCategory.RentIncome or
            TransactionCategory.DepositReceived or
            TransactionCategory.OtherIncome => CashFlowDirection.Income,
            _ => CashFlowDirection.Expense,
        };

        /// <summary>Counts against net operating income: recurring costs of running the property.</summary>
        public static bool IsOperatingExpense(TransactionCategory category) => category switch
        {
            TransactionCategory.Insurance or
            TransactionCategory.PropertyTax or
            TransactionCategory.Maintenance or
            TransactionCategory.Repairs or
            TransactionCategory.ManagementFee or
            TransactionCategory.Utilities or
            TransactionCategory.ServiceCharge or
            TransactionCategory.OtherExpense => true,
            _ => false,
        };

        /// <summary>Adds to invested capital rather than to running costs.</summary>
        public static bool IsCapital(TransactionCategory category) =>
            category is TransactionCategory.AcquisitionCost or TransactionCategory.CapitalImprovement;

        /// <summary>Debt service. Excluded from NOI so that cap rate stays independent of financing.</summary>
        public static bool IsFinancing(TransactionCategory category) =>
            category is TransactionCategory.MortgagePayment;

        /// <summary>Rental revenue proper — excludes deposits, which are a liability, not income.</summary>
        public static bool IsRentalIncome(TransactionCategory category) =>
            category is TransactionCategory.RentIncome;
    }
}
