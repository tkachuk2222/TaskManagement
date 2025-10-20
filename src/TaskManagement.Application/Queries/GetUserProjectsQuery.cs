using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Projects;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Queries;

/// <summary>
/// Query to get paginated list of user's projects
/// </summary>
public record GetUserProjectsQuery(
    string UserId,
    int PageNumber,
    int PageSize,
    string? SearchTerm,
    string? Status
) : IRequest<Result<PagedResult<ProjectResponse>>>;

/// <summary>
/// Handler for GetUserProjectsQuery - Uses Task.WhenAll to avoid await-in-loop
/// </summary>
public class GetUserProjectsQueryHandler : IRequestHandler<GetUserProjectsQuery, Result<PagedResult<ProjectResponse>>>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ITaskRepository _taskRepository;

    public GetUserProjectsQueryHandler(IProjectRepository projectRepository, ITaskRepository taskRepository)
    {
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
    }

    public async Task<Result<PagedResult<ProjectResponse>>> Handle(GetUserProjectsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 || request.PageSize > 100 ? 20 : request.PageSize;

        var (projects, totalCount) = await _projectRepository.GetUserProjectsAsync(
            request.UserId, pageNumber, pageSize, request.SearchTerm, request.Status, cancellationToken);

        // FIX: Use Task.WhenAll instead of await in loop for better performance
        var taskCountTasks = projects.Select(async project =>
        {
            var tasks = await _taskRepository.GetTasksByProjectIdAsync(project.Id, cancellationToken);
            return (project, taskCount: tasks.Count);
        }).ToList();

        var projectsWithTaskCounts = await Task.WhenAll(taskCountTasks);

        var projectResponses = projectsWithTaskCounts
            .Select(x => MapToProjectResponse(x.project, x.taskCount))
            .ToList();

        var result = new PagedResult<ProjectResponse>
        {
            Items = projectResponses,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return Result<PagedResult<ProjectResponse>>.Success(result);
    }

    private static ProjectResponse MapToProjectResponse(Project project, int taskCount)
    {
        return new ProjectResponse
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
            TaskCount = taskCount
        };
    }
}
