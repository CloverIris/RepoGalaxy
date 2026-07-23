using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

/// <summary>
/// Assigns stable visual weight without using viewport dimensions or runtime randomness.
/// The same account/feed/content key always receives the same span.
/// </summary>
public sealed class TileMosaicPolicy
{
    public const int KnowledgeInterval = 7;

    public TileSpan GetRepositorySpan(Repository repository, FeedSource source)
    {
        var value = Stable($"{source}:{repository.GitHubId}:{repository.FullName}");
        if (repository.Stars >= 100_000)
        {
            return (value % 100) switch
            {
                < 45 => new(2, 2),
                < 80 => new(3, 2),
                _ => new(4, 2)
            };
        }

        return (value % 100) switch
        {
            < 32 => new(6, 1),
            < 52 => new(4, 1),
            < 64 => new(3, 1),
            < 78 => new(2, 2),
            < 90 => new(3, 2),
            _ => new(4, 2)
        };
    }

    public TileSpan GetRankingSpan(int index) => index == 0 ? new(4, 8) : new(2, 8);

    public TipDefinition SelectTip(
        IReadOnlyList<TipDefinition> tips,
        string boardKey,
        int insertionIndex)
    {
        if (tips.Count == 0) throw new InvalidOperationException("Tip catalog is empty.");
        var value = Stable($"{boardKey}:tip:{insertionIndex}");
        return tips[(int)(value % (uint)tips.Count)];
    }

    public TileSpan GetTipSpan(TipDefinition tip, string boardKey, int insertionIndex)
    {
        var options = tip.SpanOptions is { Count: > 0 }
            ? tip.SpanOptions
            : [new(tip.ColumnSpan, tip.RowSpan)];
        var value = Stable($"{boardKey}:{tip.Key}:span:{insertionIndex}");
        return options[(int)(value % (uint)options.Count)];
    }

    private static uint Stable(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        return hash ^ (hash >> 16);
    }
}
