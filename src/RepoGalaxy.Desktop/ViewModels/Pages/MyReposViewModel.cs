using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Data.Services;
using RepoGalaxy.GitHub.Clients;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class MyReposViewModel : ViewModelBase, ISearchablePage
{
    private readonly ILogger<MyReposViewModel> _logger;
    private readonly GitHubApiClient _apiClient;
    private readonly RepositoryService _repoService;
    private readonly RepositoryDetailsViewModel _details;
    private readonly List<RepositoryViewModel> _allRepositories = [];

    public ObservableCollection<RepositoryViewModel> Repositories { get; } = [];
    public ObservableCollection<string> Languages { get; } = ["全部语言"];
    public IReadOnlyList<string> SortOptions { get; } = ["最近更新", "Stars 最多", "名称"];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isAuthenticationRequired;
    [ObservableProperty] private string _statusMessage = "登录 GitHub 后即可读取你的仓库。";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedLanguage = "全部语言";
    [ObservableProperty] private string _selectedSort = "最近更新";
    [ObservableProperty] private RepositoryViewModel? _selectedRepository;
    public bool IsEmpty => !IsLoading && !IsAuthenticationRequired && Repositories.Count == 0;

    public MyReposViewModel(ILogger<MyReposViewModel> logger, GitHubApiClient apiClient, RepositoryService repoService, RepositoryDetailsViewModel details)
    {
        _logger = logger;
        _apiClient = apiClient;
        _repoService = repoService;
        _details = details;
    }

    public void SetAuthenticationRequired()
    {
        IsAuthenticationRequired = true;
        StatusMessage = "登录 GitHub 后即可读取和筛选你的仓库。";
        Repositories.Clear();
        NotifyState();
    }

    public async Task LoadRepositoriesAsync()
    {
        if (IsLoading) return;
        try
        {
            IsAuthenticationRequired = false;
            IsLoading = true;
            StatusMessage = "正在加载仓库…";
            var repos = await _apiClient.GetCurrentUserRepositoriesAsync();
            _allRepositories.Clear();
            _allRepositories.AddRange(repos.Select(r => new RepositoryViewModel(r)));
            Languages.Clear(); Languages.Add("全部语言");
            foreach (var language in _allRepositories.Select(x => x.PrimaryLanguage).Distinct(StringComparer.OrdinalIgnoreCase).Order()) Languages.Add(language);
            ApplyFilter();
            StatusMessage = $"共 {_allRepositories.Count} 个仓库";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载我的仓库失败");
            StatusMessage = "加载失败，请检查网络或重新登录。";
            Repositories.Clear();
        }
        finally { IsLoading = false; NotifyState(); }
    }

    [RelayCommand] private Task RefreshAsync() => LoadRepositoriesAsync();
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedLanguageChanged(string value) => ApplyFilter();
    partial void OnSelectedSortChanged(string value) => ApplyFilter();
    partial void OnSelectedRepositoryChanged(RepositoryViewModel? value) { if (value is not null) _details.Show(value.Repository); }
    public void ClearDetailSelection(long? repositoryId)
    {
        if (repositoryId is null || SelectedRepository?.Repository.Id == repositoryId) SelectedRepository = null;
    }
    private void ApplyFilter()
    {
        IEnumerable<RepositoryViewModel> query = _allRepositories;
        if (!string.IsNullOrWhiteSpace(SearchText)) query = query.Where(x => x.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        if (SelectedLanguage != "全部语言") query = query.Where(x => x.PrimaryLanguage.Equals(SelectedLanguage, StringComparison.OrdinalIgnoreCase));
        query = SelectedSort switch { "Stars 最多" => query.OrderByDescending(x => x.Stars), "名称" => query.OrderBy(x => x.FullName), _ => query.OrderByDescending(x => x.Repository.UpdatedAt) };
        Repositories.Clear(); foreach (var item in query) Repositories.Add(item); NotifyState();
    }
    private void NotifyState() => OnPropertyChanged(nameof(IsEmpty));
}
