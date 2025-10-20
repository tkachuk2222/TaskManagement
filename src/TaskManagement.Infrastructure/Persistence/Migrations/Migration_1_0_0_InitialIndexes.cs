using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Configuration;

namespace TaskManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// Initial migration to create database indexes
/// Version: 1.0.0
/// </summary>
public class Migration_1_0_0_InitialIndexes : IMigration
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbSettings _settings;
    private readonly ILogger<Migration_1_0_0_InitialIndexes> _logger;

    public string Version => "1.0.0";
    public string Description => "Create initial indexes for Projects and Tasks collections";

    public Migration_1_0_0_InitialIndexes(
        IMongoDatabase database,
        IOptions<MongoDbSettings> settings,
        ILogger<Migration_1_0_0_InitialIndexes> logger)
    {
        _database = database;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task UpAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating initial indexes...");
        
        await CreateProjectIndexesAsync(cancellationToken);
        await CreateTaskIndexesAsync(cancellationToken);
        
        _logger.LogInformation("Initial indexes created successfully");
    }

    public Task DownAsync(CancellationToken cancellationToken = default)
    {
        // For MongoDB, we typically don't drop indexes on rollback
        // They don't hurt performance and might be in use
        _logger.LogWarning("Rollback not implemented for index creation");
        return Task.CompletedTask;
    }

    private async Task CreateProjectIndexesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var collection = _database.GetCollection<Project>(_settings.ProjectsCollection);

            // Compound index for common queries
            var indexKeys1 = Builders<Project>.IndexKeys
                .Ascending(p => p.OwnerId)
                .Ascending(p => p.Status)
                .Ascending(p => p.IsDeleted);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<Project>(indexKeys1, new CreateIndexOptions { Name = "idx_owner_status_deleted" }),
                cancellationToken: cancellationToken);

            // Text search index
            var indexKeys2 = Builders<Project>.IndexKeys.Text(p => p.Name).Text(p => p.Description);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<Project>(indexKeys2, new CreateIndexOptions { Name = "idx_text_search" }),
                cancellationToken: cancellationToken);

            // CreatedAt index
            var indexKeys3 = Builders<Project>.IndexKeys.Descending(p => p.CreatedAt);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<Project>(indexKeys3, new CreateIndexOptions { Name = "idx_created_at" }),
                cancellationToken: cancellationToken);

            // UpdatedAt index
            var indexKeys4 = Builders<Project>.IndexKeys.Descending(p => p.UpdatedAt);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<Project>(indexKeys4, new CreateIndexOptions { Name = "idx_updated_at" }),
                cancellationToken: cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.Message != null && ex.Message.Contains("disk space"))
        {
            _logger.LogWarning("Insufficient disk space to create project indexes. The application will continue without these indexes (queries may be slower).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create project indexes. The application will continue without these indexes (queries may be slower).");
        }
    }

    private async Task CreateTaskIndexesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var collection = _database.GetCollection<ProjectTask>(_settings.TasksCollection);

            // Project + Status + IsDeleted compound index
            var indexKeys1 = Builders<ProjectTask>.IndexKeys
                .Ascending(t => t.ProjectId)
                .Ascending(t => t.Status)
                .Ascending(t => t.IsDeleted);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(indexKeys1, new CreateIndexOptions { Name = "idx_project_status_deleted" }),
                cancellationToken: cancellationToken);

            // Other indexes...
            var indexKeys2 = Builders<ProjectTask>.IndexKeys.Ascending(t => t.AssignedToId);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(indexKeys2, new CreateIndexOptions { Name = "idx_assigned_to" }),
                cancellationToken: cancellationToken);

            var indexKeys3 = Builders<ProjectTask>.IndexKeys.Ascending(t => t.CreatedById);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(indexKeys3, new CreateIndexOptions { Name = "idx_created_by" }),
                cancellationToken: cancellationToken);

            var indexKeys4 = Builders<ProjectTask>.IndexKeys.Descending(t => t.Priority);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(indexKeys4, new CreateIndexOptions { Name = "idx_priority" }),
                cancellationToken: cancellationToken);

            var indexKeys5 = Builders<ProjectTask>.IndexKeys.Ascending(t => t.DueDate);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(indexKeys5, new CreateIndexOptions { Name = "idx_due_date" }),
                cancellationToken: cancellationToken);

            var indexKeys6 = Builders<ProjectTask>.IndexKeys.Descending(t => t.CreatedAt);
            await collection.Indexes.CreateOneAsync(
                new CreateIndexModel<ProjectTask>(indexKeys6, new CreateIndexOptions { Name = "idx_task_created_at" }),
                cancellationToken: cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.Message != null && ex.Message.Contains("disk space"))
        {
            _logger.LogWarning("Insufficient disk space to create task indexes. The application will continue without these indexes (queries may be slower).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create task indexes. The application will continue without these indexes (queries may be slower).");
        }
    }
}
