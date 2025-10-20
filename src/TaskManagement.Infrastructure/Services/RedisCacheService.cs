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
        _redis = ConnectionMultiplexer.Connect(settings.Value.ConnectionString);
        _database = _redis.GetDatabase();
        _defaultExpiration = TimeSpan.FromMinutes(settings.Value.DefaultExpirationMinutes);
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
