using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;

namespace TaskManagement.Application.Commands;

public record DeleteTaskCommand(
    string Id
) : IRequest<Result>;

public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;

    public DeleteTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Result> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _taskRepository.DeleteAsync(request.Id, cancellationToken);
        
        return deleted 
            ? Result.Success() 
            : Result.Failure("Task not found or already deleted");
    }
}
