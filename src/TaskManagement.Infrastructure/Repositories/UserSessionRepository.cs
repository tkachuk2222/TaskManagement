using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Interfaces;
using TaskManagement.Infrastructure.Configuration;

namespace TaskManagement.Infrastructure.Repositories;

public class UserSessionRepository : IUserSessionRepository
{
    private readonly IMongoCollection<UserSession> _sessions;

    public UserSessionRepository(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        var database = client.GetDatabase(settings.Value.DatabaseName);
        _sessions = database.GetCollection<UserSession>("userSessions");
        
        // Note: Index creation moved to DatabaseInitializationHostedService
        // to avoid creating indexes on every request (repository is scoped)
    }

    public async Task<UserSession> CreateSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        await _sessions.InsertOneAsync(session, cancellationToken: cancellationToken);
        return session;
    }

    public async Task<UserSession?> GetSessionByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await _sessions.Find(s => s.RefreshToken == refreshToken && s.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserSession?> GetSessionByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _sessions.Find(s => s.Id == sessionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _sessions.Find(s => s.UserId == userId && s.IsActive)
            .SortByDescending(s => s.LastActivityAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateLastActivityAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var update = Builders<UserSession>.Update
            .Set(s => s.LastActivityAt, DateTime.UtcNow);

        await _sessions.UpdateOneAsync(
            s => s.Id == sessionId,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task RevokeSessionAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
    {
        var update = Builders<UserSession>.Update
            .Set(s => s.IsActive, false)
            .Set(s => s.RevokedAt, DateTime.UtcNow)
            .Set(s => s.RevokedReason, reason);

        await _sessions.UpdateOneAsync(
            s => s.Id == sessionId,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task RevokeAllUserSessionsAsync(string userId, string reason, CancellationToken cancellationToken = default)
    {
        var update = Builders<UserSession>.Update
            .Set(s => s.IsActive, false)
            .Set(s => s.RevokedAt, DateTime.UtcNow)
            .Set(s => s.RevokedReason, reason);

        await _sessions.UpdateManyAsync(
            s => s.UserId == userId && s.IsActive,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        await _sessions.DeleteManyAsync(
            s => s.ExpiresAt < DateTime.UtcNow,
            cancellationToken);
    }

    public async Task UpdateRefreshTokenAsync(string sessionId, string newRefreshToken, string newAccessTokenHash, DateTime newExpiresAt, CancellationToken cancellationToken = default)
    {
        var update = Builders<UserSession>.Update
            .Set(s => s.RefreshToken, newRefreshToken)
            .Set(s => s.AccessTokenHash, newAccessTokenHash)
            .Set(s => s.ExpiresAt, newExpiresAt)
            .Set(s => s.LastActivityAt, DateTime.UtcNow);

        await _sessions.UpdateOneAsync(
            s => s.Id == sessionId,
            update,
            cancellationToken: cancellationToken);
    }
}
