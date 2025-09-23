using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Infrastructure;

public sealed class TokenProvider(IConfiguration configuration)
{
    public string Create(User user)
    {
        var secret = configuration["jwtSecret"]!;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var toeknDescription = new SecurityTokenDescriptor
        {
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = credentials,
            Issuer = "MoneyManager.Api",
            Audience = "MoneyManager.Api",
            Subject = new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("id", user.Id.ToString()),
                new System.Security.Claims.Claim("username", user.Username),
            })
        };

        var tokenHandler = new JsonWebTokenHandler();
        var token = tokenHandler.CreateToken(toeknDescription);

        return token;
    }
}