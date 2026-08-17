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
    public void CreateWorld_InstallsEditableSpringLayerAndSeasonWorkspaceControls()
    {
        var viewModel = new EditorViewModel();
        var world = new CampaignWorld(CreateDefinition(5, 4));

        viewModel.CreateWorld(world, generationResult: null);
        viewModel.SwitchToSeasonsWorkspace();
        viewModel.SeasonPaintAreaRadius = 99;

        Assert.NotNull(viewModel.SeasonMap);
        Assert.Equal(world.Definition, viewModel.SeasonMap.Definition);
        Assert.Equal(20, viewModel.SeasonMap.GetUsageCount("spring"));
        Assert.Equal(0, viewModel.SeasonMap.LockedTileCount);
        Assert.Equal(CampaignSeasonGenerationSettings.DefaultPriority, viewModel.SeasonPriorityIds);
        Assert.Null(viewModel.SeasonSavedGeneration);
        Assert.True(viewModel.IsSeasonsWorkspace);
        Assert.False(viewModel.IsTerrainWorkspace);
        Assert.False(viewModel.IsResourcesWorkspace);
        Assert.True(viewModel.CanEditSeasons);
        Assert.Equal("Campaign season authority", viewModel.CanvasTitle);
        Assert.Equal(4, viewModel.SeasonOptions.Count);
        Assert.Equal("spring", viewModel.SelectedSeasonId);
        Assert.Equal(12, viewModel.SeasonPaintAreaRadius);
        Assert.Equal("25 × 25 tiles", viewModel.SeasonPaintAreaText);
        Assert.True(viewModel.LockManualSeasonEdits);
        Assert.Contains("one ID per tile", viewModel.FooterFormatText, StringComparison.Ordinal);

        viewModel.SeasonSearchText = "not-present";
        Assert.Empty(viewModel.SeasonOptions);
        Assert.True(viewModel.HasNoSeasonOptions);
        Assert.Null(viewModel.SelectedSeasonId);
        Assert.False(viewModel.CanEditSeasons);

        viewModel.SeasonSearchText = "winter";
        Assert.Equal("winter", Assert.Single(viewModel.SeasonOptions).Id);
        Assert.Equal("winter", viewModel.SelectedSeasonId);
    }

    [Fact]
    public void TerrainResourceAndSeasonStrokesShareOneLifoHistory()
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
        resourceStroke.Upsert(1, 1, new CampaignResourceOccurrence("timber", 75, Locked: true));
        viewModel.RecordResourceStroke(resourceStroke.Complete("Paint timber"));

        var seasonStroke = new CampaignSeasonStrokeBuilder(viewModel.SeasonMap!);
        seasonStroke.Paint(new CampaignTileCoordinate(1, 1), "winter", locked: true);
        viewModel.RecordSeasonStroke(seasonStroke.Complete("Paint winter"));

        Assert.Equal("Undo Paint winter", viewModel.UndoMenuLabel);
        Assert.True(viewModel.Undo());
        Assert.Equal(new CampaignSeasonTile("spring"), viewModel.SeasonMap!.GetTile(1, 1));
        Assert.True(viewModel.ResourceMap!.TryGetOccurrence(1, 1, "timber", out _));
        Assert.Equal(CampaignTileType.Forest, world.Tiles.GetTile(1, 1).Type);

        Assert.True(viewModel.Undo());
        Assert.False(viewModel.ResourceMap.TryGetOccurrence(1, 1, "timber", out _));
        Assert.Equal(CampaignTileType.Forest, world.Tiles.GetTile(1, 1).Type);

        Assert.True(viewModel.Undo());
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(1, 1));

        Assert.True(viewModel.Redo());
        Assert.True(viewModel.Redo());
        Assert.True(viewModel.Redo());
        Assert.Equal(new CampaignSeasonTile("winter", true), viewModel.SeasonMap.GetTile(1, 1));
    }

    [Fact]
    public void OpenWorld_InstallsExactSeasonAuthorityPriorityAndRecipe()
    {
        var definition = CreateDefinition(3, 3);
        var world = new CampaignWorld(definition);
        var resources = new CampaignResourceMap(definition with { });
        var custom = CreateMonsoon();
        var seasons = new CampaignSeasonMap(
            definition with { },
            new CampaignSeasonCatalog([custom]),
            custom.Id);
        seasons.Paint(2, 1, "winter", locked: true);
        var priority = new[] { "winter", custom.Id, "summer" };
        var settings = new CampaignSeasonGenerationSettings(41, priorityIds: priority);
        var saved = new CampaignSeasonSavedGeneration(
            settings,
            new string('a', 64),
            new string('b', 64));
        var viewModel = new EditorViewModel();

        viewModel.OpenWorld(
            world,
            resources,
            resourceGenerationSettings: null,
            seasons,
            priority,
            saved,
            @"F:\Worlds\Seasoned",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\Seasoned");

        Assert.Same(seasons, viewModel.SeasonMap);
        Assert.Equal(priority, viewModel.SeasonPriorityIds);
        Assert.Same(saved, viewModel.SeasonSavedGeneration);
        Assert.Contains(viewModel.SeasonOptions, option =>
            option.Id == custom.Id && option.IsCustom && option.IsGenerationEnabled);
        Assert.False(viewModel.IsDirty);
        Assert.Contains("locked season tile", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateSeasons_DeletesUsedCustomWithReplacementAndPreservesLock()
    {
        var definition = CreateDefinition(4, 4);
        var custom = CreateMonsoon();
        var catalog = new CampaignSeasonCatalog([custom]);
        var seasons = new CampaignSeasonMap(definition, catalog);
        seasons.Paint(1, 2, custom.Id, locked: true);
        var viewModel = new EditorViewModel();
        viewModel.OpenWorld(
            new CampaignWorld(definition),
            new CampaignResourceMap(definition),
            resourceGenerationSettings: null,
            seasons,
            CampaignSeasonGenerationSettings.DefaultPriority,
            seasonSavedGeneration: null,
            @"F:\Worlds\ReplaceSeason",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\ReplaceSeason");
        var stroke = new CampaignSeasonStrokeBuilder(seasons);
        stroke.Paint(new CampaignTileCoordinate(0, 0), "winter", locked: true);
        viewModel.RecordSeasonStroke(stroke.Complete("Paint winter"));
        Assert.True(viewModel.CanUndo);

        var changed = viewModel.UpdateSeasons(
            CampaignSeasonCatalog.DefaultBuiltInDefinitions,
            customDefinitions: [],
            CampaignSeasonGenerationSettings.DefaultPriority,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [custom.Id] = CampaignSeasonCatalog.SpringId,
            },
            CampaignSeasonCatalog.SpringId);

        Assert.True(changed);
        Assert.False(viewModel.SeasonMap!.Catalog.Contains(custom.Id));
        Assert.Equal(new CampaignSeasonTile("spring", true), viewModel.SeasonMap.GetTile(1, 2));
        Assert.Equal(new CampaignSeasonTile("winter", true), viewModel.SeasonMap.GetTile(0, 0));
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("spring", viewModel.SelectedSeasonId);
    }

    [Fact]
    public void PinnedSeasonReportsIdentityTerrainRuleAndUndoableLocks()
    {
        var viewModel = new EditorViewModel();
        var world = new CampaignWorld(CreateDefinition(3, 3));
        world.Tiles.SetTile(1, 1, new CampaignTileData(CampaignTileType.Mountain, 1_600));
        viewModel.CreateWorld(world, generationResult: null);
        viewModel.SelectCoordinate(new CampaignTileCoordinate(1, 1));

        Assert.True(viewModel.HasPinnedSeason);
        Assert.Contains("Spring", viewModel.PinnedSeasonIdentityText, StringComparison.Ordinal);
        Assert.Contains("Mountain", viewModel.PinnedSeasonTerrainText, StringComparison.Ordinal);
        Assert.Contains("1,600 m", viewModel.PinnedSeasonTerrainText, StringComparison.Ordinal);
        Assert.Contains("priority", viewModel.PinnedSeasonRuleText, StringComparison.Ordinal);
        Assert.Contains("No accepted generation recipe", viewModel.PinnedSeasonGenerationText, StringComparison.Ordinal);
        Assert.True(viewModel.CanLockPinnedSeason);

        Assert.True(viewModel.LockPinnedSeason());
        Assert.True(viewModel.SeasonMap!.GetTile(1, 1).Locked);
        Assert.True(viewModel.CanUnlockPinnedSeason);
        Assert.True(viewModel.Undo());
        Assert.False(viewModel.SeasonMap.GetTile(1, 1).Locked);
    }

    [Fact]
    public void UpdateSeasons_EquivalentDocumentIsNoOpAndPreservesHistory()
    {
        var viewModel = new EditorViewModel();
        var world = new CampaignWorld(CreateDefinition(3, 3));
        viewModel.CreateWorld(world, generationResult: null);
        var stroke = new CampaignSeasonStrokeBuilder(viewModel.SeasonMap!);
        stroke.Paint(new CampaignTileCoordinate(1, 1), "winter", locked: true);
        viewModel.RecordSeasonStroke(stroke.Complete("Paint winter"));

        var changed = viewModel.UpdateSeasons(
            CampaignSeasonCatalog.DefaultBuiltInDefinitions,
            customDefinitions: [],
            CampaignSeasonGenerationSettings.DefaultPriority,
            new Dictionary<string, string>(StringComparer.Ordinal),
            "winter");

        Assert.False(changed);
        Assert.True(viewModel.CanUndo);
        Assert.True(viewModel.IsDirty);
        Assert.Equal(new CampaignSeasonTile("winter", true), viewModel.SeasonMap!.GetTile(1, 1));
        Assert.Equal("winter", viewModel.SelectedSeasonId);
    }

    [Fact]
    public void UpdateSeasons_MissingUsedDefinitionReplacementRejectsAtomically()
    {
        var definition = CreateDefinition(3, 3);
        var custom = CreateMonsoon();
        var seasons = new CampaignSeasonMap(definition, new CampaignSeasonCatalog([custom]));
        seasons.Paint(1, 1, custom.Id, locked: true);
        var viewModel = new EditorViewModel();
        viewModel.OpenWorld(
            new CampaignWorld(definition),
            new CampaignResourceMap(definition),
            resourceGenerationSettings: null,
            seasons,
            CampaignSeasonGenerationSettings.DefaultPriority,
            seasonSavedGeneration: null,
            projectDirectory: null,
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(() => viewModel.UpdateSeasons(
            CampaignSeasonCatalog.DefaultBuiltInDefinitions,
            customDefinitions: [],
            CampaignSeasonGenerationSettings.DefaultPriority,
            new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.Contains("Choose a replacement", exception.Message, StringComparison.Ordinal);
        Assert.Same(seasons, viewModel.SeasonMap);
        Assert.True(viewModel.SeasonMap!.Catalog.Contains(custom.Id));
        Assert.Equal(new CampaignSeasonTile(custom.Id, true), viewModel.SeasonMap.GetTile(1, 1));
    }

    [Fact]
    public void RegenerateWorld_ChangedLatticeRejectsMeaningfulSeasonAuthorityWithoutReviewedRemap()
    {
        var original = new CampaignWorld(CreateDefinition(4, 4));
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(original, generationResult: null);
        viewModel.SeasonMap!.Paint(2, 2, "winter", locked: false);
        var candidate = new CampaignWorld(CreateDefinition(5, 4));

        var exception = Assert.Throws<InvalidOperationException>(() => viewModel.RegenerateWorld(
            candidate,
            CreateGenerationResult(CampaignMapGenerationPreset.Continent, seed: 71)));

        Assert.Contains("reviewed season remap", exception.Message, StringComparison.Ordinal);
        Assert.Same(original, viewModel.World);
        Assert.Equal(new CampaignSeasonTile("winter"), viewModel.SeasonMap.GetTile(2, 2));
    }

    [Fact]
    public void CreateWorld_InstallsExactGeneratedSeasonCandidateAtomically()
    {
        var world = new CampaignWorld(CreateDefinition(5, 4));
        var catalog = new CampaignSeasonCatalog();
        var settings = new CampaignSeasonGenerationSettings(
            331,
            priorityIds: [CampaignSeasonCatalog.SummerId]);
        var generated = new CampaignSeasonWorldRegenerator().GenerateNewWorld(
            world,
            catalog,
            CampaignSeasonCatalog.SpringId,
            settings);
        var viewModel = new EditorViewModel();

        viewModel.CreateWorld(
            world,
            CreateGenerationResult(CampaignMapGenerationPreset.Continent, seed: 81),
            generated.CandidateMap,
            settings.PriorityIds,
            generated.SavedGeneration,
            generated.GenerationResult.SupportFields);

        Assert.Same(world, viewModel.World);
        Assert.Same(generated.CandidateMap, viewModel.SeasonMap);
        Assert.Same(generated.SavedGeneration, viewModel.SeasonSavedGeneration);
        Assert.Equal(settings.PriorityIds, viewModel.SeasonPriorityIds);
        Assert.Equal(20, viewModel.SeasonMap!.GetUsageCount(CampaignSeasonCatalog.SummerId));
        Assert.Equal(0, viewModel.ResourceMap!.OccurrenceCount);
        Assert.True(viewModel.IsDirty);
        Assert.Contains("Generated", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void RegenerateWorld_AcceptsExactReviewedSeasonCandidateAndClearsSharedHistory()
    {
        var original = new CampaignWorld(CreateDefinition(2, 2));
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(original, generationResult: null);
        viewModel.MarkSaved(@"F:\Worlds\SeasonRemap");
        var seasonStroke = new CampaignSeasonStrokeBuilder(viewModel.SeasonMap!);
        seasonStroke.Paint(new CampaignTileCoordinate(0, 0), CampaignSeasonCatalog.WinterId, locked: true);
        viewModel.RecordSeasonStroke(seasonStroke.Complete("Lock winter"));
        Assert.True(viewModel.CanUndo);
        var candidate = new CampaignWorld(CreateDefinition(4, 4));
        var settings = new CampaignSeasonGenerationSettings(
            149,
            priorityIds: viewModel.SeasonPriorityIds);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            original,
            viewModel.SeasonMap!,
            viewModel.SeasonPriorityIds,
            viewModel.SeasonSavedGeneration);
        var seasonResult = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidate,
            settings);

        viewModel.RegenerateWorld(
            candidate,
            CreateGenerationResult(CampaignMapGenerationPreset.Island, seed: 91),
            resourceRegenerationResult: null,
            seasonRegenerationResult: seasonResult);

        Assert.Same(candidate, viewModel.World);
        Assert.Same(seasonResult.CandidateMap, viewModel.SeasonMap);
        Assert.Same(seasonResult.SavedGeneration, viewModel.SeasonSavedGeneration);
        Assert.Equal(@"F:\Worlds\SeasonRemap", viewModel.ProjectDirectory);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.True(viewModel.IsDirty);
        Assert.Contains("locked Season target", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void RegenerateWorld_UnresolvedSeasonConflictRejectsWithoutPartialReplacement()
    {
        var original = new CampaignWorld(CreateDefinition(2, 1));
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(original, generationResult: null);
        viewModel.SeasonMap!.Paint(0, 0, CampaignSeasonCatalog.WinterId, locked: true);
        viewModel.SeasonMap.Paint(1, 0, CampaignSeasonCatalog.SpringId, locked: true);
        var candidate = new CampaignWorld(CreateDefinition(1, 1, tileSizeMeters: 2_000));
        var settings = new CampaignSeasonGenerationSettings(
            211,
            priorityIds: viewModel.SeasonPriorityIds);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            original,
            viewModel.SeasonMap,
            viewModel.SeasonPriorityIds);
        var blocked = new CampaignSeasonWorldRegenerator().Generate(source, candidate, settings);
        Assert.False(blocked.Report.CanAccept);

        var exception = Assert.Throws<InvalidOperationException>(() => viewModel.RegenerateWorld(
            candidate,
            CreateGenerationResult(CampaignMapGenerationPreset.Continent, seed: 95),
            resourceRegenerationResult: null,
            seasonRegenerationResult: blocked));

        Assert.Contains("unresolved", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(original, viewModel.World);
        Assert.Equal(new CampaignSeasonTile(CampaignSeasonCatalog.WinterId, true),
            viewModel.SeasonMap.GetTile(0, 0));
        Assert.Equal(new CampaignSeasonTile(CampaignSeasonCatalog.SpringId, true),
            viewModel.SeasonMap.GetTile(1, 0));
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
            priorityIds: viewModel.SeasonPriorityIds);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            original,
            viewModel.SeasonMap!,
            viewModel.SeasonPriorityIds);
        var reviewed = new CampaignSeasonWorldRegenerator().Generate(source, candidate, settings);
        viewModel.SeasonMap!.Paint(1, 1, CampaignSeasonCatalog.AutumnId, locked: true);

        var exception = Assert.Throws<InvalidOperationException>(() => viewModel.RegenerateWorld(
            candidate,
            CreateGenerationResult(CampaignMapGenerationPreset.EastCoast, seed: 99),
            resourceRegenerationResult: null,
            seasonRegenerationResult: reviewed));

        Assert.Contains("changed after", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(original, viewModel.World);
        Assert.Equal(new CampaignSeasonTile(CampaignSeasonCatalog.AutumnId, true),
            viewModel.SeasonMap.GetTile(1, 1));
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
                seasonalIntensity: new CampaignSeasonRange(0.2, 1)));

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
