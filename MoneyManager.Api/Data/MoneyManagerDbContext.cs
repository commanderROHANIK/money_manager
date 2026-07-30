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

                // Covers the peer-comparable lookup, which runs with the tenant filter off
                // and so cannot lean on the UserId index above.
                entity.HasIndex(p => new { p.NormalizedCity, p.PropertyType, p.CurrencyCode });

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

        private bool _explicitOwnerAllowed;

        /// <summary>
        /// Opens the one window in which an owner assigned in code is honoured instead of
        /// taken from the token, for background work that legitimately writes on a known
        /// user's behalf outside any request.
        ///
        /// This is an explicit opt-in rather than an inference from "there is no current
        /// user" on purpose. Treating the absence of a user as permission fails open: it
        /// would silently extend to any future endpoint that ends up without a resolvable
        /// principal, turning a payload-supplied <c>UserId</c> into a cross-tenant write
        /// with nothing in the type system or the tests to notice.
        /// </summary>
        public IDisposable AllowExplicitOwnerAssignment()
        {
            if (_currentUser.UserId is not null)
                throw new InvalidOperationException(
                    "Explicit owner assignment is for background work only; inside a request "
                    + "the owner always comes from the token.");

            _explicitOwnerAllowed = true;
            return new ExplicitOwnerScope(this);
        }

        private sealed class ExplicitOwnerScope(MoneyManagerDbContext context) : IDisposable
        {
            public void Dispose() => context._explicitOwnerAllowed = false;
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

                    case EntityState.Added when _explicitOwnerAllowed && entry.Entity.UserId > 0:
                        // Background work writing on a known user's behalf, inside a scope
                        // that had to be opened deliberately. A request can never reach this
                        // branch: opening the scope throws while a current user exists.
                        break;

                    case EntityState.Added:
                        throw new InvalidOperationException(
                            "Cannot persist a user-owned entity outside an authenticated request "
                            + "without an explicit owner. Background work must write inside "
                            + nameof(AllowExplicitOwnerAssignment) + ".");

                    case EntityState.Modified:
                        entry.Property(nameof(IOwnedByUser.UserId)).IsModified = false;
                        break;
                }
            }
        }
    }
}
