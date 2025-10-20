using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace TaskManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// Manages database migrations with version tracking
/// </summary>
public class MigrationRunner
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MigrationRunner> _logger;
    private readonly IMongoCollection<MigrationHistory> _migrationHistory;

    public MigrationRunner(IMongoDatabase database, ILogger<MigrationRunner> logger)
    {
        _database = database;
        _logger = logger;
        _migrationHistory = database.GetCollection<MigrationHistory>("__MigrationHistory");
    }

    /// <summary>
    /// Run all pending migrations
    /// </summary>
    public async Task RunMigrationsAsync(IEnumerable<IMigration> migrations, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting migration process...");

        // Get applied migrations
        var appliedMigrations = await _migrationHistory
            .Find(m => m.Success)
            .ToListAsync(cancellationToken);
        
        var appliedVersions = appliedMigrations.Select(m => m.Version).ToHashSet();

        // Get pending migrations (ordered by version)
        var pendingMigrations = migrations
            .Where(m => !appliedVersions.Contains(m.Version))
            .OrderBy(m => m.Version)
            .ToList();

        if (!pendingMigrations.Any())
        {
            _logger.LogInformation("No pending migrations found");
            return;
        }

        _logger.LogInformation("Found {Count} pending migration(s)", pendingMigrations.Count);

        foreach (var migration in pendingMigrations)
        {
            await RunMigrationAsync(migration, cancellationToken);
        }

        _logger.LogInformation("Migration process completed successfully");
    }

    private async Task RunMigrationAsync(IMigration migration, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running migration {Version}: {Description}", migration.Version, migration.Description);

        var historyEntry = new MigrationHistory
        {
            Version = migration.Version,
            Description = migration.Description,
            AppliedAt = DateTime.UtcNow,
            Success = false
        };

        try
        {
            await migration.UpAsync(cancellationToken);
            historyEntry.Success = true;
            
            await _migrationHistory.InsertOneAsync(historyEntry, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Successfully applied migration {Version}", migration.Version);
        }
        catch (Exception ex)
        {
            historyEntry.ErrorMessage = ex.Message;
            await _migrationHistory.InsertOneAsync(historyEntry, cancellationToken: cancellationToken);
            
            _logger.LogError(ex, "Failed to apply migration {Version}", migration.Version);
            throw new InvalidOperationException($"Migration {migration.Version} failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get migration history
    /// </summary>
    public async Task<List<MigrationHistory>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await _migrationHistory
            .Find(_ => true)
            .SortByDescending(m => m.AppliedAt)
            .ToListAsync(cancellationToken);
    }
}
