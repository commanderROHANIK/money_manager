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
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<OnboardingProgressDto>> Get(CancellationToken cancellationToken)
        {
            // Two rows is enough to tell "exactly one" from "more than one" without counting the
            // whole table, which is what SolePropertyId needs below.
            var propertyIds = await _context.RentalProperties
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .Take(2)
                .ToListAsync(cancellationToken);

            return new OnboardingProgressDto(
                HasProperty: propertyIds.Count > 0,
                HasLease: await _context.Leases.AnyAsync(cancellationToken),
                HasTransaction: await _context.PropertyTransactions.AnyAsync(cancellationToken),
                HasValuation: await _context.PropertyValuations.AnyAsync(cancellationToken),
                HasBankAccount: await _context.BankAccounts.AnyAsync(cancellationToken),
                HasLoan: await _context.Loans.AnyAsync(cancellationToken),
                HasStock: await _context.Stocks.AnyAsync(cancellationToken),
                // Only set when the landlord has exactly one property, so the checklist's guided
                // "Go" can deep-link into it without a second round-trip and without guessing which
                // property a 2+-property portfolio means.
                SolePropertyId: propertyIds.Count == 1 ? propertyIds[0] : null);
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
        bool HasLease,
        bool HasTransaction,
        bool HasValuation,
        bool HasBankAccount,
        bool HasLoan,
        bool HasStock,
        int? SolePropertyId);
}
