namespace RepoGalaxy.Core.Models;

/// <summary>GitHub repository data used by discovery, library, and workspace views.</summary>
public class Repository
{
    public long Id { get; set; }
    public string GitHubId { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName => $"{Owner}/{Name}";
    public string HtmlUrl { get; set; } = string.Empty;
    public string OwnerAvatarUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PrimaryLanguage { get; set; } = string.Empty;
    public List<string> Topics { get; set; } = new();
    public string ReadmeContent { get; set; } = string.Empty;
    public string Homepage { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public bool IsArchived { get; set; }
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int Watchers { get; set; }
    public int OpenIssues { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastPushedAt { get; set; }
    public double DiscoveryScore { get; set; }
    public bool IsBookmarked { get; set; }
    public bool IsIgnored { get; set; }
    public DateTimeOffset? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
    public DateTimeOffset CachedAt { get; set; }
    public List<LanguageInfo> Languages { get; set; } = new();

    public double CalculateDiscoveryScore()
    {
        var popularity = Stars switch { < 50 => 1.0, < 500 => 0.9, < 2000 => 0.7, < 10000 => 0.5, _ => 0.3 };
        var freshness = (DateTimeOffset.UtcNow - UpdatedAt).TotalDays switch { < 7 => 1.0, < 30 => 0.9, < 90 => 0.7, < 180 => 0.5, _ => 0.3 };
        var adoption = Stars > 0 && (double)Forks / Stars is > 0.05 and < 0.5 ? 1.0 : 0.6;
        return DiscoveryScore = popularity * 0.4 + freshness * 0.4 + adoption * 0.2;
    }
}

public class LanguageInfo
{
    public string Name { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public long Bytes { get; set; }
}

public class LocalRepository
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string? GitHubUrl { get; set; }
    public bool IsTracked { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}
