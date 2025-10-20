using FluentValidation;
using TaskManagement.Application.Commands;

namespace TaskManagement.Application.Validators.Projects;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request cannot be null");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.Name)
                .NotEmpty().WithMessage("Project name is required")
                .MinimumLength(3).WithMessage("Project name must be at least 3 characters")
                .MaximumLength(100).WithMessage("Project name must not exceed 100 characters");

            RuleFor(x => x.Request.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
                .When(x => !string.IsNullOrEmpty(x.Request.Description));

            RuleFor(x => x.Request.EndDate)
                .GreaterThan(x => x.Request.StartDate)
                .WithMessage("End date must be after start date")
                .When(x => x.Request.StartDate.HasValue && x.Request.EndDate.HasValue);
        });
    }
}
