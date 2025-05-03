namespace MoneyManager.Api.Models
{
    public class RentalProperty
    {
        public int Id { get; set; }  // Primary Key
        public string PropertyName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal RentAmount { get; set; }
        public DateTime RentDueDate { get; set; } = DateTime.UtcNow;
        public bool IsRented { get; set; }
    }
}
