using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Projects;
using TaskManagement.Contracts.Tasks;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Commands;

public record UpdateTaskCommand(
    string Id,
    UpdateTaskRequest Request
) : IRequest<Result<TaskResponse>>;

public class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, Result<TaskResponse>>
{
    private readonly ITaskRepository _taskRepository;

    public UpdateTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Result<TaskResponse>> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.Id, cancellationToken);
        
        if (task == null)
            return Result<TaskResponse>.Failure("Task not found");

        if (!string.IsNullOrWhiteSpace(request.Request.Title))
            task.Title = request.Request.Title;
        
        if (request.Request.Description != null)
            task.Description = request.Request.Description;
        
        if (request.Request.Status.HasValue)
            task.Status = request.Request.Status.Value;
        
        if (request.Request.Priority.HasValue)
            task.Priority = request.Request.Priority.Value;
        
        if (request.Request.AssignedToId != null)
            task.AssignedToId = request.Request.AssignedToId;
        
        if (request.Request.DueDate.HasValue)
            task.DueDate = request.Request.DueDate;
        
        if (request.Request.EstimatedHours.HasValue)
            task.EstimatedHours = request.Request.EstimatedHours.Value;
        
        if (request.Request.Tags != null)
            task.Tags = request.Request.Tags;

        var updated = await _taskRepository.UpdateAsync(task, cancellationToken);
        
        if (!updated)
            return Result<TaskResponse>.Failure("Failed to update task");

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
