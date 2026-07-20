using Octokit;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.GitHub.Clients;

/// <summary>
/// GitHub API 客户端实现 (基于 Octokit)
/// </summary>
public class GitHubApiClient : Core.Interfaces.IGitHubClient, IDisposable
{
    private readonly GitHubClient _client;
    private string? _accessToken;
    
    public GitHubApiClient()
    {
        _client = new GitHubClient(new ProductHeaderValue("RepoGalaxy"));
    }
    
    public void SetAccessToken(string token)
    {
        _accessToken = token;
        _client.Credentials = new Credentials(token);
    }

    public void ClearAccessToken()
    {
        _accessToken = null;
        _client.Credentials = Credentials.Anonymous;
    }
    
    public async Task<bool> IsAuthenticatedAsync()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return false;
        
        try
        {
            var user = await _client.User.Current();
            return user != null;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<Core.Models.User?> GetCurrentUserAsync()
    {
        try
        {
            var user = await _client.User.Current();
            return MapToCoreUser(user);
        }
        catch
        {
            return null;
        }
    }
    
    public async Task<GitHubRateLimit?> GetRateLimitAsync()
    {
        try
        {
            var overview = await _client.RateLimit.GetRateLimits();
            return new GitHubRateLimit
            {
                CoreRemaining = overview.Resources.Core.Remaining,
                CoreLimit = overview.Resources.Core.Limit,
                CoreResetAt = overview.Resources.Core.Reset,
                SearchRemaining = overview.Resources.Search.Remaining,
                SearchLimit = overview.Resources.Search.Limit,
                SearchResetAt = overview.Resources.Search.Reset
            };
        }
        catch { return null; }
    }

    public async Task<Core.Models.Repository?> GetRepositoryAsync(string owner, string name)
    {
        try
        {
            var repo = await _client.Repository.Get(owner, name);
            return await EnrichAndMapAsync(repo);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }
    
    public async Task<IEnumerable<Core.Models.Repository>> SearchRepositoriesAsync(string query, string? language = null, string? sort = null)
    {
        var searchQuery = query;
        if (!string.IsNullOrEmpty(language))
            searchQuery += $" language:{language}";
        
        var request = new SearchRepositoriesRequest(searchQuery)
        {
            SortField = sort?.ToLower() switch
            {
                "stars" => RepoSearchSort.Stars,
                "forks" => RepoSearchSort.Forks,
                "updated" => RepoSearchSort.Updated,
                _ => RepoSearchSort.Stars
            },
            Order = SortDirection.Descending,
            PerPage = 50
        };
        
        var result = await _client.Search.SearchRepo(request);
        var repositories = new List<Core.Models.Repository>();
        
        foreach (var repo in result.Items.Take(30))
        {
            repositories.Add(MapToCoreRepository(repo));
        }
        
        return repositories;
    }
    
    public async Task<IEnumerable<Core.Models.Repository>> GetTrendingAsync(string? language = null, string since = "daily")
    {
        var date = since.ToLower() switch
        {
            "daily" => DateTimeOffset.Now.AddDays(-1),
            "weekly" => DateTimeOffset.Now.AddDays(-7),
            "monthly" => DateTimeOffset.Now.AddDays(-30),
            _ => DateTimeOffset.Now.AddDays(-1)
        };
        
        var query = $"created:>{date:yyyy-MM-dd}";
        if (!string.IsNullOrEmpty(language))
            query += $" language:{language}";
        
        return await SearchRepositoriesAsync(query, language, "stars");
    }
    
    public async Task<IEnumerable<Core.Models.Repository>> GetUserRepositoriesAsync()
    {
        if (!await IsAuthenticatedAsync())
            return Enumerable.Empty<Core.Models.Repository>();
        
        var repos = await _client.Repository.GetAllForCurrent();
        var result = new List<Core.Models.Repository>();
        
        foreach (var repo in repos.OrderByDescending(r => r.UpdatedAt).Take(50))
        {
            result.Add(MapToCoreRepository(repo));
        }
        
        return result;
    }

    public async Task<ReleaseInfo?> GetLatestReleaseAsync(string owner, string name)
    {
        try
        {
            var release = await _client.Repository.Release.GetLatest(owner, name);
            if (release.Prerelease || release.Draft || !release.PublishedAt.HasValue) return null;
            return new ReleaseInfo
            {
                Id = release.Id,
                TagName = release.TagName,
                Name = release.Name ?? release.TagName,
                HtmlUrl = release.HtmlUrl,
                PublishedAt = release.PublishedAt.Value
            };
        }
        catch { return null; }
    }

    // 别名方法，与ViewModel使用的一致
    public async Task<IEnumerable<Core.Models.Repository>> GetCurrentUserRepositoriesAsync()
    {
        return await GetUserRepositoriesAsync();
    }
    
    public async Task<List<LanguageInfo>> GetLanguagesAsync(string owner, string name)
    {
        try
        {
            var languages = await _client.Repository.GetAllLanguages(owner, name);
            var total = languages.Sum(l => l.NumberOfBytes);
            
            return languages
                .Select(l => new LanguageInfo
                {
                    Name = l.Name,
                    Bytes = l.NumberOfBytes,
                    Percentage = total > 0 ? (double)l.NumberOfBytes / total : 0
                })
                .OrderByDescending(l => l.Percentage)
                .ToList();
        }
        catch
        {
            return new List<LanguageInfo>();
        }
    }
    
    public async Task<bool> StarRepositoryAsync(string owner, string name)
    {
        try
        {
            await _client.Activity.Starring.StarRepo(owner, name);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> UnstarRepositoryAsync(string owner, string name)
    {
        try
        {
            await _client.Activity.Starring.RemoveStarFromRepo(owner, name);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> IsStarredAsync(string owner, string name)
    {
        try
        {
            return await _client.Activity.Starring.CheckStarred(owner, name);
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<Core.Models.Repository> EnrichAndMapAsync(Octokit.Repository repo)
    {
        var result = MapToCoreRepository(repo);
        result.Languages = await GetLanguagesAsync(repo.Owner.Login, repo.Name);
        result.PrimaryLanguage = result.Languages.FirstOrDefault()?.Name ?? "Unknown";
        result.CalculateDiscoveryScore();
        return result;
    }
    
    private static Core.Models.Repository MapToCoreRepository(Octokit.Repository repo)
    {
        return new Core.Models.Repository
        {
            Id = repo.Id,
            GitHubId = repo.NodeId,
            Owner = repo.Owner.Login,
            Name = repo.Name,
            HtmlUrl = repo.HtmlUrl,
            Description = repo.Description ?? string.Empty,
            PrimaryLanguage = repo.Language ?? "Unknown",
            Topics = repo.Topics?.ToList() ?? new List<string>(),
            Homepage = repo.Homepage ?? string.Empty,
            IsPrivate = repo.Private,
            IsArchived = repo.Archived,
            Stars = repo.StargazersCount,
            Forks = repo.ForksCount,
            Watchers = repo.SubscribersCount,
            OpenIssues = repo.OpenIssuesCount,
            CreatedAt = repo.CreatedAt,
            UpdatedAt = repo.UpdatedAt,
            LastPushedAt = repo.PushedAt
        };
    }
    
    private static Core.Models.User MapToCoreUser(Octokit.User user)
    {
        return new Core.Models.User
        {
            Id = user.Id,
            GitHubId = user.NodeId,
            Login = user.Login,
            AvatarUrl = user.AvatarUrl,
            Bio = user.Bio ?? string.Empty,
            Company = user.Company ?? string.Empty,
            Location = user.Location ?? string.Empty,
            Blog = user.Blog ?? string.Empty,
            // TwitterUsername 属性不存在于当前 Octokit 版本
            PublicRepos = user.PublicRepos,
            Followers = user.Followers,
            Following = user.Following,
            CreatedAt = user.CreatedAt
        };
    }
    
    public void Dispose()
    {
        // GitHubClient 不需要显式释放
    }
}
