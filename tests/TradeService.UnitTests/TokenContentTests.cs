using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TradeFolio.Common.Security;
using TradeService.Application;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.UnitTests;

public class TokenContentTests
{
    [Fact]
    public async Task The_token_holds_only_what_the_services_need()
    {
        var options = new JwtOptions { SigningKey = new string('a', 40) };
        var service = new JwtTokenService(Options.Create(options), TimeProvider.System);
        var user = new User { Id = Guid.NewGuid(), Email = "alice@example.com" };

        var token = service.CreateAccessToken(user).Value;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, options.CreateValidationParameters());

        Assert.True(result.IsValid);
        Assert.Contains("sub", result.Claims.Keys);
        Assert.DoesNotContain("email", result.Claims.Keys);

        // A token is only encoded, not encrypted: anyone can read the payload.
        var payload = Base64UrlEncoder.Decode(token.Split('.')[1]);
        Assert.DoesNotContain("alice@example.com", payload);
    }
}