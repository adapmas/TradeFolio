using TradeFolio.Common.Security;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Application;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt, int ExpiresInSeconds);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public AccessToken CreateAccessToken(User user)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expires = now + settings.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            // Only what the API needs: who the user is. No password data, nothing sensitive.
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expires, (int)settings.AccessTokenLifetime.TotalSeconds);
    }
}
