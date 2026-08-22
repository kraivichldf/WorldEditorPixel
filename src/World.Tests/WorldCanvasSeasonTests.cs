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
    [InlineData(CampaignSeasonPaintTool.Erase)]
    [InlineData(CampaignSeasonPaintTool.Lock)]
    [InlineData(CampaignSeasonPaintTool.Unlock)]
    public void ApplySeasonToolToArea_ChangesOnlySelectedOccurrenceAndClipsAtEdge(
        CampaignSeasonPaintTool tool)
    {
        var seasons = new CampaignSeasonMap(CreateDefinition(3, 3));
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                seasons.Upsert(x, y, new("spring"));
                if (tool != CampaignSeasonPaintTool.Paint)
                {
                    seasons.Upsert(x, y, new("winter", Locked: tool == CampaignSeasonPaintTool.Unlock));
                }
            }
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
            Assert.True(seasons.TryGetOccurrence(coordinate.X, coordinate.Y, "spring", out _));
            if (tool == CampaignSeasonPaintTool.Erase)
            {
                Assert.False(seasons.TryGetOccurrence(coordinate.X, coordinate.Y, "winter", out _));
            }
            else
            {
                Assert.True(seasons.TryGetOccurrence(coordinate.X, coordinate.Y, "winter", out var winter));
                Assert.Equal(
                    tool is CampaignSeasonPaintTool.Paint or CampaignSeasonPaintTool.Lock,
                    winter.Locked);
            }
        }

        Assert.True(seasons.TryGetOccurrence(2, 2, "spring", out _));
        Assert.Equal(tool != CampaignSeasonPaintTool.Paint,
            seasons.TryGetOccurrence(2, 2, "winter", out var outside));
        if (tool != CampaignSeasonPaintTool.Paint)
        {
            Assert.Equal(tool == CampaignSeasonPaintTool.Unlock, outside.Locked);
        }
    }

    [Fact]
    public void ApplySeasonToolToArea_InvalidInputDoesNotMutateAuthority()
    {
        var seasons = new CampaignSeasonMap(CreateDefinition(3, 3));
        seasons.Upsert(0, 0, new("spring"));
        var initial = seasons.GetMaterializedOccurrences();
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
            selectedSeasonId: "spring",
            lockPaintedTiles: false));

        Assert.Equal(initialRevision, seasons.Revision);
        Assert.Equal(initial, seasons.GetMaterializedOccurrences());
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
