using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceCatalogTests
{
    [Fact]
    public void BuiltInCatalog_UsesTheAcceptedStableDefinitions()
    {
        var definitions = CampaignResourceCatalog.BuiltInDefinitions;

        Assert.Equal(16, definitions.Count);
        Assert.Equal(
        [
            "fertile-land",
            "timber",
            "fresh-water",
            "fish",
            "grazing",
            "wild-game",
            "stone",
            "clay",
            "sand-gravel",
            "salt",
            "iron-ore",
            "copper-ore",
            "tin-ore",
            "coal",
            "gold",
            "silver",
        ], definitions.Select(static definition => definition.Id));
        Assert.Equal(45, definitions.Single(static value => value.Id == "fertile-land").CoveragePercent);
        Assert.Equal(2, definitions.Single(static value => value.Id == "gold").CoveragePercent);
        Assert.Equal(CampaignResourceMedium.Water, definitions.Single(static value => value.Id == "fish").Medium);
        Assert.Contains(
            CampaignResourceSurfaceType.Desert,
            definitions.Single(static value => value.Id == "fertile-land").Rules.ExcludedTerrainSurfaces);
        Assert.Contains(
            CampaignResourceSurfaceType.Desert,
            definitions.Single(static value => value.Id == "timber").Rules.ExcludedTerrainSurfaces);
        Assert.All(definitions, static definition => definition.EnsureValid());
    }

    [Fact]
    public void Catalog_AddsCustomDefinitionsAndRejectsBuiltInConflicts()
    {
        var crystal = CreateCustom("moon-crystal");
        var catalog = new CampaignResourceCatalog([crystal]);

        Assert.Equal(17, catalog.Definitions.Count);
        Assert.Same(crystal, catalog.Get("moon-crystal"));
        Assert.False(catalog.IsBuiltIn("moon-crystal"));
        Assert.True(catalog.IsBuiltIn("iron-ore"));

        var conflict = CreateCustom("iron-ore");
        var exception = Assert.Throws<ArgumentException>(() => new CampaignResourceCatalog([conflict]));
        Assert.Contains("conflicts with a built-in", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefinitionAndRules_DefensivelyCopyCollections()
    {
        var tags = new[] { "volcanic", "highland" };
        var avoidedTags = new[] { "arid", "lowland" };
        var excludedSurfaces = new[]
        {
            CampaignResourceSurfaceType.Tundra,
            CampaignResourceSurfaceType.Desert,
        };
        var weights = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["mineralized"] = 2,
        };
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            preferredTerrainTags: tags,
            fieldWeights: weights,
            avoidedTerrainTags: avoidedTags,
            excludedTerrainSurfaces: excludedSurfaces);
        var definition = CreateCustom("star-metal", rules: rules);

        tags[0] = "changed";
        avoidedTags[0] = "changed";
        excludedSurfaces[0] = CampaignResourceSurfaceType.Forest;
        weights["mineralized"] = 9;

        Assert.Equal(["highland", "volcanic"], definition.Rules.PreferredTerrainTags);
        Assert.Equal(["arid", "lowland"], definition.Rules.AvoidedTerrainTags);
        Assert.Equal(
            [CampaignResourceSurfaceType.Desert, CampaignResourceSurfaceType.Tundra],
            definition.Rules.ExcludedTerrainSurfaces);
        Assert.Equal(2, definition.Rules.FieldWeights["mineralized"]);
    }

    [Fact]
    public void Catalog_ExposesNonMutableDefinitionViews()
    {
        var catalog = new CampaignResourceCatalog([CreateCustom("moon-crystal")]);

        var definitions = Assert.IsAssignableFrom<IList<CampaignResourceDefinition>>(catalog.Definitions);
        var builtIns = Assert.IsAssignableFrom<IList<CampaignResourceDefinition>>(
            CampaignResourceCatalog.BuiltInDefinitions);
        var customDefinitions = Assert.IsAssignableFrom<IList<CampaignResourceDefinition>>(
            catalog.CustomDefinitions);

        Assert.Throws<NotSupportedException>(() => definitions[0] = CreateCustom("replacement"));
        Assert.Throws<NotSupportedException>(() => builtIns[0] = CreateCustom("replacement"));
        Assert.Throws<NotSupportedException>(() => customDefinitions[0] = CreateCustom("replacement"));
    }

    [Fact]
    public void Rules_RejectInvalidRangesWeightsAndIncludeExcludeConflicts()
    {
        Assert.Throws<ArgumentException>(() => new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            grade: new CampaignResourceRange(0.5, 0.1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            fieldWeights: new Dictionary<string, double> { ["ore"] = 11 }));
        Assert.Throws<ArgumentException>(() => new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            customTerrainIncludes: ["ancient-forest"],
            customTerrainExcludes: ["ancient-forest"]));
        Assert.Throws<ArgumentException>(() => new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            preferredTerrainTags: ["forest"],
            avoidedTerrainTags: ["forest"]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            excludedTerrainSurfaces: [CampaignResourceSurfaceType.Unassigned]));
        Assert.Throws<ArgumentException>(() => new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            excludedTerrainSurfaces:
            [
                CampaignResourceSurfaceType.Desert,
                CampaignResourceSurfaceType.Desert,
            ]));
    }

    [Fact]
    public void Definition_RejectsUnknownEnumsAndInvalidCoverage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignResourceDefinition(
            "bad-resource",
            "Bad Resource",
            (CampaignResourceCategory)99,
            CampaignResourceDistributionProfile.Field,
            CampaignResourceMedium.Land,
            "ore",
            "#112233",
            50,
            20,
            CampaignResourceRichness.Balanced,
            CampaignResourceConcentration.Balanced));

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCustom("too-common", coverage: 101));
        Assert.Throws<ArgumentException>(() => new CampaignResourceDefinition(
            "bad-name",
            "Bad\nName",
            CampaignResourceCategory.Finite,
            CampaignResourceDistributionProfile.Vein,
            CampaignResourceMedium.Land,
            "ore",
            "#112233",
            50,
            20,
            CampaignResourceRichness.Balanced,
            CampaignResourceConcentration.Balanced));
    }

    internal static CampaignResourceDefinition CreateCustom(
        string id,
        int coverage = 0,
        CampaignResourceRuleSet? rules = null) =>
        new(
            id,
            "Custom Resource",
            CampaignResourceCategory.Finite,
            CampaignResourceDistributionProfile.Vein,
            CampaignResourceMedium.Land,
            "ore",
            "#735A91",
            mapPriority: 50,
            coverage,
            CampaignResourceRichness.Balanced,
            CampaignResourceConcentration.Balanced,
            rules);
}
