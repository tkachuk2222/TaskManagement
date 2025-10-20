using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Projects;
using TaskManagement.Contracts.Tasks;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Commands;

public record CreateTaskCommand(
    string ProjectId,
    string UserId,
    CreateTaskRequest Request
) : IRequest<Result<TaskResponse>>;

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Result<TaskResponse>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;

    public CreateTaskCommandHandler(ITaskRepository taskRepository, IProjectRepository projectRepository)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
    }

    public async Task<Result<TaskResponse>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        // Verify user has access to project
        if (!await _projectRepository.ExistsAsync(request.ProjectId, request.UserId, cancellationToken))
            return Result<TaskResponse>.Failure("Project not found or access denied");

        var validationErrors = ValidateCreateRequest(request.Request);
        if (validationErrors.Any())
            return Result<TaskResponse>.ValidationFailure(validationErrors);

        var task = new ProjectTask
        {
            Title = request.Request.Title,
            Description = request.Request.Description,
            ProjectId = request.ProjectId,
            Status = request.Request.Status,
            Priority = request.Request.Priority,
            AssignedToId = request.Request.AssignedToId,
            CreatedById = request.UserId,
            DueDate = request.Request.DueDate,
            EstimatedHours = request.Request.EstimatedHours,
            Tags = request.Request.Tags
        };

        var created = await _taskRepository.CreateAsync(task, cancellationToken);
        
        return Result<TaskResponse>.Success(MapToTaskResponse(created));
    }

    private static List<string> ValidateCreateRequest(CreateTaskRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Title))
            errors.Add("Task title is required");
        else if (request.Title.Length > 200)
            errors.Add("Task title must not exceed 200 characters");

        if (request.Description?.Length > 2000)
            errors.Add("Description must not exceed 2000 characters");

        if (request.EstimatedHours < 0)
            errors.Add("Estimated hours cannot be negative");

        return errors;
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
