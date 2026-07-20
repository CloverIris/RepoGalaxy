using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Core.Interfaces;

/// <summary>
/// 推荐引擎接口
/// </summary>
public interface IRecommendationEngine
{
    /// <summary>
    /// 获取个性化推荐
    /// </summary>
    Task<IEnumerable<Repository>> GetRecommendationsAsync(int count = 20);
    Task<IReadOnlyList<RankedRecommendation>> GetRankedRecommendationsAsync(int count = 60);
    
    /// <summary>
    /// 获取相似仓库
    /// </summary>
    Task<IEnumerable<Repository>> GetSimilarAsync(long repositoryId, int count = 10);
    
    /// <summary>
    /// 基于聚类的推荐
    /// </summary>
    Task<IEnumerable<Repository>> GetRelatedRecommendationsAsync(IEnumerable<long> seedIds, int count = 15);
    
    /// <summary>
    /// 更新用户画像
    /// </summary>
    Task UpdateUserProfileAsync();
    
    /// <summary>
    /// 记录反馈
    /// </summary>
    Task RecordFeedbackAsync(long repositoryId, FeedbackType type);
}

public enum FeedbackType
{
    View = 0,
    Click = 1,
    Bookmark = 2,
    Ignore = 3,
    Dismiss = 4
}
