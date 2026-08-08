using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MoneyManager.Api.Tests.Integration;

/// <summary>
/// Mints tokens the app would never issue, so the negative half of the authentication suite has
/// something to send. Each parameter defaults to the value the app accepts; a test overrides the
/// single one it is probing, which keeps "wrong audience" from also accidentally meaning "wrong
/// key" and passing for the wrong reason.
/// </summary>
internal static class TestTokens
{
    public static string Create(
        int userId,
        string username,
        string signingKey = ApiFactory.SigningKey,
        string issuer = ApiFactory.Issuer,
        string audience = ApiFactory.Audience,
        DateTime? issuedAt = null,
        DateTime? expires = null)
    {
        var issued = issuedAt ?? DateTime.UtcNow;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = issued,
            NotBefore = issued,
            Expires = expires ?? issued.AddHours(1),
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("username", username),
            ])
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
