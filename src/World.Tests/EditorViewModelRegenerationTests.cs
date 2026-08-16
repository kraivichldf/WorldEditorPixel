using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Tests;

public sealed class EditorViewModelRegenerationTests
{
    [Fact]
    public void TerrainSelector_ExposesBuiltInSteppeForManualPainting()
    {
        var viewModel = new EditorViewModel();

        var steppe = Assert.Single(viewModel.CampaignTileTypeOptions, option =>
            option.Type == CampaignTileType.Steppe && option.CustomTerrainId is null);

        Assert.Equal("Steppe", steppe.Name);
        Assert.Contains("semi-arid", steppe.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegenerateWorld_PreservesProjectIdentityAndClearsObsoleteHistory()
    {
        var definition = CreateDefinition(8, 8);
        var original = new CampaignWorld(definition);
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(original, generationResult: null);
        viewModel.MarkSaved(@"F:\Worlds\Northreach");

        var stamp = new CampaignTileStampBuilder(original.Tiles);
        stamp.ApplyTile(
            new CampaignTileCoordinate(0, 0),
            new CampaignTileData(CampaignTileType.Forest, 120));
        viewModel.RecordTileStroke(stamp.Complete("Paint Forest"));
        Assert.True(viewModel.CanUndo);

        var farmland = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#8DA65E",
            GenerationSharePercent: 20);
        var replacement = new CampaignWorld(definition, [farmland]);
        replacement.Tiles.SetTile(
            1,
            1,
            new CampaignTileData(CampaignTileType.Plains, 80, farmland.Id));
        var generationResult = CreateGenerationResult(
            CampaignMapGenerationPreset.Continent,
            seed: 42,
            landMix: new CampaignMapLandMix(42, 20, 8, 8, 2),
            [farmland]);

        viewModel.RegenerateWorld(replacement, generationResult);

        Assert.Same(replacement, viewModel.World);
        Assert.Equal(@"F:\Worlds\Northreach", viewModel.ProjectDirectory);
        Assert.Equal("Northreach", viewModel.WorldName);
        Assert.True(viewModel.IsDirty);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.Equal(CampaignMapGenerationPreset.Continent, viewModel.LastGenerationOptions?.Preset);
        Assert.Equal(42, viewModel.LastGenerationOptions?.Seed);
        Assert.Null(viewModel.LastGenerationOptions?.CustomTerrainDefinitions);
        Assert.Contains("project identity was kept", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void RegenerateWorld_AcceptsAChangedWorldDefinitionAndKeepsProjectIdentity()
    {
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(new CampaignWorld(CreateDefinition(8, 8)), generationResult: null);
        viewModel.MarkSaved(@"F:\Worlds\ChangingScale");
        var changedDefinitionWorld = new CampaignWorld(CreateDefinition(10, 8));

        viewModel.RegenerateWorld(
            changedDefinitionWorld,
            CreateGenerationResult(
                CampaignMapGenerationPreset.Island,
                seed: 7,
                landMix: null,
                customTerrainDefinitions: []));

        Assert.Same(changedDefinitionWorld, viewModel.World);
        Assert.Equal(10, changedDefinitionWorld.Definition.TilesX);
        Assert.Equal(8, changedDefinitionWorld.Definition.TilesY);
        Assert.Equal(@"F:\Worlds\ChangingScale", viewModel.ProjectDirectory);
        Assert.True(viewModel.IsDirty);
        Assert.Contains("World definition and tiles were replaced", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenWorld_DoesNotInventSavedGeneratorSettings()
    {
        var definition = CreateDefinition(8, 8);
        var viewModel = new EditorViewModel();
        viewModel.CreateWorld(
            new CampaignWorld(definition),
            CreateGenerationResult(
                CampaignMapGenerationPreset.EastCoast,
                seed: 99,
                landMix: null,
                customTerrainDefinitions: []));
        Assert.NotNull(viewModel.LastGenerationOptions);

        viewModel.OpenWorld(
            new CampaignWorld(definition),
            @"F:\Worlds\Loaded",
            wasConvertedFromLegacy: false,
            sourceProjectDirectory: @"F:\Worlds\Loaded");

        Assert.Null(viewModel.LastGenerationOptions);
    }

    private static CampaignMapGenerationResult CreateGenerationResult(
        CampaignMapGenerationPreset preset,
        int seed,
        CampaignMapLandMix? landMix,
        IReadOnlyList<CampaignCustomTerrainDefinition> customTerrainDefinitions) =>
        new(
            preset,
            seed,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapMountainDensity.Sparse,
            CampaignMapHydrology.Balanced,
            Tiles: [],
            LandTileCount: 64,
            SeaTileCount: 0,
            LakeTileCount: 0,
            RiverTileCount: 0,
            CliffTileCount: 0)
        {
            RequestedLandMix = landMix,
            CustomTerrainDefinitions = customTerrainDefinitions,
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
