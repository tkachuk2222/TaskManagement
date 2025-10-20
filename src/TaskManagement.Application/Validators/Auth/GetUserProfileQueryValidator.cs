using FluentValidation;
using TaskManagement.Application.Queries.Auth;

namespace TaskManagement.Application.Validators.Auth;

public class GetUserProfileQueryValidator : AbstractValidator<GetUserProfileQuery>
{
    public GetUserProfileQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required")
            .Must(BeValidGuid).WithMessage("User ID must be a valid GUID");
    }

    private bool BeValidGuid(string userId)
    {
        return Guid.TryParse(userId, out _);
    }
}
