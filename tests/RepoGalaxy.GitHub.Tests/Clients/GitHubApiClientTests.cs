using System.Net;
using System.Text;
using FluentAssertions;
using RepoGalaxy.GitHub.Clients;
using RepoGalaxy.GitHub.Services;
using RepoGalaxy.Core.Interfaces;
using Xunit;

namespace RepoGalaxy.GitHub.Tests.Clients;

public sealed class GitHubApiClientTests
{
    [Fact]
    public async Task Search_maps_the_list_response_without_detail_amplification_and_captures_next_page()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.OK,
            """{"items":[{"node_id":"node-1","owner":{"login":"avaloniaui"},"name":"avalonia","html_url":"https://github.com/AvaloniaUI/Avalonia","description":"UI","language":"C#","topics":["ui"],"private":false,"archived":false,"stargazers_count":30000,"forks_count":2500,"watchers_count":30000,"open_issues_count":100,"created_at":"2014-01-01T00:00:00Z","updated_at":"2026-07-20T00:00:00Z"}]}""",
            ("Link", "<https://api.github.com/search/repositories?q=ui&page=2>; rel=\"next\"")));
        using var orchestrator = new SyncOrchestrator();
        var budget = new GitHubRequestBudget();
        var client = Client(handler, budget, orchestrator);

        var page = await client.SearchRepositoriesPageAsync("ui", "C#", "stars");

        page.Items.Should().ContainSingle(x => x.FullName == "avaloniaui/avalonia");
        page.NextPageUrl.Should().Contain("page=2");
        handler.Requests.Should().ContainSingle();
        handler.Requests.Should().NotContain(x => x.AbsolutePath.Contains("languages", StringComparison.OrdinalIgnoreCase));
        handler.ApiVersions.Should().OnlyContain(x => x == "2026-03-10");
    }

    [Fact]
    public async Task Rate_headers_stop_the_matching_search_budget_on_forbidden_response()
    {
        var handler = new RecordingHandler(_ => Response(HttpStatusCode.Forbidden, "{}", ("X-RateLimit-Limit", "30"), ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString()), ("X-RateLimit-Resource", "search")));
        using var orchestrator = new SyncOrchestrator();
        var budget = new GitHubRequestBudget();
        var client = Client(handler, budget, orchestrator);

        (await client.SearchRepositoriesAsync("avalonia")).Should().BeEmpty();

        budget.CanSearch(out var resetAt).Should().BeFalse();
        resetAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Stale_get_uses_etag_and_reuses_cached_body_on_304()
    {
        var call = 0;
        var handler = new RecordingHandler(request =>
        {
            call++;
            if (call == 1) return Response(HttpStatusCode.OK, """{"items":[]}""", ("ETag", "\"feed-v1\""));
            request.Headers.IfNoneMatch.Should().ContainSingle(x => x.Tag == "\"feed-v1\"");
            return Response(HttpStatusCode.NotModified, string.Empty);
        });
        var cache = new AlwaysStaleCache();
        using var orchestrator = new SyncOrchestrator();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var client = new GitHubApiClient(http, new GitHubRequestBudget(), orchestrator, cache);

        _ = await client.SearchRepositoriesPageAsync("ui");
        var second = await client.SearchRepositoriesPageAsync("ui");

        second.Items.Should().BeEmpty();
        call.Should().Be(2);
    }

    private static GitHubApiClient Client(HttpMessageHandler handler, GitHubRequestBudget budget, SyncOrchestrator orchestrator)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RepoGalaxy.Tests/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        return new GitHubApiClient(http, budget, orchestrator);
    }
    private static HttpResponseMessage Response(HttpStatusCode status, string content, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers) response.Headers.TryAddWithoutValidation(name, value);
        return response;
    }
    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        public List<string?> ApiVersions { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            ApiVersions.Add(request.Headers.TryGetValues("X-GitHub-Api-Version", out var values) ? values.Single() : null);
            return Task.FromResult(response(request));
        }
    }
    private sealed class AlwaysStaleCache : ICacheService
    {
        private readonly Dictionary<string, object> _values = [];
        public Task<CacheReadResult<T>> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default) => Task.FromResult(_values.TryGetValue(key.Value, out var value) ? new CacheReadResult<T>(CacheEntryState.Stale, (T)value) : CacheReadResult<T>.Miss());
        public Task SetAsync<T>(CacheKey key, T value, CachePolicy policy, CancellationToken cancellationToken = default) { _values[key.Value] = value!; return Task.CompletedTask; }
        public Task SetValidatedAsync<T>(CacheKey key, T value, CachePolicy policy, string? etag, string? lastModified, CancellationToken cancellationToken = default) => SetAsync(key, value, policy, cancellationToken);
        public Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default) { _values.Remove(key.Value); return Task.CompletedTask; }
        public Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new CacheStatistics(0, 0, 0, 0, 0, null));
        public Task<long> PruneAsync(long persistentSizeLimitBytes, CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public void SetPersistentSizeLimit(long bytes) { }
    }
}
