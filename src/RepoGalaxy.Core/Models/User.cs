namespace RepoGalaxy.Core.Models;

/// <summary>
/// 用户实体
/// </summary>
public class User
{
    public long Id { get; set; }
    public string GitHubId { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Blog { get; set; } = string.Empty;
    public string TwitterUsername { get; set; } = string.Empty;
    
    // GitHub 统计
    public int PublicRepos { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    
    // OAuth Token (实际存储在 ISecureStorage 中，此处为内存缓存)
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }  // 用于 Token 刷新
    public DateTimeOffset? TokenExpiresAt { get; set; }
    
    // 本地状态
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
    
    /// <summary>
    /// 检查 Token 是否即将过期（5分钟缓冲）
    /// </summary>
    public bool IsTokenExpiringSoon(TimeSpan? buffer = null)
    {
        if (!TokenExpiresAt.HasValue) return false;
        var bufferTime = buffer ?? TimeSpan.FromMinutes(5);
        return DateTimeOffset.Now.Add(bufferTime) >= TokenExpiresAt.Value;
    }
}

/// <summary>
/// 用户偏好配置
/// </summary>
public class UserPreference
{
    public long Id { get; set; }
    public long UserId { get; set; }
    
    // 兴趣标签
    public List<string> InterestedTopics { get; set; } = new();
    public List<string> InterestedLanguages { get; set; } = new();
    
    // 过滤设置
    public int MinStarsThreshold { get; set; } = 0;
    public int MaxStarsThreshold { get; set; } = 1000000;
    public List<string> IgnoredTopics { get; set; } = new();
    
    // 推荐偏好
    public bool PreferFreshContent { get; set; } = true;
    public bool IncludeTrending { get; set; } = true;
    public bool PreferSmallProjects { get; set; } = false;
    
    // UI 偏好
    public bool DarkMode { get; set; } = true;
    public bool UseSystemTheme { get; set; } = true;
    public int FeedPageSize { get; set; } = 50;
    public int SyncIntervalMinutes { get; set; } = 30;
    public double NotificationThreshold { get; set; } = .75;
    
    // 缓存设置
    public int MaxCacheSizeGB { get; set; } = 2;
    public bool AutoCleanCache { get; set; } = true;
    public int MemoryCacheSizeMB { get; set; } = 256;
    public int PersistentCacheSizeMB { get; set; } = 1024;
    public int FeedCacheTtlMinutes { get; set; } = 30;
    public int DetailCacheTtlMinutes { get; set; } = 360;
    public int NewsCacheTtlMinutes { get; set; } = 30;
    public string CachePreset { get; set; } = "均衡";
}
