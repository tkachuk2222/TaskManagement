using FluentValidation;
using TaskManagement.Application.Queries;

namespace TaskManagement.Application.Validators.Projects;

public class GetProjectAnalyticsQueryValidator : AbstractValidator<GetProjectAnalyticsQuery>
{
    public GetProjectAnalyticsQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Project ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}
