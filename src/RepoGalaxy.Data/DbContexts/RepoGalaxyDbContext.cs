using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.DbContexts;

public class RepoGalaxyDbContext : DbContext
{
    public DbSet<RepositoryEntity> Repositories => Set<RepositoryEntity>();
    public DbSet<BookmarkEntity> Bookmarks => Set<BookmarkEntity>();
    public DbSet<ViewHistoryEntity> ViewHistories => Set<ViewHistoryEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<LocalRepositoryEntity> LocalRepositories => Set<LocalRepositoryEntity>();
    public DbSet<UserPreferenceEntity> UserPreferences => Set<UserPreferenceEntity>();
    
    private readonly string? _dbPath;
    
    public RepoGalaxyDbContext()
    {
        _dbPath = GetDefaultDatabasePath();
    }
    
    public RepoGalaxyDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }
    
    public RepoGalaxyDbContext(DbContextOptions<RepoGalaxyDbContext> options) : base(options)
    {
        _dbPath = null;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_dbPath}");
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Repository 索引
        modelBuilder.Entity<RepositoryEntity>()
            .HasIndex(r => new { r.Owner, r.Name })
            .IsUnique();
        
        modelBuilder.Entity<RepositoryEntity>()
            .HasIndex(r => r.Stars);
        
        modelBuilder.Entity<RepositoryEntity>()
            .HasIndex(r => r.UpdatedAt);
        
        modelBuilder.Entity<RepositoryEntity>()
            .HasIndex(r => r.IsBookmarked);
        
        modelBuilder.Entity<RepositoryEntity>()
            .HasIndex(r => r.CachedAt);
        
        // Bookmark 索引
        modelBuilder.Entity<BookmarkEntity>()
            .HasIndex(b => b.RepositoryId)
            .IsUnique();
        
        // ViewHistory 索引
        modelBuilder.Entity<ViewHistoryEntity>()
            .HasIndex(v => v.ViewedAt);
        
        // User 索引
        modelBuilder.Entity<UserEntity>()
            .HasIndex(u => u.Login)
            .IsUnique();
        
        // LocalRepository 索引
        modelBuilder.Entity<LocalRepositoryEntity>()
            .HasIndex(r => r.LocalPath)
            .IsUnique();
        
        // UserPreference 索引
        modelBuilder.Entity<UserPreferenceEntity>()
            .HasIndex(p => p.UserId)
            .IsUnique();
    }
    
    private static string GetDefaultDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "RepoGalaxy");
        
        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);
        
        return Path.Combine(appFolder, "repogalaxy.db");
    }
}
