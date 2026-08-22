using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonGenerationFingerprintTests
{
    [Fact]
    public void SourceFingerprint_IsCanonicalAndChangesWithAuthoritativeTerrain()
    {
        var definition = CreateDefinition();
        var firstWorld = new CampaignWorld(definition);
        var secondWorld = new CampaignWorld(definition with { });
        var first = Capture(firstWorld);
        var second = Capture(secondWorld);

        var firstFingerprint = CampaignSeasonGenerationFingerprint.GetSourceTerrainFingerprint(
            first.Terrain);
        var secondFingerprint = CampaignSeasonGenerationFingerprint.GetSourceTerrainFingerprint(
            second.Terrain);

        Assert.Equal(firstFingerprint, secondFingerprint);
        Assert.Matches("^[0-9a-f]{64}$", firstFingerprint);

        secondWorld.Tiles.SetTile(
            1,
            2,
            new CampaignTileData(CampaignTileType.Forest, 440));
        var changed = CampaignSeasonGenerationFingerprint.GetSourceTerrainFingerprint(
            Capture(secondWorld).Terrain);
        Assert.NotEqual(firstFingerprint, changed);
    }

    [Fact]
    public void InputFingerprint_TracksGenerationInputsButNotPresentationOnlyColor()
    {
        var catalog = new CampaignSeasonCatalog();
        var settings = new CampaignSeasonGenerationSettings(17);
        var original = CampaignSeasonGenerationFingerprint.GetInputFingerprint(catalog, settings);
        var recoloredBuiltIns = catalog.BuiltInDefinitions
            .Select(definition => new CampaignSeasonDefinition(
                definition.Id,
                definition.Name,
                definition.Fallback,
                definition.Id == CampaignSeasonCatalog.SpringId ? "#112233" : definition.ColorHex,
                definition.TintStrengthPercent,
                definition.EffectIntensityPercent,
                definition.Rule))
            .ToArray();
        var recoloredCatalog = new CampaignSeasonCatalog(
            builtInDefinitions: recoloredBuiltIns);

        Assert.Equal(
            original,
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(recoloredCatalog, settings));
        Assert.NotEqual(
            original,
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(
                catalog,
                new CampaignSeasonGenerationSettings(18)));

        var changedRules = catalog.BuiltInDefinitions
            .Select(definition => definition.Id == CampaignSeasonCatalog.WinterId
                ? new CampaignSeasonDefinition(
                    definition.Id,
                    definition.Name,
                    definition.Fallback,
                    definition.ColorHex,
                    definition.TintStrengthPercent,
                    definition.EffectIntensityPercent,
                    new CampaignSeasonRule(
                        temperatureCelsius: new CampaignSeasonRange(-273.15, 9)))
                : definition)
            .ToArray();
        var changedCatalog = new CampaignSeasonCatalog(builtInDefinitions: changedRules);
        Assert.NotEqual(
            original,
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(changedCatalog, settings));
    }

    [Fact]
    public void DiagnosticEvaluation_ReportsAllIndependentMatchesExactly()
    {
        var definition = CreateDefinition();
        var world = new CampaignWorld(definition);
        var seasons = new CampaignSeasonMap(definition);
        var source = CampaignSeasonGenerationSource.Capture(
            new CampaignSeasonTerrainQueryV2(world),
            seasons);
        var settings = new CampaignSeasonGenerationSettings(
            9,
            enabledSeasonIds:
            [
                CampaignSeasonCatalog.SpringId,
                CampaignSeasonCatalog.SummerId,
            ]);
        var support = CampaignSeasonSupportFields.Build(source.Terrain, settings);

        var diagnostic = CampaignSeasonGenerationDiagnostics.Evaluate(
            support,
            seasons.Catalog,
            settings,
            1,
            1);

        Assert.Equal(
            [CampaignSeasonCatalog.SpringId, CampaignSeasonCatalog.SummerId],
            diagnostic.MatchingSeasonIds);
        Assert.Empty(diagnostic.NonMatchingSeasonIds);
        Assert.Equal(source.Terrain.GetSample(1, 1), diagnostic.Terrain);
    }

    private static CampaignSeasonGenerationSource Capture(CampaignWorld world)
    {
        var seasons = new CampaignSeasonMap(world.Definition);
        return CampaignSeasonGenerationSource.Capture(
            new CampaignSeasonTerrainQueryV2(world),
            seasons);
    }

    private static CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            4_000,
            4_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 0);
}
