using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceGenerationViewModelTests
{
    [Fact]
    public void ResolveInitialResourceGenerationSettings_PrefersExactSavedSettingsWithoutMutatingDocument()
    {
        var definition = CreateDefinition(4, 4);
        var world = new CampaignWorld(definition);
        var resources = new CampaignResourceMap(definition with { });
        var savedSettings = new CampaignResourceGenerationSettings(
            81_301,
            seedDerivedFromWorld: true,
            abundance: CampaignResourceAbundance.Sparse);
        var viewModel = new EditorViewModel();
        viewModel.OpenWorld(
            world,
            resources,
            savedSettings,
            @"F:\Worlds\SavedResourceSettings",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\SavedResourceSettings");

        var resolved = viewModel.ResolveInitialResourceGenerationSettings();

        Assert.Same(savedSettings, resolved);
        Assert.Same(savedSettings, viewModel.ResourceGenerationSettings);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void ResolveInitialResourceGenerationSettings_UsesAcceptedTerrainSeedWithoutInstallingDefaults()
    {
        const int terrainSeed = 17_029;
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(
            new CampaignWorld(CreateDefinition(4, 4)),
            CreateTerrainGenerationResult(terrainSeed));

        var resolved = viewModel.ResolveInitialResourceGenerationSettings();

        Assert.Equal(CampaignResourceSeed.FromTerrainSeed(terrainSeed), resolved.ResourceSeed);
        Assert.True(resolved.SeedDerivedFromWorld);
        Assert.Null(viewModel.ResourceGenerationSettings);
    }

    [Fact]
    public void ResolveInitialResourceGenerationSettings_FallbackTracksAuthoritativeWorldContents()
    {
        var viewModel = CreateViewModel();

        var first = viewModel.ResolveInitialResourceGenerationSettings();
        viewModel.World!.Tiles.SetTile(
            2,
            1,
            new CampaignTileData(CampaignTileType.Forest, 310));
        var second = viewModel.ResolveInitialResourceGenerationSettings();

        Assert.NotEqual(first.ResourceSeed, second.ResourceSeed);
        Assert.True(first.SeedDerivedFromWorld);
        Assert.True(second.SeedDerivedFromWorld);
        Assert.Null(viewModel.ResourceGenerationSettings);
    }

    [Fact]
    public void AcceptResourceGeneration_InstallsExactReviewedCandidateAndClearsSharedHistory()
    {
        var viewModel = CreateViewModel();
        viewModel.MarkSaved(@"F:\Worlds\ResourcePreview");
        var originalWorld = viewModel.World;
        var originalProjectDirectory = viewModel.ProjectDirectory;
        var stroke = new CampaignResourceStrokeBuilder(viewModel.ResourceMap!);
        stroke.Upsert(0, 0, new CampaignResourceOccurrence("gold", 42, Locked: true));
        viewModel.RecordResourceStroke(stroke.Complete("Paint gold"));
        Assert.True(viewModel.CanUndo);

        var candidate = new CampaignResourceMap(
            viewModel.World!.Definition with { },
            viewModel.ResourceMap!.Catalog);
        candidate.Upsert(2, 1, new CampaignResourceOccurrence("iron-ore", 76));
        var settings = new CampaignResourceGenerationSettings(19_811, seedDerivedFromWorld: false);
        var result = CreateResult(viewModel, candidate, settings);

        viewModel.AcceptResourceGeneration(result);

        Assert.Same(originalWorld, viewModel.World);
        Assert.Equal(originalProjectDirectory, viewModel.ProjectDirectory);
        Assert.Same(candidate, viewModel.ResourceMap);
        Assert.Same(settings, viewModel.ResourceGenerationSettings);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Equal(1, viewModel.ResourceOccurrenceCount);
        Assert.True(candidate.TryGetOccurrence(2, 1, "iron-ore", out var occurrence));
        Assert.Equal(76, occurrence.Potential);
        Assert.Contains("Accepted reviewed resource candidate", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("project identity were kept", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptResourceGeneration_PreservesLegacyImportIdentity()
    {
        var definition = CreateDefinition(4, 4);
        var world = new CampaignWorld(definition);
        var resources = new CampaignResourceMap(definition with { });
        var viewModel = new EditorViewModel();
        viewModel.OpenWorld(
            world,
            resources,
            resourceGenerationSettings: null,
            projectDirectory: null,
            wasConvertedFromLegacy: true,
            sourceProjectDirectory: @"F:\Legacy\OldRealm");
        var candidate = new CampaignResourceMap(definition with { }, resources.Catalog);
        var settings = new CampaignResourceGenerationSettings(91);

        viewModel.AcceptResourceGeneration(CreateResult(viewModel, candidate, settings));

        Assert.Same(world, viewModel.World);
        Assert.Null(viewModel.ProjectDirectory);
        Assert.True(viewModel.IsLegacyImportPending);
        Assert.Equal(@"F:\Legacy\OldRealm", viewModel.ImportSourceDirectory);
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public void AcceptResourceGeneration_StaleTerrainThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var currentResources = viewModel.ResourceMap!;
        var candidate = new CampaignResourceMap(viewModel.World!.Definition, currentResources.Catalog);
        var settings = new CampaignResourceGenerationSettings(7);
        var result = CreateResult(viewModel, candidate, settings);
        viewModel.World.Tiles.SetTile(
            1,
            1,
            new CampaignTileData(CampaignTileType.Hills, 120));
        var originalStatus = viewModel.StatusMessage;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            viewModel.AcceptResourceGeneration(result));

        Assert.Contains("changed after this candidate", exception.Message, StringComparison.Ordinal);
        Assert.Same(currentResources, viewModel.ResourceMap);
        Assert.Null(viewModel.ResourceGenerationSettings);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
    }

    [Fact]
    public void AcceptResourceGeneration_StaleResourcesThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var currentResources = viewModel.ResourceMap!;
        var candidate = new CampaignResourceMap(viewModel.World!.Definition, currentResources.Catalog);
        var settings = new CampaignResourceGenerationSettings(7);
        var result = CreateResult(viewModel, candidate, settings);
        currentResources.Upsert(1, 1, new CampaignResourceOccurrence("gold", 50));
        var originalStatus = viewModel.StatusMessage;

        Assert.Throws<InvalidOperationException>(() => viewModel.AcceptResourceGeneration(result));

        Assert.Same(currentResources, viewModel.ResourceMap);
        Assert.Null(viewModel.ResourceGenerationSettings);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
    }

    [Fact]
    public void AcceptResourceGeneration_MismatchedDefinitionThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var currentResources = viewModel.ResourceMap!;
        var candidate = new CampaignResourceMap(CreateDefinition(5, 4), currentResources.Catalog);
        var settings = new CampaignResourceGenerationSettings(7);
        var result = CreateResult(viewModel, candidate, settings);
        var originalStatus = viewModel.StatusMessage;

        Assert.Throws<ArgumentException>(() => viewModel.AcceptResourceGeneration(result));

        Assert.Same(currentResources, viewModel.ResourceMap);
        Assert.Null(viewModel.ResourceGenerationSettings);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
    }

    [Fact]
    public void AcceptResourceGeneration_DifferentCatalogIdentityThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var currentResources = viewModel.ResourceMap!;
        var candidate = new CampaignResourceMap(
            viewModel.World!.Definition,
            new CampaignResourceCatalog());
        var settings = new CampaignResourceGenerationSettings(7);
        var result = CreateResult(viewModel, candidate, settings);
        var originalStatus = viewModel.StatusMessage;

        Assert.Throws<ArgumentException>(() => viewModel.AcceptResourceGeneration(result));

        Assert.Same(currentResources, viewModel.ResourceMap);
        Assert.Null(viewModel.ResourceGenerationSettings);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
    }

    [Fact]
    public void AcceptResourceGeneration_MutatedCandidateThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var currentResources = viewModel.ResourceMap!;
        var candidate = new CampaignResourceMap(
            viewModel.World!.Definition,
            currentResources.Catalog);
        var settings = new CampaignResourceGenerationSettings(7);
        var result = CreateResult(viewModel, candidate, settings);
        candidate.Upsert(1, 2, new CampaignResourceOccurrence("stone", 48));
        var originalStatus = viewModel.StatusMessage;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            viewModel.AcceptResourceGeneration(result));

        Assert.Contains("candidate changed", exception.Message, StringComparison.Ordinal);
        Assert.Same(currentResources, viewModel.ResourceMap);
        Assert.Null(viewModel.ResourceGenerationSettings);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
    }

    [Fact]
    public void CanRegenerateResources_TracksWorldResourceAndBusyState()
    {
        var viewModel = new EditorViewModel();
        Assert.False(viewModel.CanRegenerateResources);

        viewModel.CreateWorld(new CampaignWorld(CreateDefinition(4, 4)), generationResult: null);
        Assert.True(viewModel.CanRegenerateResources);

        viewModel.IsBusy = true;
        Assert.False(viewModel.CanRegenerateResources);
        viewModel.IsBusy = false;
        Assert.True(viewModel.CanRegenerateResources);
    }

    private static CampaignResourceGenerationResult CreateResult(
        EditorViewModel viewModel,
        CampaignResourceMap candidate,
        CampaignResourceGenerationSettings settings) =>
        new(
            candidate,
            settings,
            CampaignResourceGenerationScope.All,
            reports: [],
            sourceTerrainRevision: viewModel.World!.Revision,
            sourceResourceRevision: viewModel.ResourceMap!.Revision);

    private static EditorViewModel CreateViewModel()
    {
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(new CampaignWorld(CreateDefinition(4, 4)), generationResult: null);
        return viewModel;
    }

    private static CampaignMapGenerationResult CreateTerrainGenerationResult(int seed) =>
        new(
            CampaignMapGenerationPreset.Continent,
            seed,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapMountainDensity.Balanced,
            CampaignMapHydrology.Balanced,
            Tiles: [],
            LandTileCount: 16,
            SeaTileCount: 0,
            LakeTileCount: 0,
            RiverTileCount: 0,
            CliffTileCount: 0);

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
