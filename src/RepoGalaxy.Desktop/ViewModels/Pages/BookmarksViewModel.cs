using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Data.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public partial class BookmarksViewModel : ViewModelBase
{
    private readonly ILogger<BookmarksViewModel> _logger;
    private readonly RepositoryService _repoService;

    [ObservableProperty] private ObservableCollection<RepositoryViewModel> _bookmarks = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public BookmarksViewModel(
        ILogger<BookmarksViewModel> logger,
        RepositoryService repoService)
    {
        _logger = logger;
        _repoService = repoService;
    }

    public async Task LoadBookmarksAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            StatusMessage = "加载收藏...";

            var repos = await _repoService.GetBookmarksAsync();
            var viewModels = repos.Select(r => new RepositoryViewModel(r)).ToList();
            
            Bookmarks.Clear();
            foreach (var vm in viewModels)
            {
                Bookmarks.Add(vm);
            }

            StatusMessage = $"共有 {Bookmarks.Count} 个收藏";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载收藏失败");
            StatusMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveBookmarkAsync(long repoId)
    {
        try
        {
            await _repoService.ToggleBookmarkAsync(repoId);
            await LoadBookmarksAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移除收藏失败");
        }
    }
}
