using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MoneyManager.Api.Infrastructure;

namespace MoneyManager.Api.Data
{
    /// <summary>
    /// The DbContext needs an <see cref="ICurrentUser"/> for its tenant query filters, which
    /// the migration tooling has no way to supply. This factory hands it a no-tenant accessor
    /// so <c>dotnet ef</c> can build the model without spinning up the web host.
    /// </summary>
    public sealed class MoneyManagerDbContextFactory : IDesignTimeDbContextFactory<MoneyManagerDbContext>
    {
        public MoneyManagerDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<MoneyManagerDbContext>()
                .UseSqlite("Data Source=moneymanager.db")
                .Options;

            return new MoneyManagerDbContext(options, new NoCurrentUser());
        }
    }
}
