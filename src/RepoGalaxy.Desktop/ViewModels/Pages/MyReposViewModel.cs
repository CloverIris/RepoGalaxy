using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Data.Services;
using RepoGalaxy.GitHub.Clients;

namespace RepoGalaxy.Desktop.ViewModels;

public partial class MyReposViewModel : ViewModelBase
{
    private readonly ILogger<MyReposViewModel> _logger;
    private readonly GitHubApiClient _apiClient;
    private readonly RepositoryService _repoService;

    [ObservableProperty] private ObservableCollection<RepositoryViewModel> _repositories = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public MyReposViewModel(
        ILogger<MyReposViewModel> logger,
        GitHubApiClient apiClient,
        RepositoryService repoService)
    {
        _logger = logger;
        _apiClient = apiClient;
        _repoService = repoService;
    }

    public async Task LoadRepositoriesAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            StatusMessage = "加载仓库...";

            var repos = await _apiClient.GetCurrentUserRepositoriesAsync();
            var viewModels = repos.Select(r => new RepositoryViewModel(r)).ToList();
            
            Repositories.Clear();
            foreach (var vm in viewModels)
            {
                Repositories.Add(vm);
            }

            StatusMessage = $"共有 {Repositories.Count} 个仓库";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载我的仓库失败");
            StatusMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadRepositoriesAsync();
    }
}
