using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Controls;

namespace Kingdom.World.Tests;

[Collection(CampaignSeasonNativeUiCollection.CollectionName)]
public sealed class WorldCanvasKeyboardTests
{
    [Fact]
    public async Task KeyboardOnlyCanvasFlow_CanMovePaintActiveLayersAndPin()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            var world = CreateWorld(5, 5);
            var canvas = new WorldCanvas
            {
                Width = 520,
                Height = 520,
                World = world,
                SelectedCampaignTileType = CampaignTileType.Forest,
                StampHeight = 120,
            };
            var selectedCoordinates = new List<CampaignTileCoordinate>();
            var strokeCount = 0;
            canvas.TileSelected += (_, args) =>
            {
                if (args.Info is { } info)
                {
                    selectedCoordinates.Add(info.Coordinate);
                }
            };
            canvas.StrokeCompleted += (_, _) => strokeCount++;

            var window = new Window
            {
                Width = 560,
                Height = 560,
                Content = canvas,
            };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                Assert.True(canvas.Focus());
                Dispatcher.UIThread.RunJobs();

                Assert.True(canvas.IsTabStop);
                Assert.Equal("Campaign tile editing canvas", AutomationProperties.GetName(canvas));
                Assert.Contains(
                    "Press F6 from the editor to focus the map",
                    AutomationProperties.GetHelpText(canvas),
                    StringComparison.OrdinalIgnoreCase);

                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
                window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(new CampaignTileData(CampaignTileType.Forest, 120), world.Tiles.GetTile(3, 3));
                Assert.Equal(1, strokeCount);
                Assert.Equal(new CampaignTileCoordinate(3, 3), canvas.KeyboardCoordinate);

                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Dispatcher.UIThread.RunJobs();

                Assert.Equal([new CampaignTileCoordinate(3, 3)], selectedCoordinates);

                var resources = new CampaignResourceMap(world.Definition);
                var resourceId = resources.Catalog.Definitions[0].Id;
                canvas.ResourceMap = resources;
                canvas.IsResourceWorkspace = true;
                canvas.SelectedResourceId = resourceId;
                canvas.ResourcePotential = 73;
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Dispatcher.UIThread.RunJobs();

                Assert.True(resources.TryGetOccurrence(3, 3, resourceId, out var resource));
                Assert.Equal(73, resource.Potential);

                var seasons = new CampaignSeasonMap(world.Definition);
                var seasonId = seasons.Catalog.Definitions[0].Id;
                canvas.IsResourceWorkspace = false;
                canvas.SeasonMap = seasons;
                canvas.IsSeasonWorkspace = true;
                canvas.SelectedSeasonId = seasonId;
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Dispatcher.UIThread.RunJobs();

                Assert.True(seasons.TryGetOccurrence(3, 3, seasonId, out _));
                await Task.CompletedTask;
                return true;
            }
            finally
            {
                window.Close();
            }
        }, timeout.Token);
    }

    [Fact]
    public async Task MaximumEditableGrid_CanFitAndRenderWithoutOversizedSnapshot()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            var canvas = new WorldCanvas
            {
                Width = 800,
                Height = 600,
                World = CreateWorld(500, 500),
            };
            var window = new Window
            {
                Width = 840,
                Height = 640,
                Content = canvas,
            };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Assert.Equal(CampaignWorldDefinition.MaximumTileCount, canvas.World!.Definition.TileCount);
                await Task.CompletedTask;
                return true;
            }
            finally
            {
                window.Close();
            }
        }, timeout.Token);
    }

    [Fact]
    public async Task Escape_CancelsActivePointerStrokeWithoutCommitting()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            var world = CreateWorld(5, 5);
            var canvas = new WorldCanvas
            {
                Width = 500,
                Height = 500,
                World = world,
                SelectedCampaignTileType = CampaignTileType.Forest,
                StampHeight = 120,
            };
            var completedStrokeCount = 0;
            canvas.StrokeCompleted += (_, _) => completedStrokeCount++;

            var window = new Window
            {
                Width = 500,
                Height = 500,
                Content = canvas,
            };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.MouseDown(new Point(250, 250), MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();

                Assert.True(canvas.HasActiveStroke);
                Assert.Equal(
                    new CampaignTileData(CampaignTileType.Forest, 120),
                    world.Tiles.GetTile(2, 2));

                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Dispatcher.UIThread.RunJobs();

                Assert.False(canvas.HasActiveStroke);
                Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(2, 2));
                Assert.Equal(0, completedStrokeCount);
                await Task.CompletedTask;
                return true;
            }
            finally
            {
                window.Close();
            }
        }, timeout.Token);
    }

    [Fact]
    public async Task ArrowNavigation_AutoPansToKeepKeyboardCursorVisible()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            var canvas = new WorldCanvas
            {
                Width = 200,
                Height = 200,
                World = CreateWorld(20, 20),
            };
            var raisedViewports = new List<WorldCanvasViewport>();
            canvas.ViewportChanged += (_, args) => raisedViewports.Add(args.Viewport);

            var window = new Window
            {
                Width = 200,
                Height = 200,
                Content = canvas,
            };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                Assert.True(canvas.Focus());
                canvas.ApplyViewport(new WorldCanvasViewport(40, 0, 0));
                raisedViewports.Clear();

                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(new CampaignTileCoordinate(11, 10), canvas.KeyboardCoordinate);
                var viewport = Assert.Single(raisedViewports);
                Assert.Equal(new WorldCanvasViewport(40, 7, 6), viewport);
                Assert.Equal(viewport, canvas.CaptureViewport());
                await Task.CompletedTask;
                return true;
            }
            finally
            {
                window.Close();
            }
        }, timeout.Token);
    }

    [Fact]
    public async Task KeyboardNavigation_CanJumpByStepPageAndEdgeWithoutMouse()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            var canvas = new WorldCanvas
            {
                Width = 200,
                Height = 200,
                World = CreateWorld(120, 120),
            };

            var window = new Window
            {
                Width = 220,
                Height = 220,
                Content = canvas,
            };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                Assert.True(canvas.Focus());
                canvas.ApplyViewport(new WorldCanvasViewport(20, 0, 0));

                window.KeyPress(Key.Right, RawInputModifiers.Shift, PhysicalKey.ArrowRight, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(new CampaignTileCoordinate(70, 60), canvas.KeyboardCoordinate);

                window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(new CampaignTileCoordinate(70, 69), canvas.KeyboardCoordinate);

                window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(new CampaignTileCoordinate(119, 69), canvas.KeyboardCoordinate);

                await Task.CompletedTask;
                return true;
            }
            finally
            {
                window.Close();
            }
        }, timeout.Token);
    }

    [Fact]
    public async Task KeyboardZoom_ZoomsAroundCursorAndClampsAtLimits()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            var canvas = new WorldCanvas
            {
                Width = 240,
                Height = 240,
                World = CreateWorld(30, 30),
            };

            var window = new Window
            {
                Width = 260,
                Height = 260,
                Content = canvas,
            };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                Assert.True(canvas.Focus());
                canvas.ApplyViewport(new WorldCanvasViewport(10, 0, 0));

                var beforeZoom = canvas.CaptureViewport().Zoom;
                window.KeyPress(Key.Add, RawInputModifiers.None, PhysicalKey.NumPadAdd, null);
                Dispatcher.UIThread.RunJobs();

                var afterZoom = canvas.CaptureViewport();
                Assert.True(afterZoom.Zoom > beforeZoom);
                AssertKeyboardCursorVisible(canvas, afterZoom);

                window.KeyPress(Key.OemMinus, RawInputModifiers.None, PhysicalKey.Minus, "-");
                Dispatcher.UIThread.RunJobs();
                var afterMainKeyboardMinus = canvas.CaptureViewport().Zoom;
                Assert.True(afterMainKeyboardMinus < afterZoom.Zoom);

                window.KeyPress(Key.OemPlus, RawInputModifiers.Shift, PhysicalKey.Equal, "+");
                Dispatcher.UIThread.RunJobs();
                Assert.True(canvas.CaptureViewport().Zoom > afterMainKeyboardMinus);

                canvas.ApplyViewport(new WorldCanvasViewport(256, 0, 0));
                window.KeyPress(Key.Add, RawInputModifiers.None, PhysicalKey.NumPadAdd, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(256, canvas.CaptureViewport().Zoom);

                canvas.ApplyViewport(new WorldCanvasViewport(0.000001, 0, 0));
                window.KeyPress(Key.Subtract, RawInputModifiers.None, PhysicalKey.NumPadSubtract, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0.000001, canvas.CaptureViewport().Zoom);

                await Task.CompletedTask;
                return true;
            }
            finally
            {
                window.Close();
            }
        }, timeout.Token);
    }

    [Fact]
    public async Task KeyboardJumpAndZoom_StillPaintsAtTheActiveCoordinate()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            var world = CreateWorld(120, 120);
            var canvas = new WorldCanvas
            {
                Width = 240,
                Height = 240,
                World = world,
                SelectedCampaignTileType = CampaignTileType.Hills,
                StampHeight = 180,
            };

            var window = new Window
            {
                Width = 260,
                Height = 260,
                Content = canvas,
            };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                Assert.True(canvas.Focus());
                canvas.ApplyViewport(new WorldCanvasViewport(20, 0, 0));

                window.KeyPress(Key.Right, RawInputModifiers.Control, PhysicalKey.ArrowRight, null);
                window.KeyPress(Key.Add, RawInputModifiers.None, PhysicalKey.NumPadAdd, null);
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Dispatcher.UIThread.RunJobs();

                var coordinate = Assert.IsType<CampaignTileCoordinate>(canvas.KeyboardCoordinate);
                Assert.Equal(
                    new CampaignTileData(CampaignTileType.Hills, 180),
                    world.Tiles.GetTile(coordinate.X, coordinate.Y));

                await Task.CompletedTask;
                return true;
            }
            finally
            {
                window.Close();
            }
        }, timeout.Token);
    }

    private static void AssertKeyboardCursorVisible(WorldCanvas canvas, WorldCanvasViewport viewport)
    {
        var coordinate = Assert.IsType<CampaignTileCoordinate>(canvas.KeyboardCoordinate);
        var visibleWidth = canvas.Bounds.Width / viewport.Zoom;
        var visibleHeight = canvas.Bounds.Height / viewport.Zoom;
        Assert.InRange(coordinate.X + 0.5, viewport.OriginX, viewport.OriginX + visibleWidth);
        Assert.InRange(coordinate.Y + 0.5, viewport.OriginY, viewport.OriginY + visibleHeight);
    }

    private static CampaignWorld CreateWorld(int tilesX, int tilesY) =>
        new(CampaignWorldDefinition.Create(
            worldWidthMeters: tilesX * 1_000L,
            worldHeightMeters: tilesY * 1_000L,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 0));
}
