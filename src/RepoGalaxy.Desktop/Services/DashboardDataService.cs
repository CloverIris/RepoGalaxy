using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Desktop.Services;

public sealed record DashboardSnapshot(IReadOnlyList<DashboardListItem> Growth, IReadOnlyList<DashboardListItem> Rookies, IReadOnlyList<DashboardListItem> Local, IReadOnlyList<ContributionDay> Contributions, IReadOnlyList<NewsArticle> Releases, IReadOnlyList<NewsArticle> News, int Streak, int WeekCommits);

public sealed class DashboardDataService
{
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly ILazyRefreshCoordinator _refresh;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<DashboardDataService> _logger;

    public DashboardDataService(
        IDbContextFactory<RepoGalaxyDbContext> factory,
        ILazyRefreshCoordinator refresh,
        IHttpClientFactory http,
        ILogger<DashboardDataService> logger)
    {
        _factory = factory;
        _refresh = refresh;
        _http = http;
        _logger = logger;
    }

    public async Task<DashboardSnapshot> LoadAsync(CancellationToken ct = default)
    {
        var emptyLists = (
            Growth: (IReadOnlyList<DashboardListItem>)[],
            Rookies: (IReadOnlyList<DashboardListItem>)[],
            Local: (IReadOnlyList<DashboardListItem>)[]);
        var lists = await TryLoadAsync("榜单", LoadListsAsync, emptyLists, ct);
        var contributions = await TryLoadAsync("本地贡献", token => _refresh.GetOrRefreshAsync(
            CacheKey.Create("dashboard", "contributions"),
            new CachePolicy(TimeSpan.FromMinutes(10), TimeSpan.FromDays(7), ["local"]),
            ScanContributionsAsync,
            token), (IReadOnlyList<ContributionDay>)[], ct);
        var news = await TryLoadAsync("官方资讯", token => _refresh.GetOrRefreshAsync(
            CacheKey.Create("dashboard", "official-news"),
            new CachePolicy(TimeSpan.FromMinutes(30), TimeSpan.FromDays(7), ["news"]),
            LoadNewsAsync,
            token), (IReadOnlyList<NewsArticle>)[], ct);
        var releases = await TryLoadAsync("Release", LoadReleasesAsync, (IReadOnlyList<NewsArticle>)[], ct);
        var byDate = contributions.ToDictionary(x => x.Date, x => x.Count); var today = DateOnly.FromDateTime(DateTime.Today); var streak = 0; for (var day = today; byDate.GetValueOrDefault(day) > 0; day = day.AddDays(-1)) streak++;
        var week = contributions.Where(x => x.Date >= today.AddDays(-6)).Sum(x => x.Count);
        return new(lists.Growth, lists.Rookies, lists.Local, contributions, releases, news, streak, week);
    }

    private async Task<T> TryLoadAsync<T>(string section, Func<CancellationToken, Task<T>> loader, T fallback, CancellationToken ct)
    {
        try { return await loader(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "工作台分区 {Section} 加载失败", section);
            return fallback;
        }
    }
    private async Task<IReadOnlyList<NewsArticle>> LoadReleasesAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.FeedItems.AsNoTracking().Include(x => x.Repository).Where(x => x.Source == (int)FeedSource.Release).ToListAsync(ct);
        return rows.OrderByDescending(x => x.DiscoveredAt).Take(5).Select(x => new NewsArticle(x.Id, $"{x.Repository.Owner}/{x.Repository.Name}", x.Reason, (x.Repository.HtmlUrl ?? string.Empty).TrimEnd('/') + "/releases", "收藏仓库 Release", x.DiscoveredAt)).ToList();
    }
    private async Task<(IReadOnlyList<DashboardListItem> Growth, IReadOnlyList<DashboardListItem> Rookies, IReadOnlyList<DashboardListItem> Local)> LoadListsAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct); var today = DateOnly.FromDateTime(DateTime.UtcNow); var repos = await db.Repositories.AsNoTracking().Where(x => !x.IsIgnored && !x.IsPrivate).OrderByDescending(x => x.Stars).Take(500).ToListAsync(ct);
        var ids = repos.Select(x => x.Id).ToList(); var existing = await db.RepositoryMetricSnapshots.Where(x => x.SnapshotDate == today && ids.Contains(x.RepositoryId)).Select(x => x.RepositoryId).ToListAsync(ct);
        foreach (var repo in repos.Where(x => !existing.Contains(x.Id))) db.RepositoryMetricSnapshots.Add(new RepositoryMetricSnapshotEntity { RepositoryId = repo.Id, SnapshotDate = today, Stars = repo.Stars, Forks = repo.Forks, RepositoryUpdatedAt = repo.UpdatedAt }); await db.SaveChangesAsync(ct);
        var previousRows = await db.RepositoryMetricSnapshots.AsNoTracking()
            .Where(x => x.SnapshotDate < today && ids.Contains(x.RepositoryId))
            .Select(x => new { x.RepositoryId, x.SnapshotDate, x.Stars })
            .ToListAsync(ct);
        var previous = previousRows.GroupBy(x => x.RepositoryId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.SnapshotDate).First().Stars);
        var growth = repos.Select(x => new { Repo = x, Delta = x.Stars - previous.GetValueOrDefault(x.Id, x.Stars) }).OrderByDescending(x => x.Delta).ThenByDescending(x => x.Repo.Stars).Take(5).Select((x, i) => Item(x.Repo, previous.Count == 0 ? "正在积累 24 小时趋势" : $"+{x.Delta:N0} Stars", i)).ToList();
        var rookieCutoff = DateTimeOffset.UtcNow.AddDays(-30); var rookie = repos.Where(x => x.CreatedAt >= rookieCutoff).OrderByDescending(x => x.Stars).Take(5).Select((x, i) => Item(x, $"创建 {Math.Max(0, (DateTimeOffset.UtcNow - x.CreatedAt).Days)} 天 · ★ {x.Stars:N0}", i)).ToList();
        var localLanguages = DetectLocalLanguages(await db.LocalRepositories.AsNoTracking().Select(x => x.LocalPath).ToListAsync(ct)); var local = repos.Where(x => localLanguages.Contains(x.PrimaryLanguage ?? string.Empty)).OrderByDescending(x => x.DiscoveryScore).ThenByDescending(x => x.Stars).Take(5).Select((x, i) => Item(x, $"匹配本地 {x.PrimaryLanguage} 技术栈", i)).ToList();
        return (growth, rookie, local);
    }
    private async Task<IReadOnlyList<ContributionDay>> ScanContributionsAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct); var repositories = await db.LocalRepositories.AsNoTracking().Where(x => x.IsTracked).ToListAsync(ct); var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var globalEmail = await RunGitAsync(null, ["config", "--global", "--get", "user.email"], ct); if (!string.IsNullOrWhiteSpace(globalEmail)) emails.Add(globalEmail.Trim());
        var globalName = await RunGitAsync(null, ["config", "--global", "--get", "user.name"], ct); if (!string.IsNullOrWhiteSpace(globalName)) names.Add(globalName.Trim());
        foreach (var alias in await db.GitIdentityAliases.AsNoTracking().Where(x => x.IsEnabled).ToListAsync(ct)) { if (!string.IsNullOrWhiteSpace(alias.Email)) emails.Add(alias.Email); if (!string.IsNullOrWhiteSpace(alias.Name)) names.Add(alias.Name); }
        var totals = new Dictionary<DateOnly, int>();
        await db.LocalContributionDays.ExecuteDeleteAsync(ct);
        foreach (var repository in repositories.Where(x => Directory.Exists(x.LocalPath)))
        {
            var repositoryEmails = new HashSet<string>(emails, StringComparer.OrdinalIgnoreCase); var repositoryNames = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            var localEmail = await RunGitAsync(repository.LocalPath, ["config", "--get", "user.email"], ct); if (!string.IsNullOrWhiteSpace(localEmail)) repositoryEmails.Add(localEmail.Trim());
            var localName = await RunGitAsync(repository.LocalPath, ["config", "--get", "user.name"], ct); if (!string.IsNullOrWhiteSpace(localName)) repositoryNames.Add(localName.Trim());
            if (repositoryEmails.Count == 0 && repositoryNames.Count == 0) continue;
            var output = await RunGitAsync(repository.LocalPath, ["log", "--since=365.days", "--format=%aI|%an|%ae"], ct); if (output is null) continue;
            var perRepository = new Dictionary<DateOnly, int>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) { var parts = line.Split('|', 3); if (parts.Length != 3 || !repositoryEmails.Contains(parts[2]) && !repositoryNames.Contains(parts[1]) || !DateTimeOffset.TryParse(parts[0], out var time)) continue; var date = DateOnly.FromDateTime(time.LocalDateTime); totals[date] = totals.GetValueOrDefault(date) + 1; perRepository[date] = perRepository.GetValueOrDefault(date) + 1; }
            foreach (var day in perRepository) db.LocalContributionDays.Add(new LocalContributionDayEntity { LocalRepositoryId = repository.Id, Date = day.Key, CommitCount = day.Value });
        }
        var start = DateOnly.FromDateTime(DateTime.Today.AddDays(-364)); var result = Enumerable.Range(0, 365).Select(i => new ContributionDay(start.AddDays(i), totals.GetValueOrDefault(start.AddDays(i)))).ToList();
        await db.SaveChangesAsync(ct); return result;
    }
    private async Task<IReadOnlyList<NewsArticle>> LoadNewsAsync(CancellationToken ct)
    {
        var sources = new[] { ("GitHub Changelog", "https://github.blog/changelog/feed/"), ("GitHub Open Source", "https://github.blog/feed/") }; var result = new List<NewsArticle>();
        foreach (var source in sources)
        {
            using var response = await _http.CreateClient().GetAsync(source.Item2, ct); response.EnsureSuccessStatusCode(); await using var stream = await response.Content.ReadAsStreamAsync(ct); using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }); var document = await XDocument.LoadAsync(reader, LoadOptions.None, ct);
            var entries = document.Descendants().Where(x => x.Name.LocalName is "item" or "entry").Take(8);
            foreach (var item in entries) { var title = item.Elements().FirstOrDefault(x => x.Name.LocalName == "title")?.Value ?? "GitHub 更新"; var linkValue = item.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Attribute("href")?.Value ?? item.Elements().FirstOrDefault(x => x.Name.LocalName == "link")?.Value; if (!Uri.TryCreate(linkValue, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || !IsOfficialBlogHost(url.Host)) continue; var body = item.Elements().FirstOrDefault(x => x.Name.LocalName is "description" or "summary" or "content")?.Value ?? string.Empty; var dateValue = item.Elements().FirstOrDefault(x => x.Name.LocalName is "pubDate" or "published" or "updated")?.Value; _ = DateTimeOffset.TryParse(dateValue, out var published); result.Add(new NewsArticle(StableId(url.AbsoluteUri), title, StripMarkup(body), url.AbsoluteUri, source.Item1, published)); }
        }
        var news = result.OrderByDescending(x => x.PublishedAt).DistinctBy(x => x.Url).Take(8).ToList();
        await using var db = await _factory.CreateDbContextAsync(ct);
        foreach (var article in news)
        {
            var entity = await db.NewsItems.FirstOrDefaultAsync(x => x.ExternalId == article.Url, ct);
            if (entity is null) { entity = new NewsItemEntity { ExternalId = article.Url }; db.NewsItems.Add(entity); }
            entity.Title = article.Title; entity.Summary = article.Summary; entity.Url = article.Url; entity.Source = article.Source; entity.PublishedAt = article.PublishedAt; entity.FetchedAt = DateTimeOffset.UtcNow;
        }
        var retentionCutoff = DateTimeOffset.UtcNow.AddDays(-30);
        await db.NewsItems.Where(x => x.FetchedAt < retentionCutoff).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        return news;
    }
    private static async Task<string?> RunGitAsync(string? path, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        try { using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10)); var info = new ProcessStartInfo { FileName = "git", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }; if (path is not null) { info.ArgumentList.Add("-C"); info.ArgumentList.Add(path); } foreach (var argument in arguments) info.ArgumentList.Add(argument); using var process = Process.Start(info); if (process is null) return null; var output = process.StandardOutput.ReadToEndAsync(timeout.Token); await process.WaitForExitAsync(timeout.Token); return process.ExitCode == 0 ? await output : null; } catch { return null; }
    }
    private static HashSet<string> DetectLocalLanguages(IEnumerable<string> paths) { var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach (var path in paths.Where(Directory.Exists)) { try { if (Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories).Take(1).Any()) result.Add("C#"); if (File.Exists(Path.Combine(path, "package.json"))) result.Add("TypeScript"); if (File.Exists(Path.Combine(path, "Cargo.toml"))) result.Add("Rust"); if (File.Exists(Path.Combine(path, "go.mod"))) result.Add("Go"); if (File.Exists(Path.Combine(path, "pyproject.toml")) || File.Exists(Path.Combine(path, "requirements.txt"))) result.Add("Python"); } catch { } } return result; }
    private static DashboardListItem Item(RepositoryEntity x, string caption, int index) => new(x.Id, $"{x.Owner}/{x.Name}", caption, index + 1);
    private static bool IsOfficialBlogHost(string host) => host.Equals("github.blog", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".github.blog", StringComparison.OrdinalIgnoreCase);
    private static long StableId(string value) => (long)(BitConverter.ToUInt64(SHA256.HashData(Encoding.UTF8.GetBytes(value)), 0) & long.MaxValue);
    private static string StripMarkup(string value) { var text = WebUtility.HtmlDecode(Regex.Replace(value, "<[^>]+>", " ")); text = Regex.Replace(text, "\\s+", " ").Trim(); return text.Length > 140 ? text[..140] + "…" : text; }
}
