// Scanner111.Infrastructure/Persistence/Extensions/DatabaseExtensions.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Scanner111.Infrastructure.Persistence.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Ensures the database is created and applies any pending migrations.
    /// </summary>
    /// <param name="serviceProvider">The service provider containing the DbContext.</param>
    /// <param name="logger">Optional logger for logging migration information.</param>
    public static async Task EnsureDatabaseCreatedAndMigratedAsync(this IServiceProvider serviceProvider, ILogger? logger = null)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            logger?.LogInformation("Ensuring database exists and applying migrations...");
            
            // Create database if it doesn't exist
            await dbContext.Database.EnsureCreatedAsync();
            
            // Apply any pending migrations
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                await dbContext.Database.MigrateAsync();
                logger?.LogInformation("Database migrations applied successfully.");
            }
            else
            {
                logger?.LogInformation("No pending migrations found.");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error occurred while migrating the database.");
            throw;
        }
    }
    
    /// <summary>
    /// Seed initial data into the database if it's empty.
    /// </summary>
    /// <param name="serviceProvider">The service provider containing the DbContext.</param>
    /// <param name="logger">Optional logger for logging seeding information.</param>
    public static async Task SeedInitialDataAsync(this IServiceProvider serviceProvider, ILogger? logger = null)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            logger?.LogInformation("Checking if database needs seeding...");
            
            // Check if the database is empty
            if (!await dbContext.Games.AnyAsync())
            {
                logger?.LogInformation("Database is empty. Seeding initial data...");
                
                // Add initial games
                await SeedGamesAsync(dbContext);
                
                logger?.LogInformation("Initial data seeded successfully.");
            }
            else
            {
                logger?.LogInformation("Database already contains data. Skipping seeding.");
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }
    
    private static async Task SeedGamesAsync(AppDbContext dbContext)
    {
        // Seed Fallout 4
        await dbContext.Games.AddAsync(new Core.Models.Game
        {
            Id = "Fallout4",
            Name = "Fallout 4",
            ExecutableName = "Fallout4.exe",
            Version = "1.10.163.0",
            DocumentsPath = "",
            InstallPath = ""
        });
        
        // Seed Fallout 4 VR
        await dbContext.Games.AddAsync(new Core.Models.Game
        {
            Id = "Fallout4VR",
            Name = "Fallout 4 VR",
            ExecutableName = "Fallout4VR.exe",
            Version = "1.2.72.0",
            DocumentsPath = "",
            InstallPath = ""
        });
        
        // Seed Skyrim Special Edition
        await dbContext.Games.AddAsync(new Core.Models.Game
        {
            Id = "SkyrimSE",
            Name = "Skyrim Special Edition",
            ExecutableName = "SkyrimSE.exe",
            Version = "1.6.640.0",
            DocumentsPath = "",
            InstallPath = ""
        });
        
        await dbContext.SaveChangesAsync();
    }
}