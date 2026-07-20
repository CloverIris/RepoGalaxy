using System.Net;
using System.Security.Cryptography;
using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using SkiaSharp;

namespace RepoGalaxy.Desktop.Services;

public sealed class MarkdownDocumentService : IMarkdownDocumentService
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoIdentifiers()
        .Build();

    public RepoGalaxy.Core.Models.MarkdownDocument Parse(string markdown, string title, int pageCapacity = 28, string baseUrl = "")
    {
        var source = Markdig.Markdown.Parse(markdown ?? string.Empty, _pipeline);
        var blocks = new List<MarkdownBlock>();
        var headings = new List<MarkdownHeading>();
        foreach (var block in source) AddBlock(block, blocks, headings, baseUrl);
        var pages = Paginate(blocks, Math.Clamp(pageCapacity, 12, 80));
        return new(title, blocks, headings, pages);
    }

    private static void AddBlock(Block block, List<MarkdownBlock> blocks, List<MarkdownHeading> headings, string baseUrl)
    {
        switch (block)
        {
            case HtmlBlock:
                return;
            case HeadingBlock heading:
            {
                var text = InlineText(heading.Inline, blocks, baseUrl);
                var anchor = Slug(text);
                headings.Add(new(anchor, text, heading.Level, blocks.Count));
                blocks.Add(new(MarkdownBlockKind.Heading, text, heading.Level, anchor));
                return;
            }
            case ParagraphBlock paragraph:
            {
                var imageCount = blocks.Count;
                var text = InlineText(paragraph.Inline, blocks, baseUrl);
                if (!string.IsNullOrWhiteSpace(text)) blocks.Insert(imageCount, new(MarkdownBlockKind.Paragraph, text));
                return;
            }
            case QuoteBlock quote:
                blocks.Add(new(MarkdownBlockKind.Quote, ContainerText(quote, baseUrl)));
                return;
            case FencedCodeBlock fenced:
                blocks.Add(new(MarkdownBlockKind.Code, fenced.Lines.ToString() ?? string.Empty, Info: fenced.Info ?? string.Empty));
                return;
            case CodeBlock code:
                blocks.Add(new(MarkdownBlockKind.Code, code.Lines.ToString() ?? string.Empty));
                return;
            case ThematicBreakBlock:
                blocks.Add(new(MarkdownBlockKind.Rule, string.Empty));
                return;
            case ListBlock list:
                foreach (var child in list)
                    if (child is ListItemBlock item)
                    {
                        var text = ContainerText(item, baseUrl);
                        bool? isChecked = FindTaskState(item) ?? (text.StartsWith("[x]", StringComparison.OrdinalIgnoreCase) ? true : text.StartsWith("[ ]", StringComparison.Ordinal) ? false : null);
                        if (isChecked is not null) text = text[3..].TrimStart();
                        blocks.Add(new(MarkdownBlockKind.ListItem, text, list.IsOrdered ? 1 : 0, IsChecked: isChecked));
                    }
                return;
            case Table table:
            {
                var rows = new List<string>();
                foreach (var row in table.OfType<TableRow>())
                    rows.Add(string.Join("  ·  ", row.OfType<TableCell>().Select(x => ContainerText(x, baseUrl))));
                if (rows.Count > 0) blocks.Add(new(MarkdownBlockKind.Table, string.Join(Environment.NewLine, rows)));
                return;
            }
            case ContainerBlock container:
                foreach (var child in container) AddBlock(child, blocks, headings, baseUrl);
                return;
        }
    }

    private static string InlineText(ContainerInline? inline, List<MarkdownBlock>? emittedImages = null, string baseUrl = "")
    {
        if (inline is null) return string.Empty;
        var builder = new StringBuilder();
        void Walk(Inline? item)
        {
            for (; item is not null; item = item.NextSibling)
            {
                switch (item)
                {
                    case HtmlInline: break;
                    case LiteralInline literal: builder.Append(literal.Content); break;
                    case CodeInline code: builder.Append('`').Append(code.Content).Append('`'); break;
                    case LineBreakInline: builder.AppendLine(); break;
                    case LinkInline link when link.IsImage:
                        var imageUrl = ResolveUri(link.Url, baseUrl);
                        if (emittedImages is not null && imageUrl is not null) emittedImages.Add(new(MarkdownBlockKind.Image, string.Empty, Url: imageUrl, AltText: InlineText(link)));
                        else builder.Append(InlineText(link));
                        break;
                    case LinkInline link:
                        var label = InlineText(link);
                        builder.Append(label);
                        if (ResolveUri(link.Url, baseUrl) is { } safeLink) builder.Append(" (").Append(safeLink).Append(')');
                        break;
                    case ContainerInline container: Walk(container.FirstChild); break;
                }
            }
        }
        Walk(inline.FirstChild);
        return WebUtility.HtmlDecode(builder.ToString()).Trim();
    }

    private static string ContainerText(ContainerBlock block, string baseUrl)
    {
        var values = new List<string>();
        foreach (var child in block)
        {
            if (child is LeafBlock leaf && leaf.Inline is not null) values.Add(InlineText(leaf.Inline, baseUrl: baseUrl));
            else if (child is ContainerBlock container) values.Add(ContainerText(container, baseUrl));
        }
        return string.Join(Environment.NewLine, values.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
    }

    private static bool? FindTaskState(ContainerBlock block)
    {
        foreach (var child in block)
        {
            if (child is LeafBlock { Inline: { } inline })
                for (var item = inline.FirstChild; item is not null; item = item.NextSibling)
                    if (item is TaskList task) return task.Checked;
            if (child is ContainerBlock nested && FindTaskState(nested) is { } state) return state;
        }
        return null;
    }

    private static IReadOnlyList<MarkdownPage> Paginate(IReadOnlyList<MarkdownBlock> blocks, int capacity)
    {
        var pages = new List<MarkdownPage>();
        var current = new List<MarkdownBlock>();
        var used = 0;
        string anchor = string.Empty;
        foreach (var block in blocks)
        {
            var weight = Weight(block);
            if (current.Count > 0 && used + weight > capacity)
            {
                pages.Add(new(pages.Count + 1, current.ToArray(), anchor));
                current.Clear(); used = 0; anchor = string.Empty;
            }
            if (block.Kind == MarkdownBlockKind.Heading && string.IsNullOrEmpty(anchor)) anchor = block.Info;
            current.Add(block); used += weight;
        }
        if (current.Count > 0 || pages.Count == 0) pages.Add(new(pages.Count + 1, current.ToArray(), anchor));
        return pages;
    }

    private static int Weight(MarkdownBlock block) => block.Kind switch
    {
        MarkdownBlockKind.Heading => block.Level <= 2 ? 4 : 3,
        MarkdownBlockKind.Image => 10,
        MarkdownBlockKind.Code => Math.Clamp(block.Text.Count(x => x == '\n') + 3, 4, 18),
        MarkdownBlockKind.Table => Math.Clamp(block.Text.Count(x => x == '\n') * 2 + 3, 4, 18),
        _ => Math.Clamp((int)Math.Ceiling(Math.Max(1, block.Text.Length) / 105d), 1, 12)
    };

    private static string? ResolveUri(string? value, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)) return absolute.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(absolute.UserInfo) ? absolute.AbsoluteUri : null;
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var root) && Uri.TryCreate(root, value, out var resolved) && resolved.Scheme == Uri.UriSchemeHttps ? resolved.AbsoluteUri : null;
    }
    private static string Slug(string value)
    {
        var chars = value.ToLowerInvariant().Where(x => char.IsLetterOrDigit(x) || char.IsWhiteSpace(x)).Select(x => char.IsWhiteSpace(x) ? '-' : x).ToArray();
        return new string(chars).Trim('-');
    }
}

public sealed class SafeMarkdownImageService : ISafeMarkdownImageService
{
    private const int MaximumBytes = 5 * 1024 * 1024;
    private readonly IHttpClientFactory _clients;
    private readonly ICacheService _cache;

    public SafeMarkdownImageService(IHttpClientFactory clients, ICacheService cache) { _clients = clients; _cache = cache; }

    public async Task<SafeImageResult?> GetAsync(string url, long documentBudgetBytes, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !await ExternalMetadataSecurity.IsSafeAsync(uri, cancellationToken)) return null;
        var key = CacheKey.Create("markdown-image", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.GetLeftPart(UriPartial.Path)))));
        var cached = await _cache.GetAsync<SafeImageEnvelope>(key, cancellationToken);
        if (cached.Value is { } envelope) return new(envelope.Bytes, envelope.MediaType, cached.FetchedAt ?? DateTimeOffset.UtcNow, cached.State == CacheEntryState.Stale);

        var limit = (int)Math.Min(MaximumBytes, Math.Max(0, documentBudgetBytes));
        if (limit == 0) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var client = _clients.CreateClient("markdown-images");
        for (var redirects = 0; redirects <= 3; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                if (redirects == 3 || response.Headers.Location is null || !Uri.TryCreate(uri, response.Headers.Location, out var next) || !await ExternalMetadataSecurity.IsSafeAsync(next, timeout.Token)) return null;
                uri = next; continue;
            }
            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
            if (!response.IsSuccessStatusCode || !Allowed(mediaType) || response.Content.Headers.ContentLength > limit) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var memory = new MemoryStream();
            var buffer = new byte[16 * 1024];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, timeout.Token);
                if (read == 0) break;
                if (memory.Length + read > limit) return null;
                await memory.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
            var bytes = memory.ToArray();
            if (!CanDecode(bytes)) return null;
            envelope = new(bytes, mediaType);
            await _cache.SetAsync(key, envelope, new CachePolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(30), ["markdown-image", "public"]), timeout.Token);
            return new(bytes, mediaType, DateTimeOffset.UtcNow);
        }
        return null;
    }

    private static bool Allowed(string type) => type is "image/png" or "image/jpeg" or "image/webp" or "image/gif";
    private static bool CanDecode(byte[] bytes)
    {
        try { using var codec = SKCodec.Create(new SKMemoryStream(bytes)); return codec is not null && codec.Info.Width > 0 && codec.Info.Height > 0 && (long)codec.Info.Width * codec.Info.Height <= 32_000_000; }
        catch { return false; }
    }
    private sealed record SafeImageEnvelope(byte[] Bytes, string MediaType);
}
