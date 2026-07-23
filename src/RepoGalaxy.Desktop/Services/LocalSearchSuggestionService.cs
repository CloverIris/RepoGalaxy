using RepoGalaxy.Desktop.ViewModels;

namespace RepoGalaxy.Desktop.Services;

public sealed record SearchSuggestion(
    string Key,
    string Kind,
    string Title,
    string Subtitle,
    string Query);

public interface ILocalSearchSuggestionProvider
{
    IReadOnlyList<SearchSuggestion> GetSuggestions(ViewModelBase page, string query, int maximum = 8);
}

public sealed class LocalSearchSuggestionProvider : ILocalSearchSuggestionProvider
{
    public IReadOnlyList<SearchSuggestion> GetSuggestions(ViewModelBase page, string query, int maximum = 8)
    {
        query = query.Trim();
        if (query.Length == 0) return [];
        IEnumerable<SearchSuggestion> candidates = page switch
        {
            DiscoverViewModel discover => discover.Tiles
                .Where(x => x.MatchesSearch(query))
                .OrderByDescending(x => x.IsRepository)
                .Select(x => new SearchSuggestion(
                    x.Key,
                    x.IsRepository ? "仓库" : x.KindLabel,
                    x.Title,
                    x.IsRepository ? $"{x.Language} · {x.Subtitle}" : x.Caption,
                    query)),
            SubscriptionsViewModel subscriptions => subscriptions.Items
                .Where(x => Contains(x.Name, query) || Contains(x.RulesText, query))
                .Select(x => new SearchSuggestion($"subscription:{x.Item.Id}", "订阅", x.Name, x.RulesText, query)),
            LibraryViewModel library => library.Items
                .Where(x => Contains(x.FullName, query) || Contains(x.Description, query))
                .Select(x => new SearchSuggestion($"library:{x.Repository.Id}", "收藏", x.FullName, $"{x.PrimaryLanguage} · {x.Description}", query)),
            NotificationsViewModel notifications => notifications.Items
                .Where(x => Contains(x.Repository.FullName, query) || Contains(x.ReasonText, query))
                .Select(x => new SearchSuggestion($"notification:{x.Id}", "通知", x.Repository.FullName, x.ReasonText, query)),
            MyReposViewModel repositories => repositories.Repositories
                .Where(x => Contains(x.FullName, query) || Contains(x.Description, query))
                .Select(x => new SearchSuggestion($"owned:{x.Repository.Id}", "我的仓库", x.FullName, $"{x.PrimaryLanguage} · {x.Description}", query)),
            LocalReposViewModel local => local.LocalRepositories
                .Where(x => Contains(x.Name, query) || Contains(x.LocalPath, query))
                .Select(x => new SearchSuggestion($"local:{x.Id}", "本地仓库", x.Name, x.LocalPath, query)),
            _ => []
        };
        return candidates.Take(Math.Clamp(maximum, 1, 20)).ToList();
    }

    private static bool Contains(string? value, string query)
        => value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
}
