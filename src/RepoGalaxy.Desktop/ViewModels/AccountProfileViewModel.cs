using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class AccountProfileViewModel : ViewModelBase
{
    private readonly IGitHubClient _github;
    private readonly IProfileImageService _images;
    private readonly IExternalLinkService _links;
    private bool _socialLoaded;

    [ObservableProperty] private User? _user;
    [ObservableProperty] private Bitmap? _avatar;
    [ObservableProperty] private bool _isLoadingSocial;
    [ObservableProperty] private string _status = string.Empty;
    public ObservableCollection<UserSocialAccount> SocialAccounts { get; } = [];

    public AccountProfileViewModel(IGitHubClient github, IProfileImageService images, IExternalLinkService links)
    {
        _github = github;
        _images = images;
        _links = links;
    }

    public bool HasAvatar => Avatar is not null;
    public string Initial => string.IsNullOrWhiteSpace(User?.Login) ? "G" : User.Login[..1].ToUpperInvariant();
    public string DisplayName => string.IsNullOrWhiteSpace(User?.DisplayName) ? User?.Login ?? string.Empty : User.DisplayName;
    public string LoginText => string.IsNullOrWhiteSpace(User?.Login) ? string.Empty : $"@{User.Login}";
    public string Statistics => User is null ? string.Empty : $"{User.PublicRepos:N0} 仓库  ·  {User.Followers:N0} 关注者  ·  {User.Following:N0} 正在关注";
    public string Context => string.Join("  ·  ", new[] { User?.Company, User?.Location }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public async Task SetUserAsync(User? user, CancellationToken cancellationToken = default)
    {
        User = user;
        Avatar = null;
        SocialAccounts.Clear();
        _socialLoaded = false;
        Status = string.Empty;
        if (user is not null && !string.IsNullOrWhiteSpace(user.AvatarUrl))
            Avatar = await _images.GetAsync(user.AvatarUrl, cancellationToken);
        NotifyComputed();
    }

    public async Task EnsureSocialAccountsAsync(CancellationToken cancellationToken = default)
    {
        if (_socialLoaded || User is null || IsLoadingSocial) return;
        IsLoadingSocial = true;
        try
        {
            var accounts = await _github.GetUserSocialAccountsAsync(User.Login, cancellationToken);
            SocialAccounts.Clear();
            foreach (var account in accounts.Where(x => _links.CanOpen(x.Url))) SocialAccounts.Add(account);
            if (_links.CanOpen(User.Blog) && SocialAccounts.All(x => !x.Url.Equals(User.Blog, StringComparison.OrdinalIgnoreCase)))
                SocialAccounts.Insert(0, new("Blog", User.Blog));
            if (!string.IsNullOrWhiteSpace(User.TwitterUsername))
            {
                var twitter = $"https://twitter.com/{Uri.EscapeDataString(User.TwitterUsername)}";
                if (SocialAccounts.All(x => !x.Url.Equals(twitter, StringComparison.OrdinalIgnoreCase))) SocialAccounts.Add(new("Twitter", twitter));
            }
            _socialLoaded = true;
            Status = SocialAccounts.Count == 0 ? "未公开社交链接" : string.Empty;
        }
        catch (OperationCanceledException) { throw; }
        catch { Status = "社交链接暂时不可用"; }
        finally { IsLoadingSocial = false; }
    }

    [RelayCommand]
    private void OpenUrl(string? value)
    {
        if (!_links.Open(value)) Status = "无法安全打开该链接";
    }

    partial void OnAvatarChanged(Bitmap? value) => OnPropertyChanged(nameof(HasAvatar));
    partial void OnUserChanged(User? value) => NotifyComputed();
    private void NotifyComputed()
    {
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(Initial));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(LoginText));
        OnPropertyChanged(nameof(Statistics));
        OnPropertyChanged(nameof(Context));
    }
}
