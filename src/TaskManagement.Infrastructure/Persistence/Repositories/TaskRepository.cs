using MongoDB.Driver;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using DomainTaskStatus = TaskManagement.Domain.Enums.TaskStatus;
using TaskManagement.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace TaskManagement.Infrastructure.Persistence.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly IMongoCollection<ProjectTask> _tasks;
    private readonly ICacheService _cacheService;

    public TaskRepository(IMongoDatabase database, IOptions<MongoDbSettings> settings, ICacheService cacheService)
    {
        _tasks = database.GetCollection<ProjectTask>(settings.Value.TasksCollection);
        _cacheService = cacheService;
    }

    public async Task<ProjectTask?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"task:{id}";
        var cached = await _cacheService.GetAsync<ProjectTask>(cacheKey, cancellationToken);
        if (cached != null) return cached;

        var filter = Builders<ProjectTask>.Filter.And(
            Builders<ProjectTask>.Filter.Eq(t => t.Id, id),
            Builders<ProjectTask>.Filter.Eq(t => t.IsDeleted, false)
        );

        var task = await _tasks.Find(filter).FirstOrDefaultAsync(cancellationToken);
        
        if (task != null)
        {
            await _cacheService.SetAsync(cacheKey, task, TimeSpan.FromMinutes(5), cancellationToken);
        }

        return task;
    }

    public async Task<(List<ProjectTask> tasks, int totalCount)> GetProjectTasksAsync(
        string projectId,
        int pageNumber,
        int pageSize,
        DomainTaskStatus? status = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var filterBuilder = Builders<ProjectTask>.Filter;
        var filters = new List<FilterDefinition<ProjectTask>>
        {
            filterBuilder.Eq(t => t.ProjectId, projectId),
            filterBuilder.Eq(t => t.IsDeleted, false)
        };

        if (status.HasValue)
        {
            filters.Add(filterBuilder.Eq(t => t.Status, status.Value));
        }

        var filter = filterBuilder.And(filters);

        var totalCount = await _tasks.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        SortDefinition<ProjectTask> sort = sortBy?.ToLower() switch
        {
            "priority" => sortDescending 
                ? Builders<ProjectTask>.Sort.Descending(t => t.Priority) 
                : Builders<ProjectTask>.Sort.Ascending(t => t.Priority),
            "duedate" => sortDescending 
                ? Builders<ProjectTask>.Sort.Descending(t => t.DueDate) 
                : Builders<ProjectTask>.Sort.Ascending(t => t.DueDate),
            "status" => sortDescending 
                ? Builders<ProjectTask>.Sort.Descending(t => t.Status) 
                : Builders<ProjectTask>.Sort.Ascending(t => t.Status),
            _ => sortDescending 
                ? Builders<ProjectTask>.Sort.Descending(t => t.CreatedAt) 
                : Builders<ProjectTask>.Sort.Ascending(t => t.CreatedAt)
        };

        var tasks = await _tasks.Find(filter)
            .Sort(sort)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (tasks, (int)totalCount);
    }

    public async Task<ProjectTask> CreateAsync(ProjectTask task, CancellationToken cancellationToken = default)
    {
        task.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        task.CreatedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        task.IsDeleted = false;

        await _tasks.InsertOneAsync(task, cancellationToken: cancellationToken);
        
        var cacheKey = $"task:{task.Id}";
        await _cacheService.SetAsync(cacheKey, task, TimeSpan.FromMinutes(5), cancellationToken);
        await _cacheService.RemoveByPrefixAsync($"tasks:{task.ProjectId}", cancellationToken);
        await _cacheService.RemoveAsync($"project:{task.ProjectId}", cancellationToken);

        return task;
    }

    public async Task<bool> UpdateAsync(ProjectTask task, CancellationToken cancellationToken = default)
    {
        task.UpdatedAt = DateTime.UtcNow;

        var filter = Builders<ProjectTask>.Filter.And(
            Builders<ProjectTask>.Filter.Eq(t => t.Id, task.Id),
            Builders<ProjectTask>.Filter.Eq(t => t.IsDeleted, false)
        );

        var result = await _tasks.ReplaceOneAsync(filter, task, cancellationToken: cancellationToken);

        if (result.ModifiedCount > 0)
        {
            await _cacheService.RemoveAsync($"task:{task.Id}", cancellationToken);
            await _cacheService.RemoveByPrefixAsync($"tasks:{task.ProjectId}", cancellationToken);
            await _cacheService.RemoveAsync($"project:{task.ProjectId}", cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var task = await GetByIdAsync(id, cancellationToken);
        if (task == null) return false;

        var filter = Builders<ProjectTask>.Filter.And(
            Builders<ProjectTask>.Filter.Eq(t => t.Id, id),
            Builders<ProjectTask>.Filter.Eq(t => t.IsDeleted, false)
        );

        var update = Builders<ProjectTask>.Update
            .Set(t => t.IsDeleted, true)
            .Set(t => t.DeletedAt, DateTime.UtcNow)
            .Set(t => t.UpdatedAt, DateTime.UtcNow);

        var result = await _tasks.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        if (result.ModifiedCount > 0)
        {
            await _cacheService.RemoveAsync($"task:{id}", cancellationToken);
            await _cacheService.RemoveByPrefixAsync($"tasks:{task.ProjectId}", cancellationToken);
            await _cacheService.RemoveAsync($"project:{task.ProjectId}", cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<List<ProjectTask>> GetTasksByProjectIdAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ProjectTask>.Filter.And(
            Builders<ProjectTask>.Filter.Eq(t => t.ProjectId, projectId),
            Builders<ProjectTask>.Filter.Eq(t => t.IsDeleted, false)
        );

        return await _tasks.Find(filter)
            .Sort(Builders<ProjectTask>.Sort.Descending(t => t.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetTaskCountByStatusAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ProjectTask>.Filter.And(
            Builders<ProjectTask>.Filter.Eq(t => t.ProjectId, projectId),
            Builders<ProjectTask>.Filter.Eq(t => t.IsDeleted, false)
        );

        var tasks = await _tasks.Find(filter).ToListAsync(cancellationToken);
        
        return tasks.GroupBy(t => t.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetTaskCountByPriorityAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ProjectTask>.Filter.And(
            Builders<ProjectTask>.Filter.Eq(t => t.ProjectId, projectId),
            Builders<ProjectTask>.Filter.Eq(t => t.IsDeleted, false)
        );

        var tasks = await _tasks.Find(filter).ToListAsync(cancellationToken);
        
        return tasks.GroupBy(t => t.Priority.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
