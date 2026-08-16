using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Models;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Tests;

public sealed class EditorViewModelResourceTests
{
    [Fact]
    public void CreateWorld_InstallsEmptyBuiltInsAndExposesIndependentResourceControls()
    {
        var viewModel = new EditorViewModel();
        var world = new CampaignWorld(CreateDefinition(4, 4));

        viewModel.CreateWorld(world, generationResult: null);
        viewModel.SwitchToResourcesWorkspace();
        viewModel.ResourcePotential = 500;
        viewModel.ResourcePaintAreaRadius = 99;
        viewModel.SelectResourceEraseTool();

        Assert.NotNull(viewModel.ResourceMap);
        Assert.Equal(world.Definition, viewModel.ResourceMap.Definition);
        Assert.Equal(16, viewModel.ResourceOptions.Count);
        Assert.All(viewModel.ResourceOptions, option =>
            Assert.Equal($"ID: {option.Id}", option.IdText));
        Assert.Equal(0, viewModel.ResourceOccurrenceCount);
        Assert.Equal("No resource occurrences", viewModel.ResourceStatusText);
        Assert.Null(viewModel.ResourceGenerationSettings);
        Assert.True(viewModel.IsResourcesWorkspace);
        Assert.False(viewModel.IsTerrainWorkspace);
        Assert.True(viewModel.CanEditResources);
        Assert.Equal("Campaign resource potential", viewModel.CanvasTitle);
        Assert.Contains("potential 1–100", viewModel.FooterFormatText, StringComparison.Ordinal);
        Assert.Equal(100, viewModel.ResourcePotential);
        Assert.Equal(12, viewModel.ResourcePaintAreaRadius);
        Assert.Equal("25 × 25 tiles", viewModel.ResourcePaintAreaText);
        Assert.True(viewModel.LockManualResourceEdits);
        Assert.True(viewModel.IsResourceEraseTool);
        Assert.False(viewModel.IsResourceAddUpdateTool);
        Assert.False(viewModel.HasPinnedResourceOccurrences);
        Assert.True(viewModel.HasNoPinnedResourceOccurrences);
        Assert.Contains("erase selected", viewModel.ResourceStampSummary, StringComparison.Ordinal);

        viewModel.SelectedResourceCategoryFilter = CampaignResourceCategoryFilter.Renewable;
        Assert.NotEmpty(viewModel.ResourceOptions);
        Assert.All(viewModel.ResourceOptions, option =>
            Assert.Equal(CampaignResourceCategory.Renewable, option.Category));

        viewModel.SwitchToTerrainWorkspace();
        Assert.True(viewModel.IsTerrainWorkspace);
        Assert.Equal("Campaign tile surface", viewModel.CanvasTitle);
    }

    [Fact]
    public void OpenWorld_InstallsValueEqualLoadedResourcesSettingsAndCustomOptions()
    {
        var definition = CreateDefinition(4, 4);
        var equalButDistinctDefinition = definition with { };
        var world = new CampaignWorld(definition);
        var custom = CreateCustomResource();
        var catalog = new CampaignResourceCatalog([custom]);
        var resources = new CampaignResourceMap(equalButDistinctDefinition, catalog);
        resources.Upsert(2, 1, new CampaignResourceOccurrence(custom.Id, 67, Locked: true));
        var settings = CreateSettings(custom);
        var viewModel = new EditorViewModel();

        viewModel.OpenWorld(
            world,
            resources,
            settings,
            @"F:\Worlds\LoadedResources",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\LoadedResources");

        Assert.NotSame(definition, equalButDistinctDefinition);
        Assert.Equal(definition, equalButDistinctDefinition);
        Assert.Same(resources, viewModel.ResourceMap);
        Assert.Same(settings, viewModel.ResourceGenerationSettings);
        Assert.Equal(1, viewModel.ResourceOccurrenceCount);
        Assert.Contains(viewModel.ResourceOptions, option =>
            option.Id == custom.Id && option.IsCustom);
        Assert.False(viewModel.IsDirty);
        Assert.Contains("1 resource occurrence", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenWorld_MismatchedResourceDefinitionThrowsWithoutMutatingDocument()
    {
        var viewModel = new EditorViewModel();
        var current = new CampaignWorld(CreateDefinition(4, 4));
        viewModel.CreateWorld(current, generationResult: null);
        viewModel.MarkSaved(@"F:\Worlds\Current");
        var originalResources = viewModel.ResourceMap;
        var originalStatus = viewModel.StatusMessage;
        var candidateWorld = new CampaignWorld(CreateDefinition(5, 4));
        var mismatchedResources = new CampaignResourceMap(CreateDefinition(4, 4));

        Assert.Throws<ArgumentException>(() => viewModel.OpenWorld(
            candidateWorld,
            mismatchedResources,
            resourceGenerationSettings: null,
            @"F:\Worlds\Candidate",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\Candidate"));

        Assert.Same(current, viewModel.World);
        Assert.Same(originalResources, viewModel.ResourceMap);
        Assert.Equal(@"F:\Worlds\Current", viewModel.ProjectDirectory);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void TerrainAndResourceStrokesShareOneLifoHistory()
    {
        var viewModel = new EditorViewModel();
        var world = new CampaignWorld(CreateDefinition(4, 4));
        viewModel.CreateWorld(world, generationResult: null);
        var terrainAfter = new CampaignTileData(CampaignTileType.Forest, 240);
        var terrainStroke = new CampaignTileStampBuilder(world.Tiles);
        terrainStroke.ApplyTile(new CampaignTileCoordinate(1, 1), terrainAfter);
        viewModel.RecordTileStroke(terrainStroke.Complete("Paint terrain"));
        var resourceStroke = new CampaignResourceStrokeBuilder(viewModel.ResourceMap!);
        resourceStroke.Upsert(1, 1, new CampaignResourceOccurrence("iron-ore", 72, Locked: true));
        viewModel.RecordResourceStroke(resourceStroke.Complete("Paint iron"));

        Assert.Equal("Undo Paint iron", viewModel.UndoMenuLabel);
        Assert.True(viewModel.Undo());
        Assert.Equal(terrainAfter, world.Tiles.GetTile(1, 1));
        Assert.False(viewModel.ResourceMap!.TryGetOccurrence(1, 1, "iron-ore", out _));

        Assert.True(viewModel.Undo());
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(1, 1));

        Assert.True(viewModel.Redo());
        Assert.Equal(terrainAfter, world.Tiles.GetTile(1, 1));
        Assert.False(viewModel.ResourceMap.TryGetOccurrence(1, 1, "iron-ore", out _));

        Assert.True(viewModel.Redo());
        AssertOccurrence(viewModel.ResourceMap, 1, 1, "iron-ore", 72, locked: true);
    }

    [Fact]
    public void PinnedActionsAdoptLockUnlockEraseAndRemainUndoable()
    {
        var viewModel = CreateViewModel();
        var stroke = new CampaignResourceStrokeBuilder(viewModel.ResourceMap!);
        stroke.Upsert(1, 1, new CampaignResourceOccurrence("gold", 64));
        viewModel.RecordResourceStroke(stroke.Complete("Paint gold"));
        viewModel.SelectCoordinate(new CampaignTileCoordinate(1, 1));

        var row = Assert.Single(viewModel.PinnedResourceOccurrences);
        Assert.Equal("gold", row.ResourceId);
        Assert.True(viewModel.AdoptSelectedPinnedResource());
        Assert.Equal("gold", viewModel.SelectedResourceId);
        Assert.Equal(64, viewModel.ResourcePotential);
        Assert.False(viewModel.LockManualResourceEdits);

        Assert.True(viewModel.LockSelectedPinnedResource());
        AssertOccurrence(viewModel.ResourceMap!, 1, 1, "gold", 64, locked: true);
        Assert.True(viewModel.CanUnlockPinnedResource);
        Assert.True(viewModel.Undo());
        AssertOccurrence(viewModel.ResourceMap!, 1, 1, "gold", 64, locked: false);
        Assert.True(viewModel.Redo());
        AssertOccurrence(viewModel.ResourceMap!, 1, 1, "gold", 64, locked: true);

        Assert.True(viewModel.EraseSelectedPinnedResource());
        Assert.Empty(viewModel.PinnedResourceOccurrences);
        Assert.False(viewModel.ResourceMap!.TryGetOccurrence(1, 1, "gold", out _));
        Assert.True(viewModel.Undo());
        AssertOccurrence(viewModel.ResourceMap, 1, 1, "gold", 64, locked: true);
    }

    [Fact]
    public void PinnedDiagnosticsSeparateHardWarningsFromUnevaluatedGeneratorFactors()
    {
        var viewModel = CreateViewModel();
        viewModel.World!.Tiles.SetTile(
            0,
            0,
            new CampaignTileData(CampaignTileType.Plains, 20));
        var stroke = new CampaignResourceStrokeBuilder(viewModel.ResourceMap!);
        stroke.Upsert(0, 0, new CampaignResourceOccurrence("fish", 55, Locked: true));
        viewModel.RecordResourceStroke(stroke.Complete("Paint fish"));

        viewModel.SelectCoordinate(new CampaignTileCoordinate(0, 0));

        var row = Assert.Single(viewModel.PinnedResourceOccurrences);
        Assert.True(viewModel.HasPinnedResourceOccurrences);
        Assert.False(viewModel.HasNoPinnedResourceOccurrences);
        Assert.True(row.HasHardWarnings);
        Assert.Contains("requires Sea or Lake", row.HardWarningText, StringComparison.Ordinal);
        Assert.Contains("Not evaluated:", row.UnevaluatedFactorsText, StringComparison.Ordinal);
        Assert.Contains("final generator suitability", row.UnevaluatedFactorsText, StringComparison.Ordinal);
        Assert.True(viewModel.ResourceMap!.TryGetOccurrence(0, 0, "fish", out _));
        Assert.Equal(row.HardWarningText, viewModel.SelectedPinnedResourceWarningText);
    }

    [Fact]
    public void HoverReportsExactSelectedResourcePotentialAndLock()
    {
        var viewModel = CreateViewModel();
        var gold = Assert.Single(viewModel.ResourceOptions, option => option.Id == "gold");
        viewModel.SelectedResourceOption = gold;
        viewModel.ResourceMap!.Upsert(2, 1, new CampaignResourceOccurrence("gold", 73, Locked: true));

        viewModel.UpdateHover(new CampaignTilePointerInfo(
            new CampaignTileCoordinate(2, 1),
            TileSpaceX: 2.5,
            TileSpaceY: 1.5));

        Assert.Equal((byte)73, viewModel.HoverSelectedResourcePotential);
        Assert.Contains("73 / 100", viewModel.HoverSelectedResourceText, StringComparison.Ordinal);
        Assert.Contains("locked", viewModel.HoverSelectedResourceText, StringComparison.Ordinal);

        viewModel.UpdateHover(new CampaignTilePointerInfo(
            new CampaignTileCoordinate(3, 1),
            TileSpaceX: 3.5,
            TileSpaceY: 1.5));
        Assert.Null(viewModel.HoverSelectedResourcePotential);
        Assert.Contains("none", viewModel.HoverSelectedResourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void SameLatticeRegenerationPreservesCatalogSettingsAndEveryOccurrence()
    {
        var definition = CreateDefinition(4, 4);
        var custom = CreateCustomResource();
        var catalog = new CampaignResourceCatalog([custom]);
        var resources = new CampaignResourceMap(definition with { }, catalog);
        resources.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence(custom.Id, 81, Locked: true)),
            CampaignResourceMutation.Upsert(2, 2, new CampaignResourceOccurrence("gold", 35)),
        ]);
        var settings = CreateSettings(custom);
        var viewModel = new EditorViewModel();
        viewModel.OpenWorld(
            new CampaignWorld(definition),
            resources,
            settings,
            @"F:\Worlds\SameLattice",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\SameLattice");
        var replacementDefinition = definition with { MaximumHeightMeters = 5_000 };
        var replacement = new CampaignWorld(replacementDefinition);

        viewModel.RegenerateWorld(
            replacement,
            CreateGenerationResult(CampaignMapGenerationPreset.Continent, seed: 42));

        Assert.Same(replacement, viewModel.World);
        Assert.NotSame(resources, viewModel.ResourceMap);
        Assert.Equal(replacementDefinition, viewModel.ResourceMap!.Definition);
        Assert.Same(catalog, viewModel.ResourceMap.Catalog);
        Assert.Same(settings, viewModel.ResourceGenerationSettings);
        Assert.Equal(2, viewModel.ResourceOccurrenceCount);
        AssertOccurrence(viewModel.ResourceMap, 0, 0, custom.Id, 81, locked: true);
        AssertOccurrence(viewModel.ResourceMap, 2, 2, "gold", 35, locked: false);
        Assert.False(viewModel.CanUndo);
        Assert.Contains("2 resource occurrence(s) were preserved", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangedLatticeWithEmptyResourcesCreatesEmptyReboundMap()
    {
        var definition = CreateDefinition(4, 4);
        var custom = CreateCustomResource();
        var catalog = new CampaignResourceCatalog([custom]);
        var resources = new CampaignResourceMap(definition with { }, catalog);
        var settings = CreateSettings(custom);
        var viewModel = new EditorViewModel();
        viewModel.OpenWorld(
            new CampaignWorld(definition),
            resources,
            settings,
            @"F:\Worlds\EmptyResources",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\EmptyResources");
        var replacement = new CampaignWorld(CreateDefinition(5, 4));
        var resourcePreview = new CampaignResourceWorldRegenerator().Generate(
            CampaignResourceWorldRegenerationSource.Capture(
                viewModel.World!,
                viewModel.ResourceMap!,
                viewModel.ResourceGenerationSettings),
            replacement);

        viewModel.RegenerateWorld(
            replacement,
            CreateGenerationResult(CampaignMapGenerationPreset.Island, seed: 7),
            resourcePreview);

        Assert.Equal(replacement.Definition, viewModel.ResourceMap!.Definition);
        Assert.Equal(0, viewModel.ResourceOccurrenceCount);
        Assert.Same(catalog, viewModel.ResourceMap.Catalog);
        Assert.Same(settings, viewModel.ResourceGenerationSettings);
        Assert.Contains(viewModel.ResourceOptions, option => option.Id == custom.Id);
        Assert.Contains("unlocked occurrence(s) were regenerated", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangedLatticeWithResourcesThrowsBeforeMutatingAnyViewModelState()
    {
        var viewModel = CreateViewModel();
        viewModel.MarkSaved(@"F:\Worlds\ProtectedResources");
        var stroke = new CampaignResourceStrokeBuilder(viewModel.ResourceMap!);
        stroke.Upsert(1, 1, new CampaignResourceOccurrence("iron-ore", 62, Locked: true));
        viewModel.RecordResourceStroke(stroke.Complete("Paint iron"));
        var originalWorld = viewModel.World;
        var originalResources = viewModel.ResourceMap;
        var originalSettings = viewModel.ResourceGenerationSettings;
        var originalStatus = viewModel.StatusMessage;
        var originalDirty = viewModel.IsDirty;
        var originalCanUndo = viewModel.CanUndo;
        var replacement = new CampaignWorld(CreateDefinition(5, 4));

        Assert.Throws<InvalidOperationException>(() => viewModel.RegenerateWorld(
            replacement,
            CreateGenerationResult(CampaignMapGenerationPreset.Island, seed: 99)));

        Assert.Same(originalWorld, viewModel.World);
        Assert.Same(originalResources, viewModel.ResourceMap);
        Assert.Same(originalSettings, viewModel.ResourceGenerationSettings);
        Assert.Equal(@"F:\Worlds\ProtectedResources", viewModel.ProjectDirectory);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
        Assert.Equal(originalDirty, viewModel.IsDirty);
        Assert.Equal(originalCanUndo, viewModel.CanUndo);
        AssertOccurrence(viewModel.ResourceMap!, 1, 1, "iron-ore", 62, locked: true);
    }

    [Fact]
    public void ChangedLatticeAcceptsExactReviewedResourceCandidateAtomically()
    {
        var viewModel = CreateViewModel();
        viewModel.MarkSaved(@"F:\Worlds\ReviewedRemap");
        var stroke = new CampaignResourceStrokeBuilder(viewModel.ResourceMap!);
        stroke.Upsert(1, 1, new CampaignResourceOccurrence("iron-ore", 62, Locked: true));
        viewModel.RecordResourceStroke(stroke.Complete("Paint iron"));
        Assert.True(viewModel.CanUndo);
        var sourceWorld = viewModel.World!;
        var sourceResources = viewModel.ResourceMap!;
        var replacementDefinition = CampaignWorldDefinition.Create(
            worldWidthMeters: 4_000,
            worldHeightMeters: 4_000,
            campaignTileSizeMeters: 500,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 0);
        var replacement = new CampaignWorld(replacementDefinition);
        var resourcePreview = new CampaignResourceWorldRegenerator().Generate(
            CampaignResourceWorldRegenerationSource.Capture(sourceWorld, sourceResources),
            replacement);

        viewModel.RegenerateWorld(
            replacement,
            CreateGenerationResult(CampaignMapGenerationPreset.Island, seed: 41),
            resourcePreview);

        Assert.Same(replacement, viewModel.World);
        Assert.Same(resourcePreview.CandidateMap, viewModel.ResourceMap);
        Assert.Equal(@"F:\Worlds\ReviewedRemap", viewModel.ProjectDirectory);
        AssertOccurrence(viewModel.ResourceMap!, 3, 3, "iron-ore", 62, locked: true);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.True(viewModel.IsDirty);
        Assert.Contains("physical position", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("project identity was kept", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void StaleReviewedResourceCandidateCannotReplaceCurrentDocument()
    {
        var viewModel = CreateViewModel();
        var sourceWorld = viewModel.World!;
        var sourceResources = viewModel.ResourceMap!;
        sourceResources.Upsert(
            1,
            1,
            new CampaignResourceOccurrence("iron-ore", 62, Locked: true));
        var replacement = new CampaignWorld(CampaignWorldDefinition.Create(
            worldWidthMeters: 4_000,
            worldHeightMeters: 4_000,
            campaignTileSizeMeters: 500,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 0));
        var resourcePreview = new CampaignResourceWorldRegenerator().Generate(
            CampaignResourceWorldRegenerationSource.Capture(sourceWorld, sourceResources),
            replacement);
        sourceResources.Upsert(2, 2, new CampaignResourceOccurrence("gold", 51));

        Assert.Throws<InvalidOperationException>(() => viewModel.RegenerateWorld(
            replacement,
            CreateGenerationResult(CampaignMapGenerationPreset.Island, seed: 41),
            resourcePreview));

        Assert.Same(sourceWorld, viewModel.World);
        Assert.Same(sourceResources, viewModel.ResourceMap);
        AssertOccurrence(sourceResources, 1, 1, "iron-ore", 62, locked: true);
        AssertOccurrence(sourceResources, 2, 2, "gold", 51, locked: false);
    }

    private static EditorViewModel CreateViewModel()
    {
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(new CampaignWorld(CreateDefinition(4, 4)), generationResult: null);
        return viewModel;
    }

    private static CampaignResourceDefinition CreateCustomResource() =>
        new(
            "mana-crystal",
            "Mana Crystal",
            CampaignResourceCategory.Finite,
            CampaignResourceDistributionProfile.Vein,
            CampaignResourceMedium.Land,
            "crystal",
            "#7A5BC7",
            mapPriority: 70,
            coveragePercent: 5,
            CampaignResourceRichness.Rich,
            CampaignResourceConcentration.ManySmall);

    private static CampaignResourceGenerationSettings CreateSettings(
        CampaignResourceDefinition custom) =>
        new(
            resourceSeed: 741,
            seedDerivedFromWorld: false,
            abundance: CampaignResourceAbundance.Custom,
            climate: CampaignResourceClimateProfile.Temperate,
            geology: CampaignResourceGeologyProfile.VolcanicArc,
            overrides:
            [
                new CampaignResourceGenerationOverride(
                    custom.Id,
                    enabled: true,
                    coveragePercent: 9,
                    CampaignResourceRichness.Rich,
                    richnessBias: 8,
                    CampaignResourceConcentration.ManySmall,
                    mapPriority: 80),
            ]);

    private static void AssertOccurrence(
        CampaignResourceMap resources,
        int x,
        int y,
        string resourceId,
        byte potential,
        bool locked)
    {
        Assert.True(resources.TryGetOccurrence(x, y, resourceId, out var occurrence));
        Assert.Equal(potential, occurrence.Potential);
        Assert.Equal(locked, occurrence.Locked);
    }

    private static CampaignMapGenerationResult CreateGenerationResult(
        CampaignMapGenerationPreset preset,
        int seed) =>
        new(
            preset,
            seed,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapMountainDensity.Sparse,
            CampaignMapHydrology.Balanced,
            Tiles: [],
            LandTileCount: 20,
            SeaTileCount: 0,
            LakeTileCount: 0,
            RiverTileCount: 0,
            CliffTileCount: 0)
        {
            CoastlineStyle = CampaignMapCoastlineStyle.Natural,
        };

    private static CampaignWorldDefinition CreateDefinition(int tilesX, int tilesY) =>
        CampaignWorldDefinition.Create(
            tilesX * 1_000L,
            tilesY * 1_000L,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 0);
}
