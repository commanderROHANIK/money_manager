using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Data
{
    public class MoneyManagerDbContext : DbContext
    {
        public MoneyManagerDbContext(DbContextOptions<MoneyManagerDbContext> options)
            : base(options)
        {
        }

        public DbSet<BankAccount> BankAccounts { get; set; }

        public DbSet<Loan> Loans { get; set; }

        public DbSet<RentalProperty> RentalProperties { get; set; }

        public DbSet<Stock> Stocks { get; set; }

        public DbSet<UpcomingEvent> UpcomingEvents { get; set; }

        public DbSet<User> Users { get; set; }
    }
}
