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

        public DbSet<Lease> Leases { get; set; }

        public DbSet<PropertyTransaction> PropertyTransactions { get; set; }

        public DbSet<PropertyValuation> PropertyValuations { get; set; }

        public DbSet<RentPricePoint> RentPricePoints { get; set; }

        public DbSet<PropertyEvent> PropertyEvents { get; set; }

        /// <summary>Shared reference data — intentionally not tenant-scoped. See <see cref="ExchangeRate"/>.</summary>
        public DbSet<ExchangeRate> ExchangeRates { get; set; }

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
            ConfigureOwnership<Lease>(modelBuilder);
            ConfigureOwnership<PropertyTransaction>(modelBuilder);
            ConfigureOwnership<PropertyValuation>(modelBuilder);
            ConfigureOwnership<RentPricePoint>(modelBuilder);
            ConfigureOwnership<PropertyEvent>(modelBuilder);

            modelBuilder.Entity<RentalProperty>(entity =>
            {
                entity.Property(p => p.CurrencyCode).HasMaxLength(3);
                entity.HasIndex(p => new { p.UserId, p.City });

                entity.HasMany(p => p.Leases)
                      .WithOne(l => l.RentalProperty!)
                      .HasForeignKey(l => l.RentalPropertyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.Transactions)
                      .WithOne(t => t.RentalProperty!)
                      .HasForeignKey(t => t.RentalPropertyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.Valuations)
                      .WithOne(v => v.RentalProperty!)
                      .HasForeignKey(v => v.RentalPropertyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.RentPricePoints)
                      .WithOne(r => r.RentalProperty!)
                      .HasForeignKey(r => r.RentalPropertyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(p => p.Events)
                      .WithOne(e => e.RentalProperty!)
                      .HasForeignKey(e => e.RentalPropertyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Loan>(entity =>
            {
                // A mortgage is optional on a loan, and deleting a property should not
                // silently delete the debt that funded it.
                entity.HasOne(l => l.RentalProperty)
                      .WithMany()
                      .HasForeignKey(l => l.RentalPropertyId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PropertyTransaction>(entity =>
            {
                entity.HasIndex(t => new { t.RentalPropertyId, t.Date });
            });

            modelBuilder.Entity<RentPricePoint>(entity =>
            {
                entity.HasIndex(r => new { r.RentalPropertyId, r.Source, r.EffectiveFrom });
            });

            modelBuilder.Entity<PropertyValuation>(entity =>
            {
                entity.HasIndex(v => new { v.RentalPropertyId, v.ValuedOn });
            });

            modelBuilder.Entity<Lease>(entity =>
            {
                entity.HasIndex(l => new { l.RentalPropertyId, l.StartDate });
            });

            modelBuilder.Entity<PropertyEvent>(entity =>
            {
                entity.HasIndex(e => new { e.RentalPropertyId, e.OccurredOn });
            });

            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.Property(r => r.FromCurrency).HasMaxLength(3);
                entity.Property(r => r.ToCurrency).HasMaxLength(3);

                // One rate per pair per day. Re-entering a correction updates in place
                // rather than leaving two contradictory rows for the same date.
                entity.HasIndex(r => new { r.FromCurrency, r.ToCurrency, r.AsOf }).IsUnique();
            });
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
                    case EntityState.Added when _currentUser.UserId is { } currentUserId:
                        // Inside a request the owner always comes from the token, so a
                        // UserId in the payload is overwritten rather than honoured.
                        entry.Entity.UserId = currentUserId;
                        break;

                    case EntityState.Added when entry.Entity.UserId > 0:
                        // No authenticated user: background work writing on a known user's
                        // behalf. Reachable only outside a request — every HTTP endpoint is
                        // behind the fallback authorization policy — so this cannot be used
                        // to smuggle an owner in through a request body.
                        break;

                    case EntityState.Added:
                        throw new InvalidOperationException(
                            "Cannot persist a user-owned entity outside an authenticated request "
                            + "without an explicit owner.");

                    case EntityState.Modified:
                        entry.Property(nameof(IOwnedByUser.UserId)).IsModified = false;
                        break;
                }
            }
        }
    }
}
