using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonSeedTests
{
    [Fact]
    public void SeedDerivation_IsStableDistinctAndProducesUnitPhase()
    {
        var seasonSeed = CampaignSeasonSeed.FromTerrainSeed(17_029);
        var winterSeed = CampaignSeasonSeed.ForDefinition(seasonSeed, "winter");
        var summerSeed = CampaignSeasonSeed.ForDefinition(seasonSeed, "summer");
        var phase = CampaignSeasonSeed.ToPhase01(seasonSeed);

        Assert.Equal(-23_794_108, seasonSeed);
        Assert.Equal(568_139_466, winterSeed);
        Assert.Equal(-109_948_710, summerSeed);
        Assert.Equal(0.665139334043488, phase, precision: 15);
        Assert.Equal(seasonSeed, CampaignSeasonSeed.FromTerrainSeed(17_029));
        Assert.Equal(winterSeed, CampaignSeasonSeed.ForDefinition(seasonSeed, "winter"));
        Assert.NotEqual(seasonSeed, CampaignSeasonSeed.FromTerrainSeed(17_030));
        Assert.NotEqual(winterSeed, summerSeed);
        Assert.InRange(phase, 0, Math.BitDecrement(1d));
        Assert.Throws<ArgumentException>(() => CampaignSeasonSeed.ForDefinition(seasonSeed, "Bad ID"));
    }

    [Fact]
    public void CatalogFingerprint_IsStableAcrossCustomInsertionOrderAndChangesWithIds()
    {
        var wet = CampaignSeasonCatalogTests.CreateCustom("wet-season");
        var dry = CampaignSeasonCatalogTests.CreateCustom("dry-season");
        var first = new CampaignSeasonCatalog([wet, dry]);
        var second = new CampaignSeasonCatalog([dry, wet]);
        var changed = new CampaignSeasonCatalog([wet]);

        var firstFingerprint = CampaignSeasonSeed.GetCatalogIdFingerprint(first);

        Assert.Equal(64, firstFingerprint.Length);
        Assert.Equal(firstFingerprint, CampaignSeasonSeed.GetCatalogIdFingerprint(second));
        Assert.NotEqual(firstFingerprint, CampaignSeasonSeed.GetCatalogIdFingerprint(changed));
    }

    [Fact]
    public void DefaultCatalogFingerprint_IsLockedForTheBinaryInterchangeContract()
    {
        var fingerprint = CampaignSeasonSeed.GetCatalogIdFingerprint(new CampaignSeasonCatalog());

        Assert.Equal(
            "C0332D36734678DD75BAB75340DE24823780F18ACC63972E7A6C57E04D431462",
            fingerprint);
    }

    [Fact]
    public void CurrentWorldFallbackSeedUsesValueEqualDefinitionAndTerrainOnly()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 2_000,
            worldHeightMeters: 1_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var catalog = new CampaignSeasonCatalog();
        var firstWorld = new CampaignWorld(definition);
        firstWorld.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Forest, 120));
        var firstMap = new CampaignSeasonMap(definition, catalog);
        var first = CampaignSeasonSeed.FromCurrentWorld(
            CampaignSeasonGenerationSource.Capture(
                new CampaignSeasonTerrainQueryV2(firstWorld),
                firstMap));

        var equalWorld = new CampaignWorld(definition with { });
        equalWorld.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Forest, 120));
        var equalMap = new CampaignSeasonMap(definition with { }, catalog);
        equalMap.Paint(1, 0, CampaignSeasonCatalog.WinterId, locked: true);
        var equal = CampaignSeasonSeed.FromCurrentWorld(
            CampaignSeasonGenerationSource.Capture(
                new CampaignSeasonTerrainQueryV2(equalWorld),
                equalMap));

        equalWorld.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Forest, 121));
        var changed = CampaignSeasonSeed.FromCurrentWorld(
            CampaignSeasonGenerationSource.Capture(
                new CampaignSeasonTerrainQueryV2(equalWorld),
                equalMap));

        Assert.Equal(first, equal);
        Assert.NotEqual(first, changed);
    }
}
