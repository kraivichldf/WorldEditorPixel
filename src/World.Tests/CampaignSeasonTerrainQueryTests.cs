using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Campaign.V3;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonTerrainQueryTests
{
    [Fact]
    public void V2_NormalizesLegacyWaterCustomTerrainAndRiverKinds()
    {
        var definition = CreateDefinition(3, 1, 1_000);
        var custom = new CampaignCustomTerrainDefinition(
            "savanna",
            "Savanna",
            CampaignTileType.Plains,
            "#99AA55");
        var world = new CampaignWorld(definition, [custom]);
        world.Tiles.SetTiles(
        [
            new CampaignTileEntry(0, 0, new CampaignTileData(CampaignTileType.Water, -20)),
            new CampaignTileEntry(1, 0, new CampaignTileData(CampaignTileType.Plains, 120, custom.Id)),
            new CampaignTileEntry(2, 0, new CampaignTileData(CampaignTileType.LargeRiver, 40)),
        ]);
        var query = new CampaignSeasonTerrainQueryV2(world);

        var sea = query.GetSample(0, 0);
        var savanna = query.GetSample(1, 0);
        var river = query.GetSample(2, 0);

        Assert.Equal(CampaignTileType.Sea, sea.TerrainType);
        Assert.True(sea.IsSea);
        Assert.Equal(custom.Id, savanna.CustomTerrainId);
        Assert.Equal(CampaignTileType.Plains, savanna.TerrainType);
        Assert.Equal(CampaignTileType.LargeRiver, river.TerrainType);
        Assert.True(river.HasRiver);
    }

    [Fact]
    public void V3_MapsDecomposedSurfacesAndRiverOverlayToPortableTerrain()
    {
        var definition = CreateDefinition(4, 1, 1_000);
        var world = new CampaignWorldV3(definition);
        world.SetTiles(
        [
            new CampaignTileEntryV3(0, 0, new CampaignTileDataV3(CampaignSurfaceType.Wetland, 10)),
            new CampaignTileEntryV3(1, 0, new CampaignTileDataV3(CampaignSurfaceType.Tundra, 20)),
            new CampaignTileEntryV3(2, 0, new CampaignTileDataV3(CampaignSurfaceType.BarrenRock, 900)),
            new CampaignTileEntryV3(3, 0, new CampaignTileDataV3(CampaignSurfaceType.Grassland, 40)),
        ]);
        world.SetRiver(
            3,
            0,
            new RiverTileData(
                RiverOutflow.Unresolved,
                RiverJunctionKind.Confluence,
                RiverSize.Large));
        var query = new CampaignSeasonTerrainQueryV3(world);

        Assert.Equal(CampaignTileType.Plains, query.GetSample(0, 0).TerrainType);
        Assert.Equal(CampaignTileType.Steppe, query.GetSample(1, 0).TerrainType);
        Assert.Equal(CampaignTileType.Mountain, query.GetSample(2, 0).TerrainType);
        var river = query.GetSample(3, 0);
        Assert.Equal(CampaignTileType.RiverJunction, river.TerrainType);
        Assert.True(river.HasRiver);
    }

    [Fact]
    public void TerrainSample_RejectsContradictoryWaterMetadata()
    {
        Assert.Throws<ArgumentException>(() => new CampaignSeasonTerrainSample(
            CampaignTileType.Plains,
            CustomTerrainId: null,
            ElevationMeters: 0,
            CampaignSeasonWaterFeatures.Sea).EnsureValid());
        Assert.Throws<ArgumentException>(() => new CampaignSeasonTerrainSample(
            CampaignTileType.River,
            CustomTerrainId: null,
            ElevationMeters: 0,
            CampaignSeasonWaterFeatures.None).EnsureValid());
    }

    private static CampaignWorldDefinition CreateDefinition(int width, int height, int tileMeters) =>
        CampaignWorldDefinition.Create(
            (long)width * tileMeters,
            (long)height * tileMeters,
            tileMeters,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
}
