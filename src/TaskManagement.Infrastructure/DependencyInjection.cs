using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Interfaces;
using TaskManagement.Infrastructure.Configuration;
using TaskManagement.Infrastructure.Persistence;
using TaskManagement.Infrastructure.Persistence.Migrations;
using TaskManagement.Infrastructure.Persistence.Repositories;
using TaskManagement.Infrastructure.Repositories;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<MongoDbSettings>(options => configuration.GetSection("MongoDb").Bind(options));
        services.Configure<RedisSettings>(options => configuration.GetSection("Redis").Bind(options));
        services.Configure<SupabaseSettings>(options => configuration.GetSection("Supabase").Bind(options));

        // MongoDB Client (Singleton - manages its own connection pool)
        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });

        // MongoDB Database (Singleton - lightweight wrapper around client)
        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            var client = sp.GetRequiredService<IMongoClient>();
            return client.GetDatabase(settings.DatabaseName);
        });

        // Database Migrations
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<IMigration, Migration_1_0_0_InitialIndexes>();
        
        // Legacy initializer (kept for backward compatibility, can be removed)
        services.AddSingleton<IMongoDbInitializer, MongoDbInitializer>();
        
        // Database Initialization Hosted Service
        services.AddHostedService<DatabaseInitializationHostedService>();

        // Repositories
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();

        // Services
        services.AddHttpClient<SupabaseAuthService>()
            .AddPolicyHandler((serviceProvider, request) =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<SupabaseAuthService>>();
                var context = new Polly.Context().WithLogger(logger);
                return PollyConfiguration.GetFullResiliencePolicy();
            });
        services.AddScoped<IAuthService, SessionManagedAuthService>();

        return services;
    }
}
