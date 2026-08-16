using Kingdom.World.Core.Campaign.V3;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Tests;

public sealed class CampaignV3RiverTests
{
    [Fact]
    public void RiverData_PreservesLargeSizeForFutureMigration()
    {
        var data = new RiverTileData(
            RiverOutflow.East,
            RiverJunctionKind.Segment,
            RiverSize.Large);

        Assert.Equal(RiverSize.Large, data.Size);
    }

    [Fact]
    public void RiverData_RejectsUnknownSize()
    {
        var world = CampaignV3TestWorldFactory.Create(1, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => world.SetRiver(
            0,
            0,
            new RiverTileData(
                RiverOutflow.Unresolved,
                RiverJunctionKind.Segment,
                (RiverSize)99)));
    }

    [Fact]
    public void RiverOverlay_PreservesBaseSurfaceAndValidatesDirectedMouth()
    {
        var world = CampaignV3TestWorldFactory.Create(4, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 30, CampaignSurfaceType.Forest);
        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 20, CampaignSurfaceType.Forest);
        CampaignV3TestWorldFactory.SetLand(world, 2, 0, 10, CampaignSurfaceType.Forest);
        world.SetTile(3, 0, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));
        world.SetRivers(
        [
            new RiverTileEntryV3(0, 0, new RiverTileData(RiverOutflow.East)),
            new RiverTileEntryV3(1, 0, new RiverTileData(RiverOutflow.East)),
            new RiverTileEntryV3(2, 0, new RiverTileData(RiverOutflow.East)),
        ]);

        Assert.Equal(CampaignSurfaceType.Forest, world.Tiles.GetTile(1, 0).Surface);
        Assert.Empty(world.Validate());
    }

    [Fact]
    public void ExplicitThreeNeighborConfluence_WithTwoIncomingAndOneOutflow_IsValid()
    {
        var world = CreateValidConfluence();

        Assert.Empty(world.Validate());
    }

    [Fact]
    public void Segment_WithThreeRiverNeighbors_IsInvalid()
    {
        var world = CreateValidConfluence();
        world.SetRiver(
            1,
            1,
            new RiverTileData(RiverOutflow.East, RiverJunctionKind.Segment));

        var errors = world.Validate();

        Assert.Contains(errors, error => error.Contains("maximum is 2", StringComparison.Ordinal));
    }

    [Fact]
    public void FourWayRiverCrossing_IsInvalidEvenWhenMarkedConfluence()
    {
        var world = CampaignV3TestWorldFactory.Create(4, 3);
        CampaignV3TestWorldFactory.SetLand(world, 1, 1, 20);
        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 30);
        CampaignV3TestWorldFactory.SetLand(world, 2, 1, 10);
        CampaignV3TestWorldFactory.SetLand(world, 1, 2, 30);
        CampaignV3TestWorldFactory.SetLand(world, 0, 1, 30);
        world.SetTile(3, 1, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));
        world.SetRivers(
        [
            new RiverTileEntryV3(1, 1, new RiverTileData(RiverOutflow.East, RiverJunctionKind.Confluence)),
            new RiverTileEntryV3(1, 0, new RiverTileData(RiverOutflow.South)),
            new RiverTileEntryV3(2, 1, new RiverTileData(RiverOutflow.East)),
            new RiverTileEntryV3(1, 2, new RiverTileData(RiverOutflow.North)),
            new RiverTileEntryV3(0, 1, new RiverTileData(RiverOutflow.East)),
        ]);

        var errors = world.Validate();

        Assert.Contains(errors, error => error.Contains("four-way crossing", StringComparison.Ordinal));
    }

    [Fact]
    public void AdjacentRivers_RequireExactlyOneFlowAcrossSharedEdge()
    {
        var neither = CampaignV3TestWorldFactory.Create(2, 2);
        neither.SetTile(0, 0, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));
        neither.SetTile(1, 0, new CampaignTileDataV3(CampaignSurfaceType.Lake, 0));
        CampaignV3TestWorldFactory.SetLand(neither, 0, 1, 10);
        CampaignV3TestWorldFactory.SetLand(neither, 1, 1, 10);
        neither.SetRiver(0, 1, new RiverTileData(RiverOutflow.North));
        neither.SetRiver(1, 1, new RiverTileData(RiverOutflow.North));

        var both = CampaignV3TestWorldFactory.Create(2, 1);
        CampaignV3TestWorldFactory.SetLand(both, 0, 0, 10);
        CampaignV3TestWorldFactory.SetLand(both, 1, 0, 10);
        both.SetRiver(0, 0, new RiverTileData(RiverOutflow.East));
        both.SetRiver(1, 0, new RiverTileData(RiverOutflow.West));

        Assert.Contains(
            neither.Validate(),
            error => error.Contains("neither tile flows", StringComparison.Ordinal));
        Assert.Contains(
            both.Validate(),
            error => error.Contains("both tiles flow", StringComparison.Ordinal));
    }

    [Fact]
    public void RiverOutflow_CannotClimbUphill()
    {
        var world = CampaignV3TestWorldFactory.Create(3, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10);
        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 20);
        world.SetTile(2, 0, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));
        world.SetRiver(0, 0, new RiverTileData(RiverOutflow.East));
        world.SetRiver(1, 0, new RiverTileData(RiverOutflow.East));

        var errors = world.Validate();

        Assert.Contains(errors, error => error.Contains("climbs uphill", StringComparison.Ordinal));
    }

    [Fact]
    public void RiverOutflow_MustReachRiverSeaOrLake()
    {
        var world = CampaignV3TestWorldFactory.Create(2, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10);
        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 0);
        world.SetRiver(0, 0, new RiverTileData(RiverOutflow.East));

        var errors = world.Validate();

        Assert.Contains(
            errors,
            error => error.Contains("must enter an adjacent River, Sea, or Lake", StringComparison.Ordinal));
    }

    [Fact]
    public void RiverNetwork_RejectsDirectedCycles()
    {
        var world = CampaignV3TestWorldFactory.Create(2, 2);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10);
        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 10);
        CampaignV3TestWorldFactory.SetLand(world, 1, 1, 10);
        CampaignV3TestWorldFactory.SetLand(world, 0, 1, 10);
        world.SetRivers(
        [
            new RiverTileEntryV3(0, 0, new RiverTileData(RiverOutflow.East)),
            new RiverTileEntryV3(1, 0, new RiverTileData(RiverOutflow.South)),
            new RiverTileEntryV3(1, 1, new RiverTileData(RiverOutflow.West)),
            new RiverTileEntryV3(0, 1, new RiverTileData(RiverOutflow.North)),
        ]);

        var errors = world.Validate();

        Assert.Contains(errors, error => error.Contains("directed cycle", StringComparison.Ordinal));
    }

    [Fact]
    public void UnresolvedOutflow_IsAllowedOnlyForRelaxedMigrationValidation()
    {
        var world = CampaignV3TestWorldFactory.Create(1, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 10);
        world.SetRiver(0, 0, new RiverTileData(RiverOutflow.Unresolved));

        Assert.Empty(world.Validate(requireResolvedRiverOutflows: false));
        Assert.Contains(
            world.Validate(),
            error => error.Contains("unresolved outflow", StringComparison.Ordinal));
        Assert.Throws<WorldValidationException>(() => world.EnsureValid());
    }

    [Fact]
    public void RiverOverlay_RequiresLandAndMustBeRemovedBeforeChangingToWater()
    {
        var waterWorld = CampaignV3TestWorldFactory.Create(1, 1);
        waterWorld.SetTile(0, 0, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));

        Assert.Throws<InvalidOperationException>(() =>
            waterWorld.SetRiver(0, 0, new RiverTileData(RiverOutflow.Unresolved)));

        var riverWorld = CampaignV3TestWorldFactory.Create(1, 1);
        CampaignV3TestWorldFactory.SetLand(riverWorld, 0, 0, 10);
        riverWorld.SetRiver(0, 0, new RiverTileData(RiverOutflow.Unresolved));

        var blocked = Assert.Throws<InvalidOperationException>(() =>
            riverWorld.SetSurface(0, 0, CampaignSurfaceType.Lake));

        Assert.Contains("Remove the River first", blocked.Message);
        Assert.True(riverWorld.RemoveRiver(0, 0));
        Assert.True(riverWorld.SetSurface(0, 0, CampaignSurfaceType.Lake));
    }

    private static CampaignWorldV3 CreateValidConfluence()
    {
        var world = CampaignV3TestWorldFactory.Create(4, 3);
        CampaignV3TestWorldFactory.SetLand(world, 0, 1, 30);
        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 30);
        CampaignV3TestWorldFactory.SetLand(world, 1, 1, 20);
        CampaignV3TestWorldFactory.SetLand(world, 2, 1, 10);
        world.SetTile(3, 1, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));
        world.SetRivers(
        [
            new RiverTileEntryV3(0, 1, new RiverTileData(RiverOutflow.East)),
            new RiverTileEntryV3(1, 0, new RiverTileData(RiverOutflow.South)),
            new RiverTileEntryV3(1, 1, new RiverTileData(RiverOutflow.East, RiverJunctionKind.Confluence)),
            new RiverTileEntryV3(2, 1, new RiverTileData(RiverOutflow.East)),
        ]);
        return world;
    }
}
