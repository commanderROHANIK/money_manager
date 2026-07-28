namespace MoneyManager.Api.Models
{
    public class UpcomingEvent : IOwnedByUser
    {
        public int Id { get; set; }  // Primary Key
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime EventDate { get; set; } = DateTime.UtcNow;
        public bool IsRecurring { get; set; }
        public bool IsNotified { get; set; }  // Flag to track if user has been notified

        /// <summary>Optional link to the property this reminder concerns.</summary>
        public int? RentalPropertyId { get; set; }

        /// <summary>Optional link to the loan this reminder concerns.</summary>
        public int? LoanId { get; set; }
    }
}
