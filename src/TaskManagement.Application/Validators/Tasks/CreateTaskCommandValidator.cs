using FluentValidation;
using TaskManagement.Application.Commands;

namespace TaskManagement.Application.Validators.Tasks;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage("Project ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request cannot be null");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.Title)
                .NotEmpty().WithMessage("Task title is required")
                .MinimumLength(3).WithMessage("Task title must be at least 3 characters")
                .MaximumLength(200).WithMessage("Task title must not exceed 200 characters");

            RuleFor(x => x.Request.Description)
                .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters")
                .When(x => !string.IsNullOrEmpty(x.Request.Description));

            RuleFor(x => x.Request.Priority)
                .IsInEnum().WithMessage("Invalid priority value");

            RuleFor(x => x.Request.DueDate)
                .GreaterThan(DateTime.UtcNow).WithMessage("Due date must be in the future")
                .When(x => x.Request.DueDate.HasValue);
        });
    }
}
