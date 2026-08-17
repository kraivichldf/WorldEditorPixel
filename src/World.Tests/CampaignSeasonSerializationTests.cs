using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task SaveAndLoad_RoundTripsCatalogPriorityRecipeAndEveryDenseTile()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateDefinition();
        var catalog = CreateCatalog();
        var map = new CampaignSeasonMap(definition, catalog, "monsoon");
        map.Apply(
        [
            new CampaignSeasonMutation(0, 0, new CampaignSeasonTile("winter", Locked: true)),
            new CampaignSeasonMutation(2, 1, new CampaignSeasonTile("spring")),
            new CampaignSeasonMutation(1, 1, new CampaignSeasonTile("autumn", Locked: true)),
        ]);
        var priority = new[] { "winter", "monsoon", "autumn", "spring" };
        var settings = CreateSettings(priority);
        var saved = new CampaignSeasonSavedGeneration(
            settings,
            new string('A', 64),
            new string('b', 64));
        var revision = map.Revision;

        await CampaignSeasonProjectSerializer.SaveAsync(
            map,
            priority,
            saved,
            temporary.Path);
        var loaded = await CampaignSeasonProjectSerializer.LoadAsync(
            definition,
            temporary.Path);

        Assert.Equal(revision, map.Revision);
        Assert.False(loaded.WasImplicitCompatibility);
        Assert.Equal(Path.GetFullPath(temporary.Path), loaded.SourceProjectDirectory);
        Assert.Equal("monsoon", loaded.SeasonMap.DefaultSeasonId);
        Assert.Equal(priority, loaded.PriorityIds);
        Assert.Equal(map.GetAllTiles(), loaded.SeasonMap.GetAllTiles());
        Assert.Equal(2, loaded.SeasonMap.LockedTileCount);
        Assert.Equal(0, loaded.SeasonMap.Revision);
        AssertCatalogEqual(catalog, loaded.SeasonMap.Catalog);
        var loadedSaved = Assert.IsType<CampaignSeasonSavedGeneration>(loaded.SavedGeneration);
        Assert.Equal(new string('a', 64), loadedSaved.SourceTerrainFingerprint);
        Assert.Equal(new string('b', 64), loadedSaved.InputFingerprint);
        AssertSettingsEqual(settings, loadedSaved.Settings);
        Assert.All(
            SeasonFileNames,
            fileName => Assert.True(File.Exists(Path.Combine(temporary.Path, fileName)), fileName));
    }

    [Fact]
    public async Task Save_IsByteDeterministicAcrossEquivalentCatalogConstructionAndMutationOrder()
    {
        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();
        var definition = CreateDefinition();
        var firstCatalog = CreateCatalog(customOrderReversed: true);
        var secondCatalog = CreateCatalog();
        var firstMap = new CampaignSeasonMap(definition, firstCatalog);
        var secondMap = new CampaignSeasonMap(definition, secondCatalog);
        firstMap.Apply(
        [
            new CampaignSeasonMutation(2, 1, new CampaignSeasonTile("monsoon")),
            new CampaignSeasonMutation(0, 0, new CampaignSeasonTile("dry-season", true)),
        ]);
        secondMap.Apply(
        [
            new CampaignSeasonMutation(0, 0, new CampaignSeasonTile("dry-season", true)),
            new CampaignSeasonMutation(2, 1, new CampaignSeasonTile("monsoon")),
        ]);
        var priority = new[] { "winter", "monsoon", "dry-season", "spring" };
        var saved = new CampaignSeasonSavedGeneration(
            CreateSettings(priority),
            new string('1', 64),
            new string('2', 64));

        await CampaignSeasonProjectSerializer.SaveAsync(
            firstMap,
            priority,
            saved,
            first.Path);
        await CampaignSeasonProjectSerializer.SaveAsync(
            secondMap,
            priority,
            saved,
            second.Path);

        foreach (var fileName in SeasonFileNames)
        {
            Assert.Equal(
                await File.ReadAllBytesAsync(Path.Combine(first.Path, fileName)),
                await File.ReadAllBytesAsync(Path.Combine(second.Path, fileName)));
        }
    }

    [Fact]
    public async Task Load_MissingAllSidecarsCreatesCleanImplicitSpringAuthority()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateDefinition();

        var loaded = await CampaignSeasonProjectSerializer.LoadAsync(
            definition,
            Path.Combine(temporary.Path, CampaignWorldProjectSerializer.ManifestFileName));

        Assert.True(loaded.WasImplicitCompatibility);
        Assert.Null(loaded.SavedGeneration);
        Assert.Equal(CampaignSeasonGenerationSettings.DefaultPriority, loaded.PriorityIds);
        Assert.Equal(CampaignSeasonCatalog.SpringId, loaded.SeasonMap.DefaultSeasonId);
        Assert.Equal(0, loaded.SeasonMap.Revision);
        Assert.Equal(0, loaded.SeasonMap.LockedTileCount);
        Assert.Equal(
            definition.TileCount,
            loaded.SeasonMap.GetUsageCount(CampaignSeasonCatalog.SpringId));
    }

    [Theory]
    [InlineData("definitions")]
    [InlineData("generation")]
    [InlineData("layer")]
    [InlineData("definitions+generation")]
    [InlineData("generation+layer")]
    public async Task Load_RejectsPartialSeasonAuthority(string presentFiles)
    {
        using var temporary = new TemporaryDirectory();
        foreach (var token in presentFiles.Split('+'))
        {
            var fileName = token switch
            {
                "definitions" => CampaignSeasonProjectSerializer.DefinitionsFileName,
                "generation" => CampaignSeasonProjectSerializer.GenerationFileName,
                "layer" => CampaignSeasonProjectSerializer.LayerFileName,
                _ => throw new InvalidOperationException(),
            };
            await File.WriteAllTextAsync(Path.Combine(temporary.Path, fileName), "partial");
        }

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignSeasonProjectSerializer.LoadAsync(CreateDefinition(), temporary.Path));
    }

    [Fact]
    public async Task Save_WithoutRecipeRemovesStaleGenerationButKeepsDenseAuthority()
    {
        using var temporary = new TemporaryDirectory();
        var map = new CampaignSeasonMap(CreateDefinition());
        await CampaignSeasonProjectSerializer.SaveAsync(
            map,
            CampaignSeasonGenerationSettings.DefaultPriority,
            new CampaignSeasonSavedGeneration(
                new CampaignSeasonGenerationSettings(9),
                new string('3', 64),
                new string('4', 64)),
            temporary.Path);
        Assert.True(File.Exists(Path.Combine(
            temporary.Path,
            CampaignSeasonProjectSerializer.GenerationFileName)));

        await CampaignSeasonProjectSerializer.SaveAsync(map, temporary.Path);
        var loaded = await CampaignSeasonProjectSerializer.LoadAsync(map.Definition, temporary.Path);

        Assert.False(File.Exists(Path.Combine(
            temporary.Path,
            CampaignSeasonProjectSerializer.GenerationFileName)));
        Assert.True(File.Exists(Path.Combine(
            temporary.Path,
            CampaignSeasonProjectSerializer.DefinitionsFileName)));
        Assert.True(File.Exists(Path.Combine(
            temporary.Path,
            CampaignSeasonProjectSerializer.LayerFileName)));
        Assert.Null(loaded.SavedGeneration);
        Assert.False(loaded.WasImplicitCompatibility);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("null-priority")]
    [InlineData("null-priority-entry")]
    [InlineData("noncanonical-catalog")]
    [InlineData("numeric-enum")]
    public async Task Load_RejectsStrictOrSemanticallyInvalidDefinitions(string corruption)
    {
        using var temporary = new TemporaryDirectory();
        await SaveBasicAsync(temporary.Path);
        var path = Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.DefinitionsFileName);
        var json = await File.ReadAllTextAsync(path);
        json = corruption switch
        {
            "version" => json.Replace("\"version\": 1", "\"version\": 2", StringComparison.Ordinal),
            "unknown" => json.Replace("\"defaultSeasonId\"", "\"unknown\": true,\n  \"defaultSeasonId\"", StringComparison.Ordinal),
            "duplicate" => json.Replace("\"version\": 1", "\"version\": 1,\n  \"version\": 1", StringComparison.Ordinal),
            "null-priority" => ReplaceJsonPropertyValue(json, "priorityIds", "null"),
            "null-priority-entry" => ReplaceFirstPriorityEntryWithNull(json),
            "noncanonical-catalog" => ReverseDefinitionArray(json),
            "numeric-enum" => json.Replace("\"fallback\": \"spring\"", "\"fallback\": 0", StringComparison.Ordinal),
            _ => throw new InvalidOperationException(),
        };
        await File.WriteAllTextAsync(path, json);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignSeasonProjectSerializer.LoadAsync(CreateDefinition(), temporary.Path));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("null-climate")]
    [InlineData("bad-fingerprint")]
    [InlineData("numeric-enum")]
    [InlineData("invalid-climate")]
    public async Task Load_RejectsStrictOrSemanticallyInvalidGeneration(string corruption)
    {
        using var temporary = new TemporaryDirectory();
        await SaveBasicAsync(temporary.Path, includeGeneration: true);
        var path = Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.GenerationFileName);
        var json = await File.ReadAllTextAsync(path);
        json = corruption switch
        {
            "schema" => json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal),
            "unknown" => json.Replace("\"seasonSeed\"", "\"unknown\": true,\n  \"seasonSeed\"", StringComparison.Ordinal),
            "duplicate" => json.Replace("\"seasonSeed\": 123", "\"seasonSeed\": 123,\n  \"seasonSeed\": 123", StringComparison.Ordinal),
            "null-climate" => ReplaceJsonPropertyValue(json, "climate", "null"),
            "bad-fingerprint" => json.Replace(new string('1', 64), "xyz", StringComparison.Ordinal),
            "numeric-enum" => json.Replace("\"coverageMode\": \"regional\"", "\"coverageMode\": 1", StringComparison.Ordinal),
            "invalid-climate" => json.Replace("\"windPerturbationDegrees\": 13", "\"windPerturbationDegrees\": 46", StringComparison.Ordinal),
            _ => throw new InvalidOperationException(),
        };
        await File.WriteAllTextAsync(path, json);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignSeasonProjectSerializer.LoadAsync(CreateDefinition(), temporary.Path));
    }

    [Theory]
    [InlineData("magic")]
    [InlineData("version")]
    [InlineData("stride")]
    [InlineData("width")]
    [InlineData("height")]
    [InlineData("tile-count")]
    [InlineData("fingerprint")]
    [InlineData("catalog-index")]
    [InlineData("reserved")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    public async Task Load_RejectsEverySeasonLayerCorruptionClass(string corruption)
    {
        using var temporary = new TemporaryDirectory();
        await SaveBasicAsync(temporary.Path);
        var path = Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.LayerFileName);
        var bytes = await File.ReadAllBytesAsync(path);
        switch (corruption)
        {
            case "magic":
                bytes[0] ^= 0xFF;
                break;
            case "version":
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2), 2);
                break;
            case "stride":
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10, 2), 4);
                break;
            case "width":
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12, 4), 99);
                break;
            case "height":
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16, 4), 99);
                break;
            case "tile-count":
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20, 4), 99);
                break;
            case "fingerprint":
                bytes[24] ^= 0xFF;
                break;
            case "catalog-index":
                BinaryPrimitives.WriteUInt16LittleEndian(
                    bytes.AsSpan(CampaignSeasonProjectSerializer.LayerHeaderSize, 2),
                    ushort.MaxValue);
                break;
            case "reserved":
                bytes[CampaignSeasonProjectSerializer.LayerHeaderSize + 2] = 0x80;
                break;
            case "truncated":
                Array.Resize(ref bytes, bytes.Length - 1);
                break;
            case "trailing":
                Array.Resize(ref bytes, bytes.Length + 1);
                break;
            default:
                throw new InvalidOperationException();
        }

        await File.WriteAllBytesAsync(path, bytes);
        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignSeasonProjectSerializer.LoadAsync(CreateDefinition(), temporary.Path));
    }

    [Fact]
    public async Task Save_InvalidPriorityDoesNotMutateExistingFilesOrLeaveTemporaryFiles()
    {
        using var temporary = new TemporaryDirectory();
        var path = Path.Combine(temporary.Path, CampaignSeasonProjectSerializer.DefinitionsFileName);
        await File.WriteAllTextAsync(path, "original");

        await Assert.ThrowsAsync<ArgumentException>(() => CampaignSeasonProjectSerializer.SaveAsync(
            new CampaignSeasonMap(CreateDefinition()),
            new string[] { "winter", null!, "spring" },
            savedGeneration: null,
            temporary.Path));

        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.EnumerateFiles(temporary.Path, "*.tmp"));
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

    private static CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: 15_000,
            worldHeightMeters: 10_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 20);

    private static CampaignSeasonCatalog CreateCatalog(bool customOrderReversed = false)
    {
        var builtIns = CampaignSeasonCatalog.DefaultBuiltInDefinitions.ToArray();
        var winterIndex = Array.FindIndex(
            builtIns,
            static value => value.Id == CampaignSeasonCatalog.WinterId);
        var winter = builtIns[winterIndex];
        builtIns[winterIndex] = new CampaignSeasonDefinition(
            winter.Id,
            winter.Name,
            winter.Fallback,
            "#DDEEFF",
            76,
            73,
            new CampaignSeasonRule(
                temperatureCelsius: new CampaignSeasonRange(-273.15, 8),
                terrainExcludes: [CampaignTileType.Desert]));
        var monsoon = new CampaignSeasonDefinition(
            "monsoon",
            "Monsoon",
            CampaignBuiltInSeason.Summer,
            "#467A9C",
            64,
            81,
            new CampaignSeasonRule(
                latitudeDegrees: new CampaignSeasonRange(-35, 35),
                elevationMeters: new CampaignSeasonRange(-500, 2_200),
                temperatureCelsius: new CampaignSeasonRange(14, 45),
                moisture: new CampaignSeasonRange(0.65, 1),
                seasonalIntensity: new CampaignSeasonRange(0.1, 1),
                seasonalTendency: new CampaignSeasonRange(-0.3, 1),
                seaDistanceKilometers: new CampaignSeasonRange(0, 900),
                lakeDistanceKilometers: new CampaignSeasonRange(0, 400),
                riverDistanceKilometers: new CampaignSeasonRange(0, 120),
                terrainIncludes: [CampaignTileType.Plains, CampaignTileType.Forest],
                terrainExcludes: [CampaignTileType.Desert],
                customTerrainIncludes: ["wetland"],
                customTerrainExcludes: ["salt-flat"]));
        var dry = new CampaignSeasonDefinition(
            "dry-season",
            "Dry Season",
            CampaignBuiltInSeason.Autumn,
            "#C6A15B",
            42,
            58,
            new CampaignSeasonRule(
                moisture: new CampaignSeasonRange(0, 0.4)));
        var custom = customOrderReversed
            ? new[] { monsoon, dry }
            : new[] { dry, monsoon };
        return new CampaignSeasonCatalog(custom, builtIns);
    }

    private static CampaignSeasonGenerationSettings CreateSettings(
        IReadOnlyList<string> priority) =>
        new(
            seasonSeed: 123,
            seedDerivedFromTerrain: false,
            coverageMode: CampaignSeasonCoverageMode.Regional,
            regionalCenterLatitudeDegrees: 12.5,
            axialTiltDegrees: 27.25,
            climate: new CampaignSeasonClimateSettings(
                7.1,
                0.81,
                710,
                0.31,
                210,
                0.09,
                0.61,
                3.2,
                0.37,
                760,
                0.21,
                250,
                0.13,
                95,
                0.33,
                0.17,
                1_700,
                1_420,
                810,
                2_100,
                13),
            priorityIds: priority);

    private static async Task SaveBasicAsync(
        string directory,
        bool includeGeneration = false)
    {
        var catalog = CreateCatalog();
        var map = new CampaignSeasonMap(CreateDefinition(), catalog);
        var priority = new[] { "winter", "monsoon", "dry-season", "spring" };
        var saved = includeGeneration
            ? new CampaignSeasonSavedGeneration(
                CreateSettings(priority),
                new string('1', 64),
                new string('2', 64))
            : null;
        await CampaignSeasonProjectSerializer.SaveAsync(map, priority, saved, directory);
    }

    private static string ReplaceJsonPropertyValue(
        string json,
        string propertyName,
        string replacement)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        root[propertyName] = JsonNode.Parse(replacement);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ReplaceFirstPriorityEntryWithNull(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        root["priorityIds"]!.AsArray()[0] = null;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ReverseDefinitionArray(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        var definitions = root["definitions"]!.AsArray();
        var copy = definitions.Select(static node => node!.DeepClone()).Reverse().ToArray();
        definitions.Clear();
        foreach (var node in copy)
        {
            definitions.Add(node);
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void AssertCatalogEqual(
        CampaignSeasonCatalog expected,
        CampaignSeasonCatalog actual)
    {
        Assert.Equal(expected.Definitions.Count, actual.Definitions.Count);
        for (var index = 0; index < expected.Definitions.Count; index++)
        {
            var left = expected.Definitions[index];
            var right = actual.Definitions[index];
            Assert.Equal(left.Id, right.Id);
            Assert.Equal(left.Name, right.Name);
            Assert.Equal(left.Fallback, right.Fallback);
            Assert.Equal(left.ColorHex, right.ColorHex);
            Assert.Equal(left.TintStrengthPercent, right.TintStrengthPercent);
            Assert.Equal(left.EffectIntensityPercent, right.EffectIntensityPercent);
            Assert.Equal(left.Rule.LatitudeDegrees, right.Rule.LatitudeDegrees);
            Assert.Equal(left.Rule.ElevationMeters, right.Rule.ElevationMeters);
            Assert.Equal(left.Rule.TemperatureCelsius, right.Rule.TemperatureCelsius);
            Assert.Equal(left.Rule.Moisture, right.Rule.Moisture);
            Assert.Equal(left.Rule.SeasonalIntensity, right.Rule.SeasonalIntensity);
            Assert.Equal(left.Rule.SeasonalTendency, right.Rule.SeasonalTendency);
            Assert.Equal(left.Rule.SeaDistanceKilometers, right.Rule.SeaDistanceKilometers);
            Assert.Equal(left.Rule.LakeDistanceKilometers, right.Rule.LakeDistanceKilometers);
            Assert.Equal(left.Rule.RiverDistanceKilometers, right.Rule.RiverDistanceKilometers);
            Assert.Equal(left.Rule.TerrainIncludes, right.Rule.TerrainIncludes);
            Assert.Equal(left.Rule.TerrainExcludes, right.Rule.TerrainExcludes);
            Assert.Equal(left.Rule.CustomTerrainIncludes, right.Rule.CustomTerrainIncludes);
            Assert.Equal(left.Rule.CustomTerrainExcludes, right.Rule.CustomTerrainExcludes);
        }
    }

    private static void AssertSettingsEqual(
        CampaignSeasonGenerationSettings expected,
        CampaignSeasonGenerationSettings actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.SeasonSeed, actual.SeasonSeed);
        Assert.Equal(expected.SeedDerivedFromTerrain, actual.SeedDerivedFromTerrain);
        Assert.Equal(expected.CoverageMode, actual.CoverageMode);
        Assert.Equal(expected.RegionalCenterLatitudeDegrees, actual.RegionalCenterLatitudeDegrees);
        Assert.Equal(expected.AxialTiltDegrees, actual.AxialTiltDegrees);
        Assert.Equal(expected.PriorityIds, actual.PriorityIds);
        Assert.Equal(expected.Climate.LapseRateCelsiusPerKilometer, actual.Climate.LapseRateCelsiusPerKilometer);
        Assert.Equal(expected.Climate.SeaMaritimeStrength, actual.Climate.SeaMaritimeStrength);
        Assert.Equal(expected.Climate.SeaMaritimeRadiusKilometers, actual.Climate.SeaMaritimeRadiusKilometers);
        Assert.Equal(expected.Climate.LakeMaritimeStrength, actual.Climate.LakeMaritimeStrength);
        Assert.Equal(expected.Climate.LakeMaritimeRadiusKilometers, actual.Climate.LakeMaritimeRadiusKilometers);
        Assert.Equal(expected.Climate.MaximumPhaseLagOrbitFraction, actual.Climate.MaximumPhaseLagOrbitFraction);
        Assert.Equal(expected.Climate.MaritimeAmplitudeReduction, actual.Climate.MaritimeAmplitudeReduction);
        Assert.Equal(expected.Climate.TemperatureNoiseCelsius, actual.Climate.TemperatureNoiseCelsius);
        Assert.Equal(expected.Climate.SeaMoistureStrength, actual.Climate.SeaMoistureStrength);
        Assert.Equal(expected.Climate.SeaMoistureRadiusKilometers, actual.Climate.SeaMoistureRadiusKilometers);
        Assert.Equal(expected.Climate.LakeMoistureStrength, actual.Climate.LakeMoistureStrength);
        Assert.Equal(expected.Climate.LakeMoistureRadiusKilometers, actual.Climate.LakeMoistureRadiusKilometers);
        Assert.Equal(expected.Climate.RiverMoistureStrength, actual.Climate.RiverMoistureStrength);
        Assert.Equal(expected.Climate.RiverMoistureRadiusKilometers, actual.Climate.RiverMoistureRadiusKilometers);
        Assert.Equal(expected.Climate.RainShadowStrength, actual.Climate.RainShadowStrength);
        Assert.Equal(expected.Climate.MoistureNoiseStrength, actual.Climate.MoistureNoiseStrength);
        Assert.Equal(expected.Climate.TemperatureNoiseWavelengthKilometers, actual.Climate.TemperatureNoiseWavelengthKilometers);
        Assert.Equal(expected.Climate.MoistureNoiseWavelengthKilometers, actual.Climate.MoistureNoiseWavelengthKilometers);
        Assert.Equal(expected.Climate.RainShadowFetchKilometers, actual.Climate.RainShadowFetchKilometers);
        Assert.Equal(expected.Climate.RainShadowReliefMeters, actual.Climate.RainShadowReliefMeters);
        Assert.Equal(expected.Climate.WindPerturbationDegrees, actual.Climate.WindPerturbationDegrees);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"KingdomCampaignSeasonSerializationTests-{Guid.NewGuid():N}");
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
