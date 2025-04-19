// Scanner111.Infrastructure/Persistence/AppDbContext.cs
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
    public DbSet<CrashLogPlugin> CrashLogPlugins { get; set; } = null!;
    public DbSet<CrashLogCallStack> CrashLogCallStacks { get; set; } = null!;
    public DbSet<CrashLogIssue> CrashLogIssues { get; set; } = null!;
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
        
        // CrashLogPlugin configuration
        modelBuilder.Entity<CrashLogPlugin>()
            .HasKey(p => p.Id);
            
        modelBuilder.Entity<CrashLogPlugin>()
            .HasOne(p => p.CrashLog)
            .WithMany(c => c.Plugins)
            .HasForeignKey(p => p.CrashLogId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<CrashLogPlugin>()
            .Property(p => p.PluginName)
            .IsRequired()
            .HasMaxLength(255);
        
        // CrashLogCallStack configuration
        modelBuilder.Entity<CrashLogCallStack>()
            .HasKey(c => c.Id);
            
        modelBuilder.Entity<CrashLogCallStack>()
            .HasOne(c => c.CrashLog)
            .WithMany(cl => cl.CallStackEntries)
            .HasForeignKey(c => c.CrashLogId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // CrashLogIssue configuration
        modelBuilder.Entity<CrashLogIssue>()
            .HasKey(i => i.Id);
            
        modelBuilder.Entity<CrashLogIssue>()
            .HasOne(i => i.CrashLog)
            .WithMany(c => c.DetectedIssues)
            .HasForeignKey(i => i.CrashLogId)
            .OnDelete(DeleteBehavior.Cascade);
            
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