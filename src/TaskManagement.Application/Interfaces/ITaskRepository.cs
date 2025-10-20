using TaskManagement.Domain.Entities;
using DomainTaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.Application.Interfaces;

public interface ITaskRepository
{
    Task<ProjectTask?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<(List<ProjectTask> tasks, int totalCount)> GetProjectTasksAsync(
        string projectId,
        int pageNumber,
        int pageSize,
        DomainTaskStatus? status = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default);
    Task<ProjectTask> CreateAsync(ProjectTask task, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ProjectTask task, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<List<ProjectTask>> GetTasksByProjectIdAsync(string projectId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetTaskCountByStatusAsync(string projectId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetTaskCountByPriorityAsync(string projectId, CancellationToken cancellationToken = default);
}
