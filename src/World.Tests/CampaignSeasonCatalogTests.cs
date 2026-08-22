using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonCatalogTests
{
    [Fact]
    public void BuiltIns_ExposeStableIdentityOrderFallbacksAndStarterRules()
    {
        var catalog = new CampaignSeasonCatalog();

        Assert.Equal(
        [
            "spring",
            "summer",
            "fall",
            "winter",
        ], catalog.Definitions.Select(static value => value.Id));
        Assert.Equal(
            CampaignBuiltInSeason.Spring,
            catalog.Get(CampaignSeasonCatalog.SpringId).Fallback);
        Assert.Equal(
            new CampaignSeasonRange(-273.15, 5),
            catalog.Get(CampaignSeasonCatalog.WinterId).Rule.ColdSeasonTemperatureCelsius);
        Assert.Equal(
            new CampaignSeasonRange(0.12, 1),
            catalog.Get(CampaignSeasonCatalog.SpringId).Rule.Seasonality);
        Assert.Equal(
            new CampaignSeasonRange(0.12, 1),
            catalog.Get(CampaignSeasonCatalog.FallId).Rule.Seasonality);
        Assert.Equal(
            new CampaignSeasonRange(10, 100),
            catalog.Get(CampaignSeasonCatalog.SummerId).Rule.WarmSeasonTemperatureCelsius);
        Assert.All(catalog.Definitions, static definition => definition.EnsureValid());
    }

    [Fact]
    public void Catalog_AddsCustomDefinitionsInStableOrderAndRejectsBuiltInConflict()
    {
        var wet = CreateCustom("wet-season", CampaignBuiltInSeason.Summer);
        var monsoon = CreateCustom("monsoon", CampaignBuiltInSeason.Fall);
        var catalog = new CampaignSeasonCatalog([wet, monsoon]);

        Assert.Equal(
        [
            "spring",
            "summer",
            "fall",
            "winter",
            "monsoon",
            "wet-season",
        ], catalog.Definitions.Select(static value => value.Id));
        Assert.Same(monsoon, catalog.GetByIndex(catalog.GetIndex("monsoon")));
        Assert.False(catalog.IsBuiltIn("monsoon"));
        Assert.True(catalog.IsBuiltIn("winter"));

        var conflict = CreateCustom("winter", CampaignBuiltInSeason.Winter);
        Assert.Throws<ArgumentException>(() => new CampaignSeasonCatalog([conflict]));
    }

    [Fact]
    public void Catalog_ExposesNonMutableViewsAndAcceptedTechnicalLimit()
    {
        var catalog = new CampaignSeasonCatalog(
            [CreateCustom("wet-season", CampaignBuiltInSeason.Summer)]);

        Assert.Equal(ushort.MaxValue, CampaignSeasonCatalog.MaximumDefinitionCount);
        var definitions = Assert.IsAssignableFrom<IList<CampaignSeasonDefinition>>(catalog.Definitions);
        var builtIns = Assert.IsAssignableFrom<IList<CampaignSeasonDefinition>>(catalog.BuiltInDefinitions);
        var customs = Assert.IsAssignableFrom<IList<CampaignSeasonDefinition>>(catalog.CustomDefinitions);
        Assert.Throws<NotSupportedException>(() => definitions[0] = CreateCustom("replacement"));
        Assert.Throws<NotSupportedException>(() => builtIns[0] = CreateCustom("replacement"));
        Assert.Throws<NotSupportedException>(() => customs[0] = CreateCustom("replacement"));
    }

    [Fact]
    public void Catalog_AllowsBuiltInRuleOverrideButProtectsNameAndFallback()
    {
        var defaults = CampaignSeasonCatalog.DefaultBuiltInDefinitions.ToArray();
        var defaultWinter = defaults.Single(static value => value.Id == "winter");
        var changedWinter = new CampaignSeasonDefinition(
            defaultWinter.Id,
            defaultWinter.Name,
            defaultWinter.Fallback,
            defaultWinter.ColorHex,
            defaultWinter.TintStrengthPercent,
            defaultWinter.EffectIntensityPercent,
            new CampaignSeasonRule(
                temperatureCelsius: new CampaignSeasonRange(-273.15, 8)));
        defaults[Array.FindIndex(defaults, static value => value.Id == "winter")] = changedWinter;

        var catalog = new CampaignSeasonCatalog(builtInDefinitions: defaults);

        Assert.Same(changedWinter, catalog.Get("winter"));
        Assert.Throws<ArgumentException>(() => new CampaignSeasonCatalog(
            builtInDefinitions:
            [
                .. defaults.Where(static value => value.Id != "spring"),
                new CampaignSeasonDefinition(
                    "spring",
                    "Renamed Spring",
                    CampaignBuiltInSeason.Spring,
                    "#112233",
                    20,
                    20),
            ]));
    }

    [Fact]
    public void Rule_DefensivelyCopiesAndSortsTerrainFilters()
    {
        var includes = new[] { CampaignTileType.Mountain, CampaignTileType.Plains };
        var customExcludes = new[] { "burned-land", "ancient-forest" };
        var rule = new CampaignSeasonRule(
            terrainIncludes: includes,
            customTerrainExcludes: customExcludes);

        includes[0] = CampaignTileType.Sea;
        customExcludes[0] = "changed";

        Assert.Equal(
            [CampaignTileType.Plains, CampaignTileType.Mountain],
            rule.TerrainIncludes);
        Assert.Equal(["ancient-forest", "burned-land"], rule.CustomTerrainExcludes);
        var mutable = Assert.IsAssignableFrom<IList<CampaignTileType>>(rule.TerrainIncludes);
        Assert.Throws<NotSupportedException>(() => mutable[0] = CampaignTileType.Sea);
    }

    [Fact]
    public void Rule_UsesWhitelistExcludeAndCustomBaseInheritanceSemantics()
    {
        var rule = new CampaignSeasonRule(
            terrainIncludes: [CampaignTileType.Forest],
            terrainExcludes: [CampaignTileType.Desert],
            customTerrainIncludes: ["savanna"],
            customTerrainExcludes: ["burned-forest"]);

        Assert.True(rule.AllowsTerrain(CampaignTileType.Forest));
        Assert.True(rule.AllowsTerrain(CampaignTileType.Forest, "old-growth"));
        Assert.True(rule.AllowsTerrain(CampaignTileType.Plains, "savanna"));
        Assert.False(rule.AllowsTerrain(CampaignTileType.Plains));
        Assert.False(rule.AllowsTerrain(CampaignTileType.Desert));
        Assert.False(rule.AllowsTerrain(CampaignTileType.Forest, "burned-forest"));
        Assert.True(CampaignSeasonRule.Unrestricted.AllowsTerrain(CampaignTileType.Sea));
    }

    [Fact]
    public void Rule_RejectsInvalidRangesLegacyTerrainAndFilterConflicts()
    {
        Assert.Throws<ArgumentException>(() => new CampaignSeasonRule(
            moisture: new CampaignSeasonRange(0.8, 0.2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonRule(
            seasonality: new CampaignSeasonRange(-0.1, 0.5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonRule(
            seaDistanceKilometers: new CampaignSeasonRange(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonRule(
            terrainIncludes: [CampaignTileType.Coastal]));
        Assert.Throws<ArgumentException>(() => new CampaignSeasonRule(
            terrainIncludes: [CampaignTileType.Forest],
            terrainExcludes: [CampaignTileType.Forest]));
        Assert.Throws<ArgumentException>(() => new CampaignSeasonRule(
            customTerrainIncludes: ["savanna"],
            customTerrainExcludes: ["savanna"]));
    }

    [Fact]
    public void Definition_RejectsInvalidPresentationAndFallbackValues()
    {
        Assert.Throws<ArgumentException>(() => CreateCustom("Bad ID", CampaignBuiltInSeason.Spring));
        Assert.Throws<ArgumentException>(() => new CampaignSeasonDefinition(
            "wet",
            " Wet ",
            CampaignBuiltInSeason.Spring,
            "#112233",
            20,
            20));
        Assert.Throws<ArgumentException>(() => new CampaignSeasonDefinition(
            "wet",
            "Wet",
            CampaignBuiltInSeason.Spring,
            "blue",
            20,
            20));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonDefinition(
            "wet",
            "Wet",
            (CampaignBuiltInSeason)99,
            "#112233",
            20,
            20));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonDefinition(
            "wet",
            "Wet",
            CampaignBuiltInSeason.Spring,
            "#112233",
            101,
            20));
    }

    internal static CampaignSeasonDefinition CreateCustom(
        string id,
        CampaignBuiltInSeason fallback = CampaignBuiltInSeason.Spring,
        CampaignSeasonRule? rule = null) =>
        new(
            id,
            "Custom Season",
            fallback,
            "#735A91",
            tintStrengthPercent: 50,
            effectIntensityPercent: 50,
            rule);
}
