using RepoGalaxy.Core.Models;
using RepoGalaxy.Recommendation.Engine;

namespace RepoGalaxy.Recommendation.Filters;

/// <summary>
/// 内容过滤器
/// </summary>
public class ContentFilter
{
    public IEnumerable<Repository> Apply(IEnumerable<Repository> candidates, UserProfile profile)
    {
        return candidates.Where(repo => PassesFilter(repo, profile));
    }
    
    private bool PassesFilter(Repository repo, UserProfile profile)
    {
        // 1. 过滤已忽略的
        if (repo.IsIgnored)
            return false;
        
        // 2. 过滤已归档的 (可选)
        if (repo.IsArchived)
            return false;
        
        // 3. Star 数范围过滤
        if (repo.Stars < profile.MinStars || repo.Stars > profile.MaxStars)
            return false;
        
        // 4. 更新时间过滤 (太旧的不推荐)
        var daysSinceUpdate = (DateTimeOffset.Now - repo.UpdatedAt).TotalDays;
        if (daysSinceUpdate > 365) // 一年未更新
            return false;
        
        // 5. 语言偏好过滤
        if (profile.InterestedLanguages.Any() && 
            !profile.InterestedLanguages.Contains(repo.PrimaryLanguage, StringComparer.OrdinalIgnoreCase))
        {
            // 如果没有匹配的兴趣语言，但其他方面很好，也可以保留
            // 这里简化为必须匹配
            return false;
        }
        
        // 6. 去重过滤 (已在书签中的降低权重但不完全过滤)
        // 在评分阶段处理
        
        return true;
    }
}
