using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignTileLayerTests
{
    [Fact]
    public void CampaignGrid_UsesExactTileCountWithoutPartialEdges()
    {
        var definition = WorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            heightSampleSpacingMeters: 250,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumElevationMeters: -1_000,
            maximumElevationMeters: 6_000);

        Assert.Equal(140, definition.CampaignTilesX);
        Assert.Equal(140, definition.CampaignTilesY);
        Assert.Equal(19_600, definition.CampaignTilesX * definition.CampaignTilesY);
    }

    [Fact]
    public void TileAssignments_AreSparseAndClearReturnsToImplicitDefault()
    {
        var terrain = TestWorldFactory.Create();

        Assert.Equal(CampaignTileType.Unassigned, terrain.CampaignTiles.GetTileType(1, 1));
        Assert.Equal(0, terrain.CampaignTiles.AssignedTileCount);

        terrain.CampaignTiles.SetTileType(1, 1, CampaignTileType.Forest);

        Assert.Equal(CampaignTileType.Forest, terrain.CampaignTiles.GetTileType(1, 1));
        Assert.Equal(1, terrain.CampaignTiles.AssignedTileCount);

        terrain.CampaignTiles.SetTileType(1, 1, CampaignTileType.Unassigned);

        Assert.Equal(CampaignTileType.Unassigned, terrain.CampaignTiles.GetTileType(1, 1));
        Assert.Equal(0, terrain.CampaignTiles.AssignedTileCount);
    }

    [Fact]
    public void TilePainting_DoesNotChangeContinuousElevationSamples()
    {
        var terrain = TestWorldFactory.Create(initialElevation: 125);
        var stroke = new CampaignTileStrokeBuilder(terrain.CampaignTiles);

        stroke.ApplyTile(new CampaignTileCoordinate(1, 1), CampaignTileType.Hills);
        var command = stroke.Complete("Paint Hills tiles");

        Assert.Single(command.Changes);
        Assert.Equal(CampaignTileType.Hills, terrain.CampaignTiles.GetTileType(1, 1));
        for (var y = 0; y < terrain.Definition.HeightSamplesY; y++)
        {
            for (var x = 0; x < terrain.Definition.HeightSamplesX; x++)
            {
                Assert.Equal(125, terrain.GetHeight(x, y));
            }
        }
    }

    [Fact]
    public void CampaignTileStroke_UndoRestoresAndRedoReappliesWholeTileType()
    {
        var terrain = TestWorldFactory.Create();
        var stroke = new CampaignTileStrokeBuilder(terrain.CampaignTiles);
        stroke.ApplyTile(new CampaignTileCoordinate(0, 0), CampaignTileType.Plains);
        stroke.ApplyTile(new CampaignTileCoordinate(1, 0), CampaignTileType.Forest);
        var command = stroke.Complete("Paint campaign tiles");
        var history = new CommandHistory();
        history.RecordExecuted(command);

        Assert.True(history.Undo());
        Assert.Equal(CampaignTileType.Unassigned, terrain.CampaignTiles.GetTileType(0, 0));
        Assert.Equal(CampaignTileType.Unassigned, terrain.CampaignTiles.GetTileType(1, 0));

        Assert.True(history.Redo());
        Assert.Equal(CampaignTileType.Plains, terrain.CampaignTiles.GetTileType(0, 0));
        Assert.Equal(CampaignTileType.Forest, terrain.CampaignTiles.GetTileType(1, 0));
    }

    [Fact]
    public void CampaignTileLayer_RejectsCoordinatesOutsideGrid()
    {
        var terrain = TestWorldFactory.Create();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            terrain.CampaignTiles.SetTileType(2, 0, CampaignTileType.Plains));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            terrain.CampaignTiles.GetTileType(-1, 0));
    }
}
