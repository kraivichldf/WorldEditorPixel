using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Dialogs;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Tests;

public sealed class EditorViewModelCustomResourceTests
{
    [Fact]
    public void AddCustomResource_RebindsCatalogPreservesOccurrencesClearsHistoryAndSelectsIt()
    {
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(new CampaignWorld(CreateDefinition()), generationResult: null);
        var sourceMap = viewModel.ResourceMap!;
        var stroke = new CampaignResourceStrokeBuilder(sourceMap);
        stroke.Upsert(1, 1, new CampaignResourceOccurrence("iron-ore", 72, Locked: true));
        viewModel.RecordResourceStroke(stroke.Complete("Paint iron"));
        Assert.True(viewModel.CanUndo);
        var custom = CreateCustomResource("mana-crystal", "Mana Crystal");

        var changed = viewModel.UpdateCustomResources([custom], custom.Id);

        Assert.True(changed);
        Assert.NotSame(sourceMap, viewModel.ResourceMap);
        Assert.Same(custom, viewModel.ResourceMap!.Catalog.Get(custom.Id));
        Assert.True(viewModel.ResourceMap.TryGetOccurrence(1, 1, "iron-ore", out var occurrence));
        Assert.Equal((byte)72, occurrence.Potential);
        Assert.True(occurrence.Locked);
        Assert.Equal(custom.Id, viewModel.SelectedResourceId);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.True(viewModel.IsDirty);
        Assert.Contains("Preserved 1 occurrence", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void EditUsedCustomResource_PreservesOccurrenceAndAllowsNonIdentityRuleChanges()
    {
        var original = CreateCustomResource("mana-crystal", "Mana Crystal");
        var viewModel = OpenWithCustomResources([original]);
        viewModel.ResourceMap!.Upsert(
            2,
            1,
            new CampaignResourceOccurrence(original.Id, 88, Locked: true));
        var sourceMap = viewModel.ResourceMap;
        var replacement = new CampaignResourceDefinition(
            original.Id,
            "Refined Mana Crystal",
            original.Category,
            CampaignResourceDistributionProfile.Basin,
            CampaignResourceMedium.Either,
            "crystal",
            "#5A7BD8",
            mapPriority: 82,
            coveragePercent: 11,
            CampaignResourceRichness.Rich,
            CampaignResourceConcentration.FewLarge,
            new CampaignResourceRuleSet(
                CampaignResourceMedium.Either,
                elevationMeters: new CampaignResourceRange(-200, 2_800),
                preferredTerrainTags: ["mineralized", "moist"],
                fieldWeights: new Dictionary<string, double> { ["hydrothermal"] = 2.5 }));

        Assert.True(viewModel.UpdateCustomResources([replacement], replacement.Id));

        Assert.NotSame(sourceMap, viewModel.ResourceMap);
        Assert.Same(replacement, viewModel.ResourceMap!.Catalog.Get(replacement.Id));
        Assert.True(viewModel.ResourceMap.TryGetOccurrence(2, 1, replacement.Id, out var occurrence));
        Assert.Equal(new CampaignResourceOccurrence(replacement.Id, 88, Locked: true), occurrence);
        Assert.Equal("Refined Mana Crystal", viewModel.SelectedResourceOption!.Name);
    }

    [Fact]
    public void ChangingUsedCustomResourceCategoryIsRejectedWithoutMutation()
    {
        var original = CreateCustomResource("mana-crystal", "Mana Crystal");
        var viewModel = OpenWithCustomResources([original]);
        viewModel.ResourceMap!.Upsert(0, 0, new CampaignResourceOccurrence(original.Id, 55));
        var sourceMap = viewModel.ResourceMap;
        var sourceStatus = viewModel.StatusMessage;
        var replacement = CreateCustomResource(
            original.Id,
            original.Name,
            CampaignResourceCategory.Renewable);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            viewModel.UpdateCustomResources([replacement], replacement.Id));

        Assert.Contains("category is locked", exception.Message, StringComparison.Ordinal);
        Assert.Same(sourceMap, viewModel.ResourceMap);
        Assert.Same(original, viewModel.ResourceMap!.Catalog.Get(original.Id));
        Assert.Equal(sourceStatus, viewModel.StatusMessage);
        Assert.True(viewModel.ResourceMap.TryGetOccurrence(0, 0, original.Id, out _));
    }

    [Fact]
    public void DeletingUsedCustomResourceIsRejectedWithoutMutation()
    {
        var original = CreateCustomResource("mana-crystal", "Mana Crystal");
        var viewModel = OpenWithCustomResources([original]);
        viewModel.ResourceMap!.Upsert(3, 2, new CampaignResourceOccurrence(original.Id, 64, Locked: true));
        var sourceMap = viewModel.ResourceMap;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            viewModel.UpdateCustomResources([], selectedResourceId: null));

        Assert.Contains("Erase those occurrences", exception.Message, StringComparison.Ordinal);
        Assert.Same(sourceMap, viewModel.ResourceMap);
        Assert.True(viewModel.ResourceMap!.Catalog.Contains(original.Id));
        Assert.True(viewModel.ResourceMap.TryGetOccurrence(3, 2, original.Id, out _));
    }

    [Fact]
    public void DeletingUnusedCustomResourceDropsOnlyItsGenerationOverride()
    {
        var removed = CreateCustomResource("mana-crystal", "Mana Crystal");
        var retained = CreateCustomResource("amber-resin", "Amber Resin", CampaignResourceCategory.Renewable);
        var settings = new CampaignResourceGenerationSettings(
            resourceSeed: 441,
            seedDerivedFromWorld: false,
            abundance: CampaignResourceAbundance.Custom,
            climate: CampaignResourceClimateProfile.Tropical,
            geology: CampaignResourceGeologyProfile.VolcanicArc,
            overrides:
            [
                CreateOverride(removed.Id, 9),
                CreateOverride(retained.Id, 17),
                CreateOverride("gold", 4),
            ]);
        var viewModel = OpenWithCustomResources([removed, retained], settings);

        Assert.True(viewModel.UpdateCustomResources([retained], retained.Id));

        var replacement = Assert.IsType<CampaignResourceGenerationSettings>(
            viewModel.ResourceGenerationSettings);
        Assert.NotSame(settings, replacement);
        Assert.Equal(441, replacement.ResourceSeed);
        Assert.False(replacement.SeedDerivedFromWorld);
        Assert.Equal(CampaignResourceAbundance.Custom, replacement.Abundance);
        Assert.Equal(CampaignResourceClimateProfile.Tropical, replacement.Climate);
        Assert.Equal(CampaignResourceGeologyProfile.VolcanicArc, replacement.Geology);
        Assert.DoesNotContain(replacement.Overrides, value => value.ResourceId == removed.Id);
        Assert.Contains(replacement.Overrides, value => value.ResourceId == retained.Id);
        Assert.Contains(replacement.Overrides, value => value.ResourceId == "gold");
        Assert.False(viewModel.ResourceMap!.Catalog.Contains(removed.Id));
        Assert.True(viewModel.ResourceMap.Catalog.Contains(retained.Id));
    }

    [Fact]
    public void EquivalentDefinitionsAreANoOpAndPreserveMapAndHistory()
    {
        var original = CreateCustomResource("mana-crystal", "Mana Crystal");
        var viewModel = OpenWithCustomResources([original]);
        var stroke = new CampaignResourceStrokeBuilder(viewModel.ResourceMap!);
        stroke.Upsert(1, 1, new CampaignResourceOccurrence("gold", 45));
        viewModel.RecordResourceStroke(stroke.Complete("Paint gold"));
        var sourceMap = viewModel.ResourceMap;
        var equivalentCopy = CreateCustomResource("mana-crystal", "Mana Crystal");

        var changed = viewModel.UpdateCustomResources([equivalentCopy], equivalentCopy.Id);

        Assert.False(changed);
        Assert.Same(sourceMap, viewModel.ResourceMap);
        Assert.True(viewModel.CanUndo);
        Assert.Contains("unchanged", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EditorItemRoundTripsAdvancedDefinitionRules()
    {
        var definition = new CampaignResourceDefinition(
            "magic-peat",
            "Magic Peat",
            CampaignResourceCategory.Renewable,
            CampaignResourceDistributionProfile.Basin,
            CampaignResourceMedium.Land,
            "peat",
            "#495B45",
            mapPriority: 34,
            coveragePercent: 13,
            CampaignResourceRichness.Poor,
            CampaignResourceConcentration.ManySmall,
            new CampaignResourceRuleSet(
                CampaignResourceMedium.Land,
                elevationMeters: new CampaignResourceRange(-80, 550),
                grade: new CampaignResourceRange(0, 0.45),
                waterDistanceKilometers: new CampaignResourceRange(0, 22),
                regionScaleKilometers: new CampaignResourceRange(8, 42),
                preferredTerrainTags: ["moist", "sedimentary"],
                customTerrainIncludes: ["enchanted-marsh"],
                customTerrainExcludes: ["dry-steppe"],
                fieldWeights: new Dictionary<string, double>
                {
                    ["moisture"] = 2.5,
                    ["temperature"] = -1.25,
                },
                associationWeights: new Dictionary<string, double>
                {
                    ["freshwater"] = 1.75,
                },
                avoidedTerrainTags: ["arid", "exposed-rock"],
                excludedTerrainSurfaces:
                [
                    CampaignResourceSurfaceType.Desert,
                    CampaignResourceSurfaceType.Tundra,
                ]));
        var item = CustomResourceEditorItem.FromDefinition(definition, usageCount: 3);

        var roundTripped = item.BuildDefinition();

        Assert.Equal(definition.Id, roundTripped.Id);
        Assert.Equal(definition.Name, roundTripped.Name);
        Assert.Equal(definition.Category, roundTripped.Category);
        Assert.Equal(definition.DistributionProfile, roundTripped.DistributionProfile);
        Assert.Equal(definition.Medium, roundTripped.Medium);
        Assert.Equal(definition.Rules.ElevationMeters, roundTripped.Rules.ElevationMeters);
        Assert.Equal(definition.Rules.Grade, roundTripped.Rules.Grade);
        Assert.Equal(definition.Rules.WaterDistanceKilometers, roundTripped.Rules.WaterDistanceKilometers);
        Assert.Equal(definition.Rules.RegionScaleKilometers, roundTripped.Rules.RegionScaleKilometers);
        Assert.Equal(definition.Rules.PreferredTerrainTags, roundTripped.Rules.PreferredTerrainTags);
        Assert.Equal(definition.Rules.AvoidedTerrainTags, roundTripped.Rules.AvoidedTerrainTags);
        Assert.Equal(
            definition.Rules.ExcludedTerrainSurfaces,
            roundTripped.Rules.ExcludedTerrainSurfaces);
        Assert.Equal(definition.Rules.CustomTerrainIncludes, roundTripped.Rules.CustomTerrainIncludes);
        Assert.Equal(definition.Rules.CustomTerrainExcludes, roundTripped.Rules.CustomTerrainExcludes);
        Assert.Equal(definition.Rules.FieldWeights, roundTripped.Rules.FieldWeights);
        Assert.Equal(definition.Rules.AssociationWeights, roundTripped.Rules.AssociationWeights);
        Assert.True(item.IsUsed);
    }

    [Fact]
    public void EditorItemRejectsUnsupportedFactorsInsteadOfSilentlyGeneratingZero()
    {
        var item = CustomResourceEditorItem.CreateDefault("unknown-energy");
        item.FieldWeightsText = "unsupported-energy=1";

        var exception = Assert.Throws<ArgumentException>(() => item.BuildDefinition());

        Assert.Contains("unsupported factor", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicatingBuiltInCreatesIndependentIdentityAndPreservesItsRules()
    {
        var builtIn = CampaignResourceCatalog.BuiltInDefinitions.Single(value => value.Id == "fertile-land");
        var item = CustomResourceEditorItem.Duplicate(
            builtIn,
            "highland-fertile-land",
            "Highland Fertile Land");

        var duplicate = item.BuildDefinition();

        Assert.Equal("highland-fertile-land", duplicate.Id);
        Assert.Equal("Highland Fertile Land", duplicate.Name);
        Assert.Equal(builtIn.Category, duplicate.Category);
        Assert.Equal(builtIn.DistributionProfile, duplicate.DistributionProfile);
        Assert.Equal(builtIn.Medium, duplicate.Medium);
        Assert.Equal(builtIn.Rules.PreferredTerrainTags, duplicate.Rules.PreferredTerrainTags);
        Assert.Equal(builtIn.Rules.AvoidedTerrainTags, duplicate.Rules.AvoidedTerrainTags);
        Assert.Equal(
            builtIn.Rules.ExcludedTerrainSurfaces,
            duplicate.Rules.ExcludedTerrainSurfaces);
        Assert.NotSame(builtIn, duplicate);
    }

    private static EditorViewModel OpenWithCustomResources(
        IReadOnlyList<CampaignResourceDefinition> definitions,
        CampaignResourceGenerationSettings? settings = null)
    {
        var worldDefinition = CreateDefinition();
        var viewModel = new EditorViewModel();
        viewModel.OpenWorld(
            new CampaignWorld(worldDefinition),
            new CampaignResourceMap(worldDefinition with { }, new CampaignResourceCatalog(definitions)),
            settings,
            @"F:\Worlds\CustomResources",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\CustomResources");
        return viewModel;
    }

    private static CampaignResourceDefinition CreateCustomResource(
        string id,
        string name,
        CampaignResourceCategory category = CampaignResourceCategory.Finite) =>
        new(
            id,
            name,
            category,
            CampaignResourceDistributionProfile.Vein,
            CampaignResourceMedium.Land,
            "crystal",
            "#7A5BC7",
            mapPriority: 70,
            coveragePercent: 5,
            CampaignResourceRichness.Rich,
            CampaignResourceConcentration.ManySmall,
            new CampaignResourceRuleSet(
                CampaignResourceMedium.Land,
                preferredTerrainTags: ["hydrothermal", "mineralized"]));

    private static CampaignResourceGenerationOverride CreateOverride(string id, int coverage) =>
        new(
            id,
            enabled: true,
            coverage,
            CampaignResourceRichness.Balanced,
            richnessBias: 0,
            CampaignResourceConcentration.Balanced,
            mapPriority: 50);

    private static CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: 4_000,
            worldHeightMeters: 4_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 0);
}
