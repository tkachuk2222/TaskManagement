using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskManagement.Infrastructure.Persistence.Migrations;

namespace TaskManagement.Infrastructure.Persistence;

public class DatabaseInitializationHostedService : IHostedService
{
    private readonly MigrationRunner _migrationRunner;
    private readonly IEnumerable<IMigration> _migrations;
    private readonly ILogger<DatabaseInitializationHostedService> _logger;

    public DatabaseInitializationHostedService(
        MigrationRunner migrationRunner,
        IEnumerable<IMigration> migrations,
        ILogger<DatabaseInitializationHostedService> logger)
    {
        _migrationRunner = migrationRunner;
        _migrations = migrations;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting database initialization with migrations...");
            await _migrationRunner.RunMigrationsAsync(_migrations, cancellationToken);
            _logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while initializing the database");
            // Don't throw - let the application start even if migrations fail
            // This allows the app to run in read-only mode or with degraded performance
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
