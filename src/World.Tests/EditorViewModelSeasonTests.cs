using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Tests;

public sealed class EditorViewModelSeasonTests
{
    [Fact]
    public void CreateWorld_InstallsEmptyEditableOccurrenceLayerAndSeasonControls()
    {
        var viewModel = new EditorViewModel();
        var world = new CampaignWorld(CreateDefinition(5, 4));

        viewModel.CreateWorld(world, generationResult: null);
        viewModel.SwitchToSeasonsWorkspace();
        viewModel.SeasonPaintAreaRadius = 99;

        Assert.NotNull(viewModel.SeasonMap);
        Assert.Equal(world.Definition, viewModel.SeasonMap.Definition);
        Assert.Equal(0, viewModel.SeasonMap.OccurrenceCount);
        Assert.Equal(0, viewModel.SeasonMap.LockedOccurrenceCount);
        Assert.Equal(CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds, viewModel.SeasonEnabledIds);
        Assert.Null(viewModel.SeasonSavedGeneration);
        Assert.True(viewModel.IsSeasonsWorkspace);
        Assert.True(viewModel.CanEditSeasons);
        Assert.Equal(4, viewModel.SeasonOptions.Count);
        Assert.Equal("spring", viewModel.SelectedSeasonId);
        Assert.Equal(12, viewModel.SeasonPaintAreaRadius);
        Assert.Equal("25 × 25 tiles", viewModel.SeasonPaintAreaText);
        Assert.Contains("occurrence", viewModel.FooterFormatText, StringComparison.OrdinalIgnoreCase);

        viewModel.SeasonSearchText = "not-present";
        Assert.Empty(viewModel.SeasonOptions);
        Assert.True(viewModel.HasNoSeasonOptions);
        Assert.Null(viewModel.SelectedSeasonId);
        Assert.False(viewModel.CanEditSeasons);
    }

    [Fact]
    public void TerrainResourceAndSeasonOccurrenceStrokesShareOneLifoHistory()
    {
        var viewModel = new EditorViewModel();
        var world = new CampaignWorld(CreateDefinition(4, 4));
        viewModel.CreateWorld(world, generationResult: null);

        var terrainStroke = new CampaignTileStampBuilder(world.Tiles);
        terrainStroke.ApplyTile(
            new CampaignTileCoordinate(1, 1),
            new CampaignTileData(CampaignTileType.Forest, 200));
        viewModel.RecordTileStroke(terrainStroke.Complete("Paint forest"));

        var resourceStroke = new CampaignResourceStrokeBuilder(viewModel.ResourceMap!);
        resourceStroke.Upsert(1, 1, new("timber", 75, Locked: true));
        viewModel.RecordResourceStroke(resourceStroke.Complete("Paint timber"));

        var seasonStroke = new CampaignSeasonStrokeBuilder(viewModel.SeasonMap!);
        seasonStroke.Upsert(new CampaignTileCoordinate(1, 1), "winter", locked: true);
        viewModel.RecordSeasonStroke(seasonStroke.Complete("Add Winter"));

        Assert.True(viewModel.Undo());
        Assert.Empty(viewModel.SeasonMap!.GetOccurrences(1, 1));
        Assert.True(viewModel.ResourceMap!.TryGetOccurrence(1, 1, "timber", out _));
        Assert.True(viewModel.Undo());
        Assert.False(viewModel.ResourceMap.TryGetOccurrence(1, 1, "timber", out _));
        Assert.True(viewModel.Undo());
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(1, 1));

        Assert.True(viewModel.Redo());
        Assert.True(viewModel.Redo());
        Assert.True(viewModel.Redo());
        Assert.True(viewModel.SeasonMap.TryGetOccurrence(1, 1, "winter", out var winter));
        Assert.True(winter.Locked);
    }

    [Fact]
    public void OpenWorld_InstallsExactMultiSeasonAuthoritySelectionAndRecipe()
    {
        var definition = CreateDefinition(3, 3);
        var custom = CreateMonsoon();
        var catalog = new CampaignSeasonCatalog([custom]);
        var seasons = new CampaignSeasonMap(definition, catalog);
        seasons.Upsert(2, 1, new("spring"));
        seasons.Upsert(2, 1, new("summer"));
        seasons.Upsert(2, 1, new("fall"));
        seasons.Upsert(2, 1, new("winter", Locked: true));
        var enabled = new[] { "winter", custom.Id, "summer" };
        var settings = new CampaignSeasonGenerationSettings(41, enabledSeasonIds: enabled);
        var saved = new CampaignSeasonSavedGeneration(
            settings,
            new string('a', 64),
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(catalog, settings));
        var viewModel = new EditorViewModel();

        viewModel.OpenWorld(
            new CampaignWorld(definition),
            new CampaignResourceMap(definition),
            resourceGenerationSettings: null,
            seasons,
            enabled,
            saved,
            @"F:\Worlds\Seasoned",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\Seasoned");

        Assert.Same(seasons, viewModel.SeasonMap);
        Assert.Equal(enabled.Order(StringComparer.Ordinal), viewModel.SeasonEnabledIds);
        Assert.Same(saved, viewModel.SeasonSavedGeneration);
        Assert.Equal(4, viewModel.SeasonMap!.GetOccurrences(2, 1).Count);
        Assert.Contains(viewModel.SeasonOptions, option =>
            option.Id == custom.Id && option.IsCustom && option.IsGenerationEnabled);
        Assert.False(viewModel.IsDirty);
        Assert.Contains("occurrence", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateSeasons_ReplacementMergesSameIdentityAndPreservesAnyLock()
    {
        var definition = CreateDefinition(4, 4);
        var custom = CreateMonsoon();
        var catalog = new CampaignSeasonCatalog([custom]);
        var seasons = new CampaignSeasonMap(definition, catalog);
        seasons.Upsert(1, 2, new(custom.Id, Locked: true));
        seasons.Upsert(1, 2, new("spring"));
        seasons.Upsert(0, 0, new("winter", Locked: true));
        var viewModel = Open(viewModel: new EditorViewModel(), definition, seasons);

        var changed = viewModel.UpdateSeasons(
            CampaignSeasonCatalog.DefaultBuiltInDefinitions,
            customDefinitions: [],
            CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [custom.Id] = CampaignSeasonCatalog.SpringId,
            },
            CampaignSeasonCatalog.SpringId);

        Assert.True(changed);
        Assert.False(viewModel.SeasonMap!.Catalog.Contains(custom.Id));
        Assert.Single(viewModel.SeasonMap.GetOccurrences(1, 2));
        Assert.True(viewModel.SeasonMap.TryGetOccurrence(1, 2, "spring", out var spring));
        Assert.True(spring.Locked);
        Assert.True(viewModel.SeasonMap.TryGetOccurrence(0, 0, "winter", out var winter));
        Assert.True(winter.Locked);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public void PinnedSeason_ListsAllOccurrencesAndLocksSelectedIdentityUndoably()
    {
        var viewModel = new EditorViewModel();
        var world = new CampaignWorld(CreateDefinition(3, 3));
        world.Tiles.SetTile(1, 1, new(CampaignTileType.Mountain, 1_600));
        viewModel.CreateWorld(world, generationResult: null);
        viewModel.SeasonMap!.Upsert(1, 1, new("spring"));
        viewModel.SeasonMap.Upsert(1, 1, new("summer"));
        viewModel.SeasonMap.Upsert(1, 1, new("fall"));
        viewModel.SelectCoordinate(new CampaignTileCoordinate(1, 1));

        Assert.Contains("Spring", viewModel.PinnedSeasonIdentityText, StringComparison.Ordinal);
        Assert.Contains("Summer", viewModel.PinnedSeasonIdentityText, StringComparison.Ordinal);
        Assert.Contains("Fall", viewModel.PinnedSeasonIdentityText, StringComparison.Ordinal);
        Assert.Contains("Mountain", viewModel.PinnedSeasonTerrainText, StringComparison.Ordinal);
        Assert.Contains("generated independently", viewModel.PinnedSeasonRuleText, StringComparison.Ordinal);
        Assert.True(viewModel.CanLockPinnedSeason);

        Assert.True(viewModel.LockPinnedSeason());
        Assert.True(viewModel.SeasonMap.TryGetOccurrence(1, 1, "spring", out var spring));
        Assert.True(spring.Locked);
        Assert.True(viewModel.SeasonMap.TryGetOccurrence(1, 1, "summer", out var summer));
        Assert.False(summer.Locked);
        Assert.True(viewModel.Undo());
        Assert.True(viewModel.SeasonMap.TryGetOccurrence(1, 1, "spring", out spring));
        Assert.False(spring.Locked);
    }

    [Fact]
    public void UpdateSeasons_EquivalentDocumentIsNoOpAndPreservesHistory()
    {
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(new CampaignWorld(CreateDefinition(3, 3)), generationResult: null);
        var stroke = new CampaignSeasonStrokeBuilder(viewModel.SeasonMap!);
        stroke.Upsert(new CampaignTileCoordinate(1, 1), "winter", locked: true);
        viewModel.RecordSeasonStroke(stroke.Complete("Add Winter"));

        var changed = viewModel.UpdateSeasons(
            CampaignSeasonCatalog.DefaultBuiltInDefinitions,
            customDefinitions: [],
            CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
            new Dictionary<string, string>(StringComparer.Ordinal),
            "winter");

        Assert.False(changed);
        Assert.True(viewModel.CanUndo);
        Assert.True(viewModel.SeasonMap!.TryGetOccurrence(1, 1, "winter", out var winter));
        Assert.True(winter.Locked);
    }

    [Fact]
    public void UpdateSeasons_MissingUsedDefinitionReplacementRejectsAtomically()
    {
        var definition = CreateDefinition(3, 3);
        var custom = CreateMonsoon();
        var seasons = new CampaignSeasonMap(definition, new CampaignSeasonCatalog([custom]));
        seasons.Upsert(1, 1, new(custom.Id, Locked: true));
        var viewModel = Open(new EditorViewModel(), definition, seasons);

        var exception = Assert.Throws<InvalidOperationException>(() => viewModel.UpdateSeasons(
            CampaignSeasonCatalog.DefaultBuiltInDefinitions,
            customDefinitions: [],
            CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
            new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.Contains("Choose a replacement", exception.Message, StringComparison.Ordinal);
        Assert.Same(seasons, viewModel.SeasonMap);
        Assert.True(viewModel.SeasonMap!.TryGetOccurrence(1, 1, custom.Id, out _));
    }

    [Fact]
    public void RegenerateWorld_ChangedLatticeRejectsOccurrenceAuthorityWithoutReviewedRemap()
    {
        var original = new CampaignWorld(CreateDefinition(4, 4));
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(original, generationResult: null);
        viewModel.SeasonMap!.Upsert(2, 2, new("winter"));
        var candidate = new CampaignWorld(CreateDefinition(5, 4));

        var exception = Assert.Throws<InvalidOperationException>(() => viewModel.RegenerateWorld(
            candidate,
            CreateGenerationResult(CampaignMapGenerationPreset.Continent, seed: 71)));

        Assert.Contains("reviewed season remap", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(original, viewModel.World);
        Assert.True(viewModel.SeasonMap.TryGetOccurrence(2, 2, "winter", out _));
    }

    [Fact]
    public void RegenerateWorld_AcceptsReviewedCandidateWithCoexistingLocksAndClearsHistory()
    {
        var original = new CampaignWorld(CreateDefinition(2, 1));
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(original, generationResult: null);
        var stroke = new CampaignSeasonStrokeBuilder(viewModel.SeasonMap!);
        stroke.Upsert(new CampaignTileCoordinate(0, 0), "winter", locked: true);
        stroke.Upsert(new CampaignTileCoordinate(1, 0), "spring", locked: true);
        viewModel.RecordSeasonStroke(stroke.Complete("Lock two seasons"));
        var candidate = new CampaignWorld(CreateDefinition(1, 1, tileSizeMeters: 2_000));
        var settings = new CampaignSeasonGenerationSettings(
            211,
            enabledSeasonIds: viewModel.SeasonEnabledIds);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            original,
            viewModel.SeasonMap!,
            viewModel.SeasonEnabledIds);
        var reviewed = new CampaignSeasonWorldRegenerator().Generate(source, candidate, settings);
        Assert.True(reviewed.Report.CanAccept);

        viewModel.RegenerateWorld(
            candidate,
            CreateGenerationResult(CampaignMapGenerationPreset.Continent, seed: 95),
            resourceRegenerationResult: null,
            seasonRegenerationResult: reviewed);

        Assert.Same(candidate, viewModel.World);
        Assert.True(viewModel.SeasonMap!.TryGetOccurrence(0, 0, "spring", out var spring));
        Assert.True(viewModel.SeasonMap.TryGetOccurrence(0, 0, "winter", out var winter));
        Assert.True(spring.Locked);
        Assert.True(winter.Locked);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void RegenerateWorld_StaleSeasonSourceRejectsWithoutPartialReplacement()
    {
        var original = new CampaignWorld(CreateDefinition(2, 2));
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(original, generationResult: null);
        var candidate = new CampaignWorld(CreateDefinition(4, 4));
        var settings = new CampaignSeasonGenerationSettings(
            257,
            enabledSeasonIds: viewModel.SeasonEnabledIds);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            original,
            viewModel.SeasonMap!,
            viewModel.SeasonEnabledIds);
        var reviewed = new CampaignSeasonWorldRegenerator().Generate(source, candidate, settings);
        viewModel.SeasonMap!.Upsert(1, 1, new("fall", Locked: true));

        var exception = Assert.Throws<InvalidOperationException>(() => viewModel.RegenerateWorld(
            candidate,
            CreateGenerationResult(CampaignMapGenerationPreset.EastCoast, seed: 99),
            resourceRegenerationResult: null,
            seasonRegenerationResult: reviewed));

        Assert.Contains("changed after", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(original, viewModel.World);
        Assert.True(viewModel.SeasonMap.TryGetOccurrence(1, 1, "fall", out _));
    }

    private static EditorViewModel Open(
        EditorViewModel viewModel,
        CampaignWorldDefinition definition,
        CampaignSeasonMap seasons)
    {
        viewModel.OpenWorld(
            new CampaignWorld(definition),
            new CampaignResourceMap(definition),
            resourceGenerationSettings: null,
            seasons,
            CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
            seasonSavedGeneration: null,
            projectDirectory: null,
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: string.Empty);
        return viewModel;
    }

    private static CampaignSeasonDefinition CreateMonsoon() =>
        new(
            "monsoon",
            "Monsoon",
            CampaignBuiltInSeason.Summer,
            "#3F9D78",
            tintStrengthPercent: 60,
            effectIntensityPercent: 70,
            new CampaignSeasonRule(
                moisture: new CampaignSeasonRange(0.6, 1),
                seasonality: new CampaignSeasonRange(0.2, 1)));

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

    private static CampaignWorldDefinition CreateDefinition(
        int tilesX,
        int tilesY,
        int tileSizeMeters = 1_000) =>
        CampaignWorldDefinition.Create(
            tilesX * (long)tileSizeMeters,
            tilesY * (long)tileSizeMeters,
            campaignTileSizeMeters: tileSizeMeters,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 0);
}
