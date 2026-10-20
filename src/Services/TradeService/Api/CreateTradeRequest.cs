using FluentValidation;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Api;

public sealed record CreateTradeRequest(string? Symbol, string? Side, decimal Quantity, decimal Price);

public sealed class CreateTradeRequestValidator : AbstractValidator<CreateTradeRequest>
{
    public CreateTradeRequestValidator(IInstrumentLookup instruments)
    {
        RuleFor(x => x.Symbol)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Symbol is required.")
            .MaximumLength(20).WithMessage("Symbol must be at most 20 characters.")
            .MustAsync((symbol, ct) => instruments.SymbolExistsAsync(symbol!, ct))
            .WithMessage("Symbol is not a known instrument.");

        RuleFor(x => x.Side)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Side is required.")
            .Must(IsValidSide).WithMessage("Side must be BUY or SELL.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0m).WithMessage("Quantity must be greater than zero.")
            .PrecisionScale(18, 8, true)
            .WithMessage("Quantity must have at most 10 digits before and 8 digits after the decimal point.");

        RuleFor(x => x.Price)
            .GreaterThan(0m).WithMessage("Price must be greater than zero.")
            .PrecisionScale(18, 6, true)
            .WithMessage("Price must have at most 12 digits before and 6 digits after the decimal point.");
    }

    public static bool IsValidSide(string? side) =>
        side is not null &&
        (side.Trim().Equals("BUY", StringComparison.OrdinalIgnoreCase) ||
         side.Trim().Equals("SELL", StringComparison.OrdinalIgnoreCase));
}