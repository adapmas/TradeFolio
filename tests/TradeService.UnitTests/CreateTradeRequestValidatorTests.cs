using FluentValidation.TestHelper;
using TradeService.Api;
using TradeService.Infrastructure.Persistence;

namespace TradeService.UnitTests;

public class CreateTradeRequestValidatorTests
{
    private sealed class FakeInstrumentLookup(params string[] known) : IInstrumentLookup
    {
        public Task<bool> SymbolExistsAsync(string symbol, CancellationToken cancellationToken) =>
            Task.FromResult(known.Contains(symbol.Trim(), StringComparer.OrdinalIgnoreCase));
    }

    private static readonly CreateTradeRequestValidator Validator =
        new(new FakeInstrumentLookup("AAPL", "MSFT"));

    private static CreateTradeRequest Valid() => new("AAPL", "BUY", 10m, 150m);

    [Fact]
    public async Task Valid_request_has_no_errors()
    {
        var result = await Validator.TestValidateAsync(Valid());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Side_and_symbol_are_case_insensitive()
    {
        var result = await Validator.TestValidateAsync(Valid() with { Symbol = " aapl ", Side = "sell" });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task Non_positive_quantity_is_rejected(int quantity)
    {
        var result = await Validator.TestValidateAsync(Valid() with { Quantity = quantity });

        result.ShouldHaveValidationErrorFor(x => x.Quantity)
            .WithErrorMessage("Quantity must be greater than zero.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_price_is_rejected(int price)
    {
        var result = await Validator.TestValidateAsync(Valid() with { Price = price });

        result.ShouldHaveValidationErrorFor(x => x.Price)
            .WithErrorMessage("Price must be greater than zero.");
    }

    [Theory]
    [InlineData("HOLD")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Invalid_side_is_rejected(string? side)
    {
        var result = await Validator.TestValidateAsync(Valid() with { Side = side });

        result.ShouldHaveValidationErrorFor(x => x.Side);
    }

    [Fact]
    public async Task Unknown_symbol_is_rejected()
    {
        var result = await Validator.TestValidateAsync(Valid() with { Symbol = "ZZZZ" });

        result.ShouldHaveValidationErrorFor(x => x.Symbol)
            .WithErrorMessage("Symbol is not a known instrument.");
    }

    [Fact]
    public async Task Missing_symbol_is_rejected_without_hitting_the_lookup()
    {
        var result = await Validator.TestValidateAsync(Valid() with { Symbol = null });

        result.ShouldHaveValidationErrorFor(x => x.Symbol)
            .WithErrorMessage("Symbol is required.");
    }

    [Fact]
    public async Task Quantity_with_too_many_decimals_is_rejected()
    {
        var result = await Validator.TestValidateAsync(Valid() with { Quantity = 0.123456789m });

        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public async Task Price_with_too_many_decimals_is_rejected()
    {
        var result = await Validator.TestValidateAsync(Valid() with { Price = 10.1234567m });

        result.ShouldHaveValidationErrorFor(x => x.Price);
    }
}