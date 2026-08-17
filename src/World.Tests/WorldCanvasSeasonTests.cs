using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Controls;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Tests;

public sealed class WorldCanvasSeasonTests
{
    [Theory]
    [InlineData(CampaignSeasonPaintTool.Paint)]
    [InlineData(CampaignSeasonPaintTool.ResetToDefault)]
    [InlineData(CampaignSeasonPaintTool.Lock)]
    [InlineData(CampaignSeasonPaintTool.Unlock)]
    public void ApplySeasonToolToArea_RoutesWholeTileToolsAndClipsAtWorldEdge(
        CampaignSeasonPaintTool tool)
    {
        var seasons = new CampaignSeasonMap(CreateDefinition(3, 3));
        if (tool is CampaignSeasonPaintTool.ResetToDefault or CampaignSeasonPaintTool.Unlock)
        {
            seasons.Apply(seasons.GetAllTiles().Select(entry =>
                new CampaignSeasonMutation(
                    entry.X,
                    entry.Y,
                    new CampaignSeasonTile("winter", Locked: true))));
        }

        var stroke = new CampaignSeasonStrokeBuilder(seasons);
        WorldCanvas.ApplySeasonToolToArea(
            stroke,
            seasons,
            new CampaignTileCoordinate(0, 0),
            paintAreaRadius: 1,
            tool,
            selectedSeasonId: "winter",
            lockPaintedTiles: true);
        var command = stroke.Complete($"Test {tool}");

        Assert.Equal(4, command.Changes.Count);
        foreach (var coordinate in new[]
                 {
                     new CampaignTileCoordinate(0, 0),
                     new CampaignTileCoordinate(1, 0),
                     new CampaignTileCoordinate(0, 1),
                     new CampaignTileCoordinate(1, 1),
                 })
        {
            var tile = seasons.GetTile(coordinate.X, coordinate.Y);
            var expected = tool switch
            {
                CampaignSeasonPaintTool.Paint => new CampaignSeasonTile("winter", Locked: true),
                CampaignSeasonPaintTool.ResetToDefault => new CampaignSeasonTile("spring", Locked: false),
                CampaignSeasonPaintTool.Lock => new CampaignSeasonTile("spring", Locked: true),
                CampaignSeasonPaintTool.Unlock => new CampaignSeasonTile("winter", Locked: false),
                _ => throw new InvalidOperationException(),
            };
            Assert.Equal(expected, tile);
        }

        var outside = seasons.GetTile(2, 2);
        Assert.Equal(
            tool is CampaignSeasonPaintTool.ResetToDefault or CampaignSeasonPaintTool.Unlock
                ? new CampaignSeasonTile("winter", Locked: true)
                : new CampaignSeasonTile("spring", Locked: false),
            outside);
    }

    [Fact]
    public void ApplySeasonToolToArea_InvalidInputDoesNotMutateAuthority()
    {
        var seasons = new CampaignSeasonMap(CreateDefinition(3, 3));
        var initialRevision = seasons.Revision;

        Assert.Throws<ArgumentException>(() => WorldCanvas.ApplySeasonToolToArea(
            new CampaignSeasonStrokeBuilder(seasons),
            seasons,
            new CampaignTileCoordinate(1, 1),
            paintAreaRadius: 1,
            CampaignSeasonPaintTool.Paint,
            selectedSeasonId: "not-in-catalog",
            lockPaintedTiles: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldCanvas.ApplySeasonToolToArea(
            new CampaignSeasonStrokeBuilder(seasons),
            seasons,
            new CampaignTileCoordinate(1, 1),
            paintAreaRadius: 13,
            CampaignSeasonPaintTool.Lock,
            selectedSeasonId: null,
            lockPaintedTiles: false));

        Assert.Equal(initialRevision, seasons.Revision);
        Assert.All(seasons.GetAllTiles(), entry =>
            Assert.Equal(new CampaignSeasonTile("spring"), entry.Tile));
    }

    private static CampaignWorldDefinition CreateDefinition(int tilesX, int tilesY) =>
        CampaignWorldDefinition.Create(
            tilesX * 1_000L,
            tilesY * 1_000L,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 0);
}
