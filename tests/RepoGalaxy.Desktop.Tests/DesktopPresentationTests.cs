using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Services;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.Desktop.Views;
using RepoGalaxy.Desktop.Views.Dialogs;
using Xunit;

namespace RepoGalaxy.Desktop.Tests;

public sealed class DesktopPresentationTests
{
    [Fact]
    public void ExternalLinkService_OnlyAcceptsHttpsLinks()
    {
        var service = new ExternalLinkService();
        Check(service.CanOpen("https://github.com/openai"), "HTTPS GitHub URL should be accepted.");
        Check(!service.CanOpen("http://github.com/openai"), "HTTP URL should be rejected.");
        Check(!service.CanOpen("file:///C:/Windows/System32"), "File URL should be rejected.");
        Check(!service.CanOpen("not-a-uri"), "Invalid URL should be rejected.");
    }

    [Fact]
    public void RepositoryPresentation_FormatsDenseMetadata()
    {
        var repository = new Repository
        {
            Owner = "avaloniaui",
            Name = "avalonia",
            PrimaryLanguage = "C#",
            Stars = 28450,
            Forks = 2450,
            UpdatedAt = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            Topics = ["ui", "dotnet", "cross-platform"]
        };
        var presentation = new RepositoryViewModel(repository);
        Check(presentation.FullName == "avaloniaui/avalonia", "Full name formatting failed.");
        Check(presentation.StarsFormatted == "28,450", "Star formatting failed.");
        Check(presentation.TopicsText.Contains("ui", StringComparison.Ordinal), "Topic formatting failed.");
    }

    [Fact]
    public void NavigationItem_ParsesVectorGeometryAndTracksSelection()
    {
        TestAppBuilder.EnsureInitialized();
        var item = new NavigationItemViewModel("Discover", "发现", "M0,0 L10,10");
        Check(item.Icon is not null, "Vector geometry was not parsed.");
        Check(!item.IsSelected, "Navigation item should start unselected.");
        item.IsSelected = true;
        Check(item.IsSelected, "Navigation selection did not update.");
    }

    [Fact]
    public void CoreViews_LoadUnderHeadlessAvalonia()
    {
        TestAppBuilder.EnsureInitialized();
        Check(new MainWindow() is not null, "Main window failed to load.");
        Check(new DiscoverView() is not null, "Discover view failed to load.");
        Check(new SubscriptionsView() is not null, "Subscriptions view failed to load.");
        Check(new LibraryView() is not null, "Library view failed to load.");
        Check(new NotificationsView() is not null, "Notifications view failed to load.");
        Check(new MyReposView() is not null, "My repositories view failed to load.");
        Check(new LocalReposView() is not null, "Local repositories view failed to load.");
        Check(new SettingsView() is not null, "Settings view failed to load.");
        Check(new LoginDialog() is not null, "Login dialog failed to load.");
    }

    [Fact]
    public void ThemeResources_ResolveForLightAndDark()
    {
        TestAppBuilder.EnsureInitialized();
        var application = Application.Current ?? throw new InvalidOperationException("Avalonia application is unavailable.");
        var light = ((ResourceDictionary)application.Resources.ThemeDictionaries[ThemeVariant.Light])["CanvasBrush"];
        var dark = ((ResourceDictionary)application.Resources.ThemeDictionaries[ThemeVariant.Dark])["CanvasBrush"];
        Check(light is not null, "Light canvas resource is missing.");
        Check(dark is not null, "Dark canvas resource is missing.");
        Check(light?.ToString() != dark?.ToString(), "Light and dark canvas resources should differ.");
    }

    [Fact]
    public async Task DiscoveryStore_PersistsRepositoryBeforeFeedForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>().UseSqlite(connection).Options;
        var factory = new TestDbContextFactory(options);
        await using (var database = factory.CreateDbContext()) await database.Database.EnsureCreatedAsync();
        var store = new DiscoveryStore(factory);
        var repository = new Repository { Owner = "repo", Name = "sample", GitHubId = "node-1", HtmlUrl = "https://github.com/repo/sample" };

        var added = await store.AddFeedItemAsync(repository, FeedSource.Trending, new FeedReason { Summary = "test" });
        var feed = await store.GetFeedAsync(FeedSource.Trending);

        Check(added, "Feed item was not added.");
        Check(feed.Count == 1, "Feed item was not persisted.");
        Check(feed[0].RepositoryId > 0, "Feed foreign key was not generated.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TestDbContextFactory(DbContextOptions<RepoGalaxyDbContext> options) : IDbContextFactory<RepoGalaxyDbContext>
    {
        public RepoGalaxyDbContext CreateDbContext() => new(options);
    }
}
