namespace RepoGalaxy.Core.Models;

public enum MarkdownBlockKind { Heading, Paragraph, ListItem, Quote, Code, Table, Image, Rule }

public sealed record MarkdownBlock(
    MarkdownBlockKind Kind,
    string Text,
    int Level = 0,
    string Info = "",
    string Url = "",
    string AltText = "",
    bool? IsChecked = null);

public sealed record MarkdownHeading(string Id, string Text, int Level, int BlockIndex);

public sealed record MarkdownPage(int Number, IReadOnlyList<MarkdownBlock> Blocks, string Anchor = "");

public sealed record MarkdownDocument(
    string Title,
    IReadOnlyList<MarkdownBlock> Blocks,
    IReadOnlyList<MarkdownHeading> Headings,
    IReadOnlyList<MarkdownPage> Pages);

public sealed record SafeImageResult(byte[] Bytes, string MediaType, DateTimeOffset FetchedAt, bool IsStale = false);

public enum IdeFamily { VisualStudio, VisualStudioCode, Rider, CLion, IntelliJIdea, PyCharm, WebStorm, GoLand, RustRover }

[Flags]
public enum IdeCapability { None = 0, OpenFolder = 1, OpenSolution = 2, OpenProject = 4 }

public sealed record LocalIdeDescriptor(
    string Key,
    string DisplayName,
    IdeFamily Family,
    string Version,
    string ExecutablePath,
    IdeCapability Capabilities,
    int RecommendationRank = 0);

public enum CloneMode { Full, Shallow }
public enum CloneOperationState { Preparing, Cloning, Finalizing, Completed, Cancelled, Failed }

public sealed record CloneRequest(
    long RepositoryId,
    string Owner,
    string Name,
    string CloneUrl,
    string ParentDirectory,
    CloneMode Mode,
    LocalIdeDescriptor Ide);

public sealed record CloneProgress(
    CloneOperationState State,
    double? Percentage,
    string Message,
    string ErrorCode = "",
    string LocalPath = "");

public sealed record CloneResult(bool Success, string LocalPath, string ErrorCode = "", string Message = "");
