using StackExchange.Redis;
using System.Text.Json;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace TaskManagement.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly TimeSpan _defaultExpiration;

    public RedisCacheService(IOptions<RedisSettings> settings)
    {
        var connectionString = settings.Value.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Redis connection string is not configured. " +
                "Please set the Redis__ConnectionString environment variable or add Redis service to Railway.");
        }

        try
        {
            // Convert Railway Redis URL format (redis://user:password@host:port) to StackExchange.Redis format
            var parsedConnectionString = ParseRedisConnectionString(connectionString);
            _redis = ConnectionMultiplexer.Connect(parsedConnectionString);
            _database = _redis.GetDatabase();
            _defaultExpiration = TimeSpan.FromMinutes(settings.Value.DefaultExpirationMinutes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to connect to Redis at '{connectionString}'. " +
                "Please verify the connection string and ensure Redis service is running.", ex);
        }
    }

    private static string ParseRedisConnectionString(string connectionString)
    {
        // If it's already in StackExchange.Redis format (host:port), return as-is
        if (!connectionString.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        // Parse Railway/Heroku format: redis://user:password@host:port
        var uri = new Uri(connectionString);
        var host = uri.Host;
        var port = uri.Port;
        var password = uri.UserInfo.Split(':').Last();

        // Build StackExchange.Redis connection string
        return $"{host}:{port},password={password},ssl=false,abortConnect=false";
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var value = await _database.StringGetAsync(key);
        
        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        await _database.StringSetAsync(key, json, expiration ?? _defaultExpiration);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var endpoints = _redis.GetEndPoints();
        foreach (var endpoint in endpoints)
        {
            var server = _redis.GetServer(endpoint);
            var keys = server.Keys(pattern: $"{prefix}*");
            
            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
            }
        }
    }
}
