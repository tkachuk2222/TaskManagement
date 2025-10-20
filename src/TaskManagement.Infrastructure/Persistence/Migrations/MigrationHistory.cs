using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// Tracks which migrations have been applied to the database
/// </summary>
public class MigrationHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;
    
    /// <summary>
    /// Migration version identifier
    /// </summary>
    public string Version { get; set; } = null!;
    
    /// <summary>
    /// Migration description
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// When the migration was applied
    /// </summary>
    public DateTime AppliedAt { get; set; }
    
    /// <summary>
    /// Success or failure status
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
