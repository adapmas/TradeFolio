using FluentValidation;

namespace TradeService.Api;

public sealed record RegisterRequest(string? Email, string? Password);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int MinPasswordLength = 10;
    public const int MaxPasswordLength = 128;

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254).WithMessage("Email must be at most 254 characters.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinPasswordLength)
            .WithMessage($"Password must be at least {MinPasswordLength} characters.")
            .MaximumLength(MaxPasswordLength)
            .WithMessage($"Password must be at most {MaxPasswordLength} characters.")
            .Must(p => p!.Any(char.IsLetter)).WithMessage("Password must contain at least one letter.")
            .Must(p => p!.Any(char.IsDigit)).WithMessage("Password must contain at least one digit.");
    }
}