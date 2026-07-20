namespace RepoGalaxy.Core.Models;

public enum DetailTargetKind
{
    Repository,
    Organization,
    Language,
    Framework,
    ExternalProject,
    Ranking,
    Tip
}

public enum DetailLoadState { Baseline, Loading, Ready, Stale, Failed }

public sealed record DetailTarget(
    string ContentKey,
    DetailTargetKind Kind,
    string Title,
    string Subtitle,
    string SourceUrl,
    string AccentKey,
    long? RepositoryId = null,
    string Owner = "",
    string Name = "",
    string Caption = "",
    IReadOnlyList<DetailFact>? BaselineFacts = null);

public sealed record DetailFact(string Label, string Value);

public sealed record DetailSection(
    string Key,
    string Title,
    string Summary,
    IReadOnlyList<DetailFact> Facts,
    string SourceUrl = "",
    string Markdown = "");

public sealed record DetailSnapshot(
    DetailTarget Target,
    DetailLoadState State,
    string Title,
    string Description,
    string SourceName,
    string SourceUrl,
    IReadOnlyList<DetailSection> Sections,
    DateTimeOffset? FetchedAt = null,
    string StatusText = "");

public sealed record ExternalMetadata(
    string Title,
    string Description,
    string SiteName,
    string CanonicalUrl,
    string IconUrl,
    string Summary,
    DateTimeOffset FetchedAt);
