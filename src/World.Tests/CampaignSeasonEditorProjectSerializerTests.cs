using System.IO.Compression;
using System.Text.Json;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Serialization;
using Kingdom.World.Core.Terrain;
using Kingdom.World.Editor.Services;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonEditorProjectSerializerTests
{
    [Fact]
    public void ManagedFileNamesWithSeasons_ExtendsButDoesNotChangeLegacyCoordinatorScope()
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
        Assert.Equal(
        [
            CampaignWorldProjectSerializer.ManifestFileName,
            CampaignWorldProjectSerializer.CampaignTileFileName,
            CampaignWorldProjectSerializer.CustomTerrainFileName,
            CampaignResourceProjectSerializer.DefinitionsFileName,
            CampaignResourceProjectSerializer.GenerationFileName,
            CampaignResourceProjectSerializer.TilesFileName,
            CampaignSeasonProjectSerializer.DefinitionsFileName,
            CampaignSeasonProjectSerializer.GenerationFileName,
            CampaignSeasonProjectSerializer.LayerFileName,
        ], CampaignEditorProjectSerializer.ManagedFileNamesWithSeasons);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)CampaignEditorProjectSerializer.ManagedFileNamesWithSeasons)[0] =
                "changed.json");
    }

    [Fact]
    public async Task LoadWithSeasons_MissingSidecarsCreatesCleanImplicitSpringProjection()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "terrain-only");
        var world = CreateWorld();
        await CampaignWorldProjectSerializer.SaveAsync(world, projectPath);

        var loaded = await CampaignEditorProjectSerializer.LoadWithSeasonsAsync(projectPath);

        Assert.False(loaded.WasConvertedFromLegacy);
        Assert.True(loaded.SeasonsWereImplicitCompatibility);
        Assert.Equal(0, loaded.SeasonMap.OccurrenceCount);
        Assert.Equal(0, loaded.SeasonMap.GetUsageCount("spring"));
        Assert.Equal(0, loaded.SeasonMap.LockedOccurrenceCount);
        Assert.Equal(0, loaded.SeasonMap.Revision);
        Assert.Null(loaded.SeasonSavedGeneration);
        Assert.Equal(CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds, loaded.SeasonEnabledIds);
    }

    [Fact]
    public async Task LoadWithSeasons_LegacyImportNeverReadsSiblingSeasonFiles()
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
        await WorldProjectSerializer.SaveAsync(new WorldTerrain(legacyDefinition), temporary.Path);
        await File.WriteAllTextAsync(
            Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.DefinitionsFileName),
            "malformed season data that must be ignored");

        var loaded = await CampaignEditorProjectSerializer.LoadWithSeasonsAsync(temporary.Path);

        Assert.True(loaded.WasConvertedFromLegacy);
        Assert.True(loaded.SeasonsWereImplicitCompatibility);
        Assert.Equal(0, loaded.SeasonMap.GetUsageCount("spring"));
        Assert.Null(loaded.SeasonSavedGeneration);
    }

    [Fact]
    public async Task SaveAndLoadWithSeasons_RoundTripsAllAuthoritiesThroughOneManagedCommit()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "complete");
        var world = CreateWorld();
        world.Tiles.SetTile(1, 1, new CampaignTileData(CampaignTileType.Forest, 315));
        var resources = new CampaignResourceMap(world.Definition);
        resources.Upsert(0, 0, new CampaignResourceOccurrence("timber", 77, Locked: true));
        var resourceSettings = new CampaignResourceGenerationSettings(71);
        var customSeason = CreateCustomSeason();
        var seasonMap = new CampaignSeasonMap(
            world.Definition,
            new CampaignSeasonCatalog([customSeason]));
        seasonMap.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("winter", Locked: true)),
            CampaignSeasonMutation.Upsert(0, 0, new("spring")),
            CampaignSeasonMutation.Upsert(1, 1, new("spring")),
        ]);
        var enabledSeasonIds = new[] { "winter", "monsoon", "spring" };
        var seasonSettings = new CampaignSeasonGenerationSettings(
            91,
            seedDerivedFromTerrain: false,
            enabledSeasonIds: enabledSeasonIds);
        var savedSeason = new CampaignSeasonSavedGeneration(
            seasonSettings,
            new string('a', 64),
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(
                seasonMap.Catalog,
                seasonSettings));
        var worldRevision = world.Revision;
        var resourceRevision = resources.Revision;
        var seasonRevision = seasonMap.Revision;

        await CampaignEditorProjectSerializer.SaveWithSeasonsAsync(
            world,
            resources,
            resourceSettings,
            seasonMap,
            enabledSeasonIds,
            savedSeason,
            projectPath);
        var loaded = await CampaignEditorProjectSerializer.LoadWithSeasonsAsync(projectPath);

        Assert.Equal(worldRevision, world.Revision);
        Assert.Equal(resourceRevision, resources.Revision);
        Assert.Equal(seasonRevision, seasonMap.Revision);
        Assert.False(loaded.WasConvertedFromLegacy);
        Assert.False(loaded.SeasonsWereImplicitCompatibility);
        Assert.Equal(world.Tiles.GetTile(1, 1), loaded.World.Tiles.GetTile(1, 1));
        Assert.Equal(resources.GetMaterializedOccurrences(), loaded.ResourceMap.GetMaterializedOccurrences());
        Assert.Equal(
            seasonMap.GetMaterializedOccurrences(),
            loaded.SeasonMap.GetMaterializedOccurrences());
        Assert.Equal(enabledSeasonIds.Order(StringComparer.Ordinal), loaded.SeasonEnabledIds);
        Assert.Equal(91, loaded.SeasonGenerationSettings!.SeasonSeed);
        Assert.Equal(new string('a', 64), loaded.SeasonSavedGeneration!.SourceTerrainFingerprint);
        Assert.All(
        [
            CampaignWorldProjectSerializer.ManifestFileName,
            CampaignWorldProjectSerializer.CampaignTileFileName,
            CampaignResourceProjectSerializer.GenerationFileName,
            CampaignResourceProjectSerializer.TilesFileName,
            CampaignSeasonProjectSerializer.DefinitionsFileName,
            CampaignSeasonProjectSerializer.GenerationFileName,
            CampaignSeasonProjectSerializer.LayerFileName,
        ], fileName => Assert.True(File.Exists(Path.Combine(projectPath, fileName)), fileName));
        Assert.False(File.Exists(Path.Combine(
            projectPath,
            CampaignWorldProjectSerializer.CustomTerrainFileName)));
        Assert.False(File.Exists(Path.Combine(
            projectPath,
            CampaignResourceProjectSerializer.DefinitionsFileName)));
        Assert.Empty(GetStagingDirectories(temporary.Path, "complete"));
    }

    [Fact]
    public async Task SaveWithSeasons_NullRecipeDeletesOnlyStaleSeasonGeneration()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "recipe-removal");
        var world = CreateWorld();
        var resources = new CampaignResourceMap(world.Definition);
        var seasons = new CampaignSeasonMap(world.Definition);
        var enabledSeasonIds = CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds;
        await CampaignEditorProjectSerializer.SaveWithSeasonsAsync(
            world,
            resources,
            resourceGenerationSettings: null,
            seasons,
            enabledSeasonIds,
            CreateSavedGeneration(seasons, new CampaignSeasonGenerationSettings(11), '1'),
            projectPath);

        await CampaignEditorProjectSerializer.SaveWithSeasonsAsync(
            world,
            resources,
            resourceGenerationSettings: null,
            seasons,
            enabledSeasonIds,
            seasonSavedGeneration: null,
            projectPath);
        var loaded = await CampaignEditorProjectSerializer.LoadWithSeasonsAsync(projectPath);

        Assert.False(File.Exists(Path.Combine(
            projectPath,
            CampaignSeasonProjectSerializer.GenerationFileName)));
        Assert.True(File.Exists(Path.Combine(
            projectPath,
            CampaignSeasonProjectSerializer.DefinitionsFileName)));
        Assert.True(File.Exists(Path.Combine(
            projectPath,
            CampaignSeasonProjectSerializer.LayerFileName)));
        Assert.Null(loaded.SeasonSavedGeneration);
        Assert.False(loaded.SeasonsWereImplicitCompatibility);
    }

    [Fact]
    public async Task LegacySaveOverload_PreservesSeasonFilesItDoesNotOwn()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "legacy-save-safety");
        var world = CreateWorld();
        var resources = new CampaignResourceMap(world.Definition);
        var seasons = new CampaignSeasonMap(world.Definition);
        seasons.Upsert(1, 1, new("winter"));
        await CampaignEditorProjectSerializer.SaveWithSeasonsAsync(
            world,
            resources,
            resourceGenerationSettings: null,
            seasons,
            CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
            seasonSavedGeneration: null,
            projectPath);
        var before = await ReadSeasonFilesAsync(projectPath);

        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Hills, 210));
        await CampaignEditorProjectSerializer.SaveAsync(
            world,
            resources,
            resourceGenerationSettings: null,
            projectPath);
        var after = await ReadSeasonFilesAsync(projectPath);

        Assert.Equal(before.Keys, after.Keys);
        foreach (var fileName in before.Keys)
        {
            Assert.Equal(before[fileName], after[fileName]);
        }
    }

    [Fact]
    public async Task SaveWithSeasons_CommitFailureRollsBackAllNineManagedFiles()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "rollback");
        Directory.CreateDirectory(projectPath);
        var original = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [CampaignWorldProjectSerializer.ManifestFileName] = [1, 2, 3],
            [CampaignWorldProjectSerializer.CampaignTileFileName] = [4, 5, 6],
            [CampaignResourceProjectSerializer.DefinitionsFileName] = [7, 8, 9],
            [CampaignSeasonProjectSerializer.DefinitionsFileName] = [10, 11, 12],
            [CampaignSeasonProjectSerializer.LayerFileName] = [13, 14, 15],
        };
        foreach (var pair in original)
        {
            await File.WriteAllBytesAsync(Path.Combine(projectPath, pair.Key), pair.Value);
        }

        await File.WriteAllTextAsync(Path.Combine(projectPath, "unrelated.txt"), "keep");
        Directory.CreateDirectory(
            Path.Combine(projectPath, CampaignSeasonProjectSerializer.GenerationFileName));
        var world = CreateWorld();
        var resources = new CampaignResourceMap(world.Definition);
        var seasons = new CampaignSeasonMap(world.Definition);

        await Assert.ThrowsAsync<IOException>(() =>
            CampaignEditorProjectSerializer.SaveWithSeasonsAsync(
                world,
                resources,
                resourceGenerationSettings: null,
                seasons,
                CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
                CreateSavedGeneration(seasons, new CampaignSeasonGenerationSettings(9), 'a'),
                projectPath));

        foreach (var pair in original)
        {
            Assert.Equal(
                pair.Value,
                await File.ReadAllBytesAsync(Path.Combine(projectPath, pair.Key)));
        }

        Assert.True(Directory.Exists(Path.Combine(
            projectPath,
            CampaignSeasonProjectSerializer.GenerationFileName)));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(projectPath, "unrelated.txt")));
        Assert.Empty(GetStagingDirectories(temporary.Path, "rollback"));
    }

    [Fact]
    public async Task SaveWithSeasons_PreCancelledOperationLeavesNoTargetOrStagingDirectory()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "cancelled");
        var world = CreateWorld();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CampaignEditorProjectSerializer.SaveWithSeasonsAsync(
                world,
                new CampaignResourceMap(world.Definition),
                resourceGenerationSettings: null,
                new CampaignSeasonMap(world.Definition),
                CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
                seasonSavedGeneration: null,
                projectPath,
                cancellation.Token));

        Assert.False(Directory.Exists(projectPath));
        Assert.Empty(GetStagingDirectories(temporary.Path, "cancelled"));
    }

    [Fact]
    public async Task SaveWithSeasons_SeasonRevisionChangeRejectsCommitAndCleansStaging()
    {
        using var temporary = new TemporaryDirectory();
        var projectPath = Path.Combine(temporary.Path, "season-revision-change");
        var world = CreateWorld();
        var resources = new CampaignResourceMap(world.Definition);
        var seasons = new CampaignSeasonMap(world.Definition);

        var saveTask = CampaignEditorProjectSerializer.SaveWithSeasonsAsync(
            world,
            resources,
            resourceGenerationSettings: null,
            seasons,
            CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
            seasonSavedGeneration: null,
            projectPath);
        seasons.Upsert(0, 0, new("winter"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => saveTask);
        Assert.Contains("seasons changed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(projectPath));
        Assert.Empty(GetStagingDirectories(temporary.Path, "season-revision-change"));
    }

    [Fact]
    public async Task ExportWithSeasons_WritesVersionThreePackage()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        var packagePath = Path.Combine(temporary.Path, "runtime.kworld");

        await CampaignEditorProjectSerializer.ExportWithSeasonsAsync(
            world,
            new CampaignResourceMap(world.Definition),
            new CampaignSeasonMap(world.Definition),
            packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        Assert.NotNull(archive.GetEntry(CampaignWorldRuntimeExporter.SeasonIndexEntryName));
        Assert.NotNull(archive.GetEntry(CampaignWorldRuntimeExporter.SeasonRecordsEntryName));
        await using var manifestStream = archive
            .GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!
            .Open();
        using var manifest = await JsonDocument.ParseAsync(manifestStream);
        Assert.Equal(
            CampaignWorldRuntimeExporter.SeasonFormatVersion,
            manifest.RootElement.GetProperty("version").GetInt32());
    }

    private static CampaignWorld CreateWorld() =>
        new(CampaignWorldDefinition.Create(
            worldWidthMeters: 10_000,
            worldHeightMeters: 10_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000));

    private static CampaignSeasonDefinition CreateCustomSeason() =>
        new(
            "monsoon",
            "Monsoon",
            CampaignBuiltInSeason.Summer,
            "#467A9C",
            64,
            81,
            new CampaignSeasonRule(moisture: new CampaignSeasonRange(0.6, 1)));

    private static CampaignSeasonSavedGeneration CreateSavedGeneration(
        CampaignSeasonMap seasons,
        CampaignSeasonGenerationSettings settings,
        char sourceFingerprintCharacter) =>
        new(
            settings,
            new string(sourceFingerprintCharacter, 64),
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(seasons.Catalog, settings));

    private static IEnumerable<string> GetStagingDirectories(
        string parentDirectory,
        string projectName) =>
        Directory.EnumerateDirectories(parentDirectory, $".{projectName}.*.stage");

    private static async Task<Dictionary<string, byte[]>> ReadSeasonFilesAsync(string directory)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var fileName in new[]
                 {
                     CampaignSeasonProjectSerializer.DefinitionsFileName,
                     CampaignSeasonProjectSerializer.GenerationFileName,
                     CampaignSeasonProjectSerializer.LayerFileName,
                 })
        {
            var path = Path.Combine(directory, fileName);
            if (File.Exists(path))
            {
                result.Add(fileName, await File.ReadAllBytesAsync(path));
            }
        }

        return result;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"KingdomCampaignSeasonEditorSerializationTests-{Guid.NewGuid():N}");
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
