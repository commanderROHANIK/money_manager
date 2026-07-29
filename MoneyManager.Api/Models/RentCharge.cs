using System.Text.Json.Serialization;

namespace MoneyManager.Api.Models
{
    /// <summary>
    /// Rent owed for one period under one tenancy.
    ///
    /// This is what makes "who has not paid" answerable. <see cref="Lease"/> has always
    /// carried the rent and the due day, and <see cref="PropertyTransaction"/> has always
    /// recorded money arriving, but nothing reconciled the two — so a missed month looked
    /// identical to a month nobody had got round to entering.
    /// </summary>
    public class RentCharge : IOwnedByUser
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public int LeaseId { get; set; }

        [JsonIgnore]
        public Lease? Lease { get; set; }

        /// <summary>Denormalised from the lease so arrears can be listed without a join.</summary>
        public int RentalPropertyId { get; set; }

        /// <summary>
        /// First day of the period this charge covers. Together with <see cref="LeaseId"/>
        /// this is unique, which is what makes charge generation safe to re-run.
        /// </summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>Inclusive last day of the period.</summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>When the money was expected.</summary>
        public DateTime DueDate { get; set; }

        /// <summary>
        /// What is owed for this period. Less than the lease's monthly rent when the tenancy
        /// started or ended mid-period.
        /// </summary>
        public decimal AmountDue { get; set; }

        public string CurrencyCode { get; set; } = "EUR";

        /// <summary>
        /// Settled so far. Kept as a stored total rather than recomputed per request so that
        /// arrears stays one indexed query rather than a scan of the whole ledger.
        /// </summary>
        public decimal AmountSettled { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public decimal Outstanding => Math.Max(0m, AmountDue - AmountSettled);

        public bool IsSettled => AmountSettled >= AmountDue;

        /// <summary>
        /// Days past due, or zero if settled or not yet due. Counted against the date the
        /// caller considers "now" rather than a clock read inside the model, so it stays
        /// testable.
        /// </summary>
        public int DaysLate(DateTime asOf) =>
            IsSettled || asOf.Date <= DueDate.Date ? 0 : (asOf.Date - DueDate.Date).Days;
    }
}
