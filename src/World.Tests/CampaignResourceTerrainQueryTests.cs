using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.V3;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceTerrainQueryTests
{
    [Theory]
    [InlineData(CampaignTileType.Unassigned, CampaignResourceTerrainKind.Unassigned, CampaignResourceSurfaceType.Unassigned)]
    [InlineData(CampaignTileType.Water, CampaignResourceTerrainKind.Water, CampaignResourceSurfaceType.Sea)]
    [InlineData(CampaignTileType.Sea, CampaignResourceTerrainKind.Water, CampaignResourceSurfaceType.Sea)]
    [InlineData(CampaignTileType.Lake, CampaignResourceTerrainKind.Water, CampaignResourceSurfaceType.Lake)]
    [InlineData(CampaignTileType.Plains, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland)]
    [InlineData(CampaignTileType.Steppe, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland)]
    [InlineData(CampaignTileType.Hills, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland)]
    [InlineData(CampaignTileType.Beach, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland)]
    [InlineData(CampaignTileType.Coastal, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland)]
    [InlineData(CampaignTileType.Forest, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Forest)]
    [InlineData(CampaignTileType.Desert, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Desert)]
    [InlineData(CampaignTileType.Mountain, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.BarrenRock)]
    [InlineData(CampaignTileType.Cliff, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.BarrenRock)]
    [InlineData(CampaignTileType.River, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland)]
    [InlineData(CampaignTileType.LargeRiver, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland)]
    [InlineData(CampaignTileType.RiverJunction, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland)]
    public void V2_NormalizesLegacySurfaceVocabulary(
        CampaignTileType type,
        CampaignResourceTerrainKind expectedKind,
        CampaignResourceSurfaceType expectedSurface)
    {
        var world = CreateV2World(1, 1);
        if (type != CampaignTileType.Unassigned && type != CampaignTileType.Coastal)
        {
            world.Tiles.SetTile(0, 0, new CampaignTileData(type, 0));
        }

        if (type == CampaignTileType.Coastal)
        {
            // Coastal is write-blocked legacy data, but its normalization remains explicit in the adapter.
            Assert.Throws<ArgumentException>(() =>
                world.Tiles.SetTile(0, 0, new CampaignTileData(type, 0)));
            return;
        }

        var sample = new CampaignResourceTerrainQueryV2(world).GetSample(0, 0);

        Assert.Equal(expectedKind, sample.Kind);
        Assert.Equal(expectedSurface, sample.Surface);
    }

    [Fact]
    public void V2_PreservesCustomIdAndDerivesFormFromPhysicalHeightNotLegacyType()
    {
        var custom = new CampaignCustomTerrainDefinition(
            "orchard",
            "Orchard",
            CampaignTileType.Hills,
            "#467A3A");
        var world = CreateV2World(3, 3, [custom]);
        world.Tiles.SetTile(1, 1, new CampaignTileData(CampaignTileType.Hills, 0, "orchard"));
        var query = new CampaignResourceTerrainQueryV2(world);

        var flat = query.GetSample(1, 1);
        Assert.Equal("orchard", flat.CustomTerrainId);
        Assert.Equal(CampaignResourceTerrainForm.Flat, flat.Form);
        Assert.Equal(0, flat.MaximumCardinalGrade);

        world.Tiles.SetTile(1, 1, new CampaignTileData(CampaignTileType.Hills, 1_500, "orchard"));
        var steep = query.GetSample(1, 1);

        Assert.Equal(CampaignResourceTerrainForm.Cliff, steep.Form);
        Assert.Equal(0.3, steep.MaximumCardinalGrade, 12);
    }

    [Fact]
    public void V2_ReportsExactSeparateEuclideanDistancesAndInvalidatesOnRevision()
    {
        var world = CreateV2World(3, 3, tileSizeMeters: 2_000);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Sea, 0));
        world.Tiles.SetTile(2, 0, new CampaignTileData(CampaignTileType.Lake, 0));
        world.Tiles.SetTile(0, 2, new CampaignTileData(CampaignTileType.River, 0));
        var query = new CampaignResourceTerrainQueryV2(world);

        var sample = query.GetSample(2, 2);

        Assert.Equal(Math.Sqrt(8) * 2, sample.SeaDistanceKilometers, 12);
        Assert.Equal(4, sample.LakeDistanceKilometers, 12);
        Assert.Equal(4, sample.RiverDistanceKilometers, 12);
        Assert.Equal(4, sample.NearestWaterDistanceKilometers, 12);

        world.Tiles.SetTile(1, 2, new CampaignTileData(CampaignTileType.Lake, 0));
        var refreshed = query.GetSample(2, 2);

        Assert.Equal(2, refreshed.LakeDistanceKilometers, 12);
    }

    [Theory]
    [InlineData(CampaignTileType.River, CampaignResourceRiverFeatures.Present)]
    [InlineData(CampaignTileType.LargeRiver, CampaignResourceRiverFeatures.Present | CampaignResourceRiverFeatures.Large)]
    [InlineData(CampaignTileType.RiverJunction, CampaignResourceRiverFeatures.Present | CampaignResourceRiverFeatures.Junction)]
    public void V2_PreservesRiverFeatures(CampaignTileType type, CampaignResourceRiverFeatures expected)
    {
        var world = CreateV2World(1, 1);
        world.Tiles.SetTile(0, 0, new CampaignTileData(type, 0));

        Assert.Equal(expected, new CampaignResourceTerrainQueryV2(world).GetSample(0, 0).RiverFeatures);
    }

    [Fact]
    public void V2_CoastFlagsAreOnlyAppliedOnWaterFacingCells()
    {
        var world = CreateV2World(4, 3);
        world.Tiles.SetTiles(
        [
            new CampaignTileEntry(0, 0, new CampaignTileData(CampaignTileType.Beach, 0)),
            new CampaignTileEntry(1, 0, new CampaignTileData(CampaignTileType.Sea, 0)),
            new CampaignTileEntry(0, 1, new CampaignTileData(CampaignTileType.Cliff, 0)),
            new CampaignTileEntry(1, 1, new CampaignTileData(CampaignTileType.Lake, 0)),
            new CampaignTileEntry(3, 1, new CampaignTileData(CampaignTileType.Cliff, 0)),
            new CampaignTileEntry(2, 2, new CampaignTileData(CampaignTileType.Lake, 0)),
        ]);
        var query = new CampaignResourceTerrainQueryV2(world);

        var beach = query.GetSample(0, 0);
        var sea = query.GetSample(1, 0);
        var cliff = query.GetSample(0, 1);
        var inlandCliff = query.GetSample(3, 1);

        Assert.True(beach.CoastFlags.HasFlag(CampaignResourceCoastFlags.AdjacentSea));
        Assert.True(beach.CoastFlags.HasFlag(CampaignResourceCoastFlags.BeachShore));
        Assert.True(sea.CoastFlags.HasFlag(CampaignResourceCoastFlags.CoastalWater));
        Assert.True(cliff.CoastFlags.HasFlag(CampaignResourceCoastFlags.AdjacentLake));
        Assert.True(cliff.CoastFlags.HasFlag(CampaignResourceCoastFlags.CliffShore));
        Assert.False(inlandCliff.CoastFlags.HasFlag(CampaignResourceCoastFlags.AdjacentLake));
        Assert.False(inlandCliff.CoastFlags.HasFlag(CampaignResourceCoastFlags.CliffShore));
    }

    [Theory]
    [InlineData(CampaignSurfaceType.Unassigned, CampaignResourceTerrainKind.Unassigned, CampaignResourceSurfaceType.Unassigned)]
    [InlineData(CampaignSurfaceType.Grassland, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland)]
    [InlineData(CampaignSurfaceType.Forest, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Forest)]
    [InlineData(CampaignSurfaceType.Desert, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Desert)]
    [InlineData(CampaignSurfaceType.Wetland, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Wetland)]
    [InlineData(CampaignSurfaceType.Tundra, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Tundra)]
    [InlineData(CampaignSurfaceType.BarrenRock, CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.BarrenRock)]
    [InlineData(CampaignSurfaceType.Sea, CampaignResourceTerrainKind.Water, CampaignResourceSurfaceType.Sea)]
    [InlineData(CampaignSurfaceType.Lake, CampaignResourceTerrainKind.Water, CampaignResourceSurfaceType.Lake)]
    public void V3_MapsCanonicalSurfaceAndFormDirectly(
        CampaignSurfaceType surface,
        CampaignResourceTerrainKind expectedKind,
        CampaignResourceSurfaceType expectedSurface)
    {
        var world = CampaignV3TestWorldFactory.Create(1, 1);
        if (surface != CampaignSurfaceType.Unassigned)
        {
            world.SetSurface(0, 0, surface);
        }

        var sample = new CampaignResourceTerrainQueryV3(world).GetSample(0, 0);

        Assert.Equal(expectedKind, sample.Kind);
        Assert.Equal(expectedSurface, sample.Surface);
        Assert.Equal(CampaignResourceTerrainForm.Flat, sample.Form);
        Assert.Null(sample.CustomTerrainId);
    }

    [Fact]
    public void V3_ReportsFormRiverFeaturesAndEffectiveShoreStyle()
    {
        var world = CampaignV3TestWorldFactory.Create(3, 2);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 1_500, CampaignSurfaceType.BarrenRock);
        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 0);
        world.SetTile(2, 0, new CampaignTileDataV3(CampaignSurfaceType.Sea, 0));
        CampaignV3TestWorldFactory.SetLand(world, 0, 1, 0);
        world.SetRiver(
            0,
            1,
            new RiverTileData(
                RiverOutflow.Unresolved,
                RiverJunctionKind.Confluence,
                RiverSize.Large));
        var query = new CampaignResourceTerrainQueryV3(world);

        var high = query.GetSample(0, 0);
        var shore = query.GetSample(1, 0);
        var river = query.GetSample(0, 1);

        Assert.Equal(CampaignResourceTerrainForm.Cliff, high.Form);
        Assert.Equal(0.3, high.MaximumCardinalGrade, 12);
        Assert.True(shore.CoastFlags.HasFlag(CampaignResourceCoastFlags.AdjacentSea));
        Assert.True(shore.CoastFlags.HasFlag(CampaignResourceCoastFlags.BeachShore));
        Assert.Equal(
            CampaignResourceRiverFeatures.Present |
            CampaignResourceRiverFeatures.Large |
            CampaignResourceRiverFeatures.Junction,
            river.RiverFeatures);

        world.SetShoreOverride(1, 0, CardinalDirection.East, ShoreStyle.Cliff);
        var overridden = query.GetSample(1, 0);

        Assert.True(overridden.CoastFlags.HasFlag(CampaignResourceCoastFlags.CliffShore));
        Assert.False(overridden.CoastFlags.HasFlag(CampaignResourceCoastFlags.BeachShore));
    }

    [Fact]
    public void V3_ReportsExactDiagonalDistanceAndRefreshesAfterRiverEdit()
    {
        var world = CampaignV3TestWorldFactory.Create(3, 3);
        world.SetSurface(0, 0, CampaignSurfaceType.Sea);
        var query = new CampaignResourceTerrainQueryV3(world);

        var initial = query.GetSample(2, 2);
        Assert.Equal(Math.Sqrt(8) * 5, initial.SeaDistanceKilometers, 12);
        Assert.True(double.IsPositiveInfinity(initial.RiverDistanceKilometers));

        CampaignV3TestWorldFactory.SetLand(world, 1, 2, 0);
        world.SetRiver(1, 2, new RiverTileData(RiverOutflow.Unresolved));
        var refreshed = query.GetSample(2, 2);

        Assert.Equal(5, refreshed.RiverDistanceKilometers, 12);
        Assert.Equal(5, refreshed.NearestWaterDistanceKilometers, 12);
    }

    [Fact]
    public void QueriesRejectOutOfWorldCoordinatesBeforeDistanceLookup()
    {
        var v2 = new CampaignResourceTerrainQueryV2(CreateV2World(1, 1));
        var v3 = new CampaignResourceTerrainQueryV3(CampaignV3TestWorldFactory.Create(1, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => v2.GetSample(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => v3.GetSample(1, 0));
    }

    [Fact]
    public void ExactDistanceTransformMatchesBruteForceForEveryCellAndSourceKind()
    {
        const int width = 7;
        const int height = 6;
        const int tileSizeMeters = 1_500;
        var sources = new Dictionary<(int X, int Y), CampaignResourceWaterSources>
        {
            [(0, 0)] = CampaignResourceWaterSources.Sea,
            [(5, 1)] = CampaignResourceWaterSources.Sea | CampaignResourceWaterSources.River,
            [(2, 4)] = CampaignResourceWaterSources.Lake,
            [(6, 5)] = CampaignResourceWaterSources.River,
        };
        var field = new CampaignResourceDistanceField(
            width,
            height,
            tileSizeMeters,
            (x, y) => sources.GetValueOrDefault((x, y)));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var actual = field.GetDistances(x, y);
                Assert.Equal(BruteForce(x, y, CampaignResourceWaterSources.Sea), actual.Sea, 12);
                Assert.Equal(BruteForce(x, y, CampaignResourceWaterSources.Lake), actual.Lake, 12);
                Assert.Equal(BruteForce(x, y, CampaignResourceWaterSources.River), actual.River, 12);
            }
        }

        double BruteForce(int x, int y, CampaignResourceWaterSources source)
        {
            var squared = sources
                .Where(pair => (pair.Value & source) != 0)
                .Select(pair =>
                {
                    var deltaX = x - pair.Key.X;
                    var deltaY = y - pair.Key.Y;
                    return (deltaX * deltaX) + (deltaY * deltaY);
                })
                .Min();
            return Math.Sqrt(squared) * (tileSizeMeters / 1_000.0);
        }
    }

    private static CampaignWorld CreateV2World(
        int tilesX,
        int tilesY,
        IEnumerable<CampaignCustomTerrainDefinition>? customDefinitions = null,
        int tileSizeMeters = 5_000) =>
        new(
            CampaignWorldDefinition.Create(
                worldWidthMeters: tilesX * (long)tileSizeMeters,
                worldHeightMeters: tilesY * (long)tileSizeMeters,
                campaignTileSizeMeters: tileSizeMeters,
                seaLevelMeters: 0,
                minimumHeightMeters: -1_000,
                maximumHeightMeters: 6_000),
            customDefinitions);
}
