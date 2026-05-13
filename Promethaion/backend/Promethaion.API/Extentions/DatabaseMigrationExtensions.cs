using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Promethaion.Data;

namespace Promethaion.API.Extensions;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplySafeMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseMigration");

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<PAionDbContext>();

            logger.LogInformation("Checking pending migrations...");

            var pending = await db.Database.GetPendingMigrationsAsync();

            if (pending.Any())
            {
                logger.LogInformation(
                    "Applying migrations: {Migrations}",
                    string.Join(", ", pending));

                await db.Database.MigrateAsync();

                logger.LogInformation("Database migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("Database already up to date.");
            }
        }
        catch (SqlException ex)
        {
            logger.LogError(ex,
                "SQL migration failure occurred.");

            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected migration error occurred.");

            throw;
        }
    }
}