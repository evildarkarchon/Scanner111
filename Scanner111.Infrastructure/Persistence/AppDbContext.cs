using Microsoft.EntityFrameworkCore;
using Scanner111.Core.Models;

namespace Scanner111.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public DbSet<Game> Games { get; set; } = null!;
    public DbSet<CrashLog> CrashLogs { get; set; } = null!;
    public DbSet<Plugin> Plugins { get; set; } = null!;
    public DbSet<ModIssue> ModIssues { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Game configuration
        modelBuilder.Entity<Game>()
            .HasKey(g => g.Id);
        
        modelBuilder.Entity<Game>()
            .Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        modelBuilder.Entity<Game>()
            .Property(g => g.ExecutableName)
            .IsRequired()
            .HasMaxLength(100);
            
        // CrashLog configuration
        modelBuilder.Entity<CrashLog>()
            .HasKey(c => c.Id);
            
        modelBuilder.Entity<CrashLog>()
            .Property(c => c.FileName)
            .IsRequired()
            .HasMaxLength(255);
            
        modelBuilder.Entity<CrashLog>()
            .Property(c => c.FilePath)
            .IsRequired()
            .HasMaxLength(1000);
            
        modelBuilder.Entity<CrashLog>()
            .Property(c => c.GameId)
            .IsRequired()
            .HasMaxLength(100);
            
        // Plugin configuration
        modelBuilder.Entity<Plugin>()
            .HasKey(p => p.Name);
            
        modelBuilder.Entity<Plugin>()
            .Property(p => p.FileName)
            .IsRequired()
            .HasMaxLength(255);
            
        modelBuilder.Entity<Plugin>()
            .Property(p => p.FilePath)
            .IsRequired()
            .HasMaxLength(1000);
            
        // ModIssue configuration
        modelBuilder.Entity<ModIssue>()
            .HasKey(m => m.Id);
            
        modelBuilder.Entity<ModIssue>()
            .Property(m => m.PluginName)
            .IsRequired()
            .HasMaxLength(255);
            
        modelBuilder.Entity<ModIssue>()
            .Property(m => m.Description)
            .IsRequired()
            .HasMaxLength(1000);
    }
}