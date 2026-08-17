using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Controls;

namespace Kingdom.World.Tests;

public sealed class WorldCanvasAreaSelectionTests
{
    [Theory]
    [InlineData(1, 2, 4, 5, 1, 2, 4, 5)]
    [InlineData(4, 5, 1, 2, 1, 2, 4, 5)]
    [InlineData(-4, -3, 99, 88, 0, 0, 7, 5)]
    public void CreateAreaSelection_NormalizesDirectionAndClipsToWholeTiles(
        int startX,
        int startY,
        int endX,
        int endY,
        int expectedMinimumX,
        int expectedMinimumY,
        int expectedMaximumX,
        int expectedMaximumY)
    {
        var area = WorldCanvas.CreateAreaSelection(
            CreateDefinition(8, 6),
            new CampaignTileCoordinate(startX, startY),
            new CampaignTileCoordinate(endX, endY));

        Assert.Equal(expectedMinimumX, area.MinimumX);
        Assert.Equal(expectedMinimumY, area.MinimumY);
        Assert.Equal(expectedMaximumX, area.MaximumX);
        Assert.Equal(expectedMaximumY, area.MaximumY);
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
