using MongoDB.Driver;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace TaskManagement.Infrastructure.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly IMongoCollection<Project> _projects;
    private readonly ICacheService _cacheService;

    public ProjectRepository(IMongoDatabase database, IOptions<MongoDbSettings> settings, ICacheService cacheService)
    {
        _projects = database.GetCollection<Project>(settings.Value.ProjectsCollection);
        _cacheService = cacheService;
    }

    public async Task<Project?> GetByIdAsync(string id, string userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"project:{id}";
        var cached = await _cacheService.GetAsync<Project>(cacheKey, cancellationToken);
        if (cached != null) return cached;

        var filter = Builders<Project>.Filter.And(
            Builders<Project>.Filter.Eq(p => p.Id, id),
            Builders<Project>.Filter.Eq(p => p.OwnerId, userId),
            Builders<Project>.Filter.Eq(p => p.IsDeleted, false)
        );

        var project = await _projects.Find(filter).FirstOrDefaultAsync(cancellationToken);
        
        if (project != null)
        {
            await _cacheService.SetAsync(cacheKey, project, TimeSpan.FromMinutes(10), cancellationToken);
        }

        return project;
    }

    public async Task<(List<Project> projects, int totalCount)> GetUserProjectsAsync(
        string userId,
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<Project>.Filter;
        var filters = new List<FilterDefinition<Project>>
        {
            filterBuilder.Eq(p => p.OwnerId, userId),
            filterBuilder.Eq(p => p.IsDeleted, false)
        };

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filters.Add(filterBuilder.Text(searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<Domain.Enums.ProjectStatus>(status, true, out var statusEnum))
        {
            filters.Add(filterBuilder.Eq(p => p.Status, statusEnum));
        }

        var filter = filterBuilder.And(filters);

        var totalCount = await _projects.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        
        var projects = await _projects.Find(filter)
            .Sort(Builders<Project>.Sort.Descending(p => p.UpdatedAt))
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (projects, (int)totalCount);
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        project.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        project.IsDeleted = false;

        await _projects.InsertOneAsync(project, cancellationToken: cancellationToken);
        
        var cacheKey = $"project:{project.Id}";
        await _cacheService.SetAsync(cacheKey, project, TimeSpan.FromMinutes(10), cancellationToken);
        await _cacheService.RemoveByPrefixAsync($"projects:{project.OwnerId}", cancellationToken);

        return project;
    }

    public async Task<bool> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        project.UpdatedAt = DateTime.UtcNow;

        var filter = Builders<Project>.Filter.And(
            Builders<Project>.Filter.Eq(p => p.Id, project.Id),
            Builders<Project>.Filter.Eq(p => p.OwnerId, project.OwnerId),
            Builders<Project>.Filter.Eq(p => p.IsDeleted, false)
        );

        var result = await _projects.ReplaceOneAsync(filter, project, cancellationToken: cancellationToken);

        if (result.ModifiedCount > 0)
        {
            await _cacheService.RemoveAsync($"project:{project.Id}", cancellationToken);
            await _cacheService.RemoveByPrefixAsync($"projects:{project.OwnerId}", cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<bool> DeleteAsync(string id, string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Project>.Filter.And(
            Builders<Project>.Filter.Eq(p => p.Id, id),
            Builders<Project>.Filter.Eq(p => p.OwnerId, userId),
            Builders<Project>.Filter.Eq(p => p.IsDeleted, false)
        );

        var update = Builders<Project>.Update
            .Set(p => p.IsDeleted, true)
            .Set(p => p.DeletedAt, DateTime.UtcNow)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        var result = await _projects.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.ModifiedCount > 0)
        {
            await _cacheService.RemoveAsync($"project:{id}", cancellationToken);
            await _cacheService.RemoveByPrefixAsync($"projects:{userId}", cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<bool> ExistsAsync(string id, string userId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Project>.Filter.And(
            Builders<Project>.Filter.Eq(p => p.Id, id),
            Builders<Project>.Filter.Eq(p => p.OwnerId, userId),
            Builders<Project>.Filter.Eq(p => p.IsDeleted, false)
        );

        var count = await _projects.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        return count > 0;
    }
}
