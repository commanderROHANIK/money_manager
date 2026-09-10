using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;

namespace MoneyManager.Api.Controllers
{
    /// <summary>
    /// Whether this landlord has got started, answered from what exists rather than from a stored
    /// flag.
    ///
    /// <para>
    /// The same line the codebase already holds for occupancy, current rent and the rent schedule:
    /// progress is a conclusion, so it is derived. A stored "onboarded" column would be wrong the
    /// moment somebody deleted their only property, and it would need a migration — which is the
    /// second reason not to have one, since the authoring environment cannot run <c>dotnet ef</c>.
    /// </para>
    ///
    /// <para>
    /// One endpoint rather than four requests from the dashboard. The dashboard loads nothing
    /// itself — every widget fetches its own section — so there was no already-loaded data to
    /// derive this from, and asking four endpoints for their whole collections to discover whether
    /// each is empty would move real rows over the wire to answer a question about their count.
    /// </para>
    ///
    /// <para>
    /// Every query below is unfiltered on its face and tenant-scoped in fact: the global query
    /// filter on <see cref="MoneyManagerDbContext"/> applies <c>UserId == currentUser</c> to each
    /// of these sets. That is the point of putting isolation in the data layer — this controller
    /// cannot see across the boundary even though it never mentions a user.
    /// </para>
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class OnboardingController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public OnboardingController(MoneyManagerDbContext context) => _context = context;

        /// <summary>
        /// Seven existence checks, which SQLite answers as <c>EXISTS</c> without materialising a
        /// row. Cheap enough to run on every dashboard load, and the checklist stops asking once
        /// the landlord has finished — see the widget, which unmounts itself.
        ///
        /// <para>
        /// Sequential rather than <c>Task.WhenAll</c>'d, deliberately: <c>_context</c> is one
        /// scoped <see cref="MoneyManagerDbContext"/> instance, and EF Core does not allow
        /// concurrent operations on a single context — running these in parallel as written
        /// would throw at the second request, not silently misbehave. Making them genuinely
        /// parallel would mean a pooled <c>IDbContextFactory</c> and a separate context per
        /// query, which is a bigger change to make for eight sub-millisecond SQLite <c>EXISTS</c>
        /// checks against a table already scoped to one user by the query filter — not worth the
        /// added surface (and the tenant-isolation risk of wiring <see cref="ICurrentUser"/> into
        /// a second, ad-hoc context by hand) for this endpoint.
        /// </para>
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<OnboardingProgressDto>> Get(CancellationToken cancellationToken)
        {
            // Two ids, not one flag: telling "exactly one property" apart from "two or more"
            // needs a second row, and Take(2) still answers both without materialising a
            // portfolio. SoleRentalPropertyId is what lets the checklist deep-link a landlord
            // straight to the property a portfolio-wide step (tenancy/ledger/valuation) is
            // missing, without a second round-trip to fetch the property list separately — it
            // stays null the moment a second property exists, which is the point: guessing which
            // of several properties is missing something is a worse answer than not guessing.
            var propertyIds = await _context.RentalProperties
                .Select(p => p.Id)
                .Take(2)
                .ToListAsync(cancellationToken);

            return new OnboardingProgressDto(
                HasProperty: propertyIds.Count > 0,
                SoleRentalPropertyId: propertyIds.Count == 1 ? propertyIds[0] : null,
                HasLease: await _context.Leases.AnyAsync(cancellationToken),
                HasTransaction: await _context.PropertyTransactions.AnyAsync(cancellationToken),
                HasValuation: await _context.PropertyValuations.AnyAsync(cancellationToken),
                HasBankAccount: await _context.BankAccounts.AnyAsync(cancellationToken),
                HasLoan: await _context.Loans.AnyAsync(cancellationToken),
                HasStock: await _context.Stocks.AnyAsync(cancellationToken));
        }
    }

    /// <summary>
    /// What exists, not what to show. Which of these become visible steps is the browser's
    /// decision, because that depends on the feature flags the SPA has already loaded — and
    /// answering "has a bank account" for a deployment with banking switched off costs one
    /// <c>EXISTS</c> against an empty table, which is cheaper than teaching this endpoint about
    /// sections it does not otherwise care about.
    /// </summary>
    public record OnboardingProgressDto(
        bool HasProperty,
        int? SoleRentalPropertyId,
        bool HasLease,
        bool HasTransaction,
        bool HasValuation,
        bool HasBankAccount,
        bool HasLoan,
        bool HasStock);
}
