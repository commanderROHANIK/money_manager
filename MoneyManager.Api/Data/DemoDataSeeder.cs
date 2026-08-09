using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Data
{
    /// <summary>
    /// Creates the first account, and a small portfolio behind it, on a database that has
    /// neither.
    ///
    /// <para>
    /// This runs because a preview environment starts with an empty volume. An app with no data
    /// cannot be reviewed, which would defeat the point of having per-PR environments at all;
    /// and with registration disabled there is otherwise no way for an account to come to exist.
    /// </para>
    ///
    /// <para>
    /// Both halves are idempotent, and they have to be: the long-lived environment runs this on
    /// every single boot. Re-seeding there would quietly accumulate duplicate demo portfolios
    /// alongside real records.
    /// </para>
    ///
    /// <para>
    /// The account and the portfolio are written through two different contexts on purpose.
    /// <c>User</c> is not an owned entity and carries no query filter, so it can be read and
    /// written with no tenant. Everything in the portfolio *is* owned, so it needs
    /// <see cref="SeedCurrentUser"/> — both to satisfy the owner stamping in <c>SaveChanges</c>,
    /// which throws rather than write an unowned row, and to make the "already seeded" check
    /// below ask a question that has a real answer.
    /// </para>
    /// </summary>
    public static class DemoDataSeeder
    {
        public static async Task SeedAsync(
            IServiceProvider services, CancellationToken cancellationToken = default)
        {
            var options = services.GetRequiredService<IOptions<SeedOptions>>().Value;

            if (!options.Enabled)
                return;

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoDataSeeder));
            var contextOptions = services.GetRequiredService<DbContextOptions<MoneyManagerDbContext>>();
            var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();

            var userId = await EnsureAccountAsync(
                contextOptions, passwordHasher, options, logger, cancellationToken);

            if (options.IncludeDemoData)
                await EnsureDemoPortfolioAsync(contextOptions, userId, logger, cancellationToken);
        }

        /// <summary>Returns the seeded user's id, creating the account only if it is absent.</summary>
        private static async Task<int> EnsureAccountAsync(
            DbContextOptions<MoneyManagerDbContext> contextOptions,
            IPasswordHasher<User> passwordHasher,
            SeedOptions options,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            await using var context = new MoneyManagerDbContext(contextOptions, new NoCurrentUser());

            var normalizedUsername = options.Username.Trim().ToUpperInvariant();

            var existing = await context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);

            if (existing is not null)
                return existing.Id;

            var user = new User
            {
                Username = options.Username.Trim(),
                Email = options.Email.Trim(),
                NormalizedUsername = normalizedUsername,
                NormalizedEmail = options.Email.Trim().ToUpperInvariant(),
                BaseCurrency = "EUR",
            };

            // The same hasher registration the login path verifies against, so the seeded
            // account is an ordinary account rather than a special case.
            user.PasswordHash = passwordHasher.HashPassword(user, options.Password);

            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);

            // Username only. The configured password must never reach a log.
            logger.LogInformation("Seeded the initial account {Username}.", user.Username);

            return user.Id;
        }

        private static async Task EnsureDemoPortfolioAsync(
            DbContextOptions<MoneyManagerDbContext> contextOptions,
            int userId,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            await using var context = new MoneyManagerDbContext(contextOptions, new SeedCurrentUser(userId));

            // Meaningful only because the context above is scoped to the seeded user. Through a
            // null tenant the query filter would compare UserId against NULL, match nothing, and
            // report an empty database on every boot.
            if (await context.RentalProperties.AnyAsync(cancellationToken))
            {
                logger.LogInformation("Demo portfolio already present; leaving it alone.");
                return;
            }

            var today = DateTime.UtcNow.Date;

            // A local rather than reading property.PurchaseDate back: that is a nullable
            // DateTime, and .Value on it is a possible-null dereference the compiler is entitled
            // to flag — which under TreatWarningsAsErrors is a failed build, not a warning.
            var purchaseDate = today.AddYears(-3);

            var property = new RentalProperty
            {
                PropertyName = "Maple Court, Flat 2",
                Address = "14 Maple Court",
                City = "Utrecht",
                PostalCode = "3511 AA",
                CountryCode = "NL",
                PropertyType = PropertyType.Apartment,
                SizeSqm = 68m,
                Bedrooms = 2,
                PurchasePrice = 240_000m,
                PurchaseDate = purchaseDate,
                CurrencyCode = "EUR",
                Notes = "Seeded demo data.",
            };

            var lease = new Lease
            {
                TenantName = "R. Bakker",
                TenantEmail = "r.bakker@example.invalid",
                StartDate = today.AddMonths(-18),
                MonthlyRent = 1_150m,
                CurrencyCode = "EUR",
                RentDueDayOfMonth = 5,
                DepositAmount = 2_300m,
            };

            property.Leases.Add(lease);
            context.RentalProperties.Add(property);

            // Saved before the transactions so both rows carry real ids to reference. The rent
            // schedule matches a payment to a month by date, and treats a null LeaseId as
            // matching anything — but a demo whose payments name their tenancy exercises the
            // same path a real ledger does.
            await context.SaveChangesAsync(cancellationToken);

            var transactions = new List<PropertyTransaction>
            {
                new()
                {
                    RentalPropertyId = property.Id,
                    Date = purchaseDate,
                    Amount = 6_400m,
                    CurrencyCode = "EUR",
                    Category = TransactionCategory.AcquisitionCost,
                    Description = "Transfer tax and notary",
                },
                new()
                {
                    RentalPropertyId = property.Id,
                    Date = today.AddMonths(-7),
                    Amount = 480m,
                    CurrencyCode = "EUR",
                    Category = TransactionCategory.Insurance,
                    Description = "Annual building insurance",
                },
                new()
                {
                    RentalPropertyId = property.Id,
                    Date = today.AddMonths(-2),
                    Amount = 310m,
                    CurrencyCode = "EUR",
                    Category = TransactionCategory.Repairs,
                    Description = "Boiler service",
                },
            };

            // Six months of rent received, and the current month deliberately left unrecorded so
            // the rent schedule and the arrears list have something other than green to show.
            var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);

            for (var monthsAgo = 6; monthsAgo >= 1; monthsAgo--)
            {
                var paidOn = firstOfThisMonth.AddMonths(-monthsAgo).AddDays(lease.RentDueDayOfMonth - 1);

                transactions.Add(new PropertyTransaction
                {
                    RentalPropertyId = property.Id,
                    LeaseId = lease.Id,
                    Date = paidOn,
                    Amount = lease.MonthlyRent,
                    CurrencyCode = "EUR",
                    Category = TransactionCategory.RentIncome,
                    Description = $"Rent {paidOn:yyyy-MM}",
                });
            }

            context.PropertyTransactions.AddRange(transactions);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Seeded a demo portfolio for user {UserId}.", userId);
        }
    }
}
