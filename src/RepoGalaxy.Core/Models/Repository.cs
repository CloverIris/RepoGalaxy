namespace RepoGalaxy.Core.Models;

/// <summary>
/// 仓库实体 - Feed 流中的"陨石"
/// </summary>
public class Repository
{
    // GitHub 基础信息
    public long Id { get; set; }
    public string GitHubId { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName => $"{Owner}/{Name}";
    public string HtmlUrl { get; set; } = string.Empty;
    
    // 内容信息
    public string Description { get; set; } = string.Empty;
    public string PrimaryLanguage { get; set; } = string.Empty;
    public List<string> Topics { get; set; } = new();
    public string ReadmeContent { get; set; } = string.Empty;
    public string Homepage { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public bool IsArchived { get; set; }
    
    // 社交指标
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int Watchers { get; set; }
    public int OpenIssues { get; set; }
    
    // 活跃度
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastPushedAt { get; set; }
    
    // RepoGalaxy 特有属性
    public double DiscoveryScore { get; set; }
    public MeteoriteSize Size { get; set; }
    public OrbitCategory Orbit { get; set; }
    
    // 本地状态
    public bool IsBookmarked { get; set; }
    public bool IsIgnored { get; set; }
    public DateTimeOffset? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
    
    // 缓存时间戳
    public DateTimeOffset CachedAt { get; set; }
    
    // 语言分布
    public List<LanguageInfo> Languages { get; set; } = new();
    
    /// <summary>
    /// 计算陨石大小分类
    /// </summary>
    public void CalculateSize()
    {
        Size = Stars switch
        {
            < 10 => MeteoriteSize.Dust,
            < 100 => MeteoriteSize.Pebble,
            < 1000 => MeteoriteSize.Rock,
            < 10000 => MeteoriteSize.Boulder,
            < 100000 => MeteoriteSize.Asteroid,
            _ => MeteoriteSize.Moon
        };
    }
    
    /// <summary>
    /// 计算发现价值评分
    /// </summary>
    public double CalculateDiscoveryScore()
    {
        var starScore = Stars switch
        {
            < 50 => 1.0,
            < 500 => 0.9,
            < 2000 => 0.7,
            < 10000 => 0.5,
            _ => 0.3
        };
        
        var daysSinceUpdate = (DateTimeOffset.Now - UpdatedAt).TotalDays;
        var freshnessScore = daysSinceUpdate switch
        {
            < 7 => 1.0,
            < 30 => 0.9,
            < 90 => 0.7,
            < 180 => 0.5,
            _ => 0.3
        };
        
        var forkRatio = Stars > 0 ? (double)Forks / Stars : 0;
        var forkScore = forkRatio switch
        {
            > 0.05 and < 0.5 => 1.0,
            > 0.02 and < 0.05 => 0.8,
            _ => 0.6
        };
        
        DiscoveryScore = (starScore * 0.4 + freshnessScore * 0.4 + forkScore * 0.2);
        return DiscoveryScore;
    }
}

public class LanguageInfo
{
    public string Name { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public long Bytes { get; set; }
}

public enum MeteoriteSize
{
    Dust = 0, Pebble = 1, Rock = 2, Boulder = 3, Asteroid = 4, Moon = 5
}

public enum OrbitCategory
{
    Core = 0, Web = 1, Mobile = 2, AI = 3, Data = 4, DevOps = 5, Design = 6, Learning = 7, Experimental = 8
}

/// <summary>
/// 本地Git仓库模型
/// </summary>
public class LocalRepository
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string? GitHubUrl { get; set; }
    public bool IsTracked { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}
