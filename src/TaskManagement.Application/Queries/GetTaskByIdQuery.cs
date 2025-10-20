using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Tasks;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Application.Queries;

public record GetTaskByIdQuery(string TaskId, string UserId) : IRequest<Result<TaskDto>>;

public class GetTaskByIdQueryHandler : IRequestHandler<GetTaskByIdQuery, Result<TaskDto>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;

    public GetTaskByIdQueryHandler(
        ITaskRepository taskRepository,
        IProjectRepository projectRepository)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
    }

    public async Task<Result<TaskDto>> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task == null)
        {
            return Result<TaskDto>.Failure("Task not found");
        }

        // Verify user has access to the project
        var project = await _projectRepository.GetByIdAsync(task.ProjectId, request.UserId, cancellationToken);
        if (project == null || project.OwnerId != request.UserId)
        {
            return Result<TaskDto>.Failure("Unauthorized access to task");
        }

        var dto = new TaskDto
        {
            Id = task.Id!,
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

        return Result<TaskDto>.Success(dto);
    }
}
