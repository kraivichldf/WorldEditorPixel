using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Serialization;

public static class CampaignSeasonProjectSerializer
{
    public const string DefinitionsFileName = "season-definitions.json";

    public const string GenerationFileName = "season-generation.json";

    public const string LayerFileName = "season-layer.bin";

    public const int DefinitionsVersion = 1;

    public const int LayerVersion = 1;

    public const int LayerRecordStride = 3;

    public const int LayerHeaderSize = 56;

    private static readonly byte[] LayerMagic = Encoding.ASCII.GetBytes("KWSEASON");

    private static readonly string[] ManagedFileNames =
    [
        DefinitionsFileName,
        GenerationFileName,
        LayerFileName,
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static async Task SaveAsync(
        CampaignSeasonMap seasonMap,
        IEnumerable<string> priorityIds,
        CampaignSeasonSavedGeneration? savedGeneration,
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seasonMap);
        ArgumentNullException.ThrowIfNull(priorityIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        seasonMap.EnsureValid();
        var priority = priorityIds.ToArray();
        ValidatePriority(seasonMap, priority, savedGeneration);
        var capturedRevision = seasonMap.Revision;
        var desiredFiles = BuildDesiredFiles(seasonMap, priority, savedGeneration, cancellationToken);
        EnsureRevisionUnchanged(seasonMap, capturedRevision);
        var fullProjectPath = Path.GetFullPath(projectDirectory);
        Directory.CreateDirectory(fullProjectPath);

        var stagedFiles = new List<StagedFile>(desiredFiles.Count);
        try
        {
            foreach (var desiredFile in desiredFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureRevisionUnchanged(seasonMap, capturedRevision);
                var destination = Path.Combine(fullProjectPath, desiredFile.FileName);
                var temporary = destination + $".{Guid.NewGuid():N}.tmp";
                stagedFiles.Add(new StagedFile(destination, temporary));
                await File.WriteAllBytesAsync(temporary, desiredFile.Bytes, cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureRevisionUnchanged(seasonMap, capturedRevision);
            foreach (var stagedFile in stagedFiles)
            {
                File.Move(stagedFile.TemporaryPath, stagedFile.DestinationPath, overwrite: true);
            }

            var desiredNames = desiredFiles
                .Select(static file => file.FileName)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var fileName in ManagedFileNames.Reverse())
            {
                if (!desiredNames.Contains(fileName))
                {
                    File.Delete(Path.Combine(fullProjectPath, fileName));
                }
            }
        }
        finally
        {
            foreach (var stagedFile in stagedFiles)
            {
                if (File.Exists(stagedFile.TemporaryPath))
                {
                    File.Delete(stagedFile.TemporaryPath);
                }
            }
        }
    }

    public static Task SaveAsync(
        CampaignSeasonMap seasonMap,
        string projectDirectory,
        CancellationToken cancellationToken = default) =>
        SaveAsync(
            seasonMap,
            CampaignSeasonGenerationSettings.DefaultPriority,
            savedGeneration: null,
            projectDirectory,
            cancellationToken);

    public static async Task<CampaignSeasonProjectLoadResult> LoadAsync(
        CampaignWorldDefinition definition,
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        CampaignWorldDefinition.EnsureValid(definition);
        var projectDirectory = CampaignWorldProjectSerializer.GetProjectDirectory(projectPath);
        var definitionsPath = Path.Combine(projectDirectory, DefinitionsFileName);
        var generationPath = Path.Combine(projectDirectory, GenerationFileName);
        var layerPath = Path.Combine(projectDirectory, LayerFileName);
        var hasDefinitions = File.Exists(definitionsPath);
        var hasGeneration = File.Exists(generationPath);
        var hasLayer = File.Exists(layerPath);
        if (!hasDefinitions && !hasGeneration && !hasLayer)
        {
            return CreateImplicit(definition, projectDirectory);
        }

        if (!hasDefinitions || !hasLayer)
        {
            throw new WorldFormatException(
                "Season project data is incomplete. season-definitions.json and season-layer.bin must either both exist or both be absent.");
        }

        var definitions = await ReadStrictAsync<SeasonDefinitionDocument>(
            definitionsPath,
            "Season definition file",
            cancellationToken).ConfigureAwait(false);
        var (catalog, priorityIds, defaultSeasonId) = LoadCatalog(definitions, definition);
        var seasonMap = await LoadLayerAsync(
            definition,
            catalog,
            defaultSeasonId,
            layerPath,
            cancellationToken).ConfigureAwait(false);
        var savedGeneration = hasGeneration
            ? await LoadGenerationAsync(
                definition,
                catalog,
                priorityIds,
                generationPath,
                cancellationToken).ConfigureAwait(false)
            : null;
        return new CampaignSeasonProjectLoadResult(
            seasonMap,
            priorityIds,
            savedGeneration,
            projectDirectory,
            wasImplicitCompatibility: false);
    }

    public static CampaignSeasonProjectLoadResult CreateImplicit(
        CampaignWorldDefinition definition,
        string sourceProjectDirectory)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceProjectDirectory);
        var catalog = new CampaignSeasonCatalog();
        return new CampaignSeasonProjectLoadResult(
            new CampaignSeasonMap(definition, catalog, CampaignSeasonCatalog.SpringId),
            CampaignSeasonGenerationSettings.DefaultPriority,
            savedGeneration: null,
            Path.GetFullPath(sourceProjectDirectory),
            wasImplicitCompatibility: true);
    }

    private static IReadOnlyList<DesiredFile> BuildDesiredFiles(
        CampaignSeasonMap seasonMap,
        IReadOnlyList<string> priorityIds,
        CampaignSeasonSavedGeneration? savedGeneration,
        CancellationToken cancellationToken)
    {
        var definitions = new SeasonDefinitionDocument
        {
            Version = DefinitionsVersion,
            DefaultSeasonId = seasonMap.DefaultSeasonId,
            PriorityIds = priorityIds.ToArray(),
            Definitions = seasonMap.Catalog.Definitions
                .Select(definition => ToRecord(definition, seasonMap.Catalog.IsBuiltIn(definition.Id)))
                .ToArray(),
        };
        var desired = new List<DesiredFile>(capacity: 3)
        {
            CreateJsonFile(DefinitionsFileName, definitions),
        };
        if (savedGeneration is not null)
        {
            desired.Add(CreateJsonFile(
                GenerationFileName,
                ToGenerationDocument(savedGeneration)));
        }

        desired.Add(new DesiredFile(
            LayerFileName,
            BuildLayerBytes(seasonMap, cancellationToken)));
        return desired;
    }

    private static SeasonDefinitionRecord ToRecord(
        CampaignSeasonDefinition definition,
        bool builtIn) =>
        new()
        {
            Id = definition.Id,
            Name = definition.Name,
            BuiltIn = builtIn,
            Fallback = definition.Fallback,
            Color = definition.ColorHex,
            TintStrengthPercent = definition.TintStrengthPercent,
            EffectIntensityPercent = definition.EffectIntensityPercent,
            Rule = new SeasonRuleRecord
            {
                LatitudeDegrees = ToRecord(definition.Rule.LatitudeDegrees),
                ElevationMeters = ToRecord(definition.Rule.ElevationMeters),
                TemperatureCelsius = ToRecord(definition.Rule.TemperatureCelsius),
                Moisture = ToRecord(definition.Rule.Moisture),
                SeasonalIntensity = ToRecord(definition.Rule.SeasonalIntensity),
                SeasonalTendency = ToRecord(definition.Rule.SeasonalTendency),
                SeaDistanceKilometers = ToRecord(definition.Rule.SeaDistanceKilometers),
                LakeDistanceKilometers = ToRecord(definition.Rule.LakeDistanceKilometers),
                RiverDistanceKilometers = ToRecord(definition.Rule.RiverDistanceKilometers),
                TerrainIncludes = definition.Rule.TerrainIncludes.ToArray(),
                TerrainExcludes = definition.Rule.TerrainExcludes.ToArray(),
                CustomTerrainIncludes = definition.Rule.CustomTerrainIncludes.ToArray(),
                CustomTerrainExcludes = definition.Rule.CustomTerrainExcludes.ToArray(),
            },
        };

    private static SeasonRangeRecord? ToRecord(CampaignSeasonRange? range) =>
        range is { } value
            ? new SeasonRangeRecord { Minimum = value.Minimum, Maximum = value.Maximum }
            : null;

    private static SeasonGenerationDocument ToGenerationDocument(
        CampaignSeasonSavedGeneration saved) =>
        new()
        {
            SchemaVersion = saved.Settings.SchemaVersion,
            SeasonSeed = saved.Settings.SeasonSeed,
            SeedDerivedFromTerrain = saved.Settings.SeedDerivedFromTerrain,
            CoverageMode = saved.Settings.CoverageMode,
            RegionalCenterLatitudeDegrees = saved.Settings.RegionalCenterLatitudeDegrees,
            AxialTiltDegrees = saved.Settings.AxialTiltDegrees,
            SourceTerrainFingerprint = saved.SourceTerrainFingerprint,
            InputFingerprint = saved.InputFingerprint,
            Climate = ToRecord(saved.Settings.Climate),
        };

    private static SeasonClimateRecord ToRecord(CampaignSeasonClimateSettings climate) =>
        new()
        {
            LapseRateCelsiusPerKilometer = climate.LapseRateCelsiusPerKilometer,
            SeaMaritimeStrength = climate.SeaMaritimeStrength,
            SeaMaritimeRadiusKilometers = climate.SeaMaritimeRadiusKilometers,
            LakeMaritimeStrength = climate.LakeMaritimeStrength,
            LakeMaritimeRadiusKilometers = climate.LakeMaritimeRadiusKilometers,
            MaximumPhaseLagOrbitFraction = climate.MaximumPhaseLagOrbitFraction,
            MaritimeAmplitudeReduction = climate.MaritimeAmplitudeReduction,
            TemperatureNoiseCelsius = climate.TemperatureNoiseCelsius,
            SeaMoistureStrength = climate.SeaMoistureStrength,
            SeaMoistureRadiusKilometers = climate.SeaMoistureRadiusKilometers,
            LakeMoistureStrength = climate.LakeMoistureStrength,
            LakeMoistureRadiusKilometers = climate.LakeMoistureRadiusKilometers,
            RiverMoistureStrength = climate.RiverMoistureStrength,
            RiverMoistureRadiusKilometers = climate.RiverMoistureRadiusKilometers,
            RainShadowStrength = climate.RainShadowStrength,
            MoistureNoiseStrength = climate.MoistureNoiseStrength,
            TemperatureNoiseWavelengthKilometers = climate.TemperatureNoiseWavelengthKilometers,
            MoistureNoiseWavelengthKilometers = climate.MoistureNoiseWavelengthKilometers,
            RainShadowFetchKilometers = climate.RainShadowFetchKilometers,
            RainShadowReliefMeters = climate.RainShadowReliefMeters,
            WindPerturbationDegrees = climate.WindPerturbationDegrees,
        };

    private static byte[] BuildLayerBytes(
        CampaignSeasonMap seasonMap,
        CancellationToken cancellationToken)
    {
        var definition = seasonMap.Definition;
        var length = checked(LayerHeaderSize + (seasonMap.TileCount * LayerRecordStride));
        var bytes = new byte[length];
        LayerMagic.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2), LayerVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10, 2), LayerRecordStride);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12, 4), definition.TilesX);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16, 4), definition.TilesY);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20, 4), seasonMap.TileCount);
        Convert.FromHexString(CampaignSeasonSeed.GetCatalogIdFingerprint(seasonMap.Catalog))
            .CopyTo(bytes, 24);
        var entries = seasonMap.GetAllTiles();
        for (var index = 0; index < entries.Count; index++)
        {
            if ((index & 16_383) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var offset = LayerHeaderSize + (index * LayerRecordStride);
            BinaryPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(offset, 2),
                seasonMap.Catalog.GetIndex(entries[index].Tile.SeasonId));
            bytes[offset + 2] = entries[index].Tile.Locked ? (byte)1 : (byte)0;
        }

        return bytes;
    }

    private static (CampaignSeasonCatalog Catalog, string[] PriorityIds, string DefaultSeasonId)
        LoadCatalog(SeasonDefinitionDocument document, CampaignWorldDefinition definition)
    {
        if (document.Version != DefinitionsVersion)
        {
            throw new WorldFormatException(
                $"Season definition file version {document.Version} is unsupported; expected {DefinitionsVersion}.");
        }

        if (document.Definitions is null || document.PriorityIds is null)
        {
            throw new WorldFormatException(
                "Season definition file requires non-null definitions and priorityIds lists.");
        }

        try
        {
            var definitions = document.Definitions.Select(ToDefinition).ToArray();
            var builtIns = definitions
                .Where(static item => item.BuiltIn)
                .Select(static item => item.Definition)
                .ToArray();
            var custom = definitions
                .Where(static item => !item.BuiltIn)
                .Select(static item => item.Definition)
                .ToArray();
            var catalog = new CampaignSeasonCatalog(custom, builtIns);
            var canonicalIds = catalog.Definitions.Select(static value => value.Id).ToArray();
            var storedIds = definitions.Select(static value => value.Definition.Id).ToArray();
            if (!storedIds.SequenceEqual(canonicalIds, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Season definitions are not stored in canonical built-in/custom catalog order.");
            }

            if (!catalog.Contains(document.DefaultSeasonId))
            {
                throw new ArgumentException(
                    $"Default season '{document.DefaultSeasonId}' is not present in the catalog.");
            }

            var priorityIds = document.PriorityIds.ToArray();
            new CampaignSeasonGenerationSettings(0, priorityIds: priorityIds)
                .EnsureValid(catalog, definition);
            return (catalog, priorityIds, document.DefaultSeasonId);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NullReferenceException)
        {
            throw new WorldFormatException(
                $"Season definition file is invalid: {exception.Message}",
                exception);
        }
    }

    private static (CampaignSeasonDefinition Definition, bool BuiltIn) ToDefinition(
        SeasonDefinitionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Rule is null)
        {
            throw new ArgumentException($"Season definition '{record.Id}' has a null rule.");
        }

        var rule = new CampaignSeasonRule(
            ToRange(record.Rule.LatitudeDegrees),
            ToRange(record.Rule.ElevationMeters),
            ToRange(record.Rule.TemperatureCelsius),
            ToRange(record.Rule.Moisture),
            ToRange(record.Rule.SeasonalIntensity),
            ToRange(record.Rule.SeasonalTendency),
            ToRange(record.Rule.SeaDistanceKilometers),
            ToRange(record.Rule.LakeDistanceKilometers),
            ToRange(record.Rule.RiverDistanceKilometers),
            RequireArray(record.Rule.TerrainIncludes, "terrainIncludes"),
            RequireArray(record.Rule.TerrainExcludes, "terrainExcludes"),
            RequireArray(record.Rule.CustomTerrainIncludes, "customTerrainIncludes"),
            RequireArray(record.Rule.CustomTerrainExcludes, "customTerrainExcludes"));
        return (new CampaignSeasonDefinition(
            record.Id,
            record.Name,
            record.Fallback,
            record.Color,
            record.TintStrengthPercent,
            record.EffectIntensityPercent,
            rule), record.BuiltIn);
    }

    private static CampaignSeasonRange? ToRange(SeasonRangeRecord? range) =>
        range is null ? null : new CampaignSeasonRange(range.Minimum, range.Maximum);

    private static T[] RequireArray<T>(T[]? values, string name) =>
        values ?? throw new ArgumentException($"Season rule {name} cannot be null.");

    private static async Task<CampaignSeasonMap> LoadLayerAsync(
        CampaignWorldDefinition definition,
        CampaignSeasonCatalog catalog,
        string defaultSeasonId,
        string path,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new WorldFormatException($"Season layer file is invalid: {exception.Message}", exception);
        }

        if (bytes.Length < LayerHeaderSize || !bytes.AsSpan(0, 8).SequenceEqual(LayerMagic))
        {
            throw new WorldFormatException("Season layer file has an invalid or truncated KWSEASON header.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
        var stride = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10, 2));
        var width = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12, 4));
        var height = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16, 4));
        var tileCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20, 4));
        if (version != LayerVersion || stride != LayerRecordStride)
        {
            throw new WorldFormatException(
                $"Season layer version/stride {version}/{stride} is unsupported; expected {LayerVersion}/{LayerRecordStride}.");
        }

        if (width != definition.TilesX || height != definition.TilesY || tileCount != definition.TileCount)
        {
            throw new WorldFormatException(
                "Season layer dimensions or tile count do not match the campaign world definition.");
        }

        var expectedLength = checked(LayerHeaderSize + (tileCount * LayerRecordStride));
        if (bytes.Length != expectedLength)
        {
            throw new WorldFormatException(
                $"Season layer length is {bytes.Length:N0} bytes; expected exactly {expectedLength:N0}.");
        }

        var expectedFingerprint = Convert.FromHexString(
            CampaignSeasonSeed.GetCatalogIdFingerprint(catalog));
        if (!bytes.AsSpan(24, 32).SequenceEqual(expectedFingerprint))
        {
            throw new WorldFormatException(
                "Season layer catalog fingerprint does not match season-definitions.json.");
        }

        var tiles = new CampaignSeasonTile[tileCount];
        for (var index = 0; index < tileCount; index++)
        {
            if ((index & 16_383) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var offset = LayerHeaderSize + (index * LayerRecordStride);
            var catalogIndex = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
            var flags = bytes[offset + 2];
            if (catalogIndex >= catalog.Definitions.Count)
            {
                throw new WorldFormatException(
                    $"Season layer tile {index:N0} references catalog index {catalogIndex}, outside the catalog.");
            }

            if ((flags & 0xFE) != 0)
            {
                throw new WorldFormatException(
                    $"Season layer tile {index:N0} has non-zero reserved flag bits 0x{flags:X2}.");
            }

            tiles[index] = new CampaignSeasonTile(
                catalog.GetByIndex(catalogIndex).Id,
                Locked: (flags & 1) != 0);
        }

        return CampaignSeasonMap.CreateSnapshot(definition, catalog, defaultSeasonId, tiles);
    }

    private static async Task<CampaignSeasonSavedGeneration> LoadGenerationAsync(
        CampaignWorldDefinition definition,
        CampaignSeasonCatalog catalog,
        IReadOnlyList<string> priorityIds,
        string path,
        CancellationToken cancellationToken)
    {
        var document = await ReadStrictAsync<SeasonGenerationDocument>(
            path,
            "Season generation file",
            cancellationToken).ConfigureAwait(false);
        if (document.SchemaVersion != CampaignSeasonGenerationSettings.CurrentSchemaVersion)
        {
            throw new WorldFormatException(
                $"Season generation schema version {document.SchemaVersion} is unsupported; expected " +
                $"{CampaignSeasonGenerationSettings.CurrentSchemaVersion}.");
        }

        if (document.Climate is null)
        {
            throw new WorldFormatException("Season generation file has a null climate object.");
        }

        try
        {
            var climate = new CampaignSeasonClimateSettings(
                document.Climate.LapseRateCelsiusPerKilometer,
                document.Climate.SeaMaritimeStrength,
                document.Climate.SeaMaritimeRadiusKilometers,
                document.Climate.LakeMaritimeStrength,
                document.Climate.LakeMaritimeRadiusKilometers,
                document.Climate.MaximumPhaseLagOrbitFraction,
                document.Climate.MaritimeAmplitudeReduction,
                document.Climate.TemperatureNoiseCelsius,
                document.Climate.SeaMoistureStrength,
                document.Climate.SeaMoistureRadiusKilometers,
                document.Climate.LakeMoistureStrength,
                document.Climate.LakeMoistureRadiusKilometers,
                document.Climate.RiverMoistureStrength,
                document.Climate.RiverMoistureRadiusKilometers,
                document.Climate.RainShadowStrength,
                document.Climate.MoistureNoiseStrength,
                document.Climate.TemperatureNoiseWavelengthKilometers,
                document.Climate.MoistureNoiseWavelengthKilometers,
                document.Climate.RainShadowFetchKilometers,
                document.Climate.RainShadowReliefMeters,
                document.Climate.WindPerturbationDegrees);
            var settings = new CampaignSeasonGenerationSettings(
                document.SeasonSeed,
                document.SeedDerivedFromTerrain,
                document.CoverageMode,
                document.RegionalCenterLatitudeDegrees,
                document.AxialTiltDegrees,
                climate,
                priorityIds,
                document.SchemaVersion);
            settings.EnsureValid(catalog, definition);
            return new CampaignSeasonSavedGeneration(
                settings,
                document.SourceTerrainFingerprint,
                document.InputFingerprint);
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException)
        {
            throw new WorldFormatException(
                $"Season generation file is invalid: {exception.Message}",
                exception);
        }
    }

    private static void ValidatePriority(
        CampaignSeasonMap seasonMap,
        IReadOnlyList<string> priorityIds,
        CampaignSeasonSavedGeneration? savedGeneration)
    {
        new CampaignSeasonGenerationSettings(0, priorityIds: priorityIds)
            .EnsureValid(seasonMap.Catalog, seasonMap.Definition);
        if (savedGeneration is null)
        {
            return;
        }

        savedGeneration.Settings.EnsureValid(seasonMap.Catalog, seasonMap.Definition);
        if (!priorityIds.SequenceEqual(savedGeneration.Settings.PriorityIds, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Saved season generation settings must use the same priority stored in season-definitions.json.",
                nameof(savedGeneration));
        }
    }

    private static DesiredFile CreateJsonFile<T>(string fileName, T document)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        ValidateCanonicalBytes<T>(bytes, fileName);
        return new DesiredFile(fileName, bytes);
    }

    private static async Task<T> ReadStrictAsync<T>(
        string path,
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            ValidateNoDuplicateProperties(bytes);
            return JsonSerializer.Deserialize<T>(bytes, JsonOptions)
                ?? throw new WorldFormatException($"{description} is empty.");
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or NotSupportedException)
        {
            throw new WorldFormatException($"{description} is invalid: {exception.Message}", exception);
        }
    }

    private static void ValidateCanonicalBytes<T>(byte[] bytes, string fileName)
    {
        try
        {
            ValidateNoDuplicateProperties(bytes);
            if (JsonSerializer.Deserialize<T>(bytes, JsonOptions) is null)
            {
                throw new InvalidOperationException(
                    $"Canonical {fileName} serialization produced no document.");
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Canonical {fileName} serialization failed validation: {exception.Message}",
                exception);
        }
    }

    private static void ValidateNoDuplicateProperties(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        ValidateNoDuplicateProperties(document.RootElement, "$");
    }

    private static void ValidateNoDuplicateProperties(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        throw new JsonException($"Duplicate property '{property.Name}' at {path}.");
                    }

                    ValidateNoDuplicateProperties(property.Value, $"{path}.{property.Name}");
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    ValidateNoDuplicateProperties(item, $"{path}[{index}]");
                    index++;
                }

                break;
        }
    }

    private static void EnsureRevisionUnchanged(CampaignSeasonMap map, long expectedRevision)
    {
        if (map.Revision != expectedRevision)
        {
            throw new InvalidOperationException(
                "The season layer changed while the project sidecars were being saved.");
        }
    }

    private sealed record DesiredFile(string FileName, byte[] Bytes);

    private sealed record StagedFile(string DestinationPath, string TemporaryPath);

    private sealed record SeasonDefinitionDocument
    {
        [JsonRequired]
        public int Version { get; init; }

        [JsonRequired]
        public string DefaultSeasonId { get; init; } = string.Empty;

        [JsonRequired]
        public string[]? PriorityIds { get; init; }

        [JsonRequired]
        public SeasonDefinitionRecord[]? Definitions { get; init; }
    }

    private sealed record SeasonDefinitionRecord
    {
        [JsonRequired]
        public string Id { get; init; } = string.Empty;

        [JsonRequired]
        public string Name { get; init; } = string.Empty;

        [JsonRequired]
        public bool BuiltIn { get; init; }

        [JsonRequired]
        public CampaignBuiltInSeason Fallback { get; init; }

        [JsonRequired]
        public string Color { get; init; } = string.Empty;

        [JsonRequired]
        public int TintStrengthPercent { get; init; }

        [JsonRequired]
        public int EffectIntensityPercent { get; init; }

        [JsonRequired]
        public SeasonRuleRecord? Rule { get; init; }
    }

    private sealed record SeasonRuleRecord
    {
        [JsonRequired]
        public SeasonRangeRecord? LatitudeDegrees { get; init; }

        [JsonRequired]
        public SeasonRangeRecord? ElevationMeters { get; init; }

        [JsonRequired]
        public SeasonRangeRecord? TemperatureCelsius { get; init; }

        [JsonRequired]
        public SeasonRangeRecord? Moisture { get; init; }

        [JsonRequired]
        public SeasonRangeRecord? SeasonalIntensity { get; init; }

        [JsonRequired]
        public SeasonRangeRecord? SeasonalTendency { get; init; }

        [JsonRequired]
        public SeasonRangeRecord? SeaDistanceKilometers { get; init; }

        [JsonRequired]
        public SeasonRangeRecord? LakeDistanceKilometers { get; init; }

        [JsonRequired]
        public SeasonRangeRecord? RiverDistanceKilometers { get; init; }

        [JsonRequired]
        public CampaignTileType[]? TerrainIncludes { get; init; }

        [JsonRequired]
        public CampaignTileType[]? TerrainExcludes { get; init; }

        [JsonRequired]
        public string[]? CustomTerrainIncludes { get; init; }

        [JsonRequired]
        public string[]? CustomTerrainExcludes { get; init; }
    }

    private sealed record SeasonRangeRecord
    {
        [JsonRequired]
        public double Minimum { get; init; }

        [JsonRequired]
        public double Maximum { get; init; }
    }

    private sealed record SeasonGenerationDocument
    {
        [JsonRequired]
        public int SchemaVersion { get; init; }

        [JsonRequired]
        public int SeasonSeed { get; init; }

        [JsonRequired]
        public bool SeedDerivedFromTerrain { get; init; }

        [JsonRequired]
        public CampaignSeasonCoverageMode CoverageMode { get; init; }

        [JsonRequired]
        public double? RegionalCenterLatitudeDegrees { get; init; }

        [JsonRequired]
        public double AxialTiltDegrees { get; init; }

        [JsonRequired]
        public string SourceTerrainFingerprint { get; init; } = string.Empty;

        [JsonRequired]
        public string InputFingerprint { get; init; } = string.Empty;

        [JsonRequired]
        public SeasonClimateRecord? Climate { get; init; }
    }

    private sealed record SeasonClimateRecord
    {
        [JsonRequired] public double LapseRateCelsiusPerKilometer { get; init; }
        [JsonRequired] public double SeaMaritimeStrength { get; init; }
        [JsonRequired] public double SeaMaritimeRadiusKilometers { get; init; }
        [JsonRequired] public double LakeMaritimeStrength { get; init; }
        [JsonRequired] public double LakeMaritimeRadiusKilometers { get; init; }
        [JsonRequired] public double MaximumPhaseLagOrbitFraction { get; init; }
        [JsonRequired] public double MaritimeAmplitudeReduction { get; init; }
        [JsonRequired] public double TemperatureNoiseCelsius { get; init; }
        [JsonRequired] public double SeaMoistureStrength { get; init; }
        [JsonRequired] public double SeaMoistureRadiusKilometers { get; init; }
        [JsonRequired] public double LakeMoistureStrength { get; init; }
        [JsonRequired] public double LakeMoistureRadiusKilometers { get; init; }
        [JsonRequired] public double RiverMoistureStrength { get; init; }
        [JsonRequired] public double RiverMoistureRadiusKilometers { get; init; }
        [JsonRequired] public double RainShadowStrength { get; init; }
        [JsonRequired] public double MoistureNoiseStrength { get; init; }
        [JsonRequired] public double TemperatureNoiseWavelengthKilometers { get; init; }
        [JsonRequired] public double MoistureNoiseWavelengthKilometers { get; init; }
        [JsonRequired] public double RainShadowFetchKilometers { get; init; }
        [JsonRequired] public double RainShadowReliefMeters { get; init; }
        [JsonRequired] public double WindPerturbationDegrees { get; init; }
    }
}
