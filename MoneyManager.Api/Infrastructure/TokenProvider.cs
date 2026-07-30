using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Infrastructure;

public sealed class TokenProvider(IOptions<JwtSettings> settings)
{
    private readonly JwtSettings _settings = settings.Value;

    /// <summary>Matches <c>RoleClaimType</c> in the bearer configuration.</summary>
    public const string RoleClaimType = "role";

    public const string AdminRole = "admin";

    public string Create(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            // "sub" is what ICurrentUser reads to scope every query to this user.
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("username", user.Username),
        ];

        // Carried in the token rather than read per request, so it goes stale until the
        // token expires. Acceptable for a grant this coarse — it gates shared reference
        // data, not anything tenant-scoped — but it does mean revoking admin waits out
        // JwtSettings.ExpiryHours.
        if (user.IsAdmin)
            claims.Add(new Claim(RoleClaimType, AdminRole));

        var descriptor = new SecurityTokenDescriptor
        {
            Expires = DateTime.UtcNow.AddHours(_settings.ExpiryHours),
            SigningCredentials = credentials,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Subject = new ClaimsIdentity(claims)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
