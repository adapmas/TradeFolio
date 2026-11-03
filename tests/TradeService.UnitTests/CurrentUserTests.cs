using System.Security.Claims;
using TradeService.Api;
using Xunit;

namespace TradeService.UnitTests;

public class CurrentUserTests
{
    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void Reads_the_user_id_from_the_sub_claim()
    {
        var id = Guid.NewGuid();

        var ok = CurrentUser.TryGetId(Principal(new Claim("sub", id.ToString())), out var userId);

        Assert.True(ok);
        Assert.Equal(id, userId);
    }

    [Fact]
    public void A_token_without_a_sub_claim_is_not_a_user()
    {
        Assert.False(CurrentUser.TryGetId(Principal(new Claim("email", "a@b.com")), out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    public void A_sub_claim_that_is_not_a_guid_is_rejected(string sub)
    {
        Assert.False(CurrentUser.TryGetId(Principal(new Claim("sub", sub)), out _));
    }

    [Fact]
    public void A_user_id_in_a_different_claim_is_ignored()
    {
        Assert.False(CurrentUser.TryGetId(Principal(new Claim("X-User-Id", Guid.NewGuid().ToString())), out _));
    }
}