using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Tests;

public sealed class CampaignWorldTests
{
    [Fact]
    public void Definition_SevenHundredKilometresWithFiveKilometreTiles_IsExact140By140Grid()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

        Assert.Equal(140, definition.TilesX);
        Assert.Equal(140, definition.TilesY);
        Assert.Equal(19_600, definition.TileCount);
    }

    [Fact]
    public void Definition_RejectsPartialCampaignTiles()
    {
        Assert.Throws<WorldValidationException>(() => CampaignWorldDefinition.Create(
            worldWidthMeters: 701_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000));
    }

    [Fact]
    public void Definition_AcceptsTheSharedMaximumEditableGrid()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 500_000,
            worldHeightMeters: 500_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

        Assert.Equal(CampaignWorldDefinition.MaximumTileCount, definition.TileCount);
    }

    [Fact]
    public void Definition_RejectsMoreThanTheSharedMaximumEditableGrid()
    {
        var exception = Assert.Throws<WorldValidationException>(() => CampaignWorldDefinition.Create(
            worldWidthMeters: 501_000,
            worldHeightMeters: 500_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000));

        Assert.Contains("250,000 editable tiles", exception.Message, StringComparison.Ordinal);
        Assert.Contains("250,500", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TileData_IsSparseAndResettingBothFieldsRestoresImplicitDefault()
    {
        var world = CreateWorld(2, 2, defaultHeight: 25);

        Assert.Equal(new CampaignTileData(CampaignTileType.Unassigned, 25), world.Tiles.GetTile(1, 1));
        Assert.Equal(0, world.Tiles.MaterializedTileCount);

        world.Tiles.SetTile(1, 1, new CampaignTileData(CampaignTileType.Forest, 450));

        Assert.Equal(new CampaignTileData(CampaignTileType.Forest, 450), world.Tiles.GetTile(1, 1));
        Assert.Equal(1, world.Tiles.MaterializedTileCount);

        world.Tiles.SetTile(1, 1, world.Tiles.DefaultTile);

        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(1, 1));
        Assert.Equal(0, world.Tiles.MaterializedTileCount);
    }

    [Fact]
    public void CustomLandTerrain_RequiresItsMatchingBaseAndCannotBeRemovedWhilePainted()
    {
        var world = CreateWorld(2, 2);
        var farmland = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#91A85A");
        world.Tiles.SetCustomTerrainDefinitions([farmland]);

        world.Tiles.SetTile(
            0,
            0,
            new CampaignTileData(CampaignTileType.Plains, 120, "farmland"));

        Assert.True(world.Tiles.TryGetCustomTerrainDefinition("farmland", out var definition));
        Assert.Equal(farmland, definition);
        Assert.Equal(1, world.Tiles.GetCustomTerrainUsageCount("farmland"));
        Assert.Throws<ArgumentException>(() => world.Tiles.SetTile(
            1,
            0,
            new CampaignTileData(CampaignTileType.Forest, 120, "farmland")));
        Assert.Throws<InvalidOperationException>(() => world.Tiles.SetCustomTerrainDefinitions([]));
    }

    [Fact]
    public void CustomLandTerrain_CanUseSteppeAsItsPortableBase()
    {
        var world = CreateWorld(2, 2);
        var rangeland = new CampaignCustomTerrainDefinition(
            "rangeland",
            "Rangeland",
            CampaignTileType.Steppe,
            "#AA9C59");

        world.Tiles.SetCustomTerrainDefinitions([rangeland]);
        world.Tiles.SetTile(
            0,
            0,
            new CampaignTileData(CampaignTileType.Steppe, 90, rangeland.Id));

        Assert.Equal(
            new CampaignTileData(CampaignTileType.Steppe, 90, rangeland.Id),
            world.Tiles.GetTile(0, 0));
    }

    [Fact]
    public void DerivedHeight_IsExactAtCentresAndLinearBetweenNeighbouringCentres()
    {
        var world = CreateWorld(2, 1);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Plains, 100));
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Hills, 300));

        Assert.Equal(100, world.Tiles.GetDerivedHeight(0.5, 0.5));
        Assert.Equal(300, world.Tiles.GetDerivedHeight(1.5, 0.5));
        Assert.Equal(200, world.Tiles.GetDerivedHeight(1.0, 0.5));
        Assert.Equal(100, world.Tiles.GetDerivedHeight(0, 0.5));
        Assert.Equal(300, world.Tiles.GetDerivedHeight(2, 0.5));
    }

    [Fact]
    public void DerivedHeight_IsContinuousAcrossTileBoundary()
    {
        var world = CreateWorld(2, 1);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Plains, 0));
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Mountain, 1_000));

        var immediatelyLeft = world.Tiles.GetDerivedHeight(1 - 0.000001, 0.5);
        var immediatelyRight = world.Tiles.GetDerivedHeight(1 + 0.000001, 0.5);

        Assert.InRange(Math.Abs(immediatelyLeft - immediatelyRight), 0, 0.003);
    }

    [Fact]
    public void DerivedHeight_BilinearlyBlendsFourTileCentres()
    {
        var world = CreateWorld(2, 2);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Plains, 0));
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Plains, 100));
        world.Tiles.SetTile(0, 1, new CampaignTileData(CampaignTileType.Plains, 200));
        world.Tiles.SetTile(1, 1, new CampaignTileData(CampaignTileType.Plains, 300));

        Assert.Equal(150, world.Tiles.GetDerivedHeight(1, 1));
    }

    [Fact]
    public void NearbyElevation_AveragesCardinalNeighboursAndRoundsToTenMetres()
    {
        var world = CreateWorld(3, 3);
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Plains, 100));
        world.Tiles.SetTile(2, 1, new CampaignTileData(CampaignTileType.Plains, 120));
        world.Tiles.SetTile(1, 2, new CampaignTileData(CampaignTileType.Plains, 160));
        world.Tiles.SetTile(0, 1, new CampaignTileData(CampaignTileType.Plains, 200));

        var suggestion = CampaignElevationHelper.SuggestNearby(
            world.Tiles,
            new CampaignTileCoordinate(1, 1));

        Assert.Equal(150, suggestion.HeightMeters);
        Assert.Equal(4, suggestion.SourceNeighborCount);
    }

    [Fact]
    public void NearbyElevation_AtWorldEdgeUsesOnlyValidCardinalNeighbours()
    {
        var world = CreateWorld(2, 2);
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Plains, 100));
        world.Tiles.SetTile(0, 1, new CampaignTileData(CampaignTileType.Plains, 140));

        var suggestion = CampaignElevationHelper.SuggestNearby(
            world.Tiles,
            new CampaignTileCoordinate(0, 0));

        Assert.Equal(120, suggestion.HeightMeters);
        Assert.Equal(2, suggestion.SourceNeighborCount);
    }

    [Fact]
    public void NearbyElevation_WithoutNeighboursRoundsThePinnedCentreHeight()
    {
        var world = CreateWorld(1, 1);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Hills, 37));

        var suggestion = CampaignElevationHelper.SuggestNearby(
            world.Tiles,
            new CampaignTileCoordinate(0, 0));

        Assert.Equal(40, suggestion.HeightMeters);
        Assert.Equal(0, suggestion.SourceNeighborCount);
    }

    [Fact]
    public void TileStamp_UndoRestoresBothTypeAndHeightAndRedoReappliesThem()
    {
        var world = CreateWorld(2, 1);
        var stroke = new CampaignTileStampBuilder(world.Tiles);
        stroke.ApplyTile(
            new CampaignTileCoordinate(0, 0),
            new CampaignTileData(CampaignTileType.Forest, 250));
        stroke.ApplyTile(
            new CampaignTileCoordinate(1, 0),
            new CampaignTileData(CampaignTileType.Mountain, 900));
        var command = stroke.Complete("Stamp campaign tiles");
        var history = new CommandHistory();
        history.RecordExecuted(command);

        Assert.True(history.Undo());
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(0, 0));
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(1, 0));

        Assert.True(history.Redo());
        Assert.Equal(new CampaignTileData(CampaignTileType.Forest, 250), world.Tiles.GetTile(0, 0));
        Assert.Equal(new CampaignTileData(CampaignTileType.Mountain, 900), world.Tiles.GetTile(1, 0));
    }

    [Fact]
    public void RiverConnections_AreDerivedAcrossRegularAndLargeSegments()
    {
        var world = CreateWorld(3, 3);
        var river = new CampaignTileData(CampaignTileType.River, 20);
        var largeRiver = new CampaignTileData(CampaignTileType.LargeRiver, 20);
        world.Tiles.SetTile(1, 1, largeRiver);
        world.Tiles.SetTile(1, 0, river);
        world.Tiles.SetTile(1, 2, largeRiver);

        Assert.Equal(
            RiverConnections.North | RiverConnections.South,
            world.Tiles.GetRiverConnections(1, 1));
        Assert.Equal(RiverConnections.South, world.Tiles.GetRiverConnections(1, 0));
        Assert.Equal(RiverConnections.North, world.Tiles.GetRiverConnections(1, 2));
        Assert.Equal(3, world.Tiles.RiverTileCount);
        Assert.Equal(
            RiverConnections.South,
            world.Tiles.GetRiverConnections(1, 0));
    }

    [Fact]
    public void RiverTopology_BlocksAThirdExitWithoutChangingTheMap()
    {
        var world = CreateWorld(3, 3);
        var river = new CampaignTileData(CampaignTileType.River, 20);
        world.Tiles.SetTile(1, 1, river);
        world.Tiles.SetTile(1, 0, river);
        world.Tiles.SetTile(1, 2, river);
        var revisionBeforeAttempt = world.Tiles.Revision;

        var accepted = world.Tiles.TrySetTile(2, 1, river, out var failureReason);

        Assert.False(accepted);
        Assert.Contains("3 exits", failureReason);
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(2, 1));
        Assert.Equal(revisionBeforeAttempt, world.Tiles.Revision);
        Assert.Equal(3, world.Tiles.RiverTileCount);
    }

    [Fact]
    public void RiverTopology_BlocksAMixedWidthThirdExit()
    {
        var world = CreateWorld(3, 3);
        var river = new CampaignTileData(CampaignTileType.River, 20);
        var largeRiver = new CampaignTileData(CampaignTileType.LargeRiver, 20);
        world.Tiles.SetTile(1, 1, largeRiver);
        world.Tiles.SetTile(1, 0, river);
        world.Tiles.SetTile(1, 2, largeRiver);

        var accepted = world.Tiles.TrySetTile(2, 1, river, out var failureReason);

        Assert.False(accepted);
        Assert.Contains("3 exits", failureReason);
        Assert.Equal(3, world.Tiles.RiverTileCount);
    }

    [Fact]
    public void RiverSplit_TwoBranchesCreatesOneUndoableYJunction()
    {
        var world = CreateWorld(7, 7);
        var river = new CampaignTileData(CampaignTileType.River, 40);
        world.Tiles.SetTile(3, 5, river);
        world.Tiles.SetTile(3, 4, river);

        var accepted = CampaignRiverSplitBuilder.TryCreate(
            world.Tiles,
            new CampaignTileCoordinate(3, 4),
            branchCount: 2,
            requestedDirection: null,
            out var command,
            out var failureReason);

        Assert.True(accepted, failureReason);
        Assert.NotNull(command);
        Assert.Equal(CampaignTileType.RiverJunction, world.Tiles.GetTile(3, 3).Type);
        Assert.Equal(CampaignTileType.River, world.Tiles.GetTile(2, 3).Type);
        Assert.Equal(CampaignTileType.River, world.Tiles.GetTile(4, 3).Type);
        Assert.Equal(3, CountConnections(world.Tiles.GetRiverConnections(3, 3)));

        var history = new CommandHistory();
        history.RecordExecuted(command!);
        Assert.True(history.Undo());
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(3, 3));
        Assert.True(history.Redo());
        Assert.Equal(CampaignTileType.RiverJunction, world.Tiles.GetTile(3, 3).Type);
    }

    [Fact]
    public void RiverSplit_FourBranchesCascadesYJunctionsWithoutAFourWayTile()
    {
        var world = CreateWorld(9, 9);
        var largeRiver = new CampaignTileData(CampaignTileType.LargeRiver, 25);
        world.Tiles.SetTile(4, 7, largeRiver);
        world.Tiles.SetTile(4, 6, largeRiver);

        var accepted = CampaignRiverSplitBuilder.TryCreate(
            world.Tiles,
            new CampaignTileCoordinate(4, 6),
            branchCount: 4,
            requestedDirection: null,
            out var command,
            out var failureReason);

        Assert.True(accepted, failureReason);
        Assert.Equal(7, command!.Changes.Count);
        Assert.Equal(3, command.Changes.Count(change =>
            change.After.Type == CampaignTileType.RiverJunction));
        Assert.Equal(4, command.Changes.Count(change =>
            change.After.Type == CampaignTileType.LargeRiver));
        Assert.All(world.Tiles.GetRiverTiles(), entry =>
        {
            var exits = CountConnections(world.Tiles.GetRiverConnections(entry.X, entry.Y));
            Assert.InRange(
                exits,
                0,
                entry.Data.Type == CampaignTileType.RiverJunction ? 3 : 2);
        });
    }

    [Fact]
    public void RiverSplit_ThreeBranchesCanUseAnExplicitDirectionFromAnIsolatedRoot()
    {
        var world = CreateWorld(9, 9);
        world.Tiles.SetTile(
            3,
            4,
            new CampaignTileData(CampaignTileType.River, 60));

        var accepted = CampaignRiverSplitBuilder.TryCreate(
            world.Tiles,
            new CampaignTileCoordinate(3, 4),
            branchCount: 3,
            requestedDirection: RiverSplitDirection.East,
            out var command,
            out var failureReason);

        Assert.True(accepted, failureReason);
        Assert.Equal(5, command!.Changes.Count);
        Assert.Equal(2, command.Changes.Count(change =>
            change.After.Type == CampaignTileType.RiverJunction));
        Assert.Equal(3, command.Changes.Count(change =>
            change.After.Type == CampaignTileType.River));
        Assert.All(world.Tiles.GetRiverTiles(), entry =>
            Assert.NotEqual(4, CountConnections(world.Tiles.GetRiverConnections(entry.X, entry.Y))));
    }

    [Fact]
    public void RiverSplit_RejectsExistingRiverCollisionWithoutChangingTheMap()
    {
        var world = CreateWorld(7, 7);
        var river = new CampaignTileData(CampaignTileType.River, 40);
        world.Tiles.SetTile(3, 5, river);
        world.Tiles.SetTile(3, 4, river);
        world.Tiles.SetTile(2, 3, river);
        var revision = world.Tiles.Revision;

        var accepted = CampaignRiverSplitBuilder.TryCreate(
            world.Tiles,
            new CampaignTileCoordinate(3, 4),
            branchCount: 2,
            requestedDirection: null,
            out var command,
            out var failureReason);

        Assert.False(accepted);
        Assert.Null(command);
        Assert.Contains("intersect", failureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(revision, world.Tiles.Revision);
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(3, 3));
    }

    [Fact]
    public void RiverTopology_RejectsInvalidBatchAtomically()
    {
        var world = CreateWorld(3, 3);
        var river = new CampaignTileData(CampaignTileType.River, 20);
        var entries = new[]
        {
            new CampaignTileEntry(1, 1, river),
            new CampaignTileEntry(1, 0, river),
            new CampaignTileEntry(2, 1, river),
            new CampaignTileEntry(1, 2, river),
        };

        Assert.Throws<CampaignTileTopologyException>(() => world.Tiles.SetTiles(entries));
        Assert.Equal(0, world.Tiles.MaterializedTileCount);
        Assert.Equal(0, world.Tiles.RiverTileCount);
        Assert.Equal(0, world.Tiles.Revision);
    }

    [Fact]
    public void RiverTileStamp_UndoAndRedoPreserveAConnectedPath()
    {
        var world = CreateWorld(3, 1);
        var river = new CampaignTileData(CampaignTileType.River, 30);
        var stroke = new CampaignTileStampBuilder(world.Tiles);
        stroke.ApplyTile(new CampaignTileCoordinate(0, 0), river);
        stroke.ApplyTile(new CampaignTileCoordinate(1, 0), river);
        stroke.ApplyTile(new CampaignTileCoordinate(2, 0), river);
        var history = new CommandHistory();
        history.RecordExecuted(stroke.Complete("Stamp river"));

        Assert.True(history.Undo());
        Assert.Equal(0, world.Tiles.MaterializedTileCount);
        Assert.Equal(0, world.Tiles.RiverTileCount);

        Assert.True(history.Redo());
        Assert.Equal(3, world.Tiles.RiverTileCount);
        Assert.Equal(
            RiverConnections.East | RiverConnections.West,
            world.Tiles.GetRiverConnections(1, 0));
    }

    [Fact]
    public void RiverIndex_EnumeratesOnlyCurrentRiverTiles()
    {
        var world = CreateWorld(4, 1);
        var river = new CampaignTileData(CampaignTileType.River, 30);
        world.Tiles.SetTiles(
        [
            new CampaignTileEntry(0, 0, river),
            new CampaignTileEntry(1, 0, river),
            new CampaignTileEntry(2, 0, river),
        ]);
        world.Tiles.SetTile(3, 0, new CampaignTileData(CampaignTileType.Forest, 200));

        Assert.Equal(
            [0, 1, 2],
            world.Tiles.GetRiverTiles().Select(entry => entry.X).Order().ToArray());

        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Plains, 20));

        Assert.Equal(2, world.Tiles.RiverTileCount);
        Assert.Equal(
            [0, 2],
            world.Tiles.GetRiverTiles().Select(entry => entry.X).Order().ToArray());
    }

    private static int CountConnections(RiverConnections connections) =>
        Enum.GetValues<RiverConnections>()
            .Where(static connection => connection != RiverConnections.None)
            .Count(connection => connections.HasFlag(connection));

    [Fact]
    public void AutomaticCoast_NorthSeaUsesTenPercentWaterThenOriginalPlains()
    {
        var world = CreateWorld(1, 2);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Sea, 0));
        world.Tiles.SetTile(0, 1, new CampaignTileData(CampaignTileType.Plains, 10));

        Assert.Equal(
            AutomaticCoastSurfaceMaterial.Sea,
            world.Tiles.GetAutomaticCoastSurfaceMaterial(0, 1, 0.5, 0.05));
        Assert.Equal(
            AutomaticCoastSurfaceMaterial.Original,
            world.Tiles.GetAutomaticCoastSurfaceMaterial(0, 1, 0.5, 0.10));
        Assert.Equal(
            AutomaticCoastSurfaceMaterial.Original,
            world.Tiles.GetAutomaticCoastSurfaceMaterial(0, 1, 0.5, 0.20));
        Assert.Equal(CampaignTileType.Plains, world.Tiles.GetTile(0, 1).Type);
    }

    [Fact]
    public void AutomaticCoast_EastLakePreservesCustomLandIdentityInsideWaterBand()
    {
        var world = CreateWorld(2, 1);
        var farmland = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#91A85A");
        world.Tiles.SetCustomTerrainDefinitions([farmland]);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Plains, 10, "farmland"));
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Lake, 0));

        Assert.Equal(
            AutomaticCoastSurfaceMaterial.Lake,
            world.Tiles.GetAutomaticCoastSurfaceMaterial(0, 0, 0.95, 0.5));
        Assert.Equal(
            AutomaticCoastSurfaceMaterial.Original,
            world.Tiles.GetAutomaticCoastSurfaceMaterial(0, 0, 0.88, 0.5));
        Assert.Equal("farmland", world.Tiles.GetTile(0, 0).CustomTerrainId);
    }

    [Fact]
    public void AutomaticCoast_ChoosesTheNearestWaterFacingEdgeAtCorners()
    {
        var world = CreateWorld(2, 2);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Sea, 0));
        world.Tiles.SetTile(1, 1, new CampaignTileData(CampaignTileType.Lake, 0));
        world.Tiles.SetTile(0, 1, new CampaignTileData(CampaignTileType.Cliff, 10));

        Assert.Equal(
            AutomaticCoastSurfaceMaterial.Sea,
            world.Tiles.GetAutomaticCoastSurfaceMaterial(0, 1, 0.92, 0.03));
        Assert.Equal(
            AutomaticCoastSurfaceMaterial.Lake,
            world.Tiles.GetAutomaticCoastSurfaceMaterial(0, 1, 0.98, 0.08));
        Assert.Equal(CampaignTileType.Cliff, world.Tiles.GetTile(0, 1).Type);
    }

    [Fact]
    public void AutomaticCoast_WithoutAdjacentWaterUsesOriginalMaterial()
    {
        var world = CreateWorld(1, 1);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Hills, 10));

        Assert.Equal(
            AutomaticCoastSurfaceMaterial.Original,
            world.Tiles.GetAutomaticCoastSurfaceMaterial(0, 0, 0.01, 0.01));
    }

    [Fact]
    public void CoastalType_IsRejectedBecauseCoastIsDerivedFromLandAdjacency()
    {
        var world = CreateWorld(1, 1);

        var exception = Assert.Throws<ArgumentException>(() => world.Tiles.SetTile(
            0,
            0,
            new CampaignTileData(CampaignTileType.Coastal, 10)));

        Assert.Contains("legacy read-only", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CampaignWorld CreateWorld(int tilesX, int tilesY, short defaultHeight = 0) =>
        new(CampaignWorldDefinition.Create(
            worldWidthMeters: tilesX * 5_000L,
            worldHeightMeters: tilesY * 5_000L,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: defaultHeight));
}
