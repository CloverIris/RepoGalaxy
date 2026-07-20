using System.Diagnostics;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Data.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class LocalReposViewModel : ViewModelBase, ISearchablePage
{
    private readonly ILogger<LocalReposViewModel> _logger;
    private readonly RepositoryService _repoService;
    private readonly ILocalRepositoryResolver _localResolver;
    private readonly List<LocalRepoViewModel> _allRepositories = [];
    public ObservableCollection<LocalRepoViewModel> LocalRepositories { get; } = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _scanPath;
    [ObservableProperty] private string _searchText = string.Empty;
    public bool IsEmpty => !IsLoading && LocalRepositories.Count == 0;

    public LocalReposViewModel(ILogger<LocalReposViewModel> logger, RepositoryService repoService, ILocalRepositoryResolver localResolver)
    {
        _logger = logger;
        _repoService = repoService;
        _localResolver = localResolver;
        _scanPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Projects");
    }

    public async Task LoadLocalRepositoriesAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            StatusMessage = "正在读取本地仓库…";
            var repos = await _repoService.GetLocalRepositoriesAsync();
            _allRepositories.Clear();
            _allRepositories.AddRange(repos.Select(r => new LocalRepoViewModel(r)));
            ApplyFilter();
            StatusMessage = $"已收录 {_allRepositories.Count} 个本地仓库";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载本地仓库失败");
            StatusMessage = "读取本地仓库失败，请稍后重试。";
        }
        finally { IsLoading = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsLoading) return;
        try
        {
            IsLoading = true;
            if (!Directory.Exists(ScanPath)) { StatusMessage = "目录不存在，请检查路径。"; return; }
            StatusMessage = $"正在扫描 {ScanPath}…";
            var gitDirs = Directory.GetDirectories(ScanPath, ".git", SearchOption.AllDirectories);
            foreach (var gitDir in gitDirs)
            {
                var repoPath = Path.GetDirectoryName(gitDir);
                if (string.IsNullOrEmpty(repoPath)) continue;
                var origin = await _localResolver.ReadOriginAsync(repoPath);
                await _repoService.AddLocalRepositoryAsync(repoPath, Path.GetFileName(repoPath), origin);
            }
            IsLoading = false;
            await LoadLocalRepositoriesAsync();
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "部分目录无权访问，请选择权限允许的工作目录。";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫描本地仓库失败");
            StatusMessage = "扫描失败，请检查目录后重试。";
        }
        finally { IsLoading = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    [RelayCommand] private async Task RemoveAsync(long id) { await _repoService.RemoveLocalRepositoryAsync(id); await LoadLocalRepositoriesAsync(); }
    [RelayCommand] private void OpenFolder(LocalRepoViewModel item)
    {
        if (!Directory.Exists(item.LocalPath)) { StatusMessage = "该目录已经不存在。"; return; }
        try { Process.Start(new ProcessStartInfo { FileName = item.LocalPath, UseShellExecute = true }); }
        catch { StatusMessage = "无法打开该目录。"; }
    }
    [RelayCommand] private async Task CopyPathAsync(LocalRepoViewModel item)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(item.LocalPath);
            StatusMessage = "路径已复制";
        }
    }
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        LocalRepositories.Clear();
        foreach (var item in _allRepositories.Where(x => string.IsNullOrEmpty(query) || x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || x.LocalPath.Contains(query, StringComparison.OrdinalIgnoreCase))) LocalRepositories.Add(item);
        OnPropertyChanged(nameof(IsEmpty));
    }
}

public sealed class LocalRepoViewModel
{
    private readonly LocalRepository _repo;
    public LocalRepoViewModel(LocalRepository repo) => _repo = repo;
    public long Id => _repo.Id;
    public string Name => _repo.Name;
    public string LocalPath => _repo.LocalPath;
    public string? GitHubUrl => _repo.GitHubUrl;
    public DateTimeOffset AddedAt => _repo.AddedAt;
    public bool IsTracked => _repo.IsTracked;
    public string AddedText => $"添加于 {AddedAt.LocalDateTime:yyyy-MM-dd}";
}
