using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Configuration;

namespace TaskManagement.Infrastructure.Persistence;

public interface IMongoDbInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public class MongoDbInitializer : IMongoDbInitializer
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbSettings _settings;
    private readonly ILogger<MongoDbInitializer> _logger;

    public MongoDbInitializer(
        IMongoDatabase database, 
        IOptions<MongoDbSettings> settings,
        ILogger<MongoDbInitializer> logger)
    {
        _database = database;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting MongoDB database initialization...");

        await CreateProjectIndexesAsync(cancellationToken);
        await CreateTaskIndexesAsync(cancellationToken);

        _logger.LogInformation("MongoDB database initialization completed successfully");
    }

    private async Task CreateProjectIndexesAsync(CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<Project>(_settings.ProjectsCollection);
        
        var existingIndexes = await collection.Indexes.List().ToListAsync(cancellationToken);
        var indexNames = existingIndexes.Select(idx => idx["name"].AsString).ToHashSet();

        _logger.LogInformation("Creating indexes for Projects collection...");

        // Compound index for common queries (OwnerId + Status + IsDeleted)
        if (!indexNames.Contains("idx_owner_status_deleted"))
        {
            var indexKeys = Builders<Project>.IndexKeys
                .Ascending(p => p.OwnerId)
                .Ascending(p => p.Status)
                .Ascending(p => p.IsDeleted);
            var indexOptions = new CreateIndexOptions { Name = "idx_owner_status_deleted" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<Project>(indexKeys, indexOptions), 
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_owner_status_deleted");
        }

        // Text search index for Name and Description
        if (!indexNames.Contains("idx_text_search"))
        {
            var nameIndexKeys = Builders<Project>.IndexKeys
                .Text(p => p.Name)
                .Text(p => p.Description);
            var nameIndexOptions = new CreateIndexOptions { Name = "idx_text_search" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<Project>(nameIndexKeys, nameIndexOptions),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_text_search");
        }

        // Index for CreatedAt sorting
        if (!indexNames.Contains("idx_created_at"))
        {
            var createdAtKeys = Builders<Project>.IndexKeys.Descending(p => p.CreatedAt);
            var createdAtOptions = new CreateIndexOptions { Name = "idx_created_at" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<Project>(createdAtKeys, createdAtOptions),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_created_at");
        }

        // Index for UpdatedAt sorting
        if (!indexNames.Contains("idx_updated_at"))
        {
            var updatedAtKeys = Builders<Project>.IndexKeys.Descending(p => p.UpdatedAt);
            var updatedAtOptions = new CreateIndexOptions { Name = "idx_updated_at" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<Project>(updatedAtKeys, updatedAtOptions),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_updated_at");
        }

        _logger.LogInformation("Projects collection indexes created successfully");
    }

    private async Task CreateTaskIndexesAsync(CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<ProjectTask>(_settings.TasksCollection);
        
        var existingIndexes = await collection.Indexes.List().ToListAsync(cancellationToken);
        var indexNames = existingIndexes.Select(idx => idx["name"].AsString).ToHashSet();

        _logger.LogInformation("Creating indexes for Tasks collection...");

        // Compound index for ProjectId + Status + IsDeleted
        if (!indexNames.Contains("idx_project_status_deleted"))
        {
            var projectIndexKeys = Builders<ProjectTask>.IndexKeys
                .Ascending(t => t.ProjectId)
                .Ascending(t => t.Status)
                .Ascending(t => t.IsDeleted);
            var projectIndexOptions = new CreateIndexOptions { Name = "idx_project_status_deleted" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(projectIndexKeys, projectIndexOptions),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_project_status_deleted");
        }

        // Index for AssignedToId
        if (!indexNames.Contains("idx_assigned_to"))
        {
            var assignedIndexKeys = Builders<ProjectTask>.IndexKeys.Ascending(t => t.AssignedToId);
            var assignedIndexOptions = new CreateIndexOptions { Name = "idx_assigned_to" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(assignedIndexKeys, assignedIndexOptions),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_assigned_to");
        }

        // Index for CreatedById
        if (!indexNames.Contains("idx_created_by"))
        {
            var createdByIndexKeys = Builders<ProjectTask>.IndexKeys.Ascending(t => t.CreatedById);
            var createdByIndexOptions = new CreateIndexOptions { Name = "idx_created_by" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(createdByIndexKeys, createdByIndexOptions),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_created_by");
        }

        // Index for Priority sorting
        if (!indexNames.Contains("idx_priority"))
        {
            var priorityIndexKeys = Builders<ProjectTask>.IndexKeys.Descending(t => t.Priority);
            var priorityIndexOptions = new CreateIndexOptions { Name = "idx_priority" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(priorityIndexKeys, priorityIndexOptions),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_priority");
        }

        // Index for DueDate
        if (!indexNames.Contains("idx_due_date"))
        {
            var dueDateIndexKeys = Builders<ProjectTask>.IndexKeys.Ascending(t => t.DueDate);
            var dueDateIndexOptions = new CreateIndexOptions { Name = "idx_due_date" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(dueDateIndexKeys, dueDateIndexOptions),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_due_date");
        }

        // Index for CreatedAt sorting
        if (!indexNames.Contains("idx_task_created_at"))
        {
            var createdAtKeys = Builders<ProjectTask>.IndexKeys.Descending(t => t.CreatedAt);
            var createdAtOptions = new CreateIndexOptions { Name = "idx_task_created_at" };
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(createdAtKeys, createdAtOptions),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Created index: idx_task_created_at");
        }

        _logger.LogInformation("Tasks collection indexes created successfully");
    }
}
