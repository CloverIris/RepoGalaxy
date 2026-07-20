using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
    public DbSet<ApiCacheEntryEntity> ApiCacheEntries => Set<ApiCacheEntryEntity>();
    public DbSet<SyncRunEntity> SyncRuns => Set<SyncRunEntity>();
    public DbSet<SyncCheckpointEntity> SyncCheckpoints => Set<SyncCheckpointEntity>();
    public DbSet<UserRepositoryRelationEntity> UserRepositoryRelations => Set<UserRepositoryRelationEntity>();
    public DbSet<RepositoryMetricSnapshotEntity> RepositoryMetricSnapshots => Set<RepositoryMetricSnapshotEntity>();
    public DbSet<RankingBatchEntity> RankingBatches => Set<RankingBatchEntity>();
    public DbSet<RankingDecisionEntity> RankingDecisions => Set<RankingDecisionEntity>();
    public DbSet<FeedImpressionEntity> FeedImpressions => Set<FeedImpressionEntity>();
    public DbSet<RepositoryTopicEntity> RepositoryTopics => Set<RepositoryTopicEntity>();
    public DbSet<RepositoryLanguageEntity> RepositoryLanguages => Set<RepositoryLanguageEntity>();
    public DbSet<LocalContributionDayEntity> LocalContributionDays => Set<LocalContributionDayEntity>();
    public DbSet<GitIdentityAliasEntity> GitIdentityAliases => Set<GitIdentityAliasEntity>();
    public DbSet<NewsItemEntity> NewsItems => Set<NewsItemEntity>();
    public DbSet<AuthenticationAuditEventEntity> AuthenticationAuditEvents => Set<AuthenticationAuditEventEntity>();
    public DbSet<TileBoardEntity> TileBoards => Set<TileBoardEntity>();
    public DbSet<TilePlacementEntity> TilePlacements => Set<TilePlacementEntity>();
    public DbSet<SemanticIndexPlacementEntity> SemanticIndexPlacements => Set<SemanticIndexPlacementEntity>();
    public DbSet<CloneOperationEntity> CloneOperations => Set<CloneOperationEntity>();
    public DbSet<IdePreferenceEntity> IdePreferences => Set<IdePreferenceEntity>();

    // A single options constructor is required for IDbContextFactory. Keeping a
    // path-based constructor makes dependency injection unable to select an
    // activator while the desktop app is starting.
    public RepoGalaxyDbContext(DbContextOptions<RepoGalaxyDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite has no native DateTimeOffset type. Persist every instant as UTC
        // Unix milliseconds so comparisons and ordering are deterministic.
        var instantConverter = new ValueConverter<DateTimeOffset, long>(
            value => value.ToUniversalTime().ToUnixTimeMilliseconds(),
            value => DateTimeOffset.FromUnixTimeMilliseconds(value));
        var nullableInstantConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.ToUniversalTime().ToUnixTimeMilliseconds() : null,
            value => value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                    property.SetValueConverter(instantConverter);
                else if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(nullableInstantConverter);
            }
        }

        modelBuilder.Entity<RepositoryEntity>().HasIndex(r => new { r.Owner, r.Name }).IsUnique();
        modelBuilder.Entity<RepositoryEntity>().HasIndex(r => r.CachedAt);
        modelBuilder.Entity<BookmarkEntity>().HasIndex(b => b.RepositoryId).IsUnique();
        modelBuilder.Entity<BookmarkTagEntity>().HasIndex(t => new { t.BookmarkId, t.Name }).IsUnique();
        modelBuilder.Entity<DiscoverySubscriptionEntity>().HasIndex(s => s.Name).IsUnique();
        modelBuilder.Entity<FeedItemEntity>().HasIndex(f => new { f.RepositoryId, f.Source, f.BatchId }).IsUnique();
        modelBuilder.Entity<FeedItemEntity>().HasIndex(f => new { f.IsRead, f.DiscoveredAt });
        modelBuilder.Entity<ReleaseNotificationEntity>().HasIndex(r => new { r.RepositoryId, r.ReleaseId }).IsUnique();
        modelBuilder.Entity<UserEntity>().HasIndex(u => u.Login).IsUnique();
        modelBuilder.Entity<LocalRepositoryEntity>().HasIndex(r => r.LocalPath).IsUnique();
        modelBuilder.Entity<UserPreferenceEntity>().HasIndex(p => p.UserId).IsUnique();
        modelBuilder.Entity<ApiCacheEntryEntity>().HasIndex(x => x.LastAccessedAt);
        modelBuilder.Entity<ApiCacheEntryEntity>().HasIndex(x => x.StaleUntil);
        modelBuilder.Entity<SyncRunEntity>().HasIndex(x => x.CorrelationId).IsUnique();
        modelBuilder.Entity<SyncCheckpointEntity>().HasIndex(x => new { x.AccountId, x.JobType, x.ScopeKey }).IsUnique();
        modelBuilder.Entity<UserRepositoryRelationEntity>().HasIndex(x => new { x.AccountId, x.RepositoryId, x.Relation }).IsUnique();
        modelBuilder.Entity<RepositoryMetricSnapshotEntity>().HasIndex(x => new { x.RepositoryId, x.SnapshotDate }).IsUnique();
        modelBuilder.Entity<RankingBatchEntity>().HasIndex(x => x.BatchId).IsUnique();
        modelBuilder.Entity<RankingDecisionEntity>().HasIndex(x => new { x.RankingBatchId, x.RepositoryId }).IsUnique();
        modelBuilder.Entity<RepositoryTopicEntity>().HasIndex(x => new { x.RepositoryId, x.Topic }).IsUnique();
        modelBuilder.Entity<RepositoryLanguageEntity>().HasIndex(x => new { x.RepositoryId, x.Language }).IsUnique();
        modelBuilder.Entity<LocalContributionDayEntity>().HasIndex(x => new { x.LocalRepositoryId, x.Date }).IsUnique();
        modelBuilder.Entity<NewsItemEntity>().HasIndex(x => x.ExternalId).IsUnique();
        modelBuilder.Entity<AuthenticationAuditEventEntity>().HasIndex(x => x.OccurredAt);
        modelBuilder.Entity<TileBoardEntity>().HasIndex(x => new { x.ScopeKey, x.Source, x.LayoutVersion }).IsUnique();
        modelBuilder.Entity<TilePlacementEntity>().HasIndex(x => new { x.BoardId, x.ContentKind, x.ContentKey }).IsUnique();
        modelBuilder.Entity<SemanticIndexPlacementEntity>().HasIndex(x => new { x.BoardId, x.LayoutVersion, x.ItemKey }).IsUnique();
        modelBuilder.Entity<CloneOperationEntity>().HasIndex(x => x.UpdatedAt);
        modelBuilder.Entity<IdePreferenceEntity>().HasIndex(x => new { x.ScopeKey, x.TechnologyKey }).IsUnique();

        modelBuilder.Entity<UserRepositoryRelationEntity>().HasOne(x => x.Repository).WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RepositoryMetricSnapshotEntity>().HasOne<RepositoryEntity>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RepositoryTopicEntity>().HasOne<RepositoryEntity>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RepositoryLanguageEntity>().HasOne<RepositoryEntity>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<FeedImpressionEntity>().HasOne<RepositoryEntity>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RankingDecisionEntity>().HasOne<RepositoryEntity>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RankingDecisionEntity>().HasOne<RankingBatchEntity>().WithMany().HasForeignKey(x => x.RankingBatchId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LocalContributionDayEntity>().HasOne<LocalRepositoryEntity>().WithMany().HasForeignKey(x => x.LocalRepositoryId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TilePlacementEntity>().HasOne(x => x.Board).WithMany(x => x.Placements).HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TilePlacementEntity>().HasOne(x => x.Repository).WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SemanticIndexPlacementEntity>().HasOne(x => x.Board).WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
    }

}
