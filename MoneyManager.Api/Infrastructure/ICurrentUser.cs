using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace MoneyManager.Api.Infrastructure
{
    /// <summary>
    /// The authenticated user for the current request. Injected into the DbContext so that
    /// tenant filtering is applied by the data layer rather than trusted to each controller.
    /// </summary>
    public interface ICurrentUser
    {
        int? UserId { get; }
    }

    public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        public int? UserId
        {
            get
            {
                var principal = accessor.HttpContext?.User;
                if (principal is null)
                    return null;

                // Depending on whether inbound claim mapping is on, "sub" arrives either
                // under its own name or remapped to NameIdentifier. Accept both.
                var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

                return int.TryParse(value, out var id) ? id : null;
            }
        }
    }

    /// <summary>
    /// Used by design-time migration tooling and by background work that legitimately runs
    /// outside any request. Reads resolve to no tenant, so filtered queries return nothing
    /// rather than silently returning everyone's rows.
    /// </summary>
    public sealed class NoCurrentUser : ICurrentUser
    {
        public int? UserId => null;
    }

    /// <summary>
    /// A named owner for work that runs outside any request but still has to write owned rows —
    /// which today means the startup seeder, and nothing else.
    ///
    /// <para>
    /// <see cref="NoCurrentUser"/> cannot do that job. Every seeded entity implements
    /// <c>IOwnedByUser</c>, and <c>ApplyOwnership</c> throws rather than persist one with
    /// no owner, so seeding through a null tenant fails on the first <c>SaveChanges</c> — before
    /// the app starts, which is a crash loop rather than an error. Supplying the owner here
    /// leaves that guard exactly as it was: ownership is still stamped by the data layer, from
    /// an identity passed in out of band and never from a request payload.
    /// </para>
    ///
    /// <para>
    /// It also makes the global query filter mean something while seeding, which is what lets
    /// "has this already been seeded" work at all. Asked through a null tenant, that filter
    /// compares <c>UserId</c> against NULL, matches nothing, and reports an empty database on
    /// every single boot — so a guard that reads as obviously correct would duplicate the demo
    /// rows on every redeploy.
    /// </para>
    /// </summary>
    public sealed class SeedCurrentUser(int userId) : ICurrentUser
    {
        public int? UserId { get; } = userId;
    }
}
