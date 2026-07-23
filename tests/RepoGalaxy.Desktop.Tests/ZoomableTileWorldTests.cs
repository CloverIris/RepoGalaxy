using System.Net;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.Services;
using RepoGalaxy.Desktop.ViewModels;
using Xunit;

namespace RepoGalaxy.Desktop.Tests;

public sealed class ZoomableTileWorldTests
{
    [Fact]
    public void Ten_thousand_camera_inputs_collapse_into_one_frame_snapshot()
    {
        var accumulator = new CameraInputAccumulator();
        for (var i = 0; i < 10_000; i++) accumulator.AddPan(.25, -.5);

        var frame = accumulator.Drain();

        Assert.Equal(10_000, frame.InputCount);
        Assert.Equal(2_500, frame.PanX, 8);
        Assert.Equal(-5_000, frame.PanY, 8);
        Assert.True(accumulator.Drain().IsEmpty);
    }

    [Fact]
    public void Detail_portal_uses_hysteresis_and_only_suppresses_the_rail_after_snap()
    {
        var service = new DetailPortalCoordinator();
        Assert.Equal(DetailPresentationState.Board, service.Evaluate(DetailPresentationState.Board, true, .64).State);
        var portal = service.Evaluate(DetailPresentationState.Board, true, .7);
        Assert.Equal(DetailPresentationState.Portal, portal.State);
        Assert.False(portal.SuppressRightRail);
        var snapping = service.Evaluate(DetailPresentationState.Portal, true, .92);
        Assert.True(snapping.StartSnap);
        Assert.True(snapping.SuppressRightRail);
        Assert.Equal(DetailPresentationState.Full, service.Evaluate(DetailPresentationState.Full, true, .85).State);
        Assert.True(service.Evaluate(DetailPresentationState.Full, true, .81).ExitFull);
    }

    [Fact]
    public void Markdown_parser_discards_html_and_unsafe_links_then_builds_pages()
    {
        var service = new MarkdownDocumentService();
        var document = service.Parse("# Title\n\n<script>alert(1)</script>\n\n[bad](javascript:alert(1)) [good](https://github.com/)\n\n![logo](images/logo.png)\n\n- [x] done", "README", 12, "https://raw.githubusercontent.com/owner/repo/HEAD/");

        Assert.DoesNotContain(document.Blocks, x => x.Text.Contains("alert(1)", StringComparison.Ordinal));
        Assert.Contains(document.Blocks, x => x.Text.Contains("https://github.com/", StringComparison.Ordinal));
        Assert.Contains(document.Blocks, x => x.Kind == MarkdownBlockKind.Image && x.Url == "https://raw.githubusercontent.com/owner/repo/HEAD/images/logo.png");
        Assert.Contains(document.Blocks, x => x.Kind == MarkdownBlockKind.ListItem && x.IsChecked == true);
        Assert.NotEmpty(document.Pages);
    }

    [Fact]
    public void Ide_recommendation_follows_repository_technology()
    {
        var service = new LocalIdeDiscoveryService();
        var candidates = new[]
        {
            new LocalIdeDescriptor("code", "VS Code", IdeFamily.VisualStudioCode, "1", "code.exe", IdeCapability.OpenFolder),
            new LocalIdeDescriptor("clion", "CLion", IdeFamily.CLion, "2026.1", "clion64.exe", IdeCapability.OpenFolder),
            new LocalIdeDescriptor("rider", "Rider", IdeFamily.Rider, "2026.1", "rider64.exe", IdeCapability.OpenFolder)
        };
        Assert.Equal(IdeFamily.CLion, service.Recommend(candidates, "C++", ["cmake"])?.Family);
        Assert.Equal(IdeFamily.Rider, service.Recommend(candidates, "C#", ["avalonia"])?.Family);
    }
    [Theory]
    [InlineData(0, 1, TileWheelIntent.Zoom)]
    [InlineData(0, -1, TileWheelIntent.Zoom)]
    [InlineData(.25, .4, TileWheelIntent.Pan)]
    [InlineData(0, .18, TileWheelIntent.Pan)]
    public void Wheel_and_precision_touchpad_intents_are_separated(double x, double y, TileWheelIntent expected) =>
        Assert.Equal(expected, TileInputClassifier.Classify(x, y));

    [Fact]
    public void Pointer_anchored_zoom_preserves_the_world_point()
    {
        var service = new ZoomableTileLayoutService();
        var camera = new CameraState(320, 180, 1);
        const double anchorX = 417;
        const double anchorY = 263;
        var beforeX = camera.X + anchorX / camera.Zoom;
        var beforeY = camera.Y + anchorY / camera.Zoom;

        var result = service.ZoomAt(camera, 1.85, anchorX, anchorY, 1200, 720, 5000, 4000);

        Assert.Equal(beforeX, result.X + anchorX / result.Zoom, 8);
        Assert.Equal(beforeY, result.Y + anchorY / result.Zoom, 8);
    }

    [Fact]
    public void Pan_changes_only_xy_and_zoom_changes_z_around_anchor()
    {
        var service = new ZoomableTileLayoutService();
        var camera = new CameraState(500, 400, 1.2);
        var panned = service.Pan(camera, 80, -40, 1200, 720, 5000, 4000);
        Assert.Equal(camera.Zoom, panned.Zoom);
        Assert.NotEqual(camera.X, panned.X);
        Assert.NotEqual(camera.Y, panned.Y);

        var zoomed = service.ZoomAt(camera, 2, 0, 0, 1200, 720, 5000, 4000);
        Assert.Equal(camera.X, zoomed.X, 8);
        Assert.Equal(camera.Y, zoomed.Y, 8);
        Assert.Equal(2, zoomed.Zoom);
    }

    [Fact]
    public void Three_scale_thresholds_are_continuous_and_trigger_prefetch_at_55_percent()
    {
        var service = new ZoomableTileLayoutService();
        var tile = new TileWorldRect(100, 100, 596, 96);
        var fit = service.CalculateFitScale(tile, 1200, 720);

        Assert.Equal(.55, service.ScaleProfile.MinimumZoom);
        Assert.Equal(.70, service.ScaleProfile.SemanticFadeEndZoom);
        Assert.Equal(ZoomVisualMode.SemanticIndex, service.CalculateTransition(new(0, 0, .55), null, 1200, 720).Mode);
        var crossfade = service.CalculateTransition(new(0, 0, .625), null, 1200, 720);
        Assert.InRange(crossfade.SemanticIndexOpacity, .01, .99);
        Assert.InRange(crossfade.TileBoardOpacity, .01, .99);
        Assert.Equal(0, service.CalculateTransition(new(0, 0, .70), null, 1200, 720).SemanticIndexOpacity);

        Assert.False(service.CalculateTransition(new(0, 0, fit * .54, "repo:1"), tile, 1200, 720).ShouldPrefetch);
        Assert.True(service.CalculateTransition(new(0, 0, fit * .55, "repo:1"), tile, 1200, 720).ShouldPrefetch);
        Assert.Equal(0, service.CalculateTransition(new(0, 0, fit * .64, "repo:1"), tile, 1200, 720).DetailProgress);
        Assert.InRange(service.CalculateTransition(new(0, 0, fit * .8, "repo:1"), tile, 1200, 720).DetailProgress, .01, .99);
        Assert.Equal(ZoomVisualMode.Detail, service.CalculateTransition(new(0, 0, fit, "repo:1"), tile, 1200, 720).Mode);
    }

    [Fact]
    public void Semantic_catalog_caps_results_and_rejects_low_signal_topics()
    {
        var signals = new List<SemanticIndexSignal>();
        for (var i = 0; i < 20; i++) signals.Add(new(SemanticIndexKind.Language, $"Language {i}", $"repo:language:{i}", $"Language {i}"));
        for (var i = 0; i < 50; i++)
        {
            signals.Add(new(SemanticIndexKind.Framework, $"Framework {i}", $"repo:{i}:a", "C#"));
            signals.Add(new(SemanticIndexKind.Framework, $"Framework {i}", $"repo:{i}:b", "C#"));
        }
        signals.Add(new(SemanticIndexKind.Framework, "one-off-noise", "repo:noise"));
        signals.Add(new(SemanticIndexKind.Framework, "LocalOnly", "local:one", Origin: SemanticIndexSignalOrigin.LocalRepository));
        signals.Add(new(SemanticIndexKind.Framework, "https://invalid.example/topic", "repo:url"));
        signals.Add(new(SemanticIndexKind.Framework, "1234", "repo:number"));

        var result = new SemanticIndexCatalogService().Build(signals);

        Assert.True(result.Items.Count <= 48);
        Assert.True(result.Items.Count(x => x.Kind == SemanticIndexKind.Language) <= 16);
        Assert.True(result.Items.Count(x => x.Kind == SemanticIndexKind.Framework) <= 32);
        Assert.Contains(result.Items, x => x.Title == "LocalOnly");
        Assert.DoesNotContain(result.Items, x => x.Title == "one-off-noise");
        Assert.DoesNotContain(result.Items, x => x.Title.Contains("invalid.example", StringComparison.Ordinal));
        Assert.True(result.RejectedSignalCount >= 2);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.4.2")]
    [InlineData("192.168.1.8")]
    [InlineData("169.254.1.1")]
    [InlineData("100.64.0.4")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("2001:db8::1")]
    [InlineData("::ffff:127.0.0.1")]
    public void External_metadata_rejects_non_public_addresses(string value) =>
        Assert.False(ExternalMetadataSecurity.IsPublicAddress(IPAddress.Parse(value)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public void External_metadata_accepts_public_addresses(string value) =>
        Assert.True(ExternalMetadataSecurity.IsPublicAddress(IPAddress.Parse(value)));

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://[::1]/")]
    [InlineData("https://user:secret@example.com/")]
    [InlineData("https://example.com:8443/")]
    public void External_metadata_rejects_structurally_unsafe_urls(string value) =>
        Assert.False(ExternalMetadataSecurity.IsStructurallySafe(new Uri(value)));

    [Fact]
    public void External_metadata_accepts_plain_https_origin_with_path_and_query() =>
        Assert.True(ExternalMetadataSecurity.IsStructurallySafe(new Uri("https://example.com/docs/page?q=term")));

    [Fact]
    public void Local_technology_index_uses_only_real_project_markers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"repogalaxy-signals-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "sample.csproj"), "<Project><ItemGroup><PackageReference Include=\"Avalonia\" /></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(root, "package.json"), "{\"devDependencies\":{\"typescript\":\"latest\",\"react\":\"latest\"}}");

            var result = LocalTechnologyDetector.Detect([root]);

            Assert.Contains("C#", result.Languages);
            Assert.Contains("TypeScript", result.Languages);
            Assert.Contains("Avalonia", result.Frameworks);
            Assert.Contains("React", result.Frameworks);
            Assert.DoesNotContain("Rust", result.Languages);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Spatial_search_prefers_repository_then_nearest_world_position()
    {
        var candidates = new[]
        {
            new TileSearchCandidate("language:java", MetroTileKind.Language, "Java", "language", "Java", [], "", new(0, 0, 96, 96)),
            new TileSearchCandidate("repository:far", MetroTileKind.Repository, "far/java", "Java project", "Java", ["jvm"], "popular", new(900, 0, 596, 96)),
            new TileSearchCandidate("repository:near", MetroTileKind.Repository, "near/java", "Java tools", "Java", ["jvm"], "recommended", new(200, 0, 596, 96))
        };

        var result = new SpatialTileSearchService().Search("Java", candidates, 300, 50);

        Assert.Equal("repository:near", result.Matches[0].Key);
        Assert.Equal("repository:far", result.Matches[1].Key);
        Assert.Equal("language:java", result.Matches[2].Key);
    }

    [Fact]
    public void Virtual_world_is_deterministic_bounded_and_extends_into_negative_coordinates()
    {
        var service = new VirtualTileWorldService();
        var tips = new[] { new TipDefinition("history", "TIP", "History", "Body", "C#", 1, 1) };
        var window = new TileWorldWindow(-1500, -900, 1000, 700);

        var first = service.Materialize("stable-board", window, [], tips);
        var second = service.Materialize("stable-board", window, [], tips);

        Assert.NotEmpty(first);
        Assert.Equal(first.Select(x => (x.Key, x.Column, x.Row, x.ColumnSpan, x.RowSpan)),
            second.Select(x => (x.Key, x.Column, x.Row, x.ColumnSpan, x.RowSpan)));
        Assert.Contains(first, x => x.Column < 0);
        Assert.Contains(first, x => x.Row < 0);
        Assert.InRange(first.Count, 1, 800);
    }

    [Fact]
    public void Virtual_world_fills_residual_cells_when_real_content_splits_a_large_skeleton_slot()
    {
        var service = new VirtualTileWorldService();
        var tips = new[] { new TipDefinition("history", "TIP", "History", "Body", "C#", 1, 1) };
        var real = new TilePlacement(1,
            new TileContent("repository:1", MetroTileKind.Repository, "owner/repository", RepositoryId: 1),
            4, 0, 6, 1);

        var slots = service.Materialize("continuous-board", new TileWorldWindow(0, 0, 1200, 800), [real], tips);
        var covered = new HashSet<(int Column, int Row)>();
        for (var row = real.Row; row < real.Row + real.RowSpan; row++)
            for (var column = real.Column; column < real.Column + real.ColumnSpan; column++) covered.Add((column, row));
        foreach (var slot in slots)
            for (var row = slot.Row; row < slot.Row + slot.RowSpan; row++)
                for (var column = slot.Column; column < slot.Column + slot.ColumnSpan; column++) covered.Add((column, row));

        for (var row = 0; row < VirtualTileWorldService.ChunkRows; row++)
            for (var column = 0; column < VirtualTileWorldService.ChunkColumns; column++)
                Assert.Contains((column, row), covered);
    }

    [Fact]
    public void Empty_virtual_chunks_contain_one_stable_clickable_explore_tile()
    {
        var service = new VirtualTileWorldService();
        var tips = new[] { new TipDefinition("history", "TIP", "History", "Body", "C#", 1, 1) };
        var window = new TileWorldWindow(0, 0, 1200, 800);

        var first = service.Materialize("explore-board", window, [], tips);
        var second = service.Materialize("explore-board", window, [], tips);

        var firstExplore = first.Where(x => x.Content.Kind == MetroTileKind.Explore).ToArray();
        Assert.NotEmpty(firstExplore);
        Assert.All(firstExplore.GroupBy(x => x.Chunk), group => Assert.Single(group));
        Assert.Equal(
            firstExplore.Select(x => (x.Key, x.Column, x.Row, x.ColumnSpan, x.RowSpan)),
            second.Where(x => x.Content.Kind == MetroTileKind.Explore)
                .Select(x => (x.Key, x.Column, x.Row, x.ColumnSpan, x.RowSpan)));
        Assert.All(firstExplore, tile =>
        {
            Assert.Equal(2, tile.ColumnSpan);
            Assert.Equal(2, tile.RowSpan);
        });
    }

    [Fact]
    public void Virtual_world_covers_every_visible_signed_cell_exactly_once()
    {
        var service = new VirtualTileWorldService();
        var tips = new[] { new TipDefinition("history", "TIP", "History", "Body", "Rust", 1, 1) };
        var real = new[]
        {
            new TilePlacement(1, new TileContent("repository:left", MetroTileKind.Repository, "owner/left"), -8, -3, 6, 1),
            new TilePlacement(2, new TileContent("repository:right", MetroTileKind.FeaturedRepository, "owner/right"), 6, 4, 2, 2)
        };
        var window = new TileWorldWindow(-1450, -950, 3100, 2100);
        var slots = service.Materialize("coverage-board", window, real, tips);
        var counts = new Dictionary<(int Column, int Row), int>();

        foreach (var placement in real)
            Add(placement.Column, placement.Row, placement.ColumnSpan, placement.RowSpan);
        foreach (var slot in slots)
            Add(slot.Column, slot.Row, slot.ColumnSpan, slot.RowSpan);

        var left = (int)Math.Floor(window.X / VirtualTileWorldService.UnitWithGap);
        var right = (int)Math.Ceiling((window.X + window.Width) / VirtualTileWorldService.UnitWithGap) - 1;
        var top = (int)Math.Floor(window.Y / VirtualTileWorldService.UnitWithGap);
        var bottom = (int)Math.Ceiling((window.Y + window.Height) / VirtualTileWorldService.UnitWithGap) - 1;
        for (var row = top; row <= bottom; row++)
            for (var column = left; column <= right; column++)
                Assert.Equal(1, counts.GetValueOrDefault((column, row)));

        void Add(int column, int row, int width, int height)
        {
            for (var y = row; y < row + height; y++)
                for (var x = column; x < column + width; x++)
                    counts[(x, y)] = counts.GetValueOrDefault((x, y)) + 1;
        }
    }

    [Fact]
    public void Nearest_compatible_slots_extend_the_real_tile_frontier_without_template_gaps()
    {
        var service = new VirtualTileWorldService();
        var placements = new List<TilePlacement>();
        var window = new TileWorldWindow(0, 0, 1200, 800);
        var span = new TileSpan(6, 1);

        for (var index = 0; index < 10; index++)
        {
            var (column, row) = service.FindNearestCompatibleSlot("continuous-board", window, span, placements);
            placements.Add(new TilePlacement(index + 1,
                new TileContent($"repository:{index}", MetroTileKind.Repository, $"owner/repository-{index}"),
                column, row, span.Columns, span.Rows));
        }

        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var placement in placements)
            for (var row = placement.Row; row < placement.Row + placement.RowSpan; row++)
                for (var column = placement.Column; column < placement.Column + placement.ColumnSpan; column++)
                    Assert.True(occupied.Add((column, row)), "real tiles must never overlap");

        for (var index = 1; index < placements.Count; index++)
        {
            var tile = placements[index];
            Assert.True(placements.Take(index).Any(previous => Touches(tile, previous)),
                $"{tile.Content.Key} should extend the existing frontier");
        }
    }

    private static bool Touches(TilePlacement first, TilePlacement second) =>
        (first.Column < second.Column + second.ColumnSpan && first.Column + first.ColumnSpan > second.Column
            && (first.Row == second.Row + second.RowSpan || first.Row + first.RowSpan == second.Row))
        || (first.Row < second.Row + second.RowSpan && first.Row + first.RowSpan > second.Row
            && (first.Column == second.Column + second.ColumnSpan || first.Column + first.ColumnSpan == second.Column));

    [Fact]
    public void Peek_never_requests_a_snap_or_suppresses_the_right_rail()
    {
        var decision = new DetailPortalCoordinator().Evaluate(DetailPresentationState.Peek, true, .2);
        Assert.Equal(DetailPresentationState.Peek, decision.State);
        Assert.False(decision.StartSnap);
        Assert.False(decision.SuppressRightRail);
    }
}
