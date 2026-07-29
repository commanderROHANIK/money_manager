using System.Text.Json.Serialization;

namespace MoneyManager.Api.Models
{
    public class Loan : IOwnedByUser
    {
        public int Id { get; set; }  // Primary Key
        public int UserId { get; set; }
        public string LoanName { get; set; } = string.Empty;
        public decimal LoanAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public decimal InterestRate { get; set; }
        public DateTime DueDate { get; set; } = DateTime.UtcNow;
        public bool IsPaidOff { get; set; }
        public string CurrencyCode { get; set; } = "EUR";

        public LoanType LoanType { get; set; } = LoanType.Personal;

        /// <summary>
        /// Set when this loan is the mortgage secured on a property. This is what makes
        /// equity (value minus outstanding debt) and leveraged returns computable — the two
        /// were previously unrelated tables with no way to say which debt funded which asset.
        /// </summary>
        public int? RentalPropertyId { get; set; }

        [JsonIgnore]
        public RentalProperty? RentalProperty { get; set; }

        /// <summary>Contractual repayment. Needed for cash flow and cash-on-cash return.</summary>
        public decimal? MonthlyPayment { get; set; }

        public DateTime? StartDate { get; set; }
        public int? TermMonths { get; set; }
    }
}
