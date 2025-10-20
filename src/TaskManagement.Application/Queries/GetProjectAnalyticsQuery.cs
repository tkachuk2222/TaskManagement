using MediatR;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Projects;

namespace TaskManagement.Application.Queries;

public record GetProjectAnalyticsQuery(
    string Id,
    string UserId
) : IRequest<Result<ProjectAnalyticsResponse>>;

public class GetProjectAnalyticsQueryHandler : IRequestHandler<GetProjectAnalyticsQuery, Result<ProjectAnalyticsResponse>>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<GetProjectAnalyticsQueryHandler> _logger;

    public GetProjectAnalyticsQueryHandler(
        IProjectRepository projectRepository, 
        ITaskRepository taskRepository,
        ILogger<GetProjectAnalyticsQueryHandler> logger)
    {
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result<ProjectAnalyticsResponse>> Handle(GetProjectAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(request.Id, request.UserId, cancellationToken);
        
        if (project == null)
        {
            _logger.LogWarning("Project not found. ProjectId: {ProjectId}, UserId: {UserId}", request.Id, request.UserId);
            return Result<ProjectAnalyticsResponse>.Failure("Project not found");
        }

        try
        {
            // Execute both queries in parallel for better performance
            var tasksByStatusTask = _taskRepository.GetTaskCountByStatusAsync(request.Id, cancellationToken);
            var tasksByPriorityTask = _taskRepository.GetTaskCountByPriorityAsync(request.Id, cancellationToken);

            await Task.WhenAll(tasksByStatusTask, tasksByPriorityTask);

            var tasksByStatus = tasksByStatusTask.Result;
            var tasksByPriority = tasksByPriorityTask.Result;

            var totalTasks = tasksByStatus.Values.Sum();
            var completedTasks = tasksByStatus.GetValueOrDefault("Done", 0);

            var analytics = new ProjectAnalyticsResponse
            {
                ProjectId = request.Id,
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                InProgressTasks = tasksByStatus.GetValueOrDefault("InProgress", 0),
                TodoTasks = tasksByStatus.GetValueOrDefault("Todo", 0),
                BlockedTasks = tasksByStatus.GetValueOrDefault("Blocked", 0),
                CompletionPercentage = totalTasks > 0 ? (completedTasks / (double)totalTasks) * 100 : 0,
                TasksByPriority = tasksByPriority,
                TasksByStatus = tasksByStatus
            };

            _logger.LogInformation("Successfully retrieved analytics for project {ProjectId}. Total tasks: {TotalTasks}", 
                request.Id, totalTasks);

            return Result<ProjectAnalyticsResponse>.Success(analytics);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Analytics request cancelled for project {ProjectId}", request.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve project analytics. ProjectId: {ProjectId}, UserId: {UserId}", 
                request.Id, request.UserId);
            return Result<ProjectAnalyticsResponse>.Failure("Failed to retrieve project analytics");
        }
    }
}
