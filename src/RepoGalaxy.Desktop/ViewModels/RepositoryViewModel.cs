using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed class RepositoryViewModel
{
    public RepositoryViewModel(Repository repository) => Repository = repository;
    public Repository Repository { get; }
    public long Id => Repository.Id;
    public string Name => Repository.Name;
    public string Owner => Repository.Owner;
    public string FullName => Repository.FullName;
    public string Description => string.IsNullOrWhiteSpace(Repository.Description) ? "这个仓库暂时没有简介。" : Repository.Description;
    public string PrimaryLanguage => string.IsNullOrWhiteSpace(Repository.PrimaryLanguage) ? "其他" : Repository.PrimaryLanguage;
    public int Stars => Repository.Stars;
    public int Forks => Repository.Forks;
    public string StarsFormatted => Stars.ToString("N0");
    public string ForksFormatted => Forks.ToString("N0");
    public string UpdatedText => Repository.UpdatedAt == default ? "更新时间未知" : Repository.UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd");
    public string TopicsText => Repository.Topics.Count == 0 ? "暂无主题" : string.Join("  ·  ", Repository.Topics.Take(3));
    public IReadOnlyList<string> Topics => Repository.Topics;
    public string OwnerAvatarUrl => Repository.OwnerAvatarUrl;
}

public sealed class FeedItemViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public FeedItemViewModel(FeedItem item)
    {
        Item = item;
        Repository = new RepositoryViewModel(item.Repository);
    }

    public FeedItem Item { get; }
    public RepositoryViewModel Repository { get; }
    public long Id => Item.Id;
    public string SourceText => Item.Source switch
    {
        FeedSource.ForYou => "为你推荐",
        FeedSource.Subscription => "订阅匹配",
        FeedSource.Release => "版本更新",
        _ => "热门"
    };
    public string ReasonText => string.IsNullOrWhiteSpace(Item.Reason.Summary) ? "近期活跃且受到开发者关注" : Item.Reason.Summary;
    public string DiscoveredText => FormatRelative(Item.DiscoveredAt);
    public bool IsUnread => !Item.IsRead;
    public string SaveText => Item.Repository.IsBookmarked ? "已收藏" : "收藏";

    public void ToggleBookmarked()
    {
        Item.Repository.IsBookmarked = !Item.Repository.IsBookmarked;
        OnPropertyChanged(nameof(SaveText));
    }

    private static string FormatRelative(DateTimeOffset value)
    {
        var age = DateTimeOffset.Now - value.ToLocalTime();
        if (age.TotalMinutes < 2) return "刚刚发现";
        if (age.TotalHours < 1) return $"{Math.Max(1, (int)age.TotalMinutes)} 分钟前";
        if (age.TotalDays < 1) return $"{(int)age.TotalHours} 小时前";
        if (age.TotalDays < 7) return $"{(int)age.TotalDays} 天前";
        return value.LocalDateTime.ToString("yyyy-MM-dd");
    }
}
