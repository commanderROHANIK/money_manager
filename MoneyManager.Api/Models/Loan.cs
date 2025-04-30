namespace MoneyManager.Api.Models
{
    public class Loan
    {
        public int Id { get; set; }  // Primary Key
        public string LoanName { get; set; } = string.Empty;
        public decimal LoanAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public decimal InterestRate { get; set; }
        public DateTime DueDate { get; set; }
        public bool IsPaidOff { get; set; }
    }
}
