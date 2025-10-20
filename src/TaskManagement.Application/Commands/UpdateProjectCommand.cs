using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Projects;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Commands;

public record UpdateProjectCommand(
    string Id,
    string UserId,
    UpdateProjectRequest Request
) : IRequest<Result<ProjectDetailResponse>>;

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, Result<ProjectDetailResponse>>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ITaskRepository _taskRepository;

    public UpdateProjectCommandHandler(IProjectRepository projectRepository, ITaskRepository taskRepository)
    {
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
    }

    public async Task<Result<ProjectDetailResponse>> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.Id, request.UserId, cancellationToken);
        
        if (project == null)
            return Result<ProjectDetailResponse>.Failure("Project not found");

        if (!string.IsNullOrWhiteSpace(request.Request.Name))
            project.Name = request.Request.Name;
        
        if (request.Request.Description != null)
            project.Description = request.Request.Description;
        
        if (request.Request.Status.HasValue)
            project.Status = request.Request.Status.Value;
        
        if (request.Request.StartDate.HasValue)
            project.StartDate = request.Request.StartDate;
        
        if (request.Request.EndDate.HasValue)
            project.EndDate = request.Request.EndDate;
        
        if (request.Request.Tags != null)
            project.Tags = request.Request.Tags;

        var updated = await _projectRepository.UpdateAsync(project, cancellationToken);
        
        if (!updated)
            return Result<ProjectDetailResponse>.Failure("Failed to update project");

        // Fetch tasks to return the same structure as GET endpoint (for consistent ETags)
        var tasks = await _taskRepository.GetTasksByProjectIdAsync(request.Id, cancellationToken);
        
        return Result<ProjectDetailResponse>.Success(MapToProjectDetailResponse(project, tasks));
    }

    private static ProjectDetailResponse MapToProjectDetailResponse(Project project, List<ProjectTask> tasks)
    {
        return new ProjectDetailResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            OwnerId = project.OwnerId,
            Status = project.Status,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            MemberIds = project.MemberIds,
            Tags = project.Tags,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            TaskCount = tasks.Count,
            Tasks = tasks.Select(MapToTaskResponse).ToList()
        };
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
