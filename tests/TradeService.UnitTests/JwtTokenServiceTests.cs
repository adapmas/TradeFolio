using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TradeService.Application;
using TradeService.Infrastructure.Persistence;
using Xunit;

namespace TradeService.UnitTests;

public class JwtTokenServiceTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static JwtOptions NewOptions(
        char keyChar = 'a', string issuer = "tradefolio", string audience = "tradefolio-api") => new()
    {
        SigningKey = new string(keyChar, 40),
        Issuer = issuer,
        Audience = audience,
        AccessTokenMinutes = 15
    };

    private static User NewUser() => new() { Id = Guid.NewGuid(), Email = "alice@example.com" };

    private static string Issue(JwtOptions options, User user, DateTimeOffset? at = null)
    {
        var service = new JwtTokenService(
            Options.Create(options), new FixedTimeProvider(at ?? DateTimeOffset.UtcNow));
        return service.CreateAccessToken(user).Value;
    }

    private static Task<TokenValidationResult> ValidateAsync(string token, JwtOptions options) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, options.CreateValidationParameters());

    [Fact]
    public async Task Valid_token_is_accepted_and_carries_the_user_id()
    {
        var options = NewOptions();
        var user = NewUser();

        var result = await ValidateAsync(Issue(options, user), options);

        Assert.True(result.IsValid);
        Assert.Equal(user.Id.ToString(), result.Claims["sub"]);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var options = NewOptions();
        var issuedTwoHoursAgo = DateTimeOffset.UtcNow.AddHours(-2);

        var result = await ValidateAsync(Issue(options, NewUser(), issuedTwoHoursAgo), options);

        Assert.False(result.IsValid);
        Assert.IsAssignableFrom<SecurityTokenException>(result.Exception);
    }

    [Fact]
    public async Task Tampered_payload_is_rejected()
    {
        var options = NewOptions();
        var user = NewUser();
        var token = Issue(options, user);

        // Replace the user id inside the payload, keeping the original signature.
        var parts = token.Split('.');
        var payload = Base64UrlEncoder.Decode(parts[1]);
        var forged = payload.Replace(user.Id.ToString(), Guid.NewGuid().ToString());
        parts[1] = Base64UrlEncoder.Encode(forged);
        var tampered = string.Join('.', parts);

        Assert.NotEqual(token, tampered);
        Assert.False((await ValidateAsync(tampered, options)).IsValid);
    }

    [Fact]
    public async Task Token_signed_with_a_different_key_is_rejected()
    {
        var token = Issue(NewOptions(keyChar: 'b'), NewUser());

        Assert.False((await ValidateAsync(token, NewOptions(keyChar: 'a'))).IsValid);
    }

    [Fact]
    public async Task Token_from_a_different_issuer_is_rejected()
    {
        var token = Issue(NewOptions(issuer: "someone-else"), NewUser());

        Assert.False((await ValidateAsync(token, NewOptions())).IsValid);
    }

    [Fact]
    public async Task Token_for_a_different_audience_is_rejected()
    {
        var token = Issue(NewOptions(audience: "another-api"), NewUser());

        Assert.False((await ValidateAsync(token, NewOptions())).IsValid);
    }

    [Fact]
    public async Task Unsigned_token_is_rejected()
    {
        var exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var payload =
            $"{{\"sub\":\"{Guid.NewGuid()}\",\"iss\":\"tradefolio\",\"aud\":\"tradefolio-api\",\"exp\":{exp}}}";
        var unsigned = new JsonWebTokenHandler().CreateToken(payload);

        Assert.False((await ValidateAsync(unsigned, NewOptions())).IsValid);
    }

    [Fact]
    public void Missing_or_short_signing_key_fails_at_startup()
    {
        Assert.Throws<InvalidOperationException>(() => new JwtOptions { SigningKey = "" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new JwtOptions { SigningKey = "too-short" }.Validate());
        new JwtOptions { SigningKey = new string('k', 40) }.Validate();   // a good key does not throw
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Unreasonable_lifetime_fails_at_startup(int minutes)
    {
        var options = NewOptions();
        options.AccessTokenMinutes = minutes;

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}