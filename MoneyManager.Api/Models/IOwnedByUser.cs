namespace MoneyManager.Api.Models
{
    /// <summary>
    /// Marks an entity as belonging to exactly one user. Every implementor gets a global
    /// query filter in <see cref="Data.MoneyManagerDbContext"/> and has its owner stamped
    /// on insert, so tenant isolation cannot be forgotten at the call site.
    /// </summary>
    public interface IOwnedByUser
    {
        int UserId { get; set; }
    }
}
