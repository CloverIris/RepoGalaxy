using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.Services;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class RepositoryDetailsViewModel : ViewModelBase
{
    private readonly DiscoveryStore _store;
    private readonly IExternalLinkService _links;

    [ObservableProperty] private Repository? _repository;
    [ObservableProperty] private FeedReason? _reason;
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _feedback = string.Empty;

    public bool HasRepository => Repository is not null;
    public bool HasReason => Reason is not null && !string.IsNullOrWhiteSpace(Reason.Summary);
    public string StarsText => Repository?.Stars.ToString("N0") ?? "0";
    public string ForksText => Repository?.Forks.ToString("N0") ?? "0";
    public string IssuesText => Repository?.OpenIssues.ToString("N0") ?? "0";
    public string UpdatedText => Repository is null || Repository.UpdatedAt == default
        ? "更新时间未知"
        : $"更新于 {Repository.UpdatedAt.LocalDateTime:yyyy-MM-dd}";
    public string SaveText => Repository?.IsBookmarked == true ? "已收藏" : "收藏";

    public RepositoryDetailsViewModel(DiscoveryStore store, IExternalLinkService links)
    {
        _store = store;
        _links = links;
    }

    public void Show(Repository repository, FeedReason? reason = null)
    {
        Repository = repository;
        Reason = reason;
        Feedback = string.Empty;
        IsOpen = true;
        NotifyDerivedProperties();
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    [RelayCommand]
    private void OpenOnGitHub()
    {
        if (!_links.Open(Repository?.HtmlUrl)) Feedback = "无法打开 GitHub 链接，请稍后重试。";
    }

    [RelayCommand]
    private async Task ToggleSavedAsync()
    {
        if (Repository is null) return;
        await _store.ToggleSavedAsync(Repository.Id);
        Repository.IsBookmarked = !Repository.IsBookmarked;
        Feedback = Repository.IsBookmarked ? "已添加到收藏库" : "已从收藏库移除";
        OnPropertyChanged(nameof(SaveText));
    }

    partial void OnRepositoryChanged(Repository? value)
    {
        OnPropertyChanged(nameof(HasRepository));
        NotifyDerivedProperties();
    }

    partial void OnReasonChanged(FeedReason? value) => OnPropertyChanged(nameof(HasReason));

    private void NotifyDerivedProperties()
    {
        OnPropertyChanged(nameof(StarsText));
        OnPropertyChanged(nameof(ForksText));
        OnPropertyChanged(nameof(IssuesText));
        OnPropertyChanged(nameof(UpdatedText));
        OnPropertyChanged(nameof(SaveText));
    }
}
