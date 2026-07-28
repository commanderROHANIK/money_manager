namespace MoneyManager.Api.Models
{
    public class BankAccount : IOwnedByUser
    {
        public int Id { get; set; }  // Primary Key
        public int UserId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty; // e.g. "Checking", "Savings"
        public string CurrencyCode { get; set; } = "EUR";
    }
}
