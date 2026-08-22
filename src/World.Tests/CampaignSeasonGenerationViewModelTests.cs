using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonGenerationViewModelTests
{
    [Fact]
    public void ResolveInitialSeasonGenerationSettings_PrefersExactSavedRecipeWithoutMutation()
    {
        var definition = CreateDefinition(4, 4);
        var world = new CampaignWorld(definition);
        var seasons = new CampaignSeasonMap(definition with { });
        var settings = new CampaignSeasonGenerationSettings(
            83_019,
            seedDerivedFromTerrain: false,
            axialTiltDegrees: 31);
        var saved = new CampaignSeasonSavedGeneration(
            settings,
            new string('a', 64),
            new string('b', 64));
        var viewModel = new EditorViewModel();
        viewModel.OpenWorld(
            world,
            new CampaignResourceMap(definition),
            resourceGenerationSettings: null,
            seasons,
            settings.EnabledSeasonIds,
            saved,
            @"F:\Worlds\SavedSeasons",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\SavedSeasons");

        var resolved = viewModel.ResolveInitialSeasonGenerationSettings();

        Assert.Same(settings, resolved);
        Assert.Same(saved, viewModel.SeasonSavedGeneration);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void ResolveInitialSeasonGenerationSettings_UsesTerrainSeedWithoutInstallingRecipe()
    {
        const int terrainSeed = 77_031;
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(
            new CampaignWorld(CreateDefinition(4, 4)),
            CreateTerrainGenerationResult(terrainSeed));

        var resolved = viewModel.ResolveInitialSeasonGenerationSettings();

        Assert.Equal(CampaignSeasonSeed.FromTerrainSeed(terrainSeed), resolved.SeasonSeed);
        Assert.True(resolved.SeedDerivedFromTerrain);
        Assert.Null(viewModel.SeasonSavedGeneration);
    }

    [Fact]
    public void ResolveInitialSeasonGenerationSettings_FallbackTracksTerrainContents()
    {
        var viewModel = CreateViewModel();

        var first = viewModel.ResolveInitialSeasonGenerationSettings();
        viewModel.World!.Tiles.SetTile(
            2,
            1,
            new CampaignTileData(CampaignTileType.Forest, 330));
        var second = viewModel.ResolveInitialSeasonGenerationSettings();

        Assert.NotEqual(first.SeasonSeed, second.SeasonSeed);
        Assert.True(first.SeedDerivedFromTerrain);
        Assert.True(second.SeedDerivedFromTerrain);
        Assert.Null(viewModel.SeasonSavedGeneration);
    }

    [Fact]
    public void AcceptSeasonGeneration_InstallsExactCandidateRecipeAndClearsSharedHistory()
    {
        var viewModel = CreateViewModel();
        viewModel.MarkSaved(@"F:\Worlds\SeasonPreview");
        var originalWorld = viewModel.World;
        var originalResources = viewModel.ResourceMap;
        var originalProjectDirectory = viewModel.ProjectDirectory;
        var stroke = new CampaignSeasonStrokeBuilder(viewModel.SeasonMap!);
        stroke.Upsert(new CampaignTileCoordinate(0, 0), CampaignSeasonCatalog.WinterId, locked: true);
        viewModel.RecordSeasonStroke(stroke.Complete("Paint Winter"));
        Assert.True(viewModel.CanUndo);
        var settings = new CampaignSeasonGenerationSettings(
            91_733,
            seedDerivedFromTerrain: false,
            axialTiltDegrees: 26);
        var result = Generate(viewModel, settings);

        viewModel.AcceptSeasonGeneration(result);

        Assert.Same(originalWorld, viewModel.World);
        Assert.Same(originalResources, viewModel.ResourceMap);
        Assert.Equal(originalProjectDirectory, viewModel.ProjectDirectory);
        Assert.Same(result.CandidateMap, viewModel.SeasonMap);
        Assert.Same(settings, viewModel.SeasonSavedGeneration!.Settings);
        Assert.Equal(
            CampaignSeasonGenerationFingerprint.GetSourceTerrainFingerprint(result.SupportFields.Terrain),
            viewModel.SeasonSavedGeneration.SourceTerrainFingerprint);
        Assert.Equal(
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(result.CandidateMap.Catalog, settings),
            viewModel.SeasonSavedGeneration.InputFingerprint);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Contains("Accepted reviewed Season candidate", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Contains("project identity", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptSeasonGeneration_StaleTerrainThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var current = viewModel.SeasonMap;
        var result = Generate(viewModel);
        var originalStatus = viewModel.StatusMessage;
        viewModel.World!.Tiles.SetTile(
            1,
            1,
            new CampaignTileData(CampaignTileType.Hills, 450));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            viewModel.AcceptSeasonGeneration(result));

        Assert.Contains("changed after this candidate", exception.Message, StringComparison.Ordinal);
        Assert.Same(current, viewModel.SeasonMap);
        Assert.Null(viewModel.SeasonSavedGeneration);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
    }

    [Fact]
    public void AcceptSeasonGeneration_StaleSeasonAuthorityThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var current = viewModel.SeasonMap!;
        var result = Generate(viewModel);
        var originalStatus = viewModel.StatusMessage;
        current.Upsert(0, 0, new(CampaignSeasonCatalog.WinterId));

        Assert.Throws<InvalidOperationException>(() => viewModel.AcceptSeasonGeneration(result));

        Assert.Same(current, viewModel.SeasonMap);
        Assert.Null(viewModel.SeasonSavedGeneration);
        Assert.Equal(originalStatus, viewModel.StatusMessage);
    }

    [Fact]
    public void AcceptSeasonGeneration_DifferentDefinitionOrCatalogThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var current = viewModel.SeasonMap;
        var otherWorld = new CampaignWorld(CreateDefinition(5, 4));
        var otherSeasons = new CampaignSeasonMap(otherWorld.Definition);
        var differentDefinition = CampaignSeasonGenerator.Generate(
            CampaignSeasonGenerationSource.Capture(
                new CampaignSeasonTerrainQueryV2(otherWorld),
                otherSeasons),
            otherSeasons.Catalog,
            new CampaignSeasonGenerationSettings(1),
            CampaignSeasonGenerationScope.All);

        Assert.Throws<ArgumentException>(() =>
            viewModel.AcceptSeasonGeneration(differentDefinition));
        Assert.Same(current, viewModel.SeasonMap);

        var differentCatalogMap = new CampaignSeasonMap(
            viewModel.World!.Definition,
            new CampaignSeasonCatalog());
        var differentCatalog = CampaignSeasonGenerator.Generate(
            CampaignSeasonGenerationSource.Capture(
                viewModel.SeasonTerrainQuery!,
                differentCatalogMap),
            differentCatalogMap.Catalog,
            new CampaignSeasonGenerationSettings(1),
            CampaignSeasonGenerationScope.All);

        Assert.Throws<ArgumentException>(() =>
            viewModel.AcceptSeasonGeneration(differentCatalog));
        Assert.Same(current, viewModel.SeasonMap);
        Assert.Null(viewModel.SeasonSavedGeneration);
    }

    [Fact]
    public void AcceptSeasonGeneration_MutatedCandidateThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var current = viewModel.SeasonMap;
        var result = Generate(viewModel);
        var entry = result.CandidateMap.GetMaterializedOccurrences()[0];
        result.CandidateMap.SetLocked(
            entry.X,
            entry.Y,
            entry.Occurrence.SeasonId,
            !entry.Occurrence.Locked);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            viewModel.AcceptSeasonGeneration(result));

        Assert.Contains("candidate changed", exception.Message, StringComparison.Ordinal);
        Assert.Same(current, viewModel.SeasonMap);
        Assert.Null(viewModel.SeasonSavedGeneration);
    }

    [Fact]
    public void AcceptSeasonGeneration_EnabledSelectionMismatchThrowsBeforeMutation()
    {
        var viewModel = CreateViewModel();
        var settings = new CampaignSeasonGenerationSettings(
            19,
            enabledSeasonIds: [CampaignSeasonCatalog.WinterId]);
        var result = Generate(viewModel, settings);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            viewModel.AcceptSeasonGeneration(result));

        Assert.Contains("selection changed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(viewModel.SeasonSavedGeneration);
    }

    [Fact]
    public async Task AcceptedAndRebuiltDiagnosticsReportIndependentMatchesSupportAndStaleness()
    {
        var viewModel = CreateViewModel();
        var result = Generate(viewModel, new CampaignSeasonGenerationSettings(42));
        viewModel.AcceptSeasonGeneration(result);
        viewModel.SelectCoordinate(new CampaignTileCoordinate(2, 1));

        Assert.Contains("Climate", viewModel.PinnedSeasonGenerationText, StringComparison.Ordinal);
        Assert.Contains("Independent rule matches", viewModel.PinnedSeasonGenerationText, StringComparison.Ordinal);
        Assert.Contains("source current", viewModel.PinnedSeasonGenerationText, StringComparison.Ordinal);
        Assert.Contains("inputs current", viewModel.PinnedSeasonGenerationText, StringComparison.Ordinal);

        var reopened = new EditorViewModel();
        reopened.OpenWorld(
            viewModel.World!,
            viewModel.ResourceMap!,
            resourceGenerationSettings: null,
            viewModel.SeasonMap!,
            viewModel.SeasonEnabledIds,
            viewModel.SeasonSavedGeneration,
            @"F:\Worlds\ReopenedSeasons",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\ReopenedSeasons");
        Assert.True(await reopened.RebuildSeasonDiagnosticsAsync());
        reopened.SelectCoordinate(new CampaignTileCoordinate(2, 1));

        Assert.Contains("Independent rule matches", reopened.PinnedSeasonGenerationText, StringComparison.Ordinal);
        Assert.Contains("source current", reopened.PinnedSeasonGenerationText, StringComparison.Ordinal);

        reopened.World!.Tiles.SetTile(
            2,
            1,
            new CampaignTileData(CampaignTileType.Mountain, 2_000));
        Assert.Contains("source stale", reopened.PinnedSeasonGenerationText, StringComparison.Ordinal);
    }

    [Fact]
    public void CanGenerateSeasons_TracksWorldAndBusyState()
    {
        var viewModel = new EditorViewModel();
        Assert.False(viewModel.CanGenerateSeasons);

        viewModel.CreateWorld(new CampaignWorld(CreateDefinition(4, 4)), generationResult: null);
        Assert.True(viewModel.CanGenerateSeasons);

        viewModel.IsBusy = true;
        Assert.False(viewModel.CanGenerateSeasons);
        viewModel.IsBusy = false;
        Assert.True(viewModel.CanGenerateSeasons);
    }

    private static CampaignSeasonGenerationResult Generate(
        EditorViewModel viewModel,
        CampaignSeasonGenerationSettings? settings = null,
        CampaignSeasonGenerationScope? scope = null) =>
        CampaignSeasonGenerator.Generate(
            CampaignSeasonGenerationSource.Capture(
                viewModel.SeasonTerrainQuery!,
                viewModel.SeasonMap!),
            viewModel.SeasonMap!.Catalog,
            settings ?? new CampaignSeasonGenerationSettings(7),
            scope ?? CampaignSeasonGenerationScope.All);

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
