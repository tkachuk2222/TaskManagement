using TaskManagement.Domain.Entities;

namespace TaskManagement.Domain.Interfaces;

/// <summary>
/// Repository for managing user sessions and refresh tokens
/// </summary>
public interface IUserSessionRepository
{
    /// <summary>
    /// Create a new session
    /// </summary>
    Task<UserSession> CreateSessionAsync(UserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get session by refresh token
    /// </summary>
    Task<UserSession?> GetSessionByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get session by ID
    /// </summary>
    Task<UserSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active sessions for a user
    /// </summary>
    Task<IEnumerable<UserSession>> GetActiveSessionsByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update session last activity
    /// </summary>
    Task UpdateLastActivityAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke a specific session
    /// </summary>
    Task RevokeSessionAsync(string sessionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke all sessions for a user
    /// </summary>
    Task RevokeAllUserSessionsAsync(string userId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete expired sessions (cleanup)
    /// </summary>
    Task DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update session with new refresh token
    /// </summary>
    Task UpdateRefreshTokenAsync(string sessionId, string newRefreshToken, string newAccessTokenHash, DateTime newExpiresAt, CancellationToken cancellationToken = default);
}
