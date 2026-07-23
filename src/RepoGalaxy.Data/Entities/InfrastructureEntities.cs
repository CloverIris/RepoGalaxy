using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RepoGalaxy.Data.Entities;

[Table("ApiCacheEntries")]
public sealed class ApiCacheEntryEntity
{
    public const int CurrentSchema = 1;
    [Key, MaxLength(160)] public string Key { get; set; } = string.Empty;
    public int Schema { get; set; } = CurrentSchema;
    [Required] public byte[] Payload { get; set; } = [];
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public string Tags { get; set; } = string.Empty;
    public DateTimeOffset FetchedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset StaleUntil { get; set; }
    public DateTimeOffset LastAccessedAt { get; set; }
    public long SizeBytes { get; set; }
}

[Table("SyncRuns")]
public sealed class SyncRunEntity
{
    [Key] public long Id { get; set; }
    [Required] public string CorrelationId { get; set; } = string.Empty;
    [Required] public string JobType { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string State { get; set; } = "Queued";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorCode { get; set; }
}

[Table("SyncCheckpoints")]
public sealed class SyncCheckpointEntity
{
    [Key] public long Id { get; set; }
    [Required] public string AccountId { get; set; } = string.Empty;
    [Required] public string JobType { get; set; } = string.Empty;
    public string ScopeKey { get; set; } = string.Empty;
    public string? NextPageUrl { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ResumeAfter { get; set; }
    public string? LastErrorCode { get; set; }
}

[Table("UserRepositoryRelations")]
public sealed class UserRepositoryRelationEntity
{
    [Key] public long Id { get; set; }
    public long RepositoryId { get; set; }
    [Required] public string AccountId { get; set; } = string.Empty;
    [Required] public string Relation { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public DateTimeOffset? RelatedAt { get; set; }
    [ForeignKey(nameof(RepositoryId))] public RepositoryEntity Repository { get; set; } = null!;
}

[Table("RepositoryMetricSnapshots")]
public sealed class RepositoryMetricSnapshotEntity
{
    [Key] public long Id { get; set; }
    public long RepositoryId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public int Stars { get; set; }
    public int Forks { get; set; }
    public DateTimeOffset RepositoryUpdatedAt { get; set; }
}

[Table("RankingBatches")]
public sealed class RankingBatchEntity
{
    [Key] public long Id { get; set; }
    [Required] public string BatchId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Source { get; set; } = "ForYou";
    public string AlgorithmVersion { get; set; } = "heuristic-v1";
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDirty { get; set; }
    public int ProfileRevision { get; set; } = 1;
}

[Table("RankingTuningProfiles")]
public sealed class RankingTuningProfileEntity
{
    [Key] public long Id { get; set; }
    [Required, MaxLength(120)] public string ScopeKey { get; set; } = "guest";
    public int Preset { get; set; }
    public double CoarseRuleMatch { get; set; } = .30;
    public double CoarseFreshness { get; set; } = .20;
    public double CoarseStarVelocity { get; set; } = .20;
    public double CoarseQuality { get; set; } = .15;
    public double CoarsePreference { get; set; } = .15;
    public double FineCoarse { get; set; } = .45;
    public double FineContentProfile { get; set; } = .20;
    public double FineBehavior { get; set; } = .15;
    public double FineNovelty { get; set; } = .10;
    public double FineLocalRelevance { get; set; } = .10;
    public double ExplorationRatio { get; set; } = .15;
    public double Temperature { get; set; } = 1;
    public double FreshnessHalfLifeDays { get; set; } = 120;
    public int SameLanguagePerTen { get; set; } = 3;
    public int SameOwnerPerTen { get; set; } = 1;
    public int CoarseCandidateCount { get; set; } = 200;
    public int FineResultCount { get; set; } = 60;
    public int Revision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; }
}

[Table("RankingDecisions")]
public sealed class RankingDecisionEntity
{
    [Key] public long Id { get; set; }
    public long RankingBatchId { get; set; }
    public long RepositoryId { get; set; }
    public double CoarseScore { get; set; }
    public double FineScore { get; set; }
    public int Position { get; set; }
    public bool IsExploration { get; set; }
    public string FeaturesJson { get; set; } = "{}";
    public string Explanation { get; set; } = string.Empty;
}

[Table("FeedImpressions")]
public sealed class FeedImpressionEntity
{
    [Key] public long Id { get; set; }
    public long RepositoryId { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public string Action { get; set; } = "View";
    public int Position { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

[Table("RepositoryTopics")]
public sealed class RepositoryTopicEntity { [Key] public long Id { get; set; } public long RepositoryId { get; set; } [Required] public string Topic { get; set; } = string.Empty; }
[Table("RepositoryLanguages")]
public sealed class RepositoryLanguageEntity { [Key] public long Id { get; set; } public long RepositoryId { get; set; } [Required] public string Language { get; set; } = string.Empty; public double Percentage { get; set; } public long Bytes { get; set; } }

[Table("LocalContributionDays")]
public sealed class LocalContributionDayEntity { [Key] public long Id { get; set; } public long LocalRepositoryId { get; set; } public DateOnly Date { get; set; } public int CommitCount { get; set; } }
[Table("GitIdentityAliases")]
public sealed class GitIdentityAliasEntity { [Key] public long Id { get; set; } public string? Name { get; set; } public string? Email { get; set; } public bool IsEnabled { get; set; } = true; }
[Table("NewsItems")]
public sealed class NewsItemEntity { [Key] public long Id { get; set; } [Required] public string ExternalId { get; set; } = string.Empty; [Required] public string Title { get; set; } = string.Empty; public string Summary { get; set; } = string.Empty; [Required] public string Url { get; set; } = string.Empty; public string Source { get; set; } = string.Empty; public DateTimeOffset PublishedAt { get; set; } public DateTimeOffset FetchedAt { get; set; } }
[Table("AuthenticationAuditEvents")]
public sealed class AuthenticationAuditEventEntity { [Key] public long Id { get; set; } public string CorrelationId { get; set; } = string.Empty; public string EventType { get; set; } = string.Empty; public string Outcome { get; set; } = string.Empty; public string? AccountId { get; set; } public string? OriginPath { get; set; } public string? ErrorCode { get; set; } public DateTimeOffset OccurredAt { get; set; } }

[Table("TileBoards")]
public sealed class TileBoardEntity
{
    [Key] public long Id { get; set; }
    [Required, MaxLength(120)] public string ScopeKey { get; set; } = "guest";
    public int Source { get; set; }
    public double CameraX { get; set; }
    public double CameraY { get; set; }
    public double Zoom { get; set; } = 1;
    public int? ActiveIndexKind { get; set; }
    [MaxLength(160)] public string ActiveIndexKey { get; set; } = string.Empty;
    public double SemanticViewportX { get; set; } = 24;
    public double SemanticViewportY { get; set; } = 24;
    public double SemanticViewportWidth { get; set; }
    public double SemanticViewportHeight { get; set; }
    public bool SemanticViewportUserPositioned { get; set; }
    [MaxLength(64)] public string WorldSeed { get; set; } = string.Empty;
    public int ExtentColumns { get; set; } = 12;
    public int ExtentRows { get; set; } = 6;
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<TilePlacementEntity> Placements { get; set; } = new List<TilePlacementEntity>();
}

[Table("TilePlacements")]
public sealed class TilePlacementEntity
{
    [Key] public long Id { get; set; }
    public long BoardId { get; set; }
    [Required, MaxLength(48)] public string ContentKind { get; set; } = "Tip";
    [Required, MaxLength(240)] public string ContentKey { get; set; } = string.Empty;
    public long? RepositoryId { get; set; }
    public int Column { get; set; }
    public int Row { get; set; }
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public string AccentKey { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public bool IsPlaceholder { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    [ForeignKey(nameof(BoardId))] public TileBoardEntity Board { get; set; } = null!;
    [ForeignKey(nameof(RepositoryId))] public RepositoryEntity? Repository { get; set; }
}

[Table("SemanticIndexPlacements")]
public sealed class SemanticIndexPlacementEntity
{
    [Key] public long Id { get; set; }
    public long BoardId { get; set; }
    [Required, MaxLength(240)] public string ItemKey { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string Title { get; set; } = string.Empty;
    public int Kind { get; set; }
    public int ProjectCount { get; set; }
    [MaxLength(80)] public string AccentKey { get; set; } = string.Empty;
    public string ContentKeysJson { get; set; } = "[]";
    public int Column { get; set; }
    public int Row { get; set; }
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; }
    [ForeignKey(nameof(BoardId))] public TileBoardEntity Board { get; set; } = null!;
}

[Table("CloneOperations")]
public sealed class CloneOperationEntity
{
    [Key, MaxLength(40)] public string Id { get; set; } = string.Empty;
    public long RepositoryId { get; set; }
    [Required, MaxLength(200)] public string RepositoryFullName { get; set; } = string.Empty;
    [Required] public string ParentDirectory { get; set; } = string.Empty;
    [Required] public string StagingDirectory { get; set; } = string.Empty;
    [Required] public string DestinationDirectory { get; set; } = string.Empty;
    public int Mode { get; set; }
    public int State { get; set; }
    [MaxLength(80)] public string ErrorCode { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

[Table("IdePreferences")]
public sealed class IdePreferenceEntity
{
    [Key] public long Id { get; set; }
    [Required, MaxLength(120)] public string ScopeKey { get; set; } = "guest";
    [Required, MaxLength(120)] public string TechnologyKey { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string IdeKey { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
