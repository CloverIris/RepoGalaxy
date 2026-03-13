namespace RepoGalaxy.Core.Models;

/// <summary>
/// 收藏夹
/// </summary>
public class Bookmark
{
    public long Id { get; set; }
    public long RepositoryId { get; set; }
    public long? UserId { get; set; }
    public DateTimeOffset BookmarkedAt { get; set; }
    public string CollectionName { get; set; } = "默认收藏";
    public string Notes { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;
    
    // 导航属性
    public Repository Repository { get; set; } = null!;
}

/// <summary>
/// 浏览历史
/// </summary>
public class ViewHistory
{
    public long Id { get; set; }
    public long RepositoryId { get; set; }
    public DateTimeOffset ViewedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public ViewSource Source { get; set; }
    public string ReferrerTopic { get; set; } = string.Empty;
    
    // 导航属性
    public Repository Repository { get; set; } = null!;
}

public enum ViewSource
{
    Feed = 0, Search = 1, Recommendation = 2, Bookmark = 3, External = 4
}
