using RepoGalaxy.Recommendation.Engine;
using RepoGalaxy.Recommendation.Features;

namespace RepoGalaxy.Recommendation.Scoring;

/// <summary>
/// 评分计算器
/// </summary>
public class ScoreCalculator
{
    /// <summary>
    /// 计算最终推荐分数 (0-100)
    /// </summary>
    public double Calculate(RepositoryFeatures features, UserProfile profile)
    {
        var score = 0.0;
        
        // 1. 发现价值分数 (25%)
        score += features.DiscoveryScore * 25;
        
        // 2. 活跃度分数 (20%)
        score += features.ActivityScore * 20;
        
        // 3. 用户兴趣匹配 (25%)
        var interestScore = 0.0;
        interestScore += features.MatchesInterestedLanguage * 10;
        interestScore += Math.Min(features.MatchingTopicsCount * 5, 15);
        score += interestScore;
        
        // 4. 质量分数 (15%)
        var qualityScore = 0.0;
        qualityScore += features.HasDescription * 3;
        qualityScore += features.HasHomepage * 2;
        qualityScore += Math.Min(features.TopicsCount, 5);
        score += qualityScore;
        
        // 5. 个性化偏好 (15%)
        var preferenceScore = 0.0;
        
        // 偏好小而美项目
        if (profile.PreferSmallProjects)
        {
            if (features.Stars < 1000)
                preferenceScore += 8;
            else if (features.Stars < 5000)
                preferenceScore += 4;
        }
        else
        {
            if (features.Stars >= 1000)
                preferenceScore += 5;
        }
        
        // 偏好新内容
        if (profile.PreferFreshContent && features.DaysSinceUpdate < 30)
        {
            preferenceScore += 7;
        }
        
        // 已查看降权
        if (features.IsViewed == 1)
            preferenceScore -= 5;
        
        // 已收藏加权
        if (features.IsBookmarked == 1)
            preferenceScore += 3;
        
        score += Math.Max(0, preferenceScore);
        
        return Math.Min(score, 100);
    }
}
