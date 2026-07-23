using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;
using RepoGalaxy.Data.Services;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Desktop.Controls;
using RepoGalaxy.Desktop.ViewModels;
using RepoGalaxy.Desktop.Views;
using RepoGalaxy.Desktop.Views.Dialogs;
using Xunit;

namespace RepoGalaxy.Desktop.Tests;

public sealed class DesktopPresentationTests
{
    [Fact]
    public void Detail_section_defaults_to_readme_and_only_preserves_explicit_user_selection()
    {
        var baseline = new[]
        {
            new DetailSection("readme", "README", "正在加载 README", []),
            new DetailSection("overview", "仓库概览", "overview", [])
        };
        var loaded = new[]
        {
            new DetailSection("readme", "README", "README loaded", [], Markdown: "# Loaded"),
            new DetailSection("overview", "仓库概览", "overview", [])
        };

        Check(DetailSectionSelection.Select(baseline)?.Key == "readme",
            "README must remain selected while its remote content is loading.");
        Check(DetailSectionSelection.Select(loaded)?.Markdown == "# Loaded",
            "Loaded README content must replace the baseline without a tab cycle.");
        Check(DetailSectionSelection.Select(loaded, "overview")?.Key == "overview",
            "An explicit user section selection must be preserved.");
        Check(DetailSectionSelection.Select(
                [new DetailSection("overview", "仓库概览", "overview", [])])?.Key == "overview",
            "Repositories without a README must fall back to overview.");
    }

    [Fact]
    public void TipCatalog_LoadsAuditedYamlWithMixedSpans()
    {
        var tips = new TipCatalog().GetTips(new DateOnly(2026, 7, 23));

        Check(tips.Count >= 25, "The YAML knowledge catalog should provide a useful offline pool.");
        Check(tips.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() == tips.Count,
            "Tip keys must be unique.");
        Check(tips.Any(x => x.Title.Contains("Git", StringComparison.OrdinalIgnoreCase)),
            "The catalog should contain technical knowledge rather than empty placeholders.");
        Check(tips.SelectMany(x => x.SpanOptions ?? []).Distinct().Count() >= 5,
            "Knowledge cards should support several mosaic sizes.");
    }

    [Fact]
    public void TileMosaicPolicy_IsStableAndProducesMixedRepositoryGeometry()
    {
        var policy = new TileMosaicPolicy();
        var repositories = Enumerable.Range(1, 160)
            .Select(index => new Repository
            {
                GitHubId = index.ToString(),
                Owner = "owner",
                Name = $"repository-{index}",
                Stars = index % 9 == 0 ? 120_000 : index * 100
            })
            .ToArray();

        var first = repositories.Select(x => policy.GetRepositorySpan(x, FeedSource.Trending)).ToArray();
        var second = repositories.Select(x => policy.GetRepositorySpan(x, FeedSource.Trending)).ToArray();

        Check(first.SequenceEqual(second), "Mosaic spans must remain stable across refreshes.");
        Check(first.Distinct().Count() >= 5, "Repository geometry should not collapse into a regular grid.");
        Check(first.Any(x => x.Rows == 1) && first.Any(x => x.Rows > 1),
            "The mosaic must combine horizontal and multi-row repository cards.");
        Check(policy.GetRankingSpan(0) == new TileSpan(4, 8), "The primary ranking should be a 4x8 anchor.");
        Check(policy.GetRankingSpan(1) == new TileSpan(2, 8), "Secondary rankings should be 2x8 anchors.");
    }

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
    public void NavigationItem_AlwaysUsesAuditedVectorGeometry()
    {
        TestAppBuilder.EnsureInitialized();
        var item = new NavigationItemViewModel("Discover", "发现", "\uE721", "M3,5 L9,3 L7,9");

        Check(item.Glyph == "\uE721", "Windows icon-font value was not retained as a glyph.");
        Check(item.Icon is not null, "Fallback vector geometry was not parsed.");
        Check(!item.UseSystemGlyph && item.UseFallbackIcon, "Navigation must not depend on optional Windows icon-font glyphs.");
        Check(item.Group == "Primary", "Default navigation group changed unexpectedly.");
    }

    [Fact]
    public void CoreViews_LoadUnderHeadlessAvalonia()
    {
        TestAppBuilder.EnsureInitialized();
        Check(new MainWindow() is not null, "Main window failed to load.");
        Check(new StartupWindow() is not null, "Startup window failed to load.");
        Check(new DiscoverView() is not null, "Discover view failed to load.");
        Check(new SubscriptionsView() is not null, "Subscriptions view failed to load.");
        Check(new LibraryView() is not null, "Library view failed to load.");
        Check(new NotificationsView() is not null, "Notifications view failed to load.");
        Check(new MyReposView() is not null, "My repositories view failed to load.");
        Check(new LocalReposView() is not null, "Local repositories view failed to load.");
        Check(new SettingsView() is not null, "Settings view failed to load.");
        Check(new LoginDialog() is not null, "Login dialog failed to load.");
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(1366, 768)]
    [InlineData(1000, 720)]
    [InlineData(900, 640)]
    public void Discover_tile_world_arranges_at_supported_headless_viewports(double width, double height)
    {
        TestAppBuilder.EnsureInitialized();
        var view = new DiscoverView();
        var host = new Window { Width = width, Height = height, Content = view };
        try
        {
            host.Show();
            Dispatcher.UIThread.RunJobs();
            var world = view.FindControl<VirtualizedTileWorldControl>("TileWorld")
                ?? throw new InvalidOperationException("Virtualized tile world was not found.");

            Check(world.Bounds.Width > 0 && world.Bounds.Height > 0,
                $"Tile world did not arrange at {width}x{height}.");
            Check(world.ClipToBounds, "Tile world must rely only on true viewport clipping.");
        }
        finally
        {
            host.Close();
        }
    }

    [Fact]
    public void Startup_window_is_centered_responsive_and_uses_custom_chrome()
    {
        TestAppBuilder.EnsureInitialized();
        var window = new StartupWindow();
        var configure = typeof(StartupWindow).GetMethod("ConfigureWindowChrome", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Startup chrome configuration was not found.");
        configure.Invoke(window, null);

        Check(window.WindowStartupLocation == WindowStartupLocation.CenterScreen, "Startup window must open in the active screen center.");
        Check(window.WindowDecorations == WindowDecorations.None, "Startup window must use custom chrome.");
        var title = window.FindControl<Control>("StartupTitleBar") ?? throw new InvalidOperationException("Startup title bar was not found.");
        var close = window.FindControl<Button>("StartupCloseButton") ?? throw new InvalidOperationException("Startup close button was not found.");
        Check(WindowDecorationProperties.GetElementRole(title) == WindowDecorationsElementRole.TitleBar, "Startup title role is incorrect.");
        Check(WindowDecorationProperties.GetElementRole(close) == WindowDecorationsElementRole.CloseButton, "Startup close role is incorrect.");
        Check(window.FindControl<Avalonia.Controls.Shapes.Path>("StartupCloseIcon")?.Stroke is not null, "Startup close icon must be visible.");
    }

    [Fact]
    public void Tile_world_snapshot_preserves_signed_geometry_and_full_wide_tiles()
    {
        var placements = new[]
        {
            new TilePlacement(1, new TileContent("repository:wide", MetroTileKind.Repository, "owner/wide"), -12, 4, 6, 1),
            new TilePlacement(2, new TileContent("language:csharp", MetroTileKind.Language, "C#"), 3, -2, 1, 1)
        };
        var board = new TileBoardState(5, "guest", FeedSource.Trending, 0, 0, 1, null, "", 0, 0, 18, 10, placements, "seed");
        var service = new TileWorldPresentationService();

        var snapshot = service.CreateSnapshot(board, "repository:wide");
        var visible = service.QueryVisible(snapshot, new TileWorldViewport(-1250, 350, 1, 800, 500), 0);

        Check(snapshot.ContentBounds.Left == -1200 && snapshot.ContentBounds.Top == -200, "Signed content origin was not preserved.");
        Check(snapshot.Anchor?.ContentKey == "repository:wide", "Requested content anchor was not retained.");
        Check(visible.Single(x => x.Content.Key == "repository:wide").ColumnSpan == 6, "Wide Tile must remain a full 6x1 rectangle.");
    }

    [Fact]
    public void MainWindow_switches_shell_modes_at_the_four_responsive_widths()
    {
        TestAppBuilder.EnsureInitialized();
        var window = new MainWindow();
        var method = typeof(MainWindow).GetMethod("ApplyResponsiveLayout", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Responsive layout method was not found.");
        var navigation = window.FindControl<SplitView>("NavigationSplit") ?? throw new InvalidOperationException("Navigation split was not found.");
        var details = window.FindControl<SplitView>("DetailsSplit") ?? throw new InvalidOperationException("Details split was not found.");

        method.Invoke(window, [1440d]);
        Check(navigation.DisplayMode == SplitViewDisplayMode.CompactInline && details.DisplayMode == SplitViewDisplayMode.Inline, "Wide layout should keep the right rail inline.");
        method.Invoke(window, [1366d]);
        Check(navigation.DisplayMode == SplitViewDisplayMode.CompactInline && details.DisplayMode == SplitViewDisplayMode.Inline, "Desktop layout should keep the right rail inline.");
        method.Invoke(window, [1000d]);
        Check(navigation.DisplayMode == SplitViewDisplayMode.CompactInline && details.DisplayMode == SplitViewDisplayMode.Overlay, "Compact layout should use a right drawer.");
        method.Invoke(window, [900d]);
        Check(navigation.DisplayMode == SplitViewDisplayMode.Overlay && details.DisplayMode == SplitViewDisplayMode.Overlay, "Narrow layout should overlay navigation and details.");
    }

    [Fact]
    public void Custom_window_chrome_uses_Avalonia_roles_and_keeps_caption_geometry_aligned()
    {
        TestAppBuilder.EnsureInitialized();
        var window = new MainWindow();
        var configure = typeof(MainWindow).GetMethod("ConfigureWindowChrome", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Window chrome configuration was not found.");
        configure.Invoke(window, null);

        Check(window.WindowDecorations == WindowDecorations.None, "Main window must not retain a native border/title bar.");
        Check(window.ExtendClientAreaToDecorationsHint, "Main window client area must extend into custom decorations.");
        var title = window.FindControl<Control>("TitleBarDragRegion") ?? throw new InvalidOperationException("Title bar was not found.");
        var minimize = window.FindControl<Button>("MinimizeButton") ?? throw new InvalidOperationException("Minimize button was not found.");
        var maximize = window.FindControl<Button>("MaximizeButton") ?? throw new InvalidOperationException("Maximize button was not found.");
        var close = window.FindControl<Button>("CloseButton") ?? throw new InvalidOperationException("Close button was not found.");
        Check(WindowDecorationProperties.GetElementRole(title) == WindowDecorationsElementRole.TitleBar, "Title bar role is incorrect.");
        Check(WindowDecorationProperties.GetElementRole(minimize) == WindowDecorationsElementRole.MinimizeButton, "Minimize role is incorrect.");
        Check(WindowDecorationProperties.GetElementRole(maximize) == WindowDecorationsElementRole.MaximizeButton, "Maximize role is incorrect.");
        Check(WindowDecorationProperties.GetElementRole(close) == WindowDecorationsElementRole.CloseButton, "Close role is incorrect.");
        Check(minimize.Width == 46 && maximize.Width == 46 && close.Width == 46, "Caption buttons must retain three fixed 46px hit targets.");
        Check(window.FindControl<Avalonia.Controls.Shapes.Path>("MinimizeIcon")?.Stroke is not null, "Minimize icon must use a visible stroke path.");
        Check(window.FindControl<Avalonia.Controls.Shapes.Path>("MaximizeIcon")?.Stroke is not null, "Maximize icon must use a visible stroke path.");
        Check(window.FindControl<Avalonia.Controls.Shapes.Path>("CloseIcon")?.Stroke is not null, "Close icon must use a visible stroke path.");
        Check(window.FindControl<Button>("NavigationToggleButton") is not null, "Hamburger button must live in the navigation pane.");
        Check(window.Icon is not null, "Main window must use the RepoGalaxy application icon.");
        Check(window.FindControl<RepoGalaxyMark>("HeaderBrandMark") is not null, "Header must render the RepoGalaxy vector mark.");

        var login = new LoginDialog();
        var configureLogin = typeof(LoginDialog).GetMethod("ConfigureWindowChrome", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Login chrome configuration was not found.");
        configureLogin.Invoke(login, null);
        Check(login.WindowDecorations == WindowDecorations.None, "Login window must use the same custom chrome model.");
        var loginTitle = login.FindControl<Control>("LoginTitleBar") ?? throw new InvalidOperationException("Login title bar was not found.");
        Check(WindowDecorationProperties.GetElementRole(loginTitle) == WindowDecorationsElementRole.TitleBar, "Login title bar role is incorrect.");
        Check(login.Icon is not null, "Login window must use the RepoGalaxy application icon.");
        Check(login.FindControl<RepoGalaxyMark>("LoginBrandMark") is not null, "Login header must render the RepoGalaxy vector mark.");

        var startup = new StartupWindow();
        Check(startup.Icon is not null, "Startup window must use the RepoGalaxy application icon.");
        Check(startup.FindControl<RepoGalaxyMark>("StartupBrandMark") is not null, "Startup header must render the RepoGalaxy vector mark.");
    }

    [Fact]
    public void Semantic_index_uses_an_unscaled_fixed_pixel_viewport()
    {
        TestAppBuilder.EnsureInitialized();
        var view = new SemanticIndexView();
        Check(SemanticMosaicItemViewModel.Unit == 88, "Semantic index unit must remain 88 screen pixels.");
        var viewport = view.FindControl<SemanticMosaicViewport>("SemanticViewport");
        Check(viewport is not null,
            "Semantic index must use the dedicated fixed-pixel pannable viewport.");
        var scroll = viewport!.GestureRecognizers.OfType<Avalonia.Input.GestureRecognizers.ScrollGestureRecognizer>().Single();
        Check(scroll.CanHorizontallyScroll && !scroll.CanVerticallyScroll,
            "Semantic index must allow horizontal exploration while keeping its vertical position locked.");
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
    public void MetroTile_contract_uses_fixed_spans_and_accessible_palettes()
    {
        TestAppBuilder.EnsureInitialized();
        Check(TileSpan.For(MetroTileKind.Language) == new TileSpan(1, 1), "Language tile span changed.");
        Check(TileSpan.For(MetroTileKind.Technology) == new TileSpan(2, 1), "Technology tile span changed.");
        Check(TileSpan.For(MetroTileKind.Repository) == new TileSpan(6, 1), "Repository tile must remain horizontal 6x1.");
        Check(TileSpan.For(MetroTileKind.RankingList) == new TileSpan(2, 2), "Ranking tile span changed.");

        var service = new TilePaletteService();
        foreach (var accent in new[] { "C#", "JavaScript", "Python", "unknown-language", "#E81123" })
        {
            var palette = service.Create(accent);
            Check(service.ContrastRatio(palette.Background, palette.Foreground) >= 4.5, $"{accent} palette is below 4.5:1 contrast.");
        }
        Check(TileIconCatalog.Get("Python") is not null, "Bundled Python SVG did not load as local geometry.");
        Check(TileIconCatalog.Get("not-a-language") is null, "Unknown technologies should use the text fallback.");
    }

    [Fact]
    public void Wide_repository_layout_releases_missing_cover_space_and_precomputes_non_overlapping_actions()
    {
        TestAppBuilder.EnsureInitialized();
        var repository = new Repository
        {
            Id = 42,
            Owner = "owner",
            Name = "repository",
            Description = "A repository description that can wrap onto a second line.",
            Stars = 1234
        };
        var feed = new FeedItemViewModel(new FeedItem
        {
            Id = 7,
            RepositoryId = repository.Id,
            Repository = repository,
            Reason = new FeedReason { Summary = "推荐原因" }
        });
        var palette = new TilePalette("#1D4ED8", "#FFFFFF", "#DCE8FF", "#66000000");
        var withoutCover = new MetroTileViewModel(
            new TilePlacement(1, new TileContent("repository:42", MetroTileKind.Repository, repository.FullName, RepositoryId: repository.Id), 0, 0, 6, 1),
            palette,
            feed);
        var withCoverPlacement = new TilePlacement(2,
            new TileContent("repository:42:cover", MetroTileKind.Repository, repository.FullName,
                RepositoryId: repository.Id, ImageUrl: "https://avatars.githubusercontent.com/u/42"),
            0, 0, 6, 1);
        var withCover = MetroTileViewModel.CalculateRepositoryLayout(withCoverPlacement, usesCover: true)!;

        Check(withoutCover.RepositoryLayout is { UsesWideLayout: true, UsesCover: false }, "6x1 repository should use the wide layout without reserving a missing cover.");
        Check(withoutCover.RepositoryLayout!.Text.X == 8, "Missing cover must release its entire horizontal slot.");
        Check(withCover.UsesCover, "A successfully loaded image should enable the cover slot.");
        Check(withCover.Cover.Width == withCover.Cover.Height, "6x1 cover must remain exactly square.");
        var actions = withCover.Actions;
        Check(actions.Select(x => x.Action).SequenceEqual(
            [TileActionKind.Like, TileActionKind.Bookmark, TileActionKind.GitHubStar, TileActionKind.Dislike]),
            "Repository action order changed.");
        for (var index = 0; index < actions.Count; index++)
            for (var other = index + 1; other < actions.Count; other++)
                Check(!Overlaps(actions[index].Bounds, actions[other].Bounds), "Repository action hit regions overlap.");

        static bool Overlaps(TileWorldRect left, TileWorldRect right) =>
            left.X < right.X + right.Width && left.X + left.Width > right.X
            && left.Y < right.Y + right.Height && left.Y + left.Height > right.Y;
    }

    [Fact]
    public void Featured_repository_layout_keeps_a_full_cover_and_shared_action_semantics()
    {
        TestAppBuilder.EnsureInitialized();
        var repository = new Repository { Id = 8, Owner = "owner", Name = "featured" };
        var feed = new FeedItemViewModel(new FeedItem
        {
            RepositoryId = repository.Id,
            Repository = repository,
            Reason = new FeedReason()
        });
        var placement = new TilePlacement(3,
            new TileContent("featured:8", MetroTileKind.FeaturedRepository, repository.FullName,
                RepositoryId: repository.Id, ImageUrl: "https://avatars.githubusercontent.com/u/8"),
            0, 0, 2, 2);
        var tile = new MetroTileViewModel(placement, new("#1F2937", "#FFFFFF", "#D1D5DB", "#99000000"), feed);
        var loadedLayout = MetroTileViewModel.CalculateRepositoryLayout(placement, usesCover: true)!;

        Check(tile.RepositoryLayout is { UsesWideLayout: false, UsesCover: false }, "Featured repositories must not reserve space before their image is available.");
        Check(loadedLayout is { UsesWideLayout: false, UsesCover: true }, "2x2 featured repository should retain its large cover after loading.");
        Check(loadedLayout.Cover.Width == tile.Width && loadedLayout.Cover.Height == tile.Height,
            "Featured cover should fill the tile before UniformToFill cropping and scrim composition.");
        Check(loadedLayout.Actions.Count == 4, "Featured repository must expose the same four actions.");
    }

    [Fact]
    public void TileImageService_rejects_unsafe_or_deceptive_avatar_hosts()
    {
        Check(TileImageService.TryValidate("https://avatars.githubusercontent.com/u/1", out _), "GitHub avatar host should be accepted.");
        Check(TileImageService.TryValidate("https://user-images.githubusercontent.com/file.png", out _), "GitHub content subdomain should be accepted.");
        Check(!TileImageService.TryValidate("http://avatars.githubusercontent.com/u/1", out _), "HTTP avatar must be rejected.");
        Check(!TileImageService.TryValidate("https://avatars.githubusercontent.com.evil.example/u/1", out _), "Deceptive host must be rejected.");
        Check(!TileImageService.TryValidate("file:///C:/secret.png", out _), "Local paths must be rejected.");
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

    [Fact]
    public async Task CloneCleanup_UsesTranslatableSqliteCutoffAndOnlyFailsAbandonedOperations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RepoGalaxyDbContext>().UseSqlite(connection).Options;
        var factory = new TestDbContextFactory(options);
        await using (var database = factory.CreateDbContext())
        {
            await database.Database.EnsureCreatedAsync();
            var now = DateTimeOffset.UtcNow;
            database.CloneOperations.AddRange(
                Operation("old", CloneOperationState.Cloning, now.AddMinutes(-20)),
                Operation("recent", CloneOperationState.Cloning, now.AddMinutes(-2)),
                Operation("complete", CloneOperationState.Completed, now.AddMinutes(-20)));
            await database.SaveChangesAsync();
        }

        var service = new RepositoryCloneService(factory, new RepositoryService(factory));
        await service.CleanupAbandonedAsync();

        await using var verification = factory.CreateDbContext();
        var states = await verification.CloneOperations.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Check(states["old"].State == (int)CloneOperationState.Failed && states["old"].ErrorCode == "abandoned_cleanup", "Old incomplete clone was not marked failed.");
        Check(states["recent"].State == (int)CloneOperationState.Cloning, "Recent clone must not be cleaned up.");
        Check(states["complete"].State == (int)CloneOperationState.Completed, "Completed clone must not be changed.");

        static CloneOperationEntity Operation(string id, CloneOperationState state, DateTimeOffset updatedAt) => new()
        {
            Id = id,
            RepositoryFullName = "owner/repository",
            ParentDirectory = Path.GetTempPath(),
            StagingDirectory = Path.Combine(Path.GetTempPath(), $".repository.repogalaxy-{id}.tmp"),
            DestinationDirectory = Path.Combine(Path.GetTempPath(), $"repository-{id}"),
            State = (int)state,
            StartedAt = updatedAt,
            UpdatedAt = updatedAt
        };
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
