namespace MoneyManager.Api.Models
{
    public class UpcomingEvent
    {
        public int Id { get; set; }  // Primary Key
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime EventDate { get; set; } = DateTime.UtcNow;
        public bool IsRecurring { get; set; }
        public bool IsNotified { get; set; }  // Flag to track if user has been notified
    }
}
