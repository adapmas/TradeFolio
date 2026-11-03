using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TradeFolio.Common.Security;

namespace Gateway.IntegrationTests;

internal static class TestTokens
{
    public static string Create(string signingKey, Guid userId)
    {
        var options = new JwtOptions { SigningKey = signingKey };
        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(15).UtcDateTime,
            Subject = new ClaimsIdentity([new Claim("sub", userId.ToString())]),
            SigningCredentials = new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}