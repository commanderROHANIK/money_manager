namespace MoneyManager.Api.Models
{
    public class Stock
    {
        public int Id { get; set; }  // Primary Key
        public string Ticker { get; set; } = string.Empty;  // e.g. AAPL, MSFT
        public int SharesOwned { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    }
}
