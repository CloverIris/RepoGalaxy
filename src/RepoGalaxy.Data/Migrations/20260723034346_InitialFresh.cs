using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoGalaxy.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialFresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCacheEntries",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Schema = table.Column<int>(type: "INTEGER", nullable: false),
                    Payload = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ETag = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
                    StaleUntil = table.Column<long>(type: "INTEGER", nullable: false),
                    LastAccessedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCacheEntries", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AuthenticationAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CorrelationId = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", nullable: true),
                    OriginPath = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    OccurredAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CloneOperations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    RepositoryFullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ParentDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    StagingDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    DestinationDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloneOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscoverySubscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    TopicsJson = table.Column<string>(type: "TEXT", nullable: false),
                    LanguagesJson = table.Column<string>(type: "TEXT", nullable: false),
                    KeywordsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationThreshold = table.Column<double>(type: "REAL", nullable: false),
                    LastSyncedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoverySubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitIdentityAliases",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitIdentityAliases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdePreferences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TechnologyKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IdeKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdePreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalRepositories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", nullable: false),
                    GitHubUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IsTracked = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalRepositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    FetchedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RankingBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchId = table.Column<string>(type: "TEXT", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDirty = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileRevision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RankingTuningProfiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Preset = table.Column<int>(type: "INTEGER", nullable: false),
                    CoarseRuleMatch = table.Column<double>(type: "REAL", nullable: false),
                    CoarseFreshness = table.Column<double>(type: "REAL", nullable: false),
                    CoarseStarVelocity = table.Column<double>(type: "REAL", nullable: false),
                    CoarseQuality = table.Column<double>(type: "REAL", nullable: false),
                    CoarsePreference = table.Column<double>(type: "REAL", nullable: false),
                    FineCoarse = table.Column<double>(type: "REAL", nullable: false),
                    FineContentProfile = table.Column<double>(type: "REAL", nullable: false),
                    FineBehavior = table.Column<double>(type: "REAL", nullable: false),
                    FineNovelty = table.Column<double>(type: "REAL", nullable: false),
                    FineLocalRelevance = table.Column<double>(type: "REAL", nullable: false),
                    ExplorationRatio = table.Column<double>(type: "REAL", nullable: false),
                    Temperature = table.Column<double>(type: "REAL", nullable: false),
                    FreshnessHalfLifeDays = table.Column<double>(type: "REAL", nullable: false),
                    SameLanguagePerTen = table.Column<int>(type: "INTEGER", nullable: false),
                    SameOwnerPerTen = table.Column<int>(type: "INTEGER", nullable: false),
                    CoarseCandidateCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FineResultCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingTuningProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    ReleaseId = table.Column<long>(type: "INTEGER", nullable: false),
                    PublishedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GitHubId = table.Column<string>(type: "TEXT", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    PrimaryLanguage = table.Column<string>(type: "TEXT", nullable: true),
                    TopicsJson = table.Column<string>(type: "TEXT", nullable: true),
                    HtmlUrl = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerAvatarUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false),
                    Forks = table.Column<int>(type: "INTEGER", nullable: false),
                    Watchers = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenIssues = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastPushedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DiscoveryScore = table.Column<double>(type: "REAL", nullable: false),
                    IsBookmarked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsIgnored = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastViewedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ViewCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CachedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LanguagesJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncCheckpoints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", nullable: false),
                    ScopeKey = table.Column<string>(type: "TEXT", nullable: false),
                    NextPageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ResumeAfter = table.Column<long>(type: "INTEGER", nullable: true),
                    LastErrorCode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CorrelationId = table.Column<string>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TileBoards",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    CameraX = table.Column<double>(type: "REAL", nullable: false),
                    CameraY = table.Column<double>(type: "REAL", nullable: false),
                    Zoom = table.Column<double>(type: "REAL", nullable: false),
                    ActiveIndexKind = table.Column<int>(type: "INTEGER", nullable: true),
                    ActiveIndexKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SemanticViewportX = table.Column<double>(type: "REAL", nullable: false),
                    SemanticViewportY = table.Column<double>(type: "REAL", nullable: false),
                    SemanticViewportWidth = table.Column<double>(type: "REAL", nullable: false),
                    SemanticViewportHeight = table.Column<double>(type: "REAL", nullable: false),
                    SemanticViewportUserPositioned = table.Column<bool>(type: "INTEGER", nullable: false),
                    WorldSeed = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExtentColumns = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtentRows = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TileBoards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    InterestedTopicsJson = table.Column<string>(type: "TEXT", nullable: true),
                    InterestedLanguagesJson = table.Column<string>(type: "TEXT", nullable: true),
                    MinStarsThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxStarsThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    IgnoredTopicsJson = table.Column<string>(type: "TEXT", nullable: true),
                    PreferFreshContent = table.Column<bool>(type: "INTEGER", nullable: false),
                    IncludeTrending = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferSmallProjects = table.Column<bool>(type: "INTEGER", nullable: false),
                    DarkMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    FeedPageSize = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxCacheSizeGB = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoCleanCache = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseSystemTheme = table.Column<bool>(type: "INTEGER", nullable: true),
                    SyncIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationThreshold = table.Column<double>(type: "REAL", nullable: false),
                    MemoryCacheSizeMB = table.Column<int>(type: "INTEGER", nullable: false),
                    PersistentCacheSizeMB = table.Column<int>(type: "INTEGER", nullable: false),
                    FeedCacheTtlMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    DetailCacheTtlMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    NewsCacheTtlMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CachePreset = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GitHubId = table.Column<string>(type: "TEXT", nullable: false),
                    Login = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    AvatarUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Bio = table.Column<string>(type: "TEXT", nullable: true),
                    Company = table.Column<string>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", nullable: true),
                    Blog = table.Column<string>(type: "TEXT", nullable: true),
                    TwitterUsername = table.Column<string>(type: "TEXT", nullable: true),
                    ProfileUrl = table.Column<string>(type: "TEXT", nullable: true),
                    PublicRepos = table.Column<int>(type: "INTEGER", nullable: false),
                    Followers = table.Column<int>(type: "INTEGER", nullable: false),
                    Following = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastLoginAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalContributionDays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LocalRepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CommitCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalContributionDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalContributionDays_LocalRepositories_LocalRepositoryId",
                        column: x => x.LocalRepositoryId,
                        principalTable: "LocalRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookmarks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    BookmarkedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CollectionName = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookmarks_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedImpressions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    BatchId = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedImpressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedImpressions_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    MatchedRule = table.Column<string>(type: "TEXT", nullable: true),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    CoarseScore = table.Column<double>(type: "REAL", nullable: false),
                    FineScore = table.Column<double>(type: "REAL", nullable: false),
                    BatchId = table.Column<string>(type: "TEXT", nullable: false),
                    IsExploration = table.Column<bool>(type: "INTEGER", nullable: false),
                    DiscoveredAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotificationDelivered = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedItems_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RankingDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RankingBatchId = table.Column<long>(type: "INTEGER", nullable: false),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    CoarseScore = table.Column<double>(type: "REAL", nullable: false),
                    FineScore = table.Column<double>(type: "REAL", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    IsExploration = table.Column<bool>(type: "INTEGER", nullable: false),
                    FeaturesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RankingDecisions_RankingBatches_RankingBatchId",
                        column: x => x.RankingBatchId,
                        principalTable: "RankingBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RankingDecisions_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryLanguages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    Percentage = table.Column<double>(type: "REAL", nullable: false),
                    Bytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryLanguages_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryMetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false),
                    Forks = table.Column<int>(type: "INTEGER", nullable: false),
                    RepositoryUpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryMetricSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryMetricSnapshots_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryTopics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    Topic = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryTopics_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRepositoryRelations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<string>(type: "TEXT", nullable: false),
                    Relation = table.Column<string>(type: "TEXT", nullable: false),
                    IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false),
                    RelatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRepositoryRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRepositoryRelations_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ViewHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: false),
                    ViewedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    ReferrerTopic = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViewHistories_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SemanticIndexPlacements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<long>(type: "INTEGER", nullable: false),
                    ItemKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AccentKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ContentKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    Column = table.Column<int>(type: "INTEGER", nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnSpan = table.Column<int>(type: "INTEGER", nullable: false),
                    RowSpan = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemanticIndexPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SemanticIndexPlacements_TileBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "TileBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TilePlacements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentKind = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    ContentKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    RepositoryId = table.Column<long>(type: "INTEGER", nullable: true),
                    Column = table.Column<int>(type: "INTEGER", nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnSpan = table.Column<int>(type: "INTEGER", nullable: false),
                    RowSpan = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Subtitle = table.Column<string>(type: "TEXT", nullable: false),
                    Caption = table.Column<string>(type: "TEXT", nullable: false),
                    AccentKey = table.Column<string>(type: "TEXT", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    IsPlaceholder = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TilePlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TilePlacements_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TilePlacements_TileBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "TileBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BookmarkTags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookmarkId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookmarkTags_Bookmarks_BookmarkId",
                        column: x => x.BookmarkId,
                        principalTable: "Bookmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCacheEntries_LastAccessedAt",
                table: "ApiCacheEntries",
                column: "LastAccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ApiCacheEntries_StaleUntil",
                table: "ApiCacheEntries",
                column: "StaleUntil");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationAuditEvents_OccurredAt",
                table: "AuthenticationAuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_RepositoryId",
                table: "Bookmarks",
                column: "RepositoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookmarkTags_BookmarkId_Name",
                table: "BookmarkTags",
                columns: new[] { "BookmarkId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CloneOperations_UpdatedAt",
                table: "CloneOperations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoverySubscriptions_Name",
                table: "DiscoverySubscriptions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedImpressions_RepositoryId",
                table: "FeedImpressions",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_IsRead_DiscoveredAt",
                table: "FeedItems",
                columns: new[] { "IsRead", "DiscoveredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_RepositoryId_Source_BatchId",
                table: "FeedItems",
                columns: new[] { "RepositoryId", "Source", "BatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdePreferences_ScopeKey_TechnologyKey",
                table: "IdePreferences",
                columns: new[] { "ScopeKey", "TechnologyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalContributionDays_LocalRepositoryId_Date",
                table: "LocalContributionDays",
                columns: new[] { "LocalRepositoryId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalRepositories_LocalPath",
                table: "LocalRepositories",
                column: "LocalPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsItems_ExternalId",
                table: "NewsItems",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RankingBatches_BatchId",
                table: "RankingBatches",
                column: "BatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RankingDecisions_RankingBatchId_RepositoryId",
                table: "RankingDecisions",
                columns: new[] { "RankingBatchId", "RepositoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RankingDecisions_RepositoryId",
                table: "RankingDecisions",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RankingTuningProfiles_ScopeKey",
                table: "RankingTuningProfiles",
                column: "ScopeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNotifications_RepositoryId_ReleaseId",
                table: "ReleaseNotifications",
                columns: new[] { "RepositoryId", "ReleaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_CachedAt",
                table: "Repositories",
                column: "CachedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Owner_Name",
                table: "Repositories",
                columns: new[] { "Owner", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryLanguages_RepositoryId_Language",
                table: "RepositoryLanguages",
                columns: new[] { "RepositoryId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryMetricSnapshots_RepositoryId_SnapshotDate",
                table: "RepositoryMetricSnapshots",
                columns: new[] { "RepositoryId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryTopics_RepositoryId_Topic",
                table: "RepositoryTopics",
                columns: new[] { "RepositoryId", "Topic" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SemanticIndexPlacements_BoardId_ItemKey",
                table: "SemanticIndexPlacements",
                columns: new[] { "BoardId", "ItemKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncCheckpoints_AccountId_JobType_ScopeKey",
                table: "SyncCheckpoints",
                columns: new[] { "AccountId", "JobType", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncRuns_CorrelationId",
                table: "SyncRuns",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TileBoards_ScopeKey_Source",
                table: "TileBoards",
                columns: new[] { "ScopeKey", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TilePlacements_BoardId_ContentKind_ContentKey",
                table: "TilePlacements",
                columns: new[] { "BoardId", "ContentKind", "ContentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TilePlacements_RepositoryId",
                table: "TilePlacements",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRepositoryRelations_AccountId_RepositoryId_Relation",
                table: "UserRepositoryRelations",
                columns: new[] { "AccountId", "RepositoryId", "Relation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRepositoryRelations_RepositoryId",
                table: "UserRepositoryRelations",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Login",
                table: "Users",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViewHistories_RepositoryId",
                table: "ViewHistories",
                column: "RepositoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCacheEntries");

            migrationBuilder.DropTable(
                name: "AuthenticationAuditEvents");

            migrationBuilder.DropTable(
                name: "BookmarkTags");

            migrationBuilder.DropTable(
                name: "CloneOperations");

            migrationBuilder.DropTable(
                name: "DiscoverySubscriptions");

            migrationBuilder.DropTable(
                name: "FeedImpressions");

            migrationBuilder.DropTable(
                name: "FeedItems");

            migrationBuilder.DropTable(
                name: "GitIdentityAliases");

            migrationBuilder.DropTable(
                name: "IdePreferences");

            migrationBuilder.DropTable(
                name: "LocalContributionDays");

            migrationBuilder.DropTable(
                name: "NewsItems");

            migrationBuilder.DropTable(
                name: "RankingDecisions");

            migrationBuilder.DropTable(
                name: "RankingTuningProfiles");

            migrationBuilder.DropTable(
                name: "ReleaseNotifications");

            migrationBuilder.DropTable(
                name: "RepositoryLanguages");

            migrationBuilder.DropTable(
                name: "RepositoryMetricSnapshots");

            migrationBuilder.DropTable(
                name: "RepositoryTopics");

            migrationBuilder.DropTable(
                name: "SemanticIndexPlacements");

            migrationBuilder.DropTable(
                name: "SyncCheckpoints");

            migrationBuilder.DropTable(
                name: "SyncRuns");

            migrationBuilder.DropTable(
                name: "TilePlacements");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "UserRepositoryRelations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ViewHistories");

            migrationBuilder.DropTable(
                name: "Bookmarks");

            migrationBuilder.DropTable(
                name: "LocalRepositories");

            migrationBuilder.DropTable(
                name: "RankingBatches");

            migrationBuilder.DropTable(
                name: "TileBoards");

            migrationBuilder.DropTable(
                name: "Repositories");
        }
    }
}
