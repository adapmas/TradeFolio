namespace TradeFolio.Common.Correlation;

public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// Keeps a caller-supplied id only if it is a GUID; anything else is replaced with a new one.
    /// Accepting nothing but GUIDs means a hostile header can never reach logs or events.
    /// </summary>
    public static string Resolve(string? incoming) =>
        Guid.TryParse(incoming, out var id) && id != Guid.Empty
            ? id.ToString("D")
            : Guid.NewGuid().ToString("D");
}