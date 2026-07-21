using Avalonia.Media.Imaging;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public interface IProfileImageService
{
    Task<Bitmap?> GetAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class ProfileImageService : IProfileImageService
{
    private readonly ITileImageService _images;
    public ProfileImageService(ITileImageService images) => _images = images;
    public async Task<Bitmap?> GetAsync(string url, CancellationToken cancellationToken = default)
        => (await _images.GetAsync(url, cancellationToken))?.Bitmap;
}
