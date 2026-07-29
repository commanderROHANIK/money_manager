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

    public string Create(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Expires = DateTime.UtcNow.AddHours(_settings.ExpiryHours),
            SigningCredentials = credentials,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Subject = new ClaimsIdentity(
            [
                // "sub" is what ICurrentUser reads to scope every query to this user.
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("username", user.Username),
            ])
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
