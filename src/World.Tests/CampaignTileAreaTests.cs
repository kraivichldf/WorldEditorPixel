using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignTileAreaTests
{
    [Fact]
    public void CenteredArea_ExpandsSymmetricallyIntoCompleteTiles()
    {
        var area = CampaignTileArea.Centered(
            CreateDefinition(5, 5),
            new CampaignTileCoordinate(2, 2),
            radius: 1);

        Assert.Equal(1, area.MinimumX);
        Assert.Equal(1, area.MinimumY);
        Assert.Equal(3, area.MaximumX);
        Assert.Equal(3, area.MaximumY);
        Assert.Equal(3, area.Width);
        Assert.Equal(3, area.Height);
        Assert.Equal(
        [
            new CampaignTileCoordinate(1, 1), new CampaignTileCoordinate(2, 1), new CampaignTileCoordinate(3, 1),
            new CampaignTileCoordinate(1, 2), new CampaignTileCoordinate(2, 2), new CampaignTileCoordinate(3, 2),
            new CampaignTileCoordinate(1, 3), new CampaignTileCoordinate(2, 3), new CampaignTileCoordinate(3, 3),
        ],
        area.EnumerateCoordinates().ToArray());
    }

    [Fact]
    public void CenteredArea_ClipsItsFootprintAtTheWorldEdge()
    {
        var area = CampaignTileArea.Centered(
            CreateDefinition(5, 5),
            new CampaignTileCoordinate(0, 0),
            radius: 2);

        Assert.Equal(0, area.MinimumX);
        Assert.Equal(0, area.MinimumY);
        Assert.Equal(2, area.MaximumX);
        Assert.Equal(2, area.MaximumY);
        Assert.Equal(9, area.EnumerateCoordinates().Count());
    }

    [Fact]
    public void AreaStamp_RecordsEveryUniqueTileAsOneUndoableCommand()
    {
        var tiles = new CampaignTileMap(CreateDefinition(5, 5));
        var data = new CampaignTileData(CampaignTileType.Forest, 250);
        var stroke = new CampaignTileStampBuilder(tiles);

        foreach (var coordinate in CampaignTileArea.Centered(
                     tiles.Definition,
                     new CampaignTileCoordinate(2, 2),
                     radius: 1).EnumerateCoordinates())
        {
            stroke.ApplyTile(coordinate, data);
        }

        var command = stroke.Complete("Stamp 3 × 3 tiles");

        Assert.Equal(9, command.Changes.Count);
        command.Undo();
        Assert.All(
            CampaignTileArea.Centered(tiles.Definition, new CampaignTileCoordinate(2, 2), radius: 1).EnumerateCoordinates(),
            coordinate => Assert.Equal(tiles.DefaultTile, tiles.GetTile(coordinate.X, coordinate.Y)));

        command.Execute();
        Assert.All(
            CampaignTileArea.Centered(tiles.Definition, new CampaignTileCoordinate(2, 2), radius: 1).EnumerateCoordinates(),
            coordinate => Assert.Equal(data, tiles.GetTile(coordinate.X, coordinate.Y)));
    }

    [Fact]
    public void CenteredArea_RejectsNegativeRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignTileArea.Centered(
            CreateDefinition(1, 1),
            new CampaignTileCoordinate(0, 0),
            radius: -1));
    }

    private static CampaignWorldDefinition CreateDefinition(int tilesX, int tilesY) =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: tilesX * 5_000L,
            worldHeightMeters: tilesY * 5_000L,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
}
