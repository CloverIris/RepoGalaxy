using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RepoGalaxy.Data.Entities;

[Table("Repositories")]
public class RepositoryEntity
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    public string GitHubId { get; set; } = string.Empty;
    
    [Required]
    public string Owner { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    public string? PrimaryLanguage { get; set; }
    public string? TopicsJson { get; set; }
    public string? HtmlUrl { get; set; }
    
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int Watchers { get; set; }
    public int OpenIssues { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastPushedAt { get; set; }
    
    public int OrbitCategoryId { get; set; }
    public double DiscoveryScore { get; set; }
    
    // 本地状态
    public bool IsBookmarked { get; set; }
    public bool IsIgnored { get; set; }
    public DateTimeOffset? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
    
    // 缓存时间戳
    public DateTimeOffset CachedAt { get; set; }
    
    // 语言分布 JSON
    public string? LanguagesJson { get; set; }
    
    // 导航属性
    public ICollection<BookmarkEntity> Bookmarks { get; set; } = new List<BookmarkEntity>();
    public ICollection<ViewHistoryEntity> ViewHistories { get; set; } = new List<ViewHistoryEntity>();
}

[Table("Bookmarks")]
public class BookmarkEntity
{
    [Key]
    public long Id { get; set; }
    
    public long RepositoryId { get; set; }
    public DateTimeOffset BookmarkedAt { get; set; }
    public string CollectionName { get; set; } = "默认收藏";
    public string? Notes { get; set; }
    public int Priority { get; set; }
    
    [ForeignKey(nameof(RepositoryId))]
    public RepositoryEntity Repository { get; set; } = null!;
}

[Table("ViewHistories")]
public class ViewHistoryEntity
{
    [Key]
    public long Id { get; set; }
    
    public long RepositoryId { get; set; }
    public DateTimeOffset ViewedAt { get; set; }
    public long DurationSeconds { get; set; }
    public int Source { get; set; }
    public string? ReferrerTopic { get; set; }
    
    [ForeignKey(nameof(RepositoryId))]
    public RepositoryEntity Repository { get; set; } = null!;
}

[Table("Users")]
public class UserEntity
{
    [Key]
    public long Id { get; set; }
    
    public string GitHubId { get; set; } = string.Empty;
    
    [Required]
    public string Login { get; set; } = string.Empty;
    
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public int PublicRepos { get; set; }
    public int Followers { get; set; }
    
    // Token 已移至 SecureStorage，不再明文存储
    // 之前的安全漏洞：AccessToken 明文存储在数据库中
    
    public DateTimeOffset? LastLoginAt { get; set; }
}

[Table("LocalRepositories")]
public class LocalRepositoryEntity
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string LocalPath { get; set; } = string.Empty;
    
    // 关联的GitHub仓库URL（可选）
    public string? GitHubUrl { get; set; }
    
    // 是否已跟踪（与数据库中的GitHub仓库关联）
    public bool IsTracked { get; set; }
    
    // 添加时间
    public DateTimeOffset AddedAt { get; set; }
}

/// <summary>
/// 用户偏好设置实体
/// </summary>
[Table("UserPreferences")]
public class UserPreferenceEntity
{
    [Key]
    public long Id { get; set; }
    
    // 关联用户（单用户应用，通常只有一条记录）
    public long UserId { get; set; }
    
    // 兴趣标签 - JSON 存储
    public string? InterestedTopicsJson { get; set; }
    
    // 兴趣语言 - JSON 存储
    public string? InterestedLanguagesJson { get; set; }
    
    // 过滤设置
    public int MinStarsThreshold { get; set; } = 0;
    public int MaxStarsThreshold { get; set; } = 1000000;
    
    // 忽略的话题 - JSON 存储
    public string? IgnoredTopicsJson { get; set; }
    
    // 推荐偏好
    public bool PreferFreshContent { get; set; } = true;
    public bool IncludeTrending { get; set; } = true;
    public bool PreferSmallProjects { get; set; } = false;
    
    // UI 偏好
    public bool DarkMode { get; set; } = true;
    public int FeedPageSize { get; set; } = 50;
    
    // 缓存设置
    public int MaxCacheSizeGB { get; set; } = 2;
    public bool AutoCleanCache { get; set; } = true;
    
    // 推荐算法权重（高级设置）
    public double DiscoveryScoreWeight { get; set; } = 0.25;
    public double ActivityScoreWeight { get; set; } = 0.20;
    public double InterestMatchWeight { get; set; } = 0.25;
    public double QualityScoreWeight { get; set; } = 0.15;
    public double PreferenceWeight { get; set; } = 0.15;
    
    // 最后更新时间
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.Now;
}
