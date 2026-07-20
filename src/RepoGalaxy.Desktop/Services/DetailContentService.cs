using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public sealed class DetailContentService : IDetailContentService
{
    private readonly IGitHubClient _github;
    private readonly IExternalMetadataExtractor _external;

    public DetailContentService(IGitHubClient github, IExternalMetadataExtractor external) { _github = github; _external = external; }

    public DetailSnapshot CreateBaseline(DetailTarget target)
    {
        var sections = new List<DetailSection>
        {
            new("overview", "概览", target.Subtitle, target.BaselineFacts ?? [], target.SourceUrl),
            new("reason", "推荐依据", target.Caption, [], target.SourceUrl)
        };
        return new(target, DetailLoadState.Baseline, target.Title, target.Subtitle, SourceName(target.SourceUrl), target.SourceUrl, sections, StatusText: "正在准备结构化详情");
    }

    public async Task<DetailSnapshot> LoadAsync(DetailTarget target, CancellationToken cancellationToken = default)
    {
        try
        {
            return target.Kind == DetailTargetKind.Repository && !string.IsNullOrWhiteSpace(target.Owner) && !string.IsNullOrWhiteSpace(target.Name)
                ? await LoadRepositoryAsync(target, cancellationToken)
                : await LoadExternalAsync(target, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch { return CreateBaseline(target) with { State = DetailLoadState.Failed, StatusText = "详情暂时不可用，继续显示本地信息" }; }
    }

    private async Task<DetailSnapshot> LoadRepositoryAsync(DetailTarget target, CancellationToken cancellationToken)
    {
        var repositoryTask = Safe(() => _github.GetRepositoryAsync(target.Owner, target.Name, cancellationToken));
        var languagesTask = Safe(() => _github.GetLanguagesAsync(target.Owner, target.Name, cancellationToken), new List<LanguageInfo>());
        var readmeTask = Safe(() => _github.GetReadmeAsync(target.Owner, target.Name, cancellationToken));
        var releaseTask = Safe(() => _github.GetLatestReleaseAsync(target.Owner, target.Name, cancellationToken));
        await Task.WhenAll(repositoryTask, languagesTask, readmeTask, releaseTask);
        var repository = repositoryTask.Result;
        var languages = languagesTask.Result;
        var release = releaseTask.Result;
        var sections = new List<DetailSection>();
        var facts = new List<DetailFact>(target.BaselineFacts ?? []);
        if (repository is not null)
        {
            facts.AddRange([
                new("Stars", repository.Stars.ToString("N0")),
                new("Forks", repository.Forks.ToString("N0")),
                new("Issues", repository.OpenIssues.ToString("N0")),
                new("更新时间", repository.UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd"))]);
            sections.Add(new("overview", "仓库概览", repository.Description, facts, repository.HtmlUrl));
            sections.Add(new("topics", "主题", string.Join(" · ", repository.Topics), [], repository.HtmlUrl));
        }
        else sections.Add(new("overview", "仓库概览", target.Subtitle, facts, target.SourceUrl));
        if (languages.Count > 0) sections.Add(new("languages", "语言构成", string.Join(" · ", languages.Take(8).Select(x => $"{x.Name} {x.Percentage:P0}")), languages.Take(8).Select(x => new DetailFact(x.Name, x.Percentage.ToString("P1"))).ToList(), target.SourceUrl));
        if (!string.IsNullOrWhiteSpace(readmeTask.Result)) sections.Add(new("readme", "README", "使用分页阅读完整 README；滚轮继续用于退出详情。", [], target.SourceUrl, readmeTask.Result!));
        if (release is not null) sections.Add(new("release", "最新正式 Release", $"{release.Name} · {release.PublishedAt.LocalDateTime:yyyy-MM-dd}", [new("版本", release.TagName)], release.HtmlUrl));
        return new(target, DetailLoadState.Ready, repository?.FullName ?? target.Title, repository?.Description ?? target.Subtitle, "GitHub", repository?.HtmlUrl ?? target.SourceUrl, sections, DateTimeOffset.UtcNow, "结构化详情已更新");
    }

    private async Task<DetailSnapshot> LoadExternalAsync(DetailTarget target, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.SourceUrl)) return CreateBaseline(target) with { State = DetailLoadState.Ready, StatusText = "这是本地聚合详情" };
        var metadata = await _external.ExtractAsync(target.SourceUrl, cancellationToken);
        if (metadata is null) return CreateBaseline(target) with { State = DetailLoadState.Failed, StatusText = "远程页面不可用，继续显示本地信息" };
        var sections = new List<DetailSection>
        {
            new("overview", "页面概览", string.IsNullOrWhiteSpace(metadata.Description) ? target.Subtitle : metadata.Description, target.BaselineFacts ?? [], metadata.CanonicalUrl),
            new("summary", "内容摘要", metadata.Summary, [], metadata.CanonicalUrl),
            new("related", "当前信号", target.Caption, [], target.SourceUrl)
        };
        return new(target, DetailLoadState.Ready, string.IsNullOrWhiteSpace(metadata.Title) ? target.Title : metadata.Title, target.Subtitle, metadata.SiteName, metadata.CanonicalUrl, sections, metadata.FetchedAt, "安全静态内容已更新");
    }

    private static async Task<T?> Safe<T>(Func<Task<T?>> operation) where T : class { try { return await operation(); } catch (OperationCanceledException) { throw; } catch { return null; } }
    private static async Task<T> Safe<T>(Func<Task<T>> operation, T fallback) { try { return await operation(); } catch (OperationCanceledException) { throw; } catch { return fallback; } }
    private static string SourceName(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "RepoGalaxy";
}
