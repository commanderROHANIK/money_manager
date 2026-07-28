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
    }
}
