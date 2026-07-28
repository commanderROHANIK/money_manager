namespace MoneyManager.Api.Models
{
    /// <summary>
    /// A tenancy. Rent belongs to a tenancy rather than to a building, which is what makes
    /// occupancy history, vacancy periods and rent changes over time expressible at all.
    /// </summary>
    public class Lease : IOwnedByUser
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public int RentalPropertyId { get; set; }
        public RentalProperty? RentalProperty { get; set; }

        public string TenantName { get; set; } = string.Empty;
        public string? TenantEmail { get; set; }
        public string? TenantPhone { get; set; }

        public DateTime StartDate { get; set; }

        /// <summary>Null means open-ended and still running.</summary>
        public DateTime? EndDate { get; set; }

        public decimal MonthlyRent { get; set; }
        public string CurrencyCode { get; set; } = "EUR";

        public int RentDueDayOfMonth { get; set; } = 1;

        public decimal? DepositAmount { get; set; }

        public string? Notes { get; set; }

        public bool IsActiveOn(DateTime date) =>
            StartDate.Date <= date.Date && (EndDate is null || EndDate.Value.Date >= date.Date);
    }
}
