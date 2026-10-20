namespace TradeService.Infrastructure.Persistence;

public static class InstrumentSeed
{
    public static readonly Instrument[] All =
    [
        Create("a0000000-0000-0000-0000-000000000001", "AAPL", "Apple Inc.", "Technology"),
        Create("a0000000-0000-0000-0000-000000000002", "MSFT", "Microsoft Corporation", "Technology"),
        Create("a0000000-0000-0000-0000-000000000003", "NVDA", "NVIDIA Corporation", "Technology"),
        Create("a0000000-0000-0000-0000-000000000004", "GOOGL", "Alphabet Inc.", "Communication Services"),
        Create("a0000000-0000-0000-0000-000000000005", "AMZN", "Amazon.com Inc.", "Consumer Discretionary"),
        Create("a0000000-0000-0000-0000-000000000006", "TSLA", "Tesla Inc.", "Consumer Discretionary"),
        Create("a0000000-0000-0000-0000-000000000007", "JPM", "JPMorgan Chase & Co.", "Financials"),
        Create("a0000000-0000-0000-0000-000000000008", "JNJ", "Johnson & Johnson", "Health Care"),
        Create("a0000000-0000-0000-0000-000000000009", "XOM", "Exxon Mobil Corporation", "Energy"),
        Create("a0000000-0000-0000-0000-000000000010", "KO", "The Coca-Cola Company", "Consumer Staples"),
    ];

    private static Instrument Create(string id, string symbol, string name, string sector) => new()
    {
        Id = Guid.Parse(id),
        Symbol = symbol,
        Name = name,
        Sector = sector,
        Currency = "USD"
    };
}