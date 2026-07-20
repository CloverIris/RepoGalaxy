using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public static class ExternalMetadataSecurity
{
    public const int MaximumBytes = 2 * 1024 * 1024;

    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = ConnectSafelyAsync
    };

    public static async Task<bool> IsSafeAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!IsStructurallySafe(uri)) return false;
        IPAddress[] addresses;
        try { addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken); }
        catch { return false; }
        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    public static bool IsStructurallySafe(Uri uri) => uri.IsAbsoluteUri
        && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.Port == 443
        && !string.IsNullOrWhiteSpace(uri.DnsSafeHost)
        && !IPAddress.TryParse(uri.Host, out _);

    private static async ValueTask<Stream> ConnectSafelyAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new HttpRequestException("external_address_rejected");
        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException) { socket.Dispose(); throw; }
            catch (Exception ex) { socket.Dispose(); lastError = ex; }
        }
        throw new HttpRequestException("external_connection_failed", lastError);
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None)) return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv4MappedToIPv6) return IsPublicAddress(address.MapToIPv4());
            if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal) return false;
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) return false;
            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8) return false;
            return true;
        }
        var b = address.GetAddressBytes();
        if (b[0] is 0 or 10 or 127 || b[0] >= 224) return false;
        if (b[0] == 169 && b[1] == 254) return false;
        if (b[0] == 172 && b[1] is >= 16 and <= 31) return false;
        if (b[0] == 192 && b[1] == 168) return false;
        if (b[0] == 100 && b[1] is >= 64 and <= 127) return false;
        if (b[0] == 198 && b[1] is 18 or 19) return false;
        if (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2) return false;
        if (b[0] == 198 && b[1] == 51 && b[2] == 100) return false;
        if (b[0] == 203 && b[1] == 0 && b[2] == 113) return false;
        return true;
    }
}

public sealed class ExternalMetadataExtractor : IExternalMetadataExtractor
{
    private static readonly Regex MetaRegex = new("<meta\\s+[^>]*(?:name|property)\\s*=\\s*['\"](?<key>[^'\"]+)['\"][^>]*content\\s*=\\s*['\"](?<value>[^'\"]*)['\"][^>]*>|<meta\\s+[^>]*content\\s*=\\s*['\"](?<value2>[^'\"]*)['\"][^>]*(?:name|property)\\s*=\\s*['\"](?<key2>[^'\"]+)['\"][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
    private static readonly Regex TitleRegex = new("<title[^>]*>(?<value>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
    private static readonly Regex LinkRegex = new("<link\\s+[^>]*rel\\s*=\\s*['\"](?<rel>[^'\"]+)['\"][^>]*href\\s*=\\s*['\"](?<href>[^'\"]+)['\"][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
    private static readonly Regex JsonLdRegex = new("<script[^>]+type\\s*=\\s*['\"]application/ld\\+json['\"][^>]*>(?<value>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
    private static readonly Regex ScriptStyleRegex = new("<(script|style|noscript)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
    private readonly IHttpClientFactory _clients;
    private readonly ILazyRefreshCoordinator _refresh;

    public ExternalMetadataExtractor(IHttpClientFactory clients, ILazyRefreshCoordinator refresh) { _clients = clients; _refresh = refresh; }

    public async Task<ExternalMetadata?> ExtractAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !await ExternalMetadataSecurity.IsSafeAsync(uri, cancellationToken)) return null;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        var envelope = await _refresh.GetOrRefreshAsync(new CacheKey($"external-detail:{hash}"),
            new CachePolicy(TimeSpan.FromHours(6), TimeSpan.FromDays(7), ["external-detail", "public"]),
            ct => FetchAsync(uri, ct), cancellationToken);
        return envelope.Metadata;
    }

    private async Task<MetadataEnvelope> FetchAsync(Uri initial, CancellationToken cancellationToken)
    {
        var current = initial;
        for (var redirect = 0; redirect <= 3; redirect++)
        {
            if (!await ExternalMetadataSecurity.IsSafeAsync(current, cancellationToken)) return new(null);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("RepoGalaxy/1.0 (+desktop metadata preview)");
            request.Headers.Accept.ParseAdd("text/html, application/xhtml+xml, application/json;q=0.8");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            using var response = await _clients.CreateClient("external-metadata").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (IsRedirect(response.StatusCode))
            {
                if (redirect == 3 || response.Headers.Location is null) return new(null);
                current = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location);
                continue;
            }
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > ExternalMetadataSecurity.MaximumBytes) return new(null);
            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (mediaType is not ("text/html" or "application/xhtml+xml" or "application/json" or "application/ld+json")) return new(null);
            var text = await ReadLimitedAsync(response.Content, timeout.Token);
            if (text is null) return new(null);
            return new(Parse(text, current, mediaType));
        }
        return new(null);
    }

    private static async Task<string?> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (memory.Length + read > ExternalMetadataSecurity.MaximumBytes) return null;
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var charset = content.Headers.ContentType?.CharSet?.Trim('"');
        Encoding encoding;
        try { encoding = string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset); }
        catch { encoding = Encoding.UTF8; }
        return encoding.GetString(memory.ToArray());
    }

    private static ExternalMetadata Parse(string text, Uri source, string? mediaType)
    {
        if (mediaType is "application/json" or "application/ld+json") return ParseJson(text, source);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in MetaRegex.Matches(text))
        {
            var key = match.Groups["key"].Success ? match.Groups["key"].Value : match.Groups["key2"].Value;
            var value = match.Groups["value"].Success ? match.Groups["value"].Value : match.Groups["value2"].Value;
            if (!string.IsNullOrWhiteSpace(key) && !values.ContainsKey(key)) values[key] = Clean(value, 600);
        }
        var title = Value(values, "og:title") ?? Clean(TitleRegex.Match(text).Groups["value"].Value, 200);
        var description = Value(values, "og:description", "description") ?? string.Empty;
        var site = Value(values, "og:site_name") ?? source.Host;
        var icon = LinkRegex.Matches(text).Cast<Match>().FirstOrDefault(x => x.Groups["rel"].Value.Contains("icon", StringComparison.OrdinalIgnoreCase))?.Groups["href"].Value ?? string.Empty;
        if (Uri.TryCreate(source, icon, out var iconUri) && iconUri.Scheme == Uri.UriSchemeHttps) icon = iconUri.GetLeftPart(UriPartial.Path);
        else icon = string.Empty;
        var summary = ExtractJsonLdSummary(text) ?? Clean(TagRegex.Replace(ScriptStyleRegex.Replace(text, " "), " "), 1600);
        return new(title, description, site, source.GetLeftPart(UriPartial.Path), icon, summary, DateTimeOffset.UtcNow);
    }

    private static ExternalMetadata ParseJson(string text, Uri source)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 24 });
            var root = document.RootElement;
            var title = JsonString(root, "name") ?? JsonString(root, "headline") ?? source.Host;
            var description = JsonString(root, "description") ?? string.Empty;
            return new(Clean(title, 200), Clean(description, 600), source.Host, source.GetLeftPart(UriPartial.Path), string.Empty, Clean(description, 1600), DateTimeOffset.UtcNow);
        }
        catch { return new(source.Host, string.Empty, source.Host, source.GetLeftPart(UriPartial.Path), string.Empty, string.Empty, DateTimeOffset.UtcNow); }
    }

    private static string? ExtractJsonLdSummary(string html)
    {
        foreach (Match match in JsonLdRegex.Matches(html))
        {
            try
            {
                using var document = JsonDocument.Parse(match.Groups["value"].Value, new JsonDocumentOptions { MaxDepth = 24 });
                var description = JsonString(document.RootElement, "description");
                if (!string.IsNullOrWhiteSpace(description)) return Clean(description, 1600);
            }
            catch { }
        }
        return null;
    }

    private static string? JsonString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) { var value = JsonString(item, name); if (value is not null) return value; }
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String) return property.GetString();
        return null;
    }

    private static string? Value(Dictionary<string, string> values, params string[] keys) => keys.Select(x => values.GetValueOrDefault(x)).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    private static string Clean(string value, int maximum) { var result = WebUtility.HtmlDecode(Regex.Replace(value ?? string.Empty, "\\s+", " ")).Trim(); return result.Length <= maximum ? result : result[..maximum] + "…"; }
    private static bool IsRedirect(HttpStatusCode code) => (int)code is 301 or 302 or 303 or 307 or 308;
    private sealed record MetadataEnvelope(ExternalMetadata? Metadata);
}
