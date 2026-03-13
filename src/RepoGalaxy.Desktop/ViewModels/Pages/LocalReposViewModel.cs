using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public partial class LocalReposViewModel : ViewModelBase
{
    private readonly ILogger<LocalReposViewModel> _logger;
    private readonly RepositoryService _repoService;

    [ObservableProperty] private ObservableCollection<LocalRepoViewModel> _localRepositories = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _scanPath = string.Empty;

    public LocalReposViewModel(
        ILogger<LocalReposViewModel> logger,
        RepositoryService repoService)
    {
        _logger = logger;
        _repoService = repoService;
        
        // 默认扫描用户目录下的Projects文件夹
        ScanPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            "Projects");
    }

    public async Task LoadLocalRepositoriesAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            StatusMessage = "加载本地仓库...";

            var repos = await _repoService.GetLocalRepositoriesAsync();
            var viewModels = repos.Select(r => new LocalRepoViewModel(r)).ToList();
            
            LocalRepositories.Clear();
            foreach (var vm in viewModels)
            {
                LocalRepositories.Add(vm);
            }

            StatusMessage = $"找到 {LocalRepositories.Count} 个本地仓库";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载本地仓库失败");
            StatusMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = $"扫描 {ScanPath}...";

            if (!Directory.Exists(ScanPath))
            {
                StatusMessage = "目录不存在";
                return;
            }

            // 递归查找.git目录
            var gitDirs = Directory.GetDirectories(ScanPath, ".git", SearchOption.AllDirectories);
            
            StatusMessage = $"找到 {gitDirs.Length} 个Git仓库";
            
            // 添加到数据库
            foreach (var gitDir in gitDirs)
            {
                var repoPath = Path.GetDirectoryName(gitDir);
                if (string.IsNullOrEmpty(repoPath)) continue;

                var repoName = Path.GetFileName(repoPath);
                
                // 检查是否已存在
                var existing = await _repoService.GetLocalRepositoryByPathAsync(repoPath);
                if (existing == null)
                {
                    await _repoService.AddLocalRepositoryAsync(repoPath, repoName);
                }
            }

            await LoadLocalRepositoriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "扫描本地仓库失败");
            StatusMessage = $"扫描失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(long id)
    {
        try
        {
            await _repoService.RemoveLocalRepositoryAsync(id);
            await LoadLocalRepositoriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移除本地仓库失败");
        }
    }
}

public class LocalRepoViewModel : ObservableObject
{
    private readonly LocalRepository _repo;

    public LocalRepoViewModel(LocalRepository repo)
    {
        _repo = repo;
    }

    public long Id => _repo.Id;
    public string Name => _repo.Name;
    public string LocalPath => _repo.LocalPath;
    public string? GitHubUrl => _repo.GitHubUrl;
    public DateTimeOffset AddedAt => _repo.AddedAt;
    public bool IsTracked => _repo.IsTracked;
}
