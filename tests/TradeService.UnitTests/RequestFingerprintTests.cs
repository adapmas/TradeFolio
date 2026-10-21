using TradeService.Application;
using TradeService.Infrastructure.Persistence;

namespace TradeService.UnitTests;

public class RequestFingerprintTests
{
    private static string Hash(TradeSide side = TradeSide.Buy, string symbol = "AAPL", decimal quantity = 10m, decimal price = 150m) =>
        RequestFingerprint.Compute(side, symbol, quantity, price);

    [Fact]
    public void Same_request_gives_the_same_fingerprint()
    {
        Assert.Equal(Hash(), Hash());
    }

    [Fact]
    public void Fingerprint_is_64_hex_characters()
    {
        var hash = Hash();

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void Symbol_case_and_spaces_do_not_matter()
    {
        Assert.Equal(Hash(symbol: "AAPL"), Hash(symbol: " aapl "));
    }

    [Fact]
    public void Trailing_zeros_do_not_matter()
    {
        Assert.Equal(Hash(quantity: 10m, price: 150m), Hash(quantity: 10.00000000m, price: 150.000000m));
    }

    [Fact]
    public void Different_side_gives_a_different_fingerprint()
    {
        Assert.NotEqual(Hash(side: TradeSide.Buy), Hash(side: TradeSide.Sell));
    }

    [Fact]
    public void Different_symbol_gives_a_different_fingerprint()
    {
        Assert.NotEqual(Hash(symbol: "AAPL"), Hash(symbol: "MSFT"));
    }

    [Fact]
    public void Different_quantity_gives_a_different_fingerprint()
    {
        Assert.NotEqual(Hash(quantity: 10m), Hash(quantity: 11m));
    }

    [Fact]
    public void Different_price_gives_a_different_fingerprint()
    {
        Assert.NotEqual(Hash(price: 150m), Hash(price: 150.01m));
    }
}