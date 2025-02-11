using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

public class DatabaseMigrationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(IServiceProvider serviceProvider, ILogger<DatabaseMigrationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task MigrateDatabaseAsync()
    {
        int maxRetries = 5;
        int delaySeconds = 10;

        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                _logger.LogInformation("Applying database migrations...");
                await dbContext.Database.MigrateAsync();
                _logger.LogInformation("Database migration completed successfully.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Database migration failed. Retry {retry + 1}/{maxRetries} in {delaySeconds} seconds...");
                await Task.Delay(delaySeconds * 1000);
            }
        }

        _logger.LogError("Database migration failed after multiple attempts. Exiting.");
    }
}
