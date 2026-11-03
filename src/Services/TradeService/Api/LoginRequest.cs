using FluentValidation;

namespace TradeService.Api;

public sealed record LoginRequest(string? Email, string? Password);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254).WithMessage("Email must be at most 254 characters.");

        // No complexity rules at login. The maximum length stops huge inputs being hashed.
        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(RegisterRequestValidator.MaxPasswordLength)
            .WithMessage($"Password must be at most {RegisterRequestValidator.MaxPasswordLength} characters.");
    }
}