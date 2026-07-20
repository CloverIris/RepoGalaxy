using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.DbContexts;

public class RepoGalaxyDbContext : DbContext
{
    public DbSet<RepositoryEntity> Repositories => Set<RepositoryEntity>();
    public DbSet<BookmarkEntity> Bookmarks => Set<BookmarkEntity>();
    public DbSet<BookmarkTagEntity> BookmarkTags => Set<BookmarkTagEntity>();
    public DbSet<ViewHistoryEntity> ViewHistories => Set<ViewHistoryEntity>();
    public DbSet<DiscoverySubscriptionEntity> DiscoverySubscriptions => Set<DiscoverySubscriptionEntity>();
    public DbSet<FeedItemEntity> FeedItems => Set<FeedItemEntity>();
    public DbSet<ReleaseNotificationEntity> ReleaseNotifications => Set<ReleaseNotificationEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<LocalRepositoryEntity> LocalRepositories => Set<LocalRepositoryEntity>();
    public DbSet<UserPreferenceEntity> UserPreferences => Set<UserPreferenceEntity>();

    // A single options constructor is required for IDbContextFactory. Keeping a
    // path-based constructor makes dependency injection unable to select an
    // activator while the desktop app is starting.
    public RepoGalaxyDbContext(DbContextOptions<RepoGalaxyDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RepositoryEntity>().HasIndex(r => new { r.Owner, r.Name }).IsUnique();
        modelBuilder.Entity<RepositoryEntity>().HasIndex(r => r.CachedAt);
        modelBuilder.Entity<BookmarkEntity>().HasIndex(b => b.RepositoryId).IsUnique();
        modelBuilder.Entity<BookmarkTagEntity>().HasIndex(t => new { t.BookmarkId, t.Name }).IsUnique();
        modelBuilder.Entity<DiscoverySubscriptionEntity>().HasIndex(s => s.Name).IsUnique();
        modelBuilder.Entity<FeedItemEntity>().HasIndex(f => new { f.RepositoryId, f.Source }).IsUnique();
        modelBuilder.Entity<FeedItemEntity>().HasIndex(f => new { f.IsRead, f.DiscoveredAt });
        modelBuilder.Entity<ReleaseNotificationEntity>().HasIndex(r => new { r.RepositoryId, r.ReleaseId }).IsUnique();
        modelBuilder.Entity<UserEntity>().HasIndex(u => u.Login).IsUnique();
        modelBuilder.Entity<LocalRepositoryEntity>().HasIndex(r => r.LocalPath).IsUnique();
        modelBuilder.Entity<UserPreferenceEntity>().HasIndex(p => p.UserId).IsUnique();
    }

}
