using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Application;

public static class RequestFingerprint
{
    /// <summary>
    /// A stable fingerprint of a trade request. Case of the symbol and trailing zeros
    /// in numbers do not change it, so "aapl" and "AAPL", or 10 and 10.0, are the same request.
    /// </summary>
    public static string Compute(TradeSide side, string symbol, decimal quantity, decimal price)
    {
        var canonical = string.Join(
            '|',
            side.ToString(),
            symbol.Trim().ToUpperInvariant(),
            quantity.ToString("0.########", CultureInfo.InvariantCulture),
            price.ToString("0.######", CultureInfo.InvariantCulture));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}