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
                    "Arrow keys move the tile cursor",
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
