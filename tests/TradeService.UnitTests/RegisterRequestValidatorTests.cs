using TradeService.Api;
using Xunit;

namespace TradeService.UnitTests;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(new RegisterRequest("alice@example.com", "Sup3rSecret99"));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null, "Sup3rSecret99", "email")]
    [InlineData("", "Sup3rSecret99", "email")]
    [InlineData("not-an-email", "Sup3rSecret99", "email")]
    [InlineData("a@example.com", null, "password")]
    [InlineData("a@example.com", "Sh0rt", "password")]
    [InlineData("a@example.com", "onlylettersnodigit", "password")]
    [InlineData("a@example.com", "12345678901234", "password")]
    public void Invalid_request_fails_on_the_expected_field(string? email, string? password, string field)
    {
        var result = _validator.Validate(new RegisterRequest(email, password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Overlong_email_and_password_are_rejected()
    {
        var longEmail = new string('a', 250) + "@x.com";
        var longPassword = new string('a', 128) + "1";

        Assert.False(_validator.Validate(new RegisterRequest(longEmail, "Sup3rSecret99")).IsValid);
        Assert.False(_validator.Validate(new RegisterRequest("a@example.com", longPassword)).IsValid);
    }
}