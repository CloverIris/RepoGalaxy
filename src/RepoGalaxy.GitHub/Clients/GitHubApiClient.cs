using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.GitHub.Services;

namespace RepoGalaxy.GitHub.Clients;

public sealed class GitHubApiClient : IGitHubClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly GitHubRequestBudget _budget;
    private readonly ISyncOrchestrator _orchestrator;
    private readonly ICacheService? _cache;
    private readonly IUserService? _users;
    private string? _accessToken;
    private string _cachePartition = "guest";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public GitHubApiClient(HttpClient http, GitHubRequestBudget budget, ISyncOrchestrator orchestrator, ICacheService? cache = null, IUserService? users = null) { _http = http; _budget = budget; _orchestrator = orchestrator; _cache = cache; _users = users; }
    public void SetAccessToken(string token, string? accountId = null) { _accessToken = token; _cachePartition = string.IsNullOrWhiteSpace(accountId) ? "authenticated" : accountId; }
    public void ClearAccessToken() { _accessToken = null; _cachePartition = "guest"; }
    public async Task<bool> IsAuthenticatedAsync() => await GetCurrentUserAsync() is not null;
    public async Task<User?> GetCurrentUserAsync() => string.IsNullOrWhiteSpace(_accessToken) ? null : await ValidateTokenAsync(_accessToken);
    public async Task<User?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default) { var response = await SendAsync<UserDto>(HttpMethod.Get, "user", token, cancellationToken); return response.StatusCode == 200 && response.Data is not null ? Map(response.Data) : null; }

    public async Task<GitHubRateLimit?> GetRateLimitAsync()
    {
        var response = await SendAsync<RateLimitDto>(HttpMethod.Get, "rate_limit", null, CancellationToken.None);
        if (response.Data?.Resources is not { } r) return null;
        return new GitHubRateLimit { CoreLimit = r.Core.Limit, CoreRemaining = r.Core.Remaining, CoreResetAt = FromEpoch(r.Core.Reset), SearchLimit = r.Search.Limit, SearchRemaining = r.Search.Remaining, SearchResetAt = FromEpoch(r.Search.Reset) };
    }
    public async Task<Repository?> GetRepositoryAsync(string owner, string name) { var response = await SendAsync<RepositoryDto>(HttpMethod.Get, $"repos/{Esc(owner)}/{Esc(name)}", null, CancellationToken.None); if (response.Data is null) return null; var repo = Map(response.Data); repo.CalculateDiscoveryScore(); return repo; }
    public async Task<IEnumerable<Repository>> SearchRepositoriesAsync(string query, string? language = null, string? sort = null)
        => (await SearchRepositoriesPageAsync(query, language, sort)).Items;

    public async Task<GitHubPage<Repository>> SearchRepositoriesPageAsync(string query, string? language = null, string? sort = null, string? nextPageUrl = null, CancellationToken cancellationToken = default)
    {
        if (nextPageUrl is null)
        {
            if (!string.IsNullOrWhiteSpace(language) && !query.Contains("language:", StringComparison.OrdinalIgnoreCase)) query += $" language:{language}";
            nextPageUrl = $"search/repositories?q={Uri.EscapeDataString(query)}&sort={Uri.EscapeDataString(sort ?? "stars")}&order=desc&per_page=50";
        }
        var response = await SendAsync<SearchDto>(HttpMethod.Get, nextPageUrl, null, cancellationToken);
        return new(response.Data?.Items.Select(Map).ToList() ?? [], response.NextPageUrl, response.RateLimit, response.ETag, response.LastModified);
    }
    public async Task<IEnumerable<Repository>> GetTrendingAsync(string? language = null, string since = "daily", CancellationToken cancellationToken = default) { var days = since.ToLowerInvariant() switch { "weekly" => 7, "monthly" => 30, _ => 1 }; return (await SearchRepositoriesPageAsync($"pushed:>{DateTime.UtcNow.AddDays(-days):yyyy-MM-dd} stars:>100 archived:false fork:false", language, "stars", cancellationToken: cancellationToken)).Items; }
    public async Task<IEnumerable<Repository>> GetUserRepositoriesAsync() { var result = new List<Repository>(); string? next = null; do { var page = await GetUserRepositoriesPageAsync(next); result.AddRange(page.Items); next = page.NextPageUrl; } while (next is not null); return result; }
    public async Task<GitHubPage<Repository>> GetUserRepositoriesPageAsync(string? nextPageUrl = null, CancellationToken cancellationToken = default) => await PageAsync(nextPageUrl ?? "user/repos?visibility=all&affiliation=owner,collaborator,organization_member&sort=updated&per_page=100", cancellationToken);
    public async Task<GitHubPage<Repository>> GetStarredRepositoriesPageAsync(string? nextPageUrl = null, CancellationToken cancellationToken = default) => await PageAsync(nextPageUrl ?? "user/starred?sort=created&direction=desc&per_page=100", cancellationToken);
    public async Task<ReleaseInfo?> GetLatestReleaseAsync(string owner, string name, CancellationToken cancellationToken = default) { var response = await SendAsync<ReleaseDto>(HttpMethod.Get, $"repos/{Esc(owner)}/{Esc(name)}/releases/latest", null, cancellationToken); var r = response.Data; return r is null || r.Prerelease || r.Draft || r.PublishedAt is null ? null : new ReleaseInfo { Id = r.Id, TagName = r.TagName, Name = r.Name ?? r.TagName, HtmlUrl = r.HtmlUrl, PublishedAt = r.PublishedAt.Value }; }
    public Task<IEnumerable<Repository>> GetCurrentUserRepositoriesAsync() => GetUserRepositoriesAsync();
    public async Task<List<LanguageInfo>> GetLanguagesAsync(string owner, string name) { var response = await SendAsync<Dictionary<string, long>>(HttpMethod.Get, $"repos/{Esc(owner)}/{Esc(name)}/languages", null, CancellationToken.None); if (response.Data is null) return []; var total = response.Data.Values.Sum(); return response.Data.Select(x => new LanguageInfo { Name = x.Key, Bytes = x.Value, Percentage = total == 0 ? 0 : (double)x.Value / total }).OrderByDescending(x => x.Bytes).ToList(); }
    public async Task<bool> StarRepositoryAsync(string owner, string name) { var success = (await SendAsync<object>(HttpMethod.Put, $"user/starred/{Esc(owner)}/{Esc(name)}", null, CancellationToken.None)).StatusCode is 204; if (success && _cache is not null) await _cache.InvalidateTagAsync("github:starred"); return success; }
    public async Task<bool> UnstarRepositoryAsync(string owner, string name) { var success = (await SendAsync<object>(HttpMethod.Delete, $"user/starred/{Esc(owner)}/{Esc(name)}", null, CancellationToken.None)).StatusCode is 204; if (success && _cache is not null) await _cache.InvalidateTagAsync("github:starred"); return success; }
    public async Task<bool> IsStarredAsync(string owner, string name) => (await SendAsync<object>(HttpMethod.Get, $"user/starred/{Esc(owner)}/{Esc(name)}", null, CancellationToken.None)).StatusCode is 204;
    private async Task<GitHubPage<Repository>> PageAsync(string uri, CancellationToken ct) { var response = await SendAsync<List<RepositoryDto>>(HttpMethod.Get, uri, null, ct); return new(response.Data?.Select(Map).ToList() ?? [], response.Data is null ? null : response.NextPageUrl, response.RateLimit, response.ETag, response.LastModified); }

    private async Task<GitHubResponse<T>> SendAsync<T>(HttpMethod method, string uri, string? overrideToken, CancellationToken ct)
    {
        var normalized = Normalize(uri);
        if (_cache is null || method != HttpMethod.Get || overrideToken is not null || !CanCache(normalized))
            return await _orchestrator.EnqueueAsync(PriorityFor(uri), token => SendCoreAsync<T>(method, uri, overrideToken, null, null, token), ct);

        var key = CacheKey.Create("github", _cachePartition, normalized);
        var cached = await _cache.GetAsync<CachedResponse<T>>(key, ct);
        if (cached.State == CacheEntryState.Fresh && cached.Value is { } fresh) return FromCache(fresh);
        try
        {
            var response = await _orchestrator.EnqueueAsync(PriorityFor(uri), token => SendCoreAsync<T>(method, uri, overrideToken, cached.Value?.ETag, cached.Value?.LastModified, token), ct);
            var policy = await CachePolicyForAsync(normalized);
            if (response.NotModified && cached.Value is { } notModified)
            {
                await _cache.SetValidatedAsync(key, notModified, policy, notModified.ETag, notModified.LastModified, ct);
                return FromCache(notModified) with { RateLimit = response.RateLimit, NotModified = true };
            }
            if (response.StatusCode is >= 200 and < 300 && response.Data is not null)
            {
                var stored = new CachedResponse<T>(response.Data, response.ETag, response.LastModified, response.NextPageUrl, response.RateLimit);
                await _cache.SetValidatedAsync(key, stored, policy, response.ETag, response.LastModified, ct);
            }
            if (response.StatusCode is 403 or 429 or >= 500 && cached.State == CacheEntryState.Stale && cached.Value is { } staleResponse) return FromCache(staleResponse);
            return response;
        }
        catch (HttpRequestException) when (cached.State == CacheEntryState.Stale && cached.Value is not null) { return FromCache(cached.Value); }
    }

    private async Task<GitHubResponse<T>> SendCoreAsync<T>(HttpMethod method, string uri, string? overrideToken, string? etag, string? lastModified, CancellationToken ct)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var request = new HttpRequestMessage(method, Normalize(uri));
            var token = overrideToken ?? _accessToken; if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrWhiteSpace(etag)) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            if (!string.IsNullOrWhiteSpace(lastModified) && DateTimeOffset.TryParse(lastModified, out var modified)) request.Headers.IfModifiedSince = modified;
            try
            {
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                var rate = ReadRate(response.Headers); if (rate is not null) _budget.Update(rate);
                var nextPage = NextLink(response.Headers);
                if ((int)response.StatusCode >= 500 && attempt < 2) { await RetryDelayAsync(attempt, ct); continue; }
                if (response.StatusCode == HttpStatusCode.NotModified) return new(default, 304, rate?.Resource ?? "core", rate, NotModified: true, NextPageUrl: nextPage);
                if (!response.IsSuccessStatusCode) return new(default, (int)response.StatusCode, rate?.Resource ?? "core", rate, NextPageUrl: nextPage);
                if (response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object)) return new(default, (int)response.StatusCode, rate?.Resource ?? "core", rate, NextPageUrl: nextPage);
                await using var stream = await response.Content.ReadAsStreamAsync(ct); var data = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
                return new(data, (int)response.StatusCode, rate?.Resource ?? "core", rate, Header(response, "ETag"), Header(response, "Last-Modified"), NextPageUrl: nextPage);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested) { last = ex; if (attempt < 2) await RetryDelayAsync(attempt, ct); }
        }
        throw new HttpRequestException("GitHub 请求在有限重试后仍然失败。", last);
    }
    private static string Normalize(string uri) { if (!Uri.TryCreate(uri, UriKind.Absolute, out var absolute)) return uri; if (!absolute.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase) || absolute.Scheme != Uri.UriSchemeHttps) throw new InvalidOperationException("拒绝不可信的 GitHub 分页地址。"); return absolute.PathAndQuery.TrimStart('/'); }
    private async Task<CachePolicy> CachePolicyForAsync(string uri)
    {
        var preferences = _users is null ? null : await _users.GetPreferencesAsync();
        var minutes = uri.StartsWith("search/", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("user/", StringComparison.OrdinalIgnoreCase)
            ? preferences?.FeedCacheTtlMinutes ?? 30
            : preferences?.DetailCacheTtlMinutes ?? 360;
        var tags = new List<string> { "github", _cachePartition == "guest" ? "public" : "private" };
        if (uri.StartsWith("user/starred", StringComparison.OrdinalIgnoreCase)) tags.Add("github:starred");
        return new CachePolicy(TimeSpan.FromMinutes(Math.Clamp(minutes, 5, 1440)), TimeSpan.FromDays(7), tags);
    }
    private static bool CanCache(string uri)
    {
        var path = uri.Split('?', 2)[0];
        if (path.Equals("user", StringComparison.OrdinalIgnoreCase) || path.Equals("rate_limit", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.StartsWith("user/starred/", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
    private static GitHubResponse<T> FromCache<T>(CachedResponse<T> cached) => new(cached.Data, 200, "cache", cached.RateLimit, cached.ETag, cached.LastModified, NextPageUrl: cached.NextPageUrl);
    private static Task RetryDelayAsync(int attempt, CancellationToken ct) => Task.Delay((attempt == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(3)) + TimeSpan.FromMilliseconds(Random.Shared.Next(40, 260)), ct);
    private static SyncPriority PriorityFor(string uri)
    {
        var path = Normalize(uri).Split('?', 2)[0];
        if (path.Equals("user", StringComparison.OrdinalIgnoreCase)) return SyncPriority.SessionValidation;
        if (path.StartsWith("user/repos", StringComparison.OrdinalIgnoreCase) || path.StartsWith("user/starred", StringComparison.OrdinalIgnoreCase)) return SyncPriority.LoginInitialization;
        if (path.StartsWith("search/", StringComparison.OrdinalIgnoreCase)) return SyncPriority.SubscriptionSync;
        if (path.Contains("/releases", StringComparison.OrdinalIgnoreCase)) return SyncPriority.ReleaseCheck;
        if (path.StartsWith("repos/", StringComparison.OrdinalIgnoreCase)) return SyncPriority.InteractiveDetails;
        return SyncPriority.BackgroundRefresh;
    }
    private static string Esc(string value) => Uri.EscapeDataString(value);
    private static string? Header(HttpResponseMessage r, string name) => r.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
    private static string? NextLink(HttpResponseHeaders headers) { if (!headers.TryGetValues("Link", out var values)) return null; foreach (var part in string.Join(',', values).Split(',')) if (part.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase)) { var start = part.IndexOf('<'); var end = part.IndexOf('>'); if (start >= 0 && end > start) return part[(start + 1)..end]; } return null; }
    private static GitHubRateWindow? ReadRate(HttpResponseHeaders h) { if (!TryInt(h, "X-RateLimit-Limit", out var limit) || !TryInt(h, "X-RateLimit-Remaining", out var remaining) || !TryLong(h, "X-RateLimit-Reset", out var reset)) return null; var resource = h.TryGetValues("X-RateLimit-Resource", out var values) ? values.FirstOrDefault() ?? "core" : "core"; return new(resource, limit, remaining, FromEpoch(reset)); }
    private static bool TryInt(HttpResponseHeaders h, string name, out int value) { value = 0; return h.TryGetValues(name, out var v) && int.TryParse(v.FirstOrDefault(), out value); }
    private static bool TryLong(HttpResponseHeaders h, string name, out long value) { value = 0; return h.TryGetValues(name, out var v) && long.TryParse(v.FirstOrDefault(), out value); }
    private static DateTimeOffset FromEpoch(long value) => DateTimeOffset.FromUnixTimeSeconds(value);
    private static Repository Map(RepositoryDto r) => new() { GitHubId = r.NodeId, Owner = r.Owner?.Login ?? string.Empty, OwnerAvatarUrl = r.Owner?.AvatarUrl ?? string.Empty, Name = r.Name, HtmlUrl = r.HtmlUrl, Description = r.Description ?? string.Empty, PrimaryLanguage = r.Language ?? "Unknown", Topics = r.Topics ?? [], Homepage = r.Homepage ?? string.Empty, IsPrivate = r.Private, IsArchived = r.Archived, Stars = r.StargazersCount, Forks = r.ForksCount, Watchers = r.WatchersCount, OpenIssues = r.OpenIssuesCount, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt, LastPushedAt = r.PushedAt };
    private static User Map(UserDto u) => new() { GitHubId = u.NodeId, Login = u.Login, AvatarUrl = u.AvatarUrl, Bio = u.Bio ?? string.Empty, Company = u.Company ?? string.Empty, Location = u.Location ?? string.Empty, Blog = u.Blog ?? string.Empty, PublicRepos = u.PublicRepos, Followers = u.Followers, Following = u.Following, CreatedAt = u.CreatedAt };
    public void Dispose() { }
    private sealed record CachedResponse<T>(T Data, string? ETag, string? LastModified, string? NextPageUrl, GitHubRateWindow? RateLimit);

    private sealed record OwnerDto([property: JsonPropertyName("login")] string Login, [property: JsonPropertyName("avatar_url")] string? AvatarUrl);
    private sealed record RepositoryDto([property: JsonPropertyName("node_id")] string NodeId, [property: JsonPropertyName("owner")] OwnerDto? Owner, [property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("html_url")] string HtmlUrl, [property: JsonPropertyName("description")] string? Description, [property: JsonPropertyName("language")] string? Language, [property: JsonPropertyName("topics")] List<string>? Topics, [property: JsonPropertyName("homepage")] string? Homepage, [property: JsonPropertyName("private")] bool Private, [property: JsonPropertyName("archived")] bool Archived, [property: JsonPropertyName("stargazers_count")] int StargazersCount, [property: JsonPropertyName("forks_count")] int ForksCount, [property: JsonPropertyName("watchers_count")] int WatchersCount, [property: JsonPropertyName("open_issues_count")] int OpenIssuesCount, [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt, [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt, [property: JsonPropertyName("pushed_at")] DateTimeOffset? PushedAt);
    private sealed record SearchDto([property: JsonPropertyName("items")] List<RepositoryDto> Items);
    private sealed record UserDto([property: JsonPropertyName("node_id")] string NodeId, [property: JsonPropertyName("login")] string Login, [property: JsonPropertyName("avatar_url")] string AvatarUrl, [property: JsonPropertyName("bio")] string? Bio, [property: JsonPropertyName("company")] string? Company, [property: JsonPropertyName("location")] string? Location, [property: JsonPropertyName("blog")] string? Blog, [property: JsonPropertyName("public_repos")] int PublicRepos, [property: JsonPropertyName("followers")] int Followers, [property: JsonPropertyName("following")] int Following, [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
    private sealed record ReleaseDto([property: JsonPropertyName("id")] long Id, [property: JsonPropertyName("tag_name")] string TagName, [property: JsonPropertyName("name")] string? Name, [property: JsonPropertyName("html_url")] string HtmlUrl, [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt, [property: JsonPropertyName("prerelease")] bool Prerelease, [property: JsonPropertyName("draft")] bool Draft);
    private sealed record ResourceDto([property: JsonPropertyName("limit")] int Limit, [property: JsonPropertyName("remaining")] int Remaining, [property: JsonPropertyName("reset")] long Reset);
    private sealed record ResourcesDto([property: JsonPropertyName("core")] ResourceDto Core, [property: JsonPropertyName("search")] ResourceDto Search);
    private sealed record RateLimitDto([property: JsonPropertyName("resources")] ResourcesDto Resources);
}
