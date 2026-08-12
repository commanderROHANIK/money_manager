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
            var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);

            // Three properties rather than one, because the question this product exists to answer
            // — "which of my properties is underperforming, and by how much" — has no answer over a
            // portfolio of one. The third is deliberately the weak one, and it is weak for reasons
            // the ledger can show: it is empty, it needed a roof, and it is carrying the larger
            // mortgage.
            var utrecht = HealthyFlat(today);
            var budapest = ForintFlat(today);
            var rotterdam = StrugglingFlat(today, firstOfThisMonth);

            context.RentalProperties.AddRange(utrecht, budapest, rotterdam);

            // Saved before the ledger so every row below can reference a real id. The rent schedule
            // matches a payment to a month by date and treats a null LeaseId as matching anything,
            // but a demo whose payments name their tenancy exercises the path a real ledger does.
            await context.SaveChangesAsync(cancellationToken);

            var transactions = new List<PropertyTransaction>();

            transactions.AddRange(HealthyFlatLedger(utrecht, today, firstOfThisMonth));
            transactions.AddRange(ForintFlatLedger(budapest, today, firstOfThisMonth));
            transactions.AddRange(StrugglingFlatLedger(rotterdam, today, firstOfThisMonth));

            context.PropertyTransactions.AddRange(transactions);
            context.Loans.AddRange(Mortgages(utrecht, rotterdam, today, firstOfThisMonth));
            context.PropertyEvents.AddRange(Timeline(utrecht, budapest, rotterdam, today, firstOfThisMonth));
            context.UpcomingEvents.AddRange(Reminders(utrecht, budapest, today));

            // A rate the user "entered", so a mixed-currency portfolio has a total on a machine
            // that has never reached the network — the demo cannot depend on egress it may not
            // have. It is also the more interesting starting state: the conversion note reads
            // "rate you entered", and the Settings screen can be shown handing that pair over to
            // the ECB live. Seeding a fetched-looking row instead would assert a provenance
            // nothing actually fetched, which is the one thing this feature must never do.
            context.ExchangeRates.Add(new ExchangeRate
            {
                BaseCurrency = "EUR",
                QuoteCurrency = "HUF",
                Rate = 398.0m,
                AsOf = today.AddMonths(-2),
                Source = ExchangeRateSource.Manual,
            });

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Seeded a demo portfolio of {Properties} properties for user {UserId}.", 3, userId);
        }

        /// <summary>
        /// Utrecht: occupied, paying, and the one with a valuation on record — so the dashboard has
        /// something to compare the other two against.
        /// </summary>
        private static RentalProperty HealthyFlat(DateTime today)
        {
            var purchased = today.AddYears(-3);

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
                PurchaseDate = purchased,
                CurrencyCode = "EUR",
                Notes = "Seeded demo data.",
            };

            property.Leases.Add(new Lease
            {
                TenantName = "R. Bakker",
                TenantEmail = "r.bakker@example.invalid",
                StartDate = today.AddMonths(-18),
                MonthlyRent = 1_150m,
                CurrencyCode = "EUR",
                RentDueDayOfMonth = 5,
                DepositAmount = 2_300m,
            });

            // An appraisal rather than the purchase price, so this property's return is computed
            // from a real valuation while Rotterdam's falls back — which is what makes the
            // "no valuation on record, using purchase price" warning visible side by side with a
            // figure that did not need it.
            property.Valuations.Add(new PropertyValuation
            {
                ValuedOn = today.AddMonths(-6),
                Value = 265_000m,
                CurrencyCode = "EUR",
                Source = ValuationSource.Appraisal,
                Notes = "Bank appraisal for the mortgage review.",
            });

            return property;
        }

        /// <summary>
        /// Budapest, in forint: the reason the portfolio total has to convert at all, and therefore
        /// the reason the conversion note has anything to disclose.
        /// </summary>
        private static RentalProperty ForintFlat(DateTime today)
        {
            var property = new RentalProperty
            {
                PropertyName = "Rákóczi út 12, Flat 4",
                Address = "Rákóczi út 12",
                City = "Budapest",
                PostalCode = "1072",
                CountryCode = "HU",
                PropertyType = PropertyType.Apartment,
                SizeSqm = 54m,
                Bedrooms = 2,
                PurchasePrice = 78_000_000m,
                PurchaseDate = today.AddYears(-2),
                CurrencyCode = "HUF",
                Notes = "Seeded demo data.",
            };

            property.Leases.Add(new Lease
            {
                TenantName = "K. Nagy",
                TenantEmail = "k.nagy@example.invalid",
                StartDate = today.AddMonths(-14),
                MonthlyRent = 265_000m,
                CurrencyCode = "HUF",
                RentDueDayOfMonth = 10,
                DepositAmount = 530_000m,
            });

            property.Valuations.Add(new PropertyValuation
            {
                ValuedOn = today.AddMonths(-3),
                Value = 88_000_000m,
                CurrencyCode = "HUF",
                Source = ValuationSource.OwnerEstimate,
                Notes = "Asking prices for comparable flats on the street.",
            });

            return property;
        }

        /// <summary>
        /// Rotterdam: the underperformer, and the point of having three.
        ///
        /// <para>
        /// Nothing about it is broken data — the tenancy ended four months ago and was never
        /// replaced, the roof cost nine thousand, and it carries the larger mortgage. Every one of
        /// those is a fact in the ledger, so the dashboard can say *why* it is behind rather than
        /// only that it is. It deliberately has no valuation, which is what makes the fallback
        /// warning appear next to a property that did not need one.
        /// </para>
        /// </summary>
        private static RentalProperty StrugglingFlat(DateTime today, DateTime firstOfThisMonth)
        {
            var property = new RentalProperty
            {
                PropertyName = "Kerkstraat 8",
                Address = "8 Kerkstraat",
                City = "Rotterdam",
                PostalCode = "3011 AB",
                CountryCode = "NL",
                PropertyType = PropertyType.Apartment,
                SizeSqm = 61m,
                Bedrooms = 1,
                PurchasePrice = 310_000m,
                PurchaseDate = today.AddYears(-2),
                CurrencyCode = "EUR",
                Notes = "Seeded demo data. Vacant since the last tenancy ended.",
            };

            // Ended, not absent. Occupancy and current rent are derived from the tenancy running
            // today (CLAUDE.md: "Derived, never stored"), so an ended lease is what makes this
            // property read as vacant — and it keeps its rent history, which a deleted one would
            // have thrown away.
            property.Leases.Add(new Lease
            {
                TenantName = "J. de Vries",
                TenantEmail = "j.devries@example.invalid",
                StartDate = today.AddMonths(-22),
                EndDate = firstOfThisMonth.AddMonths(-4),
                MonthlyRent = 1_050m,
                CurrencyCode = "EUR",
                RentDueDayOfMonth = 1,
                DepositAmount = 2_100m,
                Notes = "Gave notice; not yet re-let.",
            });

            return property;
        }

        private static IEnumerable<PropertyTransaction> HealthyFlatLedger(
            RentalProperty property, DateTime today, DateTime firstOfThisMonth)
        {
            var lease = property.Leases.First();

            yield return Spend(property, today.AddYears(-3), 6_400m, TransactionCategory.AcquisitionCost,
                "Transfer tax and notary");
            yield return Spend(property, today.AddMonths(-7), 480m, TransactionCategory.Insurance,
                "Annual building insurance");
            yield return Spend(property, today.AddMonths(-2), 310m, TransactionCategory.Repairs,
                "Boiler service");

            // Paid for the whole tenancy except the current month, which is deliberately left
            // unrecorded so the rent schedule and the arrears list have something other than green
            // to show. Stopping six months in would instead read as a year of unexplained arrears
            // on the property this portfolio is meant to hold up as the healthy one.
            foreach (var payment in RentReceived(property, lease, firstOfThisMonth, from: 17, to: 1))
                yield return payment;

            foreach (var payment in MortgagePaid(property, firstOfThisMonth, 780m, months: 6))
                yield return payment;
        }

        private static IEnumerable<PropertyTransaction> ForintFlatLedger(
            RentalProperty property, DateTime today, DateTime firstOfThisMonth)
        {
            var lease = property.Leases.First();

            yield return Spend(property, today.AddYears(-2), 2_100_000m, TransactionCategory.AcquisitionCost,
                "Duty and legal fees");
            yield return Spend(property, today.AddMonths(-5), 62_000m, TransactionCategory.PropertyTax,
                "Local property tax");
            yield return Spend(property, today.AddMonths(-3), 145_000m, TransactionCategory.Maintenance,
                "Communal stairwell repainting");

            // Paid up to and including this month, so one property in the portfolio is unambiguously
            // current — otherwise "in arrears" looks like the normal state of the app.
            foreach (var payment in RentReceived(property, lease, firstOfThisMonth, from: 13, to: 0))
                yield return payment;
        }

        private static IEnumerable<PropertyTransaction> StrugglingFlatLedger(
            RentalProperty property, DateTime today, DateTime firstOfThisMonth)
        {
            var lease = property.Leases.First();

            yield return Spend(property, today.AddYears(-2), 9_300m, TransactionCategory.AcquisitionCost,
                "Transfer tax and notary");
            yield return Spend(property, today.AddMonths(-3), 8_900m, TransactionCategory.Repairs,
                "Roof replacement after storm damage");
            yield return Spend(property, today.AddMonths(-9), 1_200m, TransactionCategory.ServiceCharge,
                "Annual service charge");
            yield return Spend(property, today.AddMonths(-1), 340m, TransactionCategory.Utilities,
                "Standing charges while empty");

            // Rent runs to the end of the tenancy and then stops. The gap is the story: no arrears,
            // no missing paperwork, simply nobody living there.
            foreach (var payment in RentReceived(property, lease, firstOfThisMonth, from: 21, to: 5))
                yield return payment;

            // The mortgage does not stop when the tenant leaves, which is most of why this property
            // is behind.
            foreach (var payment in MortgagePaid(property, firstOfThisMonth, 1_180m, months: 6))
                yield return payment;
        }

        /// <summary>
        /// One rent payment per month from <paramref name="from"/> months ago down to
        /// <paramref name="to"/> (0 being the current month), on the tenancy's own due day.
        /// </summary>
        private static IEnumerable<PropertyTransaction> RentReceived(
            RentalProperty property, Lease lease, DateTime firstOfThisMonth, int from, int to)
        {
            for (var monthsAgo = from; monthsAgo >= to; monthsAgo--)
            {
                var paidOn = firstOfThisMonth.AddMonths(-monthsAgo).AddDays(lease.RentDueDayOfMonth - 1);

                yield return new PropertyTransaction
                {
                    RentalPropertyId = property.Id,
                    LeaseId = lease.Id,
                    Date = paidOn,
                    Amount = lease.MonthlyRent,
                    CurrencyCode = lease.CurrencyCode,
                    Category = TransactionCategory.RentIncome,
                    Description = $"Rent {paidOn:yyyy-MM}",
                };
            }
        }

        private static IEnumerable<PropertyTransaction> MortgagePaid(
            RentalProperty property, DateTime firstOfThisMonth, decimal amount, int months)
        {
            for (var monthsAgo = months; monthsAgo >= 1; monthsAgo--)
            {
                var paidOn = firstOfThisMonth.AddMonths(-monthsAgo);

                yield return new PropertyTransaction
                {
                    RentalPropertyId = property.Id,
                    Date = paidOn,
                    Amount = amount,
                    CurrencyCode = property.CurrencyCode,
                    Category = TransactionCategory.MortgagePayment,
                    Description = $"Mortgage {paidOn:yyyy-MM}",
                };
            }
        }

        /// <summary>
        /// Positive, always. Direction comes from the category via <c>TransactionCategoryInfo</c>,
        /// which is the single sign convention in this codebase — a negative amount here would be a
        /// second one.
        /// </summary>
        private static PropertyTransaction Spend(
            RentalProperty property, DateTime on, decimal amount, TransactionCategory category, string what) =>
            new()
            {
                RentalPropertyId = property.Id,
                Date = on,
                Amount = amount,
                CurrencyCode = property.CurrencyCode,
                Category = category,
                Description = what,
            };

        private static IEnumerable<Loan> Mortgages(
            RentalProperty utrecht, RentalProperty rotterdam, DateTime today, DateTime firstOfThisMonth)
        {
            yield return new Loan
            {
                LoanName = "Maple Court mortgage",
                LoanType = LoanType.Mortgage,
                RentalPropertyId = utrecht.Id,
                LoanAmount = 168_000m,
                RemainingBalance = 141_200m,
                InterestRate = 3.4m,
                CurrencyCode = "EUR",
                MonthlyPayment = 780m,
                StartDate = today.AddYears(-3),
                TermMonths = 360,
                DueDate = firstOfThisMonth.AddMonths(1),
            };

            yield return new Loan
            {
                LoanName = "Kerkstraat mortgage",
                LoanType = LoanType.Mortgage,
                RentalPropertyId = rotterdam.Id,
                LoanAmount = 217_000m,
                RemainingBalance = 198_400m,
                InterestRate = 4.1m,
                CurrencyCode = "EUR",
                MonthlyPayment = 1_180m,
                StartDate = today.AddYears(-2),
                TermMonths = 360,
                DueDate = firstOfThisMonth.AddMonths(1),
            };
        }

        private static IEnumerable<PropertyEvent> Timeline(
            RentalProperty utrecht,
            RentalProperty budapest,
            RentalProperty rotterdam,
            DateTime today,
            DateTime firstOfThisMonth)
        {
            yield return Happened(utrecht, today.AddYears(-3), PropertyEventType.Purchase, "Completed purchase");
            yield return Happened(utrecht, today.AddMonths(-18), PropertyEventType.TenantMovedIn, "R. Bakker moved in");
            yield return Happened(utrecht, today.AddMonths(-6), PropertyEventType.Valuation, "Appraised at €265,000");

            yield return Happened(budapest, today.AddYears(-2), PropertyEventType.Purchase, "Completed purchase");
            yield return Happened(budapest, today.AddMonths(-14), PropertyEventType.TenantMovedIn, "K. Nagy moved in");

            yield return Happened(rotterdam, today.AddYears(-2), PropertyEventType.Purchase, "Completed purchase");
            yield return Happened(rotterdam, firstOfThisMonth.AddMonths(-4), PropertyEventType.TenantMovedOut,
                "J. de Vries moved out");
            yield return Happened(rotterdam, today.AddMonths(-3), PropertyEventType.Maintenance,
                "Roof replaced after storm damage");
        }

        private static PropertyEvent Happened(
            RentalProperty property, DateTime on, PropertyEventType type, string title) =>
            new()
            {
                RentalPropertyId = property.Id,
                OccurredOn = on,
                Type = type,
                Title = title,
            };

        /// <summary>
        /// Enough for the dashboard's agenda to be worth looking at. Behind the <c>Events</c> flag,
        /// which is on by default — a switched-on section with nothing in it is the specific thing
        /// this seeding is meant to stop.
        /// </summary>
        private static IEnumerable<UpcomingEvent> Reminders(
            RentalProperty utrecht, RentalProperty budapest, DateTime today)
        {
            yield return new UpcomingEvent
            {
                Title = "Building insurance renewal",
                Description = "Maple Court, Flat 2 — policy renews annually.",
                EventDate = today.AddDays(18),
                RentalPropertyId = utrecht.Id,
                IsRecurring = true,
            };

            yield return new UpcomingEvent
            {
                Title = "Annual inspection",
                Description = "Rákóczi út 12 — arrange access with the tenant.",
                EventDate = today.AddDays(27),
                RentalPropertyId = budapest.Id,
            };

            yield return new UpcomingEvent
            {
                Title = "Re-let Kerkstraat 8",
                Description = "Vacant since the last tenancy ended. Agent to confirm asking rent.",
                EventDate = today.AddDays(5),
            };
        }
    }
}
