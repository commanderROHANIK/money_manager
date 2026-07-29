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
}
