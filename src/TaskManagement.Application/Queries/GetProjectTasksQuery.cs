using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Projects;
using TaskManagement.Domain.Entities;
using DomainTaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.Application.Queries;

public record GetProjectTasksQuery(
    string ProjectId,
    string UserId,
    int PageNumber,
    int PageSize,
    DomainTaskStatus? Status,
    string? SortBy,
    bool SortDescending
) : IRequest<Result<PagedResult<TaskResponse>>>;

public class GetProjectTasksQueryHandler : IRequestHandler<GetProjectTasksQuery, Result<PagedResult<TaskResponse>>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IProjectRepository _projectRepository;

    public GetProjectTasksQueryHandler(ITaskRepository taskRepository, IProjectRepository projectRepository)
    {
        _taskRepository = taskRepository;
        _projectRepository = projectRepository;
    }

    public async Task<Result<PagedResult<TaskResponse>>> Handle(GetProjectTasksQuery request, CancellationToken cancellationToken)
    {
        // Verify user has access to project
        if (!await _projectRepository.ExistsAsync(request.ProjectId, request.UserId, cancellationToken))
            return Result<PagedResult<TaskResponse>>.Failure("Project not found or access denied");

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 || request.PageSize > 100 ? 20 : request.PageSize;

        var (tasks, totalCount) = await _taskRepository.GetProjectTasksAsync(
            request.ProjectId, pageNumber, pageSize, request.Status, request.SortBy, request.SortDescending, cancellationToken);

        var result = new PagedResult<TaskResponse>
        {
            Items = tasks.Select(MapToTaskResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return Result<PagedResult<TaskResponse>>.Success(result);
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
