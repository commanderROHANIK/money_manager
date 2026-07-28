namespace MoneyManager.Api.Models
{
    /// <summary>
    /// One movement of money for one property. This is the entity the rest of the product
    /// rests on: without it nothing can record that rent arrived or that a boiler was
    /// replaced, and every return metric is guesswork.
    /// </summary>
    public class PropertyTransaction : IOwnedByUser
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public int RentalPropertyId { get; set; }
        public RentalProperty? RentalProperty { get; set; }

        /// <summary>Set when the payment is attributable to a specific tenancy.</summary>
        public int? LeaseId { get; set; }

        public DateTime Date { get; set; }

        /// <summary>
        /// Always positive. Direction comes from the category, so there is one sign
        /// convention instead of two ways to represent an expense.
        /// </summary>
        public decimal Amount { get; set; }

        public string CurrencyCode { get; set; } = "EUR";

        public TransactionCategory Category { get; set; }

        public string Description { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public CashFlowDirection Direction => TransactionCategoryInfo.DirectionOf(Category);

        /// <summary>Amount as it affects cash: income positive, expense negative.</summary>
        public decimal SignedAmount =>
            Direction == CashFlowDirection.Income ? Amount : -Amount;
    }
}
