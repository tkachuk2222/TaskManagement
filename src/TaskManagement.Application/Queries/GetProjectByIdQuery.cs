using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Projects;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Queries;

public record GetProjectByIdQuery(
    string Id,
    string UserId
) : IRequest<Result<ProjectDetailResponse>>;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, Result<ProjectDetailResponse>>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ITaskRepository _taskRepository;

    public GetProjectByIdQueryHandler(IProjectRepository projectRepository, ITaskRepository taskRepository)
    {
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
    }

    public async Task<Result<ProjectDetailResponse>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.Id, request.UserId, cancellationToken);
        
        if (project == null)
            return Result<ProjectDetailResponse>.Failure("Project not found");

        var tasks = await _taskRepository.GetTasksByProjectIdAsync(request.Id, cancellationToken);
        
        var response = new ProjectDetailResponse
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

        return Result<ProjectDetailResponse>.Success(response);
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
