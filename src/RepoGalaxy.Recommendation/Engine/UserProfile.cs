using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Recommendation.Engine;

/// <summary>Compatibility profile used by the feature-level unit tests and legacy scoring helpers.</summary>
public sealed class UserProfile
{
    public List<string> InterestedTopics { get; set; } = [];
    public List<string> InterestedLanguages { get; set; } = [];
    public List<Repository> BookmarkedRepos { get; set; } = [];
    public List<Repository> ViewedRepos { get; set; } = [];
    public int MinStars { get; set; }
    public int MaxStars { get; set; } = 1_000_000;
    public bool PreferFreshContent { get; set; } = true;
    public bool PreferSmallProjects { get; set; }
    public Dictionary<string, double> TopicWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> LanguageWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
