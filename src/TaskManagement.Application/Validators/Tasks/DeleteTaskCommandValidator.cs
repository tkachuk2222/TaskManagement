using FluentValidation;
using TaskManagement.Application.Commands;

namespace TaskManagement.Application.Validators.Tasks;

public class DeleteTaskCommandValidator : AbstractValidator<DeleteTaskCommand>
{
    public DeleteTaskCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Task ID is required");
    }
}
