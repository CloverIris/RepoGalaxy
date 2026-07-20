using System.Text.RegularExpressions;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

/// <summary>Builds the small, high-signal navigation index shown at the far zoom level.</summary>
public sealed class SemanticIndexCatalogService : ISemanticIndexCatalogService
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public SemanticIndexCatalogResult Build(IReadOnlyList<SemanticIndexSignal> signals, SemanticIndexPolicy? policy = null)
    {
        policy ??= new SemanticIndexPolicy();
        var rejected = 0;
        var entries = new Dictionary<(SemanticIndexKind Kind, string Key), Aggregate>();

        foreach (var signal in signals)
        {
            var title = Normalize(signal.Title);
            if (!IsValid(title, policy.MaximumTitleLength)) { rejected++; continue; }
            var key = (signal.Kind, title.ToLowerInvariant());
            if (!entries.TryGetValue(key, out var aggregate)) entries[key] = aggregate = new(title, signal.Kind);
            if (!string.IsNullOrWhiteSpace(signal.ContentKey)) aggregate.ContentKeys.Add(signal.ContentKey);
            if (signal.Origin == SemanticIndexSignalOrigin.Feed && !string.IsNullOrWhiteSpace(signal.ContentKey)) aggregate.RepositoryKeys.Add(signal.ContentKey);
            if (signal.Origin is SemanticIndexSignalOrigin.Subscription or SemanticIndexSignalOrigin.LocalRepository) aggregate.IsPinned = true;
            if (signal.Origin == SemanticIndexSignalOrigin.DedicatedTile) aggregate.HasDedicatedTile = true;
            aggregate.HasOfficialLink |= signal.HasOfficialLink;
            if (!string.IsNullOrWhiteSpace(signal.AssociatedLanguage))
                aggregate.LanguageVotes[signal.AssociatedLanguage.Trim()] = aggregate.LanguageVotes.GetValueOrDefault(signal.AssociatedLanguage.Trim()) + 1;
        }

        var languages = Select(entries.Values.Where(x => x.Kind == SemanticIndexKind.Language), policy.MaximumLanguages, minimumRepositories: 0);
        var frameworks = Select(entries.Values.Where(x => x.Kind == SemanticIndexKind.Framework), policy.MaximumFrameworks, policy.MinimumFrameworkRepositories);
        var items = languages.Concat(frameworks)
            .OrderBy(x => x.Kind).ThenByDescending(Score).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .Select(ToItem).ToList();
        return new(items, rejected);
    }

    private static IEnumerable<Aggregate> Select(IEnumerable<Aggregate> values, int maximum, int minimumRepositories) => values
        .Where(x => x.IsPinned || x.HasDedicatedTile || x.HasOfficialLink || x.RepositoryKeys.Count >= minimumRepositories)
        .OrderByDescending(x => x.IsPinned)
        .ThenByDescending(x => x.RepositoryKeys.Count)
        .ThenByDescending(x => x.HasDedicatedTile)
        .ThenByDescending(x => x.HasOfficialLink)
        .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
        .Take(Math.Max(0, maximum));

    private static int Score(Aggregate value) =>
        (value.IsPinned ? 1_000_000 : 0) + value.RepositoryKeys.Count * 10_000 + (value.HasDedicatedTile ? 1_000 : 0) + (value.HasOfficialLink ? 100 : 0);

    private static SemanticIndexItem ToItem(Aggregate value)
    {
        var accent = value.Kind == SemanticIndexKind.Language
            ? value.Title
            : value.LanguageVotes.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault().Key ?? string.Empty;
        return new($"{value.Kind}:{value.Title.ToLowerInvariant()}", value.Title, value.Kind, value.RepositoryKeys.Count, accent, value.ContentKeys.Order(StringComparer.Ordinal).ToList());
    }

    private static string Normalize(string value) => Whitespace.Replace(value?.Trim() ?? string.Empty, " ");
    private static bool IsValid(string value, int maximumLength) => value.Length is > 0 && value.Length <= maximumLength
        && !value.All(char.IsDigit)
        && !(Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Scheme));

    private sealed class Aggregate(string title, SemanticIndexKind kind)
    {
        public string Title { get; } = title;
        public SemanticIndexKind Kind { get; } = kind;
        public HashSet<string> ContentKeys { get; } = new(StringComparer.Ordinal);
        public HashSet<string> RepositoryKeys { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> LanguageVotes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsPinned { get; set; }
        public bool HasDedicatedTile { get; set; }
        public bool HasOfficialLink { get; set; }
    }
}
