using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Projects;
using TaskManagement.Contracts.Tasks;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Commands;

public record AssignTaskCommand(
    string Id,
    AssignTaskRequest Request
) : IRequest<Result<TaskResponse>>;

public class AssignTaskCommandHandler : IRequestHandler<AssignTaskCommand, Result<TaskResponse>>
{
    private readonly ITaskRepository _taskRepository;

    public AssignTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Result<TaskResponse>> Handle(AssignTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.Id, cancellationToken);
        
        if (task == null)
            return Result<TaskResponse>.Failure("Task not found");

        task.AssignedToId = request.Request.UserId;

        var updated = await _taskRepository.UpdateAsync(task, cancellationToken);
        
        if (!updated)
            return Result<TaskResponse>.Failure("Failed to assign task");

        return Result<TaskResponse>.Success(MapToTaskResponse(task));
    }

    private static TaskResponse MapToTaskResponse(ProjectTask task)
    {
        return new TaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            ProjectId = task.ProjectId,
            Status = task.Status,
            Priority = task.Priority,
            AssignedToId = task.AssignedToId,
            CreatedById = task.CreatedById,
            DueDate = task.DueDate,
            CompletedAt = task.CompletedAt,
            EstimatedHours = task.EstimatedHours,
            Tags = task.Tags,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }
}
