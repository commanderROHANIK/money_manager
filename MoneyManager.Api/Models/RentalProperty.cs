namespace MoneyManager.Api.Models
{
    public class RentalProperty : IOwnedByUser
    {
        public int Id { get; set; }  // Primary Key
        public int UserId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal RentAmount { get; set; }
        public DateTime RentDueDate { get; set; } = DateTime.UtcNow;
        public bool IsRented { get; set; }

        /// <summary>
        /// The currency every figure on this property is denominated in. Fixed at creation:
        /// per-property analytics then run with no FX conversion at all, and conversion is
        /// confined to portfolio-level rollups.
        /// </summary>
        public string CurrencyCode { get; set; } = "EUR";
    }
}
