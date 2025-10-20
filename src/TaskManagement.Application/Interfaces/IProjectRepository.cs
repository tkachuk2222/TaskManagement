using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string id, string userId, CancellationToken cancellationToken = default);
    Task<(List<Project> projects, int totalCount)> GetUserProjectsAsync(
        string userId, 
        int pageNumber, 
        int pageSize, 
        string? searchTerm = null,
        string? status = null,
        CancellationToken cancellationToken = default);
    Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, string userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string id, string userId, CancellationToken cancellationToken = default);
}
