using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Data
{
    public class MoneyManagerDbContext : DbContext
    {
        private readonly ICurrentUser _currentUser;

        public MoneyManagerDbContext(DbContextOptions<MoneyManagerDbContext> options, ICurrentUser currentUser)
            : base(options)
        {
            _currentUser = currentUser;
        }

        public DbSet<BankAccount> BankAccounts { get; set; }

        public DbSet<Loan> Loans { get; set; }

        public DbSet<RentalProperty> RentalProperties { get; set; }

        public DbSet<Stock> Stocks { get; set; }

        public DbSet<UpcomingEvent> UpcomingEvents { get; set; }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.NormalizedUsername).IsUnique();
                entity.HasIndex(u => u.NormalizedEmail).IsUnique();
                entity.Property(u => u.BaseCurrency).HasMaxLength(3);
            });

            // Every owned entity is filtered to the requesting user and cascades when that
            // user is deleted. Applying this centrally is the point: a controller that
            // forgets a .Where(...) still cannot read across the tenant boundary, whereas
            // per-query filtering fails open the moment someone forgets one.
            ConfigureOwnership<BankAccount>(modelBuilder);
            ConfigureOwnership<Loan>(modelBuilder);
            ConfigureOwnership<RentalProperty>(modelBuilder);
            ConfigureOwnership<Stock>(modelBuilder);
            ConfigureOwnership<UpcomingEvent>(modelBuilder);
        }

        private void ConfigureOwnership<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, IOwnedByUser
        {
            modelBuilder.Entity<TEntity>(entity =>
            {
                entity.HasIndex(e => e.UserId);

                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Referencing the context field (rather than capturing a value) is what lets
                // EF re-evaluate the tenant per query instead of baking the first one into
                // the cached model.
                entity.HasQueryFilter(e => e.UserId == _currentUser.UserId);
            });
        }

        public override int SaveChanges()
        {
            ApplyOwnership();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyOwnership();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Stamps the owner on insert and pins it on update, so ownership is never taken
        /// from the request body and an existing row can never be reassigned to another user.
        /// </summary>
        private void ApplyOwnership()
        {
            foreach (var entry in ChangeTracker.Entries<IOwnedByUser>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.UserId = _currentUser.UserId
                            ?? throw new InvalidOperationException(
                                "Cannot persist a user-owned entity outside an authenticated request.");
                        break;

                    case EntityState.Modified:
                        entry.Property(nameof(IOwnedByUser.UserId)).IsModified = false;
                        break;
                }
            }
        }
    }
}
