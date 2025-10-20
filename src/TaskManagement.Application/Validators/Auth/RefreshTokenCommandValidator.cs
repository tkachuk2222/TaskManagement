using FluentValidation;
using TaskManagement.Application.Commands.Auth;

namespace TaskManagement.Application.Validators.Auth;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required")
            .MinimumLength(10).WithMessage("Invalid refresh token format");
    }
}
