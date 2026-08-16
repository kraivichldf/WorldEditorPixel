using Kingdom.World.Core.Campaign.V3;

namespace Kingdom.World.Tests;

public sealed class CampaignV3ShoreTests
{
    [Fact]
    public void EveryCanonicalLandSurface_ReceivesAnAutomaticShore()
    {
        CampaignSurfaceType[] landSurfaces =
        [
            CampaignSurfaceType.Grassland,
            CampaignSurfaceType.Forest,
            CampaignSurfaceType.Desert,
            CampaignSurfaceType.Wetland,
            CampaignSurfaceType.Tundra,
            CampaignSurfaceType.BarrenRock,
        ];

        foreach (var surface in landSurfaces)
        {
            var world = CampaignV3TestWorldFactory.Create(2, 1);
            CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10, surface);
            world.SetTile(1, 0, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));

            Assert.Equal(
                ShoreStyle.Beach,
                world.GetEffectiveShoreStyle(0, 0, CardinalDirection.East));
        }
    }

    [Fact]
    public void ForestHillsRiverAndBeach_ComposeOnOneAuthoritativeTile()
    {
        var world = CampaignV3TestWorldFactory.Create(2, 1);
        CampaignV3TestWorldFactory.SetLand(
            world,
            0,
            0,
            200,
            CampaignSurfaceType.Forest);
        world.SetTile(1, 0, new CampaignTileDataV3(CampaignSurfaceType.Lake, 0));
        world.SetRiver(0, 0, new RiverTileData(RiverOutflow.East));
        world.SetShoreOverride(0, 0, CardinalDirection.East, ShoreStyle.Beach);

        Assert.Equal(CampaignSurfaceType.Forest, world.Tiles.GetTile(0, 0).Surface);
        Assert.Equal(TerrainForm.Hills, world.GetTerrainForm(0, 0));
        Assert.True(world.Rivers.HasRiver(0, 0));
        Assert.Equal(
            ShoreStyle.Beach,
            world.GetEffectiveShoreStyle(0, 0, CardinalDirection.East));
        Assert.Empty(world.Validate());
    }

    [Fact]
    public void AutoShore_DerivesBeachOrCliffFromWaterFacingGrade()
    {
        var world = CampaignV3TestWorldFactory.Create(2, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10);
        world.SetTile(1, 0, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));

        Assert.Equal(
            ShoreStyle.Beach,
            world.GetEffectiveShoreStyle(0, 0, CardinalDirection.East));

        world.SetHeight(0, 0, 2_000);

        Assert.Equal(
            ShoreStyle.Cliff,
            world.GetEffectiveShoreStyle(0, 0, CardinalDirection.East));
    }

    [Fact]
    public void ExplicitShoreOverride_IsPerEdgeAndAutoRemovesIt()
    {
        var world = CampaignV3TestWorldFactory.Create(2, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10);
        world.SetTile(1, 0, new CampaignTileDataV3(CampaignSurfaceType.Lake, 0));

        world.SetShoreOverride(0, 0, CardinalDirection.East, ShoreStyle.Cliff);

        Assert.Equal(1, world.Shores.OverrideCount);
        Assert.Equal(ShoreStyle.Cliff, world.Shores.GetOverride(0, 0, CardinalDirection.East));
        Assert.Equal(
            ShoreStyle.Cliff,
            world.GetEffectiveShoreStyle(0, 0, CardinalDirection.East));

        world.SetShoreOverride(0, 0, CardinalDirection.East, ShoreStyle.Auto);

        Assert.Equal(0, world.Shores.OverrideCount);
        Assert.Equal(ShoreStyle.Auto, world.Shores.GetOverride(0, 0, CardinalDirection.East));
        Assert.Equal(
            ShoreStyle.Beach,
            world.GetEffectiveShoreStyle(0, 0, CardinalDirection.East));
    }

    [Fact]
    public void ShoreOverride_RequiresLandEdgeFacingSeaOrLake()
    {
        var world = CampaignV3TestWorldFactory.Create(2, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10);
        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 0);

        var error = Assert.Throws<InvalidOperationException>(() =>
            world.SetShoreOverride(0, 0, CardinalDirection.East, ShoreStyle.Beach));

        Assert.Contains("must face Sea or Lake", error.Message);
        Assert.Equal(0, world.Shores.OverrideCount);
    }

    [Fact]
    public void SurfaceEdit_ClearsShoreOverrideWhenWaterBoundaryDisappears()
    {
        var world = CampaignV3TestWorldFactory.Create(2, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10);
        world.SetTile(1, 0, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));
        world.SetShoreOverride(0, 0, CardinalDirection.East, ShoreStyle.Cliff);

        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 0);

        Assert.Equal(0, world.Shores.OverrideCount);
        Assert.Empty(world.Shores.Validate());
    }
}
