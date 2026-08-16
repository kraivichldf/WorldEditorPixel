using System.IO.Compression;
using System.Text.Json;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Serialization;
using Kingdom.World.Core.Terrain;
using Kingdom.World.Editor.Services;

namespace Kingdom.World.Tests;

public sealed class CampaignEditorProjectSerializerTests
{
    [Fact]
    public void ManagedFileNames_AreCompleteDeterministicAndReadOnly()
    {
        Assert.Equal(
        [
            CampaignWorldProjectSerializer.ManifestFileName,
            CampaignWorldProjectSerializer.CampaignTileFileName,
            CampaignWorldProjectSerializer.CustomTerrainFileName,
            CampaignResourceProjectSerializer.DefinitionsFileName,
            CampaignResourceProjectSerializer.GenerationFileName,
            CampaignResourceProjectSerializer.TilesFileName,
        ], CampaignEditorProjectSerializer.ManagedFileNames);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)CampaignEditorProjectSerializer.ManagedFileNames)[0] = "changed.json");
    }

    [Fact]
    public async Task Load_MissingResourceFilesCreatesCleanBuiltInEmptyLayer()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "terrain-only");
        var world = CreateWorld();
        world.Tiles.SetTile(1, 1, new CampaignTileData(CampaignTileType.Forest, 320));
        await CampaignWorldProjectSerializer.SaveAsync(world, projectPath);

        var loaded = await CampaignEditorProjectSerializer.LoadAsync(projectPath);

        Assert.False(loaded.WasConvertedFromLegacy);
        Assert.Equal(world.Definition, loaded.World.Definition);
        Assert.Equal(world.Tiles.GetTile(1, 1), loaded.World.Tiles.GetTile(1, 1));
        Assert.Equal(world.Definition, loaded.ResourceMap.Definition);
        Assert.Empty(loaded.ResourceMap.Catalog.CustomDefinitions);
        Assert.Equal(CampaignResourceCatalog.BuiltInDefinitions.Count, loaded.ResourceMap.Catalog.Definitions.Count);
        Assert.Equal(0, loaded.ResourceMap.OccurrenceCount);
        Assert.Null(loaded.ResourceGenerationSettings);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsCompleteTerrainAndResourceAuthority()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "complete-project");
        var customTerrain = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#8DA65E",
            GenerationSharePercent: 15);
        var world = CreateWorld([customTerrain]);
        world.Tiles.SetTile(
            2,
            1,
            new CampaignTileData(CampaignTileType.Plains, 145, customTerrain.Id));
        var customResource = CreateCustomResource();
        var resources = new CampaignResourceMap(
            world.Definition,
            new CampaignResourceCatalog([customResource]));
        resources.Apply(
        [
            CampaignResourceMutation.Upsert(2, 1, new CampaignResourceOccurrence("amber", 87, true)),
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("gold", 33)),
        ]);
        var settings = new CampaignResourceGenerationSettings(
            resourceSeed: -771_204,
            seedDerivedFromWorld: false,
            abundance: CampaignResourceAbundance.Custom,
            climate: CampaignResourceClimateProfile.Temperate,
            geology: CampaignResourceGeologyProfile.FoldBelt,
            overrides:
            [
                new CampaignResourceGenerationOverride(
                    "amber",
                    enabled: true,
                    coveragePercent: 7,
                    CampaignResourceRichness.Rich,
                    richnessBias: 8,
                    CampaignResourceConcentration.FewLarge,
                    mapPriority: 63),
            ]);
        var worldRevision = world.Revision;
        var resourceRevision = resources.Revision;

        await CampaignEditorProjectSerializer.SaveAsync(
            world,
            resources,
            settings,
            projectPath);
        var loaded = await CampaignEditorProjectSerializer.LoadAsync(projectPath);

        Assert.Equal(worldRevision, world.Revision);
        Assert.Equal(resourceRevision, resources.Revision);
        Assert.Equal(world.Definition, loaded.World.Definition);
        Assert.Equal(world.Tiles.GetMaterializedTiles().OrderBy(TileKey),
            loaded.World.Tiles.GetMaterializedTiles().OrderBy(TileKey));
        Assert.Equal([customTerrain], loaded.World.Tiles.CustomTerrainDefinitions);
        Assert.Equal(resources.GetMaterializedOccurrences(), loaded.ResourceMap.GetMaterializedOccurrences());
        var loadedDefinition = Assert.Single(loaded.ResourceMap.Catalog.CustomDefinitions);
        Assert.Equal(customResource.Id, loadedDefinition.Id);
        Assert.Equal(customResource.Name, loadedDefinition.Name);
        Assert.Equal(customResource.Rules.RegionScaleKilometers, loadedDefinition.Rules.RegionScaleKilometers);
        var loadedSettings = Assert.IsType<CampaignResourceGenerationSettings>(
            loaded.ResourceGenerationSettings);
        Assert.Equal(settings.ResourceSeed, loadedSettings.ResourceSeed);
        Assert.Equal(settings.SeedDerivedFromWorld, loadedSettings.SeedDerivedFromWorld);
        Assert.Equal("amber", Assert.Single(loadedSettings.Overrides).ResourceId);
        Assert.All(
            CampaignEditorProjectSerializer.ManagedFileNames,
            fileName => Assert.True(File.Exists(Path.Combine(projectPath, fileName)), fileName));
        Assert.Empty(GetStagingDirectories(temporary.Path, "complete-project"));
    }

    [Fact]
    public async Task Save_EmptyResourceLayerRemovesEveryStaleResourceFileAndReopensEmpty()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "remove-stale-resources");
        var world = CreateWorld();
        var populated = new CampaignResourceMap(
            world.Definition,
            new CampaignResourceCatalog([CreateCustomResource()]));
        populated.Upsert(0, 0, new CampaignResourceOccurrence("amber", 81, true));
        await CampaignEditorProjectSerializer.SaveAsync(
            world,
            populated,
            new CampaignResourceGenerationSettings(717),
            projectPath);
        Assert.All(
            ResourceFileNames,
            fileName => Assert.True(File.Exists(Path.Combine(projectPath, fileName)), fileName));

        await CampaignEditorProjectSerializer.SaveAsync(
            world,
            new CampaignResourceMap(world.Definition),
            resourceGenerationSettings: null,
            projectPath);
        var loaded = await CampaignEditorProjectSerializer.LoadAsync(projectPath);

        Assert.All(
            ResourceFileNames,
            fileName => Assert.False(File.Exists(Path.Combine(projectPath, fileName)), fileName));
        Assert.Empty(loaded.ResourceMap.Catalog.CustomDefinitions);
        Assert.Equal(0, loaded.ResourceMap.OccurrenceCount);
        Assert.Null(loaded.ResourceGenerationSettings);
        Assert.Empty(GetStagingDirectories(temporary.Path, "remove-stale-resources"));
    }

    [Fact]
    public async Task Load_MalformedResourceFileRejectsTheCompleteCandidate()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "malformed");
        await CampaignWorldProjectSerializer.SaveAsync(CreateWorld(), projectPath);
        await File.WriteAllTextAsync(
            Path.Combine(projectPath, CampaignResourceProjectSerializer.TilesFileName),
            "{\"version\":1,\"tiles\":[{\"x\":0,\"y\":0,\"resources\":[" +
            "{\"id\":\"unknown\",\"potential\":50,\"locked\":false}]}]}");

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignEditorProjectSerializer.LoadAsync(projectPath));
    }

    [Fact]
    public async Task Load_LegacyImportNeverReadsSiblingResourceFiles()
    {
        using var temporary = new TemporaryDirectory();
        var legacyDefinition = WorldDefinition.Create(
            worldWidthMeters: 80,
            worldHeightMeters: 40,
            heightSampleSpacingMeters: 10,
            campaignTileSizeMeters: 40,
            seaLevelMeters: 0,
            minimumElevationMeters: -1_000,
            maximumElevationMeters: 6_000,
            chunkSize: 4);
        var legacy = new WorldTerrain(legacyDefinition);
        legacy.CampaignTiles.SetTileType(0, 0, CampaignTileType.Forest);
        await WorldProjectSerializer.SaveAsync(legacy, temporary.Path);
        await File.WriteAllTextAsync(
            Path.Combine(temporary.Path, CampaignResourceProjectSerializer.DefinitionsFileName),
            "malformed resource data that must be ignored");

        var loaded = await CampaignEditorProjectSerializer.LoadAsync(temporary.Path);

        Assert.True(loaded.WasConvertedFromLegacy);
        Assert.Equal(0, loaded.ResourceMap.OccurrenceCount);
        Assert.Empty(loaded.ResourceMap.Catalog.CustomDefinitions);
        Assert.Null(loaded.ResourceGenerationSettings);
    }

    [Fact]
    public async Task Export_WritesResourceAwareRuntimePackageVersionTwo()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        var resources = new CampaignResourceMap(world.Definition);
        resources.Upsert(1, 0, new CampaignResourceOccurrence("timber", 72, true));
        var packagePath = Path.Combine(temporary.Path, "runtime.kworld");

        await CampaignEditorProjectSerializer.ExportAsync(world, resources, packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        Assert.Equal(
        [
            CampaignWorldRuntimeExporter.TileDataEntryName,
            CampaignWorldRuntimeExporter.ResourceIndexEntryName,
            CampaignWorldRuntimeExporter.ResourceRecordsEntryName,
            CampaignWorldRuntimeExporter.ManifestEntryName,
        ], archive.Entries.Select(static entry => entry.FullName));
        await using var manifestStream = archive
            .GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!
            .Open();
        using var manifest = await JsonDocument.ParseAsync(manifestStream);
        Assert.Equal(
            CampaignWorldRuntimeExporter.ResourceFormatVersion,
            manifest.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Save_RevisionChangeRejectsCommitAndCleansStaging()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "revision-change");
        var customTerrain = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#8DA65E",
            GenerationSharePercent: 15);
        var world = CreateWorld([customTerrain]);
        var resources = new CampaignResourceMap(world.Definition);

        var saveTask = CampaignEditorProjectSerializer.SaveAsync(
            world,
            resources,
            resourceGenerationSettings: null,
            projectPath);
        resources.Upsert(0, 0, new CampaignResourceOccurrence("gold", 90, true));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => saveTask);
        Assert.Contains("resources changed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(projectPath));
        Assert.Empty(GetStagingDirectories(temporary.Path, "revision-change"));
    }

    [Fact]
    public async Task Save_CommitFailureRollsBackEveryManagedFileAndCleansStaging()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "rollback");
        Directory.CreateDirectory(projectPath);
        var originalManifest = new byte[] { 1, 2, 3, 4 };
        var originalTiles = new byte[] { 5, 6, 7, 8 };
        var originalDefinitions = new byte[] { 9, 10, 11 };
        await File.WriteAllBytesAsync(
            Path.Combine(projectPath, CampaignWorldProjectSerializer.ManifestFileName),
            originalManifest);
        await File.WriteAllBytesAsync(
            Path.Combine(projectPath, CampaignWorldProjectSerializer.CampaignTileFileName),
            originalTiles);
        await File.WriteAllBytesAsync(
            Path.Combine(projectPath, CampaignResourceProjectSerializer.DefinitionsFileName),
            originalDefinitions);
        await File.WriteAllTextAsync(Path.Combine(projectPath, "unrelated.txt"), "keep me");
        Directory.CreateDirectory(
            Path.Combine(projectPath, CampaignResourceProjectSerializer.GenerationFileName));
        var customTerrain = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#8DA65E",
            GenerationSharePercent: 15);
        var world = CreateWorld([customTerrain]);
        var resources = new CampaignResourceMap(
            world.Definition,
            new CampaignResourceCatalog([CreateCustomResource()]));
        resources.Upsert(0, 0, new CampaignResourceOccurrence("amber", 66, true));

        await Assert.ThrowsAsync<IOException>(() => CampaignEditorProjectSerializer.SaveAsync(
            world,
            resources,
            new CampaignResourceGenerationSettings(42),
            projectPath));

        Assert.Equal(
            originalManifest,
            await File.ReadAllBytesAsync(
                Path.Combine(projectPath, CampaignWorldProjectSerializer.ManifestFileName)));
        Assert.Equal(
            originalTiles,
            await File.ReadAllBytesAsync(
                Path.Combine(projectPath, CampaignWorldProjectSerializer.CampaignTileFileName)));
        Assert.Equal(
            originalDefinitions,
            await File.ReadAllBytesAsync(
                Path.Combine(projectPath, CampaignResourceProjectSerializer.DefinitionsFileName)));
        Assert.False(File.Exists(
            Path.Combine(projectPath, CampaignWorldProjectSerializer.CustomTerrainFileName)));
        Assert.False(File.Exists(
            Path.Combine(projectPath, CampaignResourceProjectSerializer.TilesFileName)));
        Assert.Equal("keep me", await File.ReadAllTextAsync(Path.Combine(projectPath, "unrelated.txt")));
        Assert.True(Directory.Exists(
            Path.Combine(projectPath, CampaignResourceProjectSerializer.GenerationFileName)));
        Assert.Empty(GetStagingDirectories(temporary.Path, "rollback"));
    }

    [Fact]
    public async Task Save_PreCancelledOperationLeavesNoTargetOrStagingDirectory()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "cancelled");
        var world = CreateWorld();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CampaignEditorProjectSerializer.SaveAsync(
                world,
                new CampaignResourceMap(world.Definition),
                resourceGenerationSettings: null,
                projectPath,
                cancellation.Token));

        Assert.False(Directory.Exists(projectPath));
        Assert.Empty(GetStagingDirectories(temporary.Path, "cancelled"));
    }

    private static CampaignWorld CreateWorld(
        IReadOnlyList<CampaignCustomTerrainDefinition>? customTerrainDefinitions = null) =>
        new(
            CampaignWorldDefinition.Create(
                worldWidthMeters: 15_000,
                worldHeightMeters: 10_000,
                campaignTileSizeMeters: 5_000,
                seaLevelMeters: 0,
                minimumHeightMeters: -1_000,
                maximumHeightMeters: 6_000,
                defaultTileHeightMeters: 20),
            customTerrainDefinitions);

    private static CampaignResourceDefinition CreateCustomResource() =>
        new(
            "amber",
            "Amber",
            CampaignResourceCategory.Finite,
            CampaignResourceDistributionProfile.SurfaceDeposit,
            CampaignResourceMedium.Land,
            "amber",
            "#E6A53A",
            mapPriority: 63,
            coveragePercent: 7,
            CampaignResourceRichness.Rich,
            CampaignResourceConcentration.FewLarge,
            new CampaignResourceRuleSet(
                CampaignResourceMedium.Land,
                elevationMeters: new CampaignResourceRange(-100, 1_500),
                regionScaleKilometers: CampaignResourceRange.NonNegative(4, 22),
                preferredTerrainTags: ["forest", "mineralized"],
                fieldWeights: new Dictionary<string, double> { ["erosion"] = 1.5 }));

    private static readonly string[] ResourceFileNames =
    [
        CampaignResourceProjectSerializer.DefinitionsFileName,
        CampaignResourceProjectSerializer.GenerationFileName,
        CampaignResourceProjectSerializer.TilesFileName,
    ];

    private static string TileKey(CampaignTileEntry entry) =>
        $"{entry.Y:D10}:{entry.X:D10}";

    private static IEnumerable<string> GetStagingDirectories(string parentDirectory, string projectName) =>
        Directory.EnumerateDirectories(parentDirectory, $".{projectName}.*.stage");

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"KingdomEditorProjectTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
