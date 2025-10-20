using FluentValidation;
using TaskManagement.Application.Commands;

namespace TaskManagement.Application.Validators.Tasks;

public class AssignTaskCommandValidator : AbstractValidator<AssignTaskCommand>
{
    public AssignTaskCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Task ID is required");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Request cannot be null");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.UserId)
                .NotEmpty().WithMessage("User ID is required");
        });
    }
}
