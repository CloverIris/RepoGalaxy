using RepoGalaxy.Core.Models;
using RepoGalaxy.Recommendation.Engine;

namespace RepoGalaxy.Recommendation.Features;

/// <summary>
/// 特征提取器
/// </summary>
public class FeatureExtractor
{
    public RepositoryFeatures Extract(Repository repo, UserProfile profile)
    {
        var features = new RepositoryFeatures
        {
            // 基础特征
            Stars = repo.Stars,
            Forks = repo.Forks,
            OpenIssues = repo.OpenIssues,
            
            // 时间特征
            DaysSinceUpdate = (DateTimeOffset.Now - repo.UpdatedAt).TotalDays,
            RepositoryAge = (DateTimeOffset.Now - repo.CreatedAt).TotalDays,
            
            // 质量特征
            HasDescription = !string.IsNullOrWhiteSpace(repo.Description) ? 1 : 0,
            HasHomepage = !string.IsNullOrWhiteSpace(repo.Homepage) ? 1 : 0,
            TopicsCount = repo.Topics.Count,
            LanguagesCount = repo.Languages.Count,
            
            // 用户匹配特征
            MatchesInterestedLanguage = profile.InterestedLanguages
                .Contains(repo.PrimaryLanguage, StringComparer.OrdinalIgnoreCase) ? 1 : 0,
            MatchingTopicsCount = repo.Topics
                .Intersect(profile.InterestedTopics, StringComparer.OrdinalIgnoreCase)
                .Count(),
            IsBookmarked = repo.IsBookmarked ? 1 : 0,
            IsViewed = profile.ViewedRepos.Any(v => v.Id == repo.Id) ? 1 : 0,
            
            // 发现价值
            DiscoveryScore = repo.DiscoveryScore,
            
            // 计算活跃度
            ActivityScore = CalculateActivityScore(repo)
        };
        
        return features;
    }
    
    private double CalculateActivityScore(Repository repo)
    {
        var daysSinceUpdate = (DateTimeOffset.Now - repo.UpdatedAt).TotalDays;
        
        // 更新频率分数
        var freshnessScore = daysSinceUpdate switch
        {
            < 7 => 1.0,
            < 30 => 0.8,
            < 90 => 0.6,
            < 180 => 0.4,
            _ => 0.2
        };
        
        // 社区健康分数 (Issue 处理率)
        var issueScore = repo.OpenIssues > 0 
            ? Math.Min(1.0, repo.Stars / (double)(repo.OpenIssues * 10)) 
            : 1.0;
        
        // Fork 活跃度
        var forkScore = repo.Stars > 0 
            ? Math.Min(1.0, repo.Forks / (double)repo.Stars * 3) 
            : 0;
        
        return (freshnessScore * 0.5 + issueScore * 0.3 + forkScore * 0.2);
    }
}

/// <summary>
/// 仓库特征
/// </summary>
public class RepositoryFeatures
{
    // 基础特征
    public int Stars { get; set; }
    public int Forks { get; set; }
    public int OpenIssues { get; set; }
    
    // 时间特征
    public double DaysSinceUpdate { get; set; }
    public double RepositoryAge { get; set; }
    
    // 质量特征
    public int HasDescription { get; set; }
    public int HasHomepage { get; set; }
    public int TopicsCount { get; set; }
    public int LanguagesCount { get; set; }
    
    // 用户匹配特征
    public int MatchesInterestedLanguage { get; set; }
    public int MatchingTopicsCount { get; set; }
    public int IsBookmarked { get; set; }
    public int IsViewed { get; set; }
    
    // 计算特征
    public double DiscoveryScore { get; set; }
    public double ActivityScore { get; set; }
}
