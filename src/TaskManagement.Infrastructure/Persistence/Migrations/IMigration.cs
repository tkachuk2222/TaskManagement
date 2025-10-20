namespace TaskManagement.Infrastructure.Persistence.Migrations;

public interface IMigration
{
    /// <summary>
    /// Unique migration version (e.g., "1.0.0", "20250119_001")
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// Description of what this migration does
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Execute the migration
    /// </summary>
    Task UpAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rollback the migration (optional for MongoDB)
    /// </summary>
    Task DownAsync(CancellationToken cancellationToken = default);
}
