using System.Buffers.Binary;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Serialization;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonSerializationTests
{
    private static readonly string[] SeasonFileNames =
    [
        CampaignSeasonProjectSerializer.DefinitionsFileName,
        CampaignSeasonProjectSerializer.GenerationFileName,
        CampaignSeasonProjectSerializer.LayerFileName,
    ];

    [Fact]
    public async Task SaveAndLoad_RoundTripsZeroToManyOccurrencesPerTileAndRecipe()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateDefinition();
        var catalog = CreateCatalog();
        var map = new CampaignSeasonMap(definition, catalog);
        map.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("spring")),
            CampaignSeasonMutation.Upsert(0, 0, new("summer")),
            CampaignSeasonMutation.Upsert(0, 0, new("fall", Locked: true)),
            CampaignSeasonMutation.Upsert(2, 1, new("spring")),
            CampaignSeasonMutation.Upsert(2, 1, new("summer")),
            CampaignSeasonMutation.Upsert(2, 1, new("fall")),
            CampaignSeasonMutation.Upsert(2, 1, new("winter", Locked: true)),
            CampaignSeasonMutation.Upsert(1, 1, new("monsoon")),
        ]);
        var settings = CreateSettings(["winter", "monsoon", "fall", "spring"]);
        var saved = CreateSaved(catalog, settings);
        var revision = map.Revision;

        await CampaignSeasonProjectSerializer.SaveAsync(map, saved, temporary.Path);
        var loaded = await CampaignSeasonProjectSerializer.LoadAsync(definition, temporary.Path);

        Assert.Equal(revision, map.Revision);
        Assert.False(loaded.WasImplicitCompatibility);
        Assert.Equal(Path.GetFullPath(temporary.Path), loaded.SourceProjectDirectory);
        Assert.Equal(map.GetMaterializedOccurrences(), loaded.SeasonMap.GetMaterializedOccurrences());
        Assert.Equal(2, loaded.SeasonMap.LockedOccurrenceCount);
        Assert.Equal(0, loaded.SeasonMap.Revision);
        AssertCatalogEqual(catalog, loaded.SeasonMap.Catalog);
        var loadedSaved = Assert.IsType<CampaignSeasonSavedGeneration>(loaded.SavedGeneration);
        AssertSettingsEqual(settings, loadedSaved.Settings);
        Assert.Equal(saved.SourceTerrainFingerprint, loadedSaved.SourceTerrainFingerprint);
        Assert.Equal(saved.InputFingerprint, loadedSaved.InputFingerprint);
        Assert.All(SeasonFileNames, fileName =>
            Assert.True(File.Exists(Path.Combine(temporary.Path, fileName)), fileName));
    }

    [Fact]
    public async Task Save_IsByteDeterministicAcrossCatalogAndMutationInsertionOrder()
    {
        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();
        var definition = CreateDefinition();
        var firstCatalog = CreateCatalog(reverseCustomInput: true);
        var secondCatalog = CreateCatalog();
        var firstMap = new CampaignSeasonMap(definition, firstCatalog);
        var secondMap = new CampaignSeasonMap(definition, secondCatalog);
        firstMap.Apply(
        [
            CampaignSeasonMutation.Upsert(2, 1, new("monsoon")),
            CampaignSeasonMutation.Upsert(0, 0, new("winter", Locked: true)),
            CampaignSeasonMutation.Upsert(0, 0, new("spring")),
        ]);
        secondMap.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("spring")),
            CampaignSeasonMutation.Upsert(0, 0, new("winter", Locked: true)),
            CampaignSeasonMutation.Upsert(2, 1, new("monsoon")),
        ]);
        var settings = CreateSettings(["monsoon", "spring", "winter"]);

        await CampaignSeasonProjectSerializer.SaveAsync(
            firstMap,
            CreateSaved(firstCatalog, settings),
            first.Path);
        await CampaignSeasonProjectSerializer.SaveAsync(
            secondMap,
            CreateSaved(secondCatalog, settings),
            second.Path);

        foreach (var fileName in SeasonFileNames)
        {
            Assert.Equal(
                await File.ReadAllBytesAsync(Path.Combine(first.Path, fileName)),
                await File.ReadAllBytesAsync(Path.Combine(second.Path, fileName)));
        }
    }

    [Fact]
    public async Task LayerBinary_StoresPerTileOccurrenceSpansAndLockFlags()
    {
        using var temporary = new TemporaryDirectory();
        var map = new CampaignSeasonMap(CreateDefinition());
        map.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("spring")),
            CampaignSeasonMutation.Upsert(0, 0, new("summer", Locked: true)),
            CampaignSeasonMutation.Upsert(2, 1, new("winter")),
        ]);

        await CampaignSeasonProjectSerializer.SaveAsync(map, temporary.Path);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(
            temporary.Path,
            CampaignSeasonProjectSerializer.LayerFileName));

        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28, 4)));
        Assert.Equal(0u, ReadTileFirst(bytes, 0));
        Assert.Equal(2u, ReadTileCount(bytes, 0));
        Assert.Equal(2u, ReadTileFirst(bytes, 1));
        Assert.Equal(0u, ReadTileCount(bytes, 1));
        Assert.Equal(2u, ReadTileFirst(bytes, 5));
        Assert.Equal(1u, ReadTileCount(bytes, 5));
        var occurrenceBase = CampaignSeasonProjectSerializer.LayerHeaderSize +
            ((int)map.TileCount * CampaignSeasonProjectSerializer.LayerIndexRecordStride);
        Assert.Equal(0, bytes[occurrenceBase + 2]);
        Assert.Equal(1, bytes[occurrenceBase + CampaignSeasonProjectSerializer.LayerOccurrenceRecordStride + 2]);
    }

    [Fact]
    public async Task MissingAllSidecars_CreatesAnEmptyCompatibilityLayer()
    {
        using var temporary = new TemporaryDirectory();
        var loaded = await CampaignSeasonProjectSerializer.LoadAsync(
            CreateDefinition(),
            Path.Combine(temporary.Path, CampaignWorldProjectSerializer.ManifestFileName));

        Assert.True(loaded.WasImplicitCompatibility);
        Assert.Null(loaded.SavedGeneration);
        Assert.Equal(0, loaded.SeasonMap.OccurrenceCount);
        Assert.Equal(0, loaded.SeasonMap.LockedOccurrenceCount);
        Assert.Equal(0, loaded.SeasonMap.Revision);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Load_RejectsPartialSeasonAuthority(bool definitions, bool layer)
    {
        using var temporary = new TemporaryDirectory();
        if (definitions)
        {
            await File.WriteAllTextAsync(
                Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.DefinitionsFileName),
                "{}");
        }

        if (layer)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.LayerFileName),
                [0]);
        }

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignSeasonProjectSerializer.LoadAsync(CreateDefinition(), temporary.Path));
    }

    [Fact]
    public async Task SaveWithoutRecipe_RemovesStaleGenerationButKeepsOccurrenceAuthority()
    {
        using var temporary = new TemporaryDirectory();
        var catalog = new CampaignSeasonCatalog();
        var map = new CampaignSeasonMap(CreateDefinition(), catalog);
        map.Upsert(0, 0, new("spring"));
        var settings = new CampaignSeasonGenerationSettings(9);
        await CampaignSeasonProjectSerializer.SaveAsync(
            map,
            CreateSaved(catalog, settings),
            temporary.Path);

        await CampaignSeasonProjectSerializer.SaveAsync(map, temporary.Path);
        var loaded = await CampaignSeasonProjectSerializer.LoadAsync(map.Definition, temporary.Path);

        Assert.False(File.Exists(Path.Combine(
            temporary.Path,
            CampaignSeasonProjectSerializer.GenerationFileName)));
        Assert.Null(loaded.SavedGeneration);
        Assert.True(loaded.SeasonMap.TryGetOccurrence(0, 0, "spring", out _));
    }

    [Fact]
    public async Task Load_RejectsNonContiguousOrDuplicateOccurrenceSpans()
    {
        using var temporary = new TemporaryDirectory();
        var map = new CampaignSeasonMap(CreateDefinition());
        map.Upsert(0, 0, new("spring"));
        map.Upsert(0, 0, new("summer"));
        await CampaignSeasonProjectSerializer.SaveAsync(map, temporary.Path);
        var path = Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.LayerFileName);
        var bytes = await File.ReadAllBytesAsync(path);

        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(CampaignSeasonProjectSerializer.LayerHeaderSize + 8, 4),
            1);
        await File.WriteAllBytesAsync(path, bytes);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignSeasonProjectSerializer.LoadAsync(map.Definition, temporary.Path));
    }

    [Fact]
    public async Task Load_RejectsUnknownJsonMembersAndInvalidOccurrenceFlags()
    {
        using var temporary = new TemporaryDirectory();
        var map = new CampaignSeasonMap(CreateDefinition());
        map.Upsert(0, 0, new("spring"));
        await CampaignSeasonProjectSerializer.SaveAsync(map, temporary.Path);
        var definitionsPath = Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.DefinitionsFileName);
        var json = await File.ReadAllTextAsync(definitionsPath);
        await File.WriteAllTextAsync(
            definitionsPath,
            json.Replace("\"version\": 1", "\"version\": 1,\n  \"unknown\": true", StringComparison.Ordinal));
        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignSeasonProjectSerializer.LoadAsync(map.Definition, temporary.Path));

        await CampaignSeasonProjectSerializer.SaveAsync(map, temporary.Path);
        var layerPath = Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.LayerFileName);
        var bytes = await File.ReadAllBytesAsync(layerPath);
        var occurrenceOffset = CampaignSeasonProjectSerializer.LayerHeaderSize +
            ((int)map.TileCount * CampaignSeasonProjectSerializer.LayerIndexRecordStride);
        bytes[occurrenceOffset + 2] = 0x80;
        await File.WriteAllBytesAsync(layerPath, bytes);
        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignSeasonProjectSerializer.LoadAsync(map.Definition, temporary.Path));
    }

    [Fact]
    public async Task Save_PreCancelledOperationLeavesNoAuthorityOrTemporaryFile()
    {
        using var temporary = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CampaignSeasonProjectSerializer.SaveAsync(
                new CampaignSeasonMap(CreateDefinition()),
                temporary.Path,
                cancellation.Token));

        Assert.Empty(Directory.EnumerateFiles(temporary.Path));
    }

    private static uint ReadTileFirst(byte[] bytes, int tileIndex) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(
            CampaignSeasonProjectSerializer.LayerHeaderSize +
            (tileIndex * CampaignSeasonProjectSerializer.LayerIndexRecordStride),
            4));

    private static uint ReadTileCount(byte[] bytes, int tileIndex) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(
            CampaignSeasonProjectSerializer.LayerHeaderSize +
            (tileIndex * CampaignSeasonProjectSerializer.LayerIndexRecordStride) + 4,
            4));

    private static CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: 15_000,
            worldHeightMeters: 10_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 20);

    private static CampaignSeasonCatalog CreateCatalog(bool reverseCustomInput = false)
    {
        var monsoon = new CampaignSeasonDefinition(
            "monsoon",
            "Monsoon",
            CampaignBuiltInSeason.Summer,
            "#467A9C",
            64,
            81,
            new CampaignSeasonRule(
                latitudeDegrees: new(-35, 35),
                warmSeasonTemperatureCelsius: new(14, 45),
                annualTemperatureRangeCelsius: new(0, 35),
                moisture: new(0.65, 1),
                seasonality: new(0.1, 1),
                terrainExcludes: [CampaignTileType.Desert]));
        var dry = new CampaignSeasonDefinition(
            "dry-season",
            "Dry Season",
            CampaignBuiltInSeason.Fall,
            "#C6A15B",
            42,
            58,
            new CampaignSeasonRule(moisture: new(0, 0.4)));
        return new CampaignSeasonCatalog(reverseCustomInput ? [monsoon, dry] : [dry, monsoon]);
    }

    private static CampaignSeasonGenerationSettings CreateSettings(
        IReadOnlyList<string> enabled) =>
        new(
            seasonSeed: 123,
            seedDerivedFromTerrain: false,
            coverageMode: CampaignSeasonCoverageMode.Regional,
            regionalCenterLatitudeDegrees: 12.5,
            axialTiltDegrees: 27.25,
            enabledSeasonIds: enabled);

    private static CampaignSeasonSavedGeneration CreateSaved(
        CampaignSeasonCatalog catalog,
        CampaignSeasonGenerationSettings settings) =>
        new(
            settings,
            new string('a', 64),
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(catalog, settings));

    private static void AssertCatalogEqual(
        CampaignSeasonCatalog expected,
        CampaignSeasonCatalog actual)
    {
        Assert.Equal(expected.Definitions.Select(static value => value.Id),
            actual.Definitions.Select(static value => value.Id));
        foreach (var expectedDefinition in expected.Definitions)
        {
            var actualDefinition = actual.Get(expectedDefinition.Id);
            Assert.Equal(expectedDefinition.Name, actualDefinition.Name);
            Assert.Equal(expectedDefinition.ColorHex, actualDefinition.ColorHex);
            Assert.Equal(expectedDefinition.Rule.LatitudeDegrees, actualDefinition.Rule.LatitudeDegrees);
            Assert.Equal(expectedDefinition.Rule.WarmSeasonTemperatureCelsius,
                actualDefinition.Rule.WarmSeasonTemperatureCelsius);
            Assert.Equal(expectedDefinition.Rule.AnnualTemperatureRangeCelsius,
                actualDefinition.Rule.AnnualTemperatureRangeCelsius);
            Assert.Equal(expectedDefinition.Rule.Moisture, actualDefinition.Rule.Moisture);
            Assert.Equal(expectedDefinition.Rule.Seasonality, actualDefinition.Rule.Seasonality);
            Assert.Equal(expectedDefinition.Rule.TerrainExcludes, actualDefinition.Rule.TerrainExcludes);
        }
    }

    private static void AssertSettingsEqual(
        CampaignSeasonGenerationSettings expected,
        CampaignSeasonGenerationSettings actual)
    {
        Assert.Equal(expected.SeasonSeed, actual.SeasonSeed);
        Assert.Equal(expected.SeedDerivedFromTerrain, actual.SeedDerivedFromTerrain);
        Assert.Equal(expected.CoverageMode, actual.CoverageMode);
        Assert.Equal(expected.RegionalCenterLatitudeDegrees, actual.RegionalCenterLatitudeDegrees);
        Assert.Equal(expected.AxialTiltDegrees, actual.AxialTiltDegrees);
        Assert.Equal(expected.EnabledSeasonIds, actual.EnabledSeasonIds);
        Assert.Equal(expected.Climate.LapseRateCelsiusPerKilometer,
            actual.Climate.LapseRateCelsiusPerKilometer);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"WorldEditorPixel-SeasonSerialization-{Guid.NewGuid():N}");
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
