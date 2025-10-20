using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TaskManagement.Domain.Entities;

/// <summary>
/// Represents an active user session with refresh token
/// </summary>
public class UserSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>
    /// Supabase user ID (from JWT sub claim)
    /// </summary>
    [BsonElement("userId")]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// User email for easy lookup
    /// </summary>
    [BsonElement("email")]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Refresh token from Supabase (encrypted)
    /// </summary>
    [BsonElement("refreshToken")]
    public string RefreshToken { get; set; } = null!;

    /// <summary>
    /// Access token hash (for validation)
    /// </summary>
    [BsonElement("accessTokenHash")]
    public string AccessTokenHash { get; set; } = null!;

    /// <summary>
    /// When the session was created
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the session expires (30 days default)
    /// </summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    [BsonElement("lastActivityAt")]
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IP address of the client
    /// </summary>
    [BsonElement("ipAddress")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent (browser/device info)
    /// </summary>
    [BsonElement("userAgent")]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Device information
    /// </summary>
    [BsonElement("deviceInfo")]
    public DeviceInfo? DeviceInfo { get; set; }

    /// <summary>
    /// Whether the session is active
    /// </summary>
    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the session was revoked (null if still active)
    /// </summary>
    [BsonElement("revokedAt")]
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Reason for revocation (logout, security, expired, etc.)
    /// </summary>
    [BsonElement("revokedReason")]
    public string? RevokedReason { get; set; }
}

public class DeviceInfo
{
    [BsonElement("deviceType")]
    public string? DeviceType { get; set; } // Mobile, Desktop, Tablet

    [BsonElement("os")]
    public string? OS { get; set; } // Windows, macOS, Linux, iOS, Android

    [BsonElement("browser")]
    public string? Browser { get; set; } // Chrome, Firefox, Safari, Edge

    [BsonElement("location")]
    public string? Location { get; set; } // City, Country (from IP)
}
