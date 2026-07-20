using Avalonia.Media.Imaging;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using SkiaSharp;

namespace RepoGalaxy.Desktop.Services;

public sealed record TileImageAsset(Bitmap Bitmap, string DominantColor);

public interface ITileImageService
{
    Task<TileImageAsset?> GetAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class TileImageService : ITileImageService
{
    private const int MaximumBytes = 512 * 1024;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICacheService _cache;

    public TileImageService(IHttpClientFactory httpClientFactory, ICacheService cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<TileImageAsset?> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!TryValidate(url, out var uri)) return null;
        var key = CacheKey.Create("tile-avatar", uri.AbsolutePath);
        var cached = await _cache.GetAsync<byte[]>(key, cancellationToken);
        var bytes = cached.Value;
        if (bytes is null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await _httpClientFactory.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaximumBytes || !IsImage(response.Content.Headers.ContentType?.MediaType)) return null;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var memory = new MemoryStream();
            var buffer = new byte[16 * 1024];
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                if (memory.Length + read > MaximumBytes) return null;
                await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            bytes = memory.ToArray();
            if (!CanDecode(bytes)) return null;
            await _cache.SetAsync(key, bytes, new CachePolicy(TimeSpan.FromDays(7), TimeSpan.FromDays(30), ["tile-avatar", "public"]), cancellationToken);
        }

        try
        {
            var bitmap = new Bitmap(new MemoryStream(bytes, writable: false));
            return new(bitmap, Dominant(bytes));
        }
        catch { return null; }
    }

    public static bool TryValidate(string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            && candidate.Scheme == Uri.UriSchemeHttps
            && (candidate.Host.Equals("avatars.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                || candidate.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
        {
            uri = candidate;
            return true;
        }
        uri = null!;
        return false;
    }

    private static bool IsImage(string? mediaType) => mediaType is "image/png" or "image/jpeg" or "image/webp";
    private static bool CanDecode(byte[] bytes) { try { using var image = SKBitmap.Decode(bytes); return image is not null && image.Width > 0 && image.Height > 0; } catch { return false; } }
    private static string Dominant(byte[] bytes)
    {
        try
        {
            using var source = SKBitmap.Decode(bytes);
            if (source is null) return "#2D5F8B";
            using var scaled = source.Resize(new SKImageInfo(24, 24), SKSamplingOptions.Default);
            if (scaled is null) return "#2D5F8B";
            long r = 0, g = 0, b = 0, weight = 0;
            foreach (var color in scaled.Pixels)
            {
                if (color.Alpha < 96) continue;
                var saturation = Math.Max(color.Red, Math.Max(color.Green, color.Blue)) - Math.Min(color.Red, Math.Min(color.Green, color.Blue));
                var sampleWeight = 1 + saturation / 32;
                r += color.Red * sampleWeight; g += color.Green * sampleWeight; b += color.Blue * sampleWeight; weight += sampleWeight;
            }
            return weight == 0 ? "#2D5F8B" : $"#{r / weight:X2}{g / weight:X2}{b / weight:X2}";
        }
        catch { return "#2D5F8B"; }
    }
}
