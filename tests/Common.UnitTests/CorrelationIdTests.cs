using TradeFolio.Common.Correlation;
using Xunit;

namespace Common.UnitTests;

public class CorrelationIdTests
{
    [Fact]
    public void A_valid_guid_is_kept_in_standard_form()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id.ToString("D"), CorrelationId.Resolve(id.ToString("N").ToUpperInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("abc; DROP TABLE trades;--")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Anything_else_is_replaced_with_a_new_guid(string? incoming)
    {
        var result = CorrelationId.Resolve(incoming);

        Assert.True(Guid.TryParse(result, out var parsed));
        Assert.NotEqual(Guid.Empty, parsed);
    }

    [Fact]
    public void Each_generated_id_is_different()
    {
        Assert.NotEqual(CorrelationId.Resolve(null), CorrelationId.Resolve(null));
    }
}