namespace MoneyManager.Api.Models
{
    /// <summary>
    /// Records that a landlord has seen and dismissed one due-date agenda entry.
    ///
    /// <para>
    /// Identified by <see cref="Key"/> rather than a foreign key into any one table, because the
    /// entry it points at might not be a row at all: it can be a manual <see cref="UpcomingEvent"/>,
    /// or an entry <c>Services.Agenda.AgendaBuilder</c> derived from a lease or a loan and never
    /// stored anywhere. <c>AgendaBuilder</c> and this table are the only two places that need to
    /// agree on the key format.
    /// </para>
    ///
    /// <para>
    /// A rent key embeds the due date it was computed for (<c>rent:{leaseId}:{yyyy-MM-dd}</c>), so
    /// acknowledging this month's reminder does not silently swallow next month's — that is a
    /// different string, and was never acknowledged. A loan key and a manual-event key carry no
    /// date (<c>loan:{loanId}</c>, <c>manual:{eventId}</c>), because neither has a monthly
    /// occurrence to distinguish: acknowledging either is a standing "stop reminding me" until the
    /// underlying row itself changes.
    /// </para>
    /// </summary>
    public class AgendaAcknowledgement : IOwnedByUser
    {
        public int Id { get; set; }  // Primary Key
        public int UserId { get; set; }

        public string Key { get; set; } = string.Empty;

        public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
    }
}
