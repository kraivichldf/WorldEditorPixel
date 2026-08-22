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

    public const int LayerHeaderSize = 64;

    public const int LayerIndexRecordStride = 8;

    public const int LayerOccurrenceRecordStride = 3;

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
        CampaignSeasonSavedGeneration? savedGeneration,
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seasonMap);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        seasonMap.EnsureValid();
        if (savedGeneration is not null)
        {
            savedGeneration.Settings.EnsureValid(seasonMap.Catalog, seasonMap.Definition);
            var expectedInput = CampaignSeasonGenerationFingerprint.GetInputFingerprint(
                seasonMap.Catalog,
                savedGeneration.Settings);
            if (!string.Equals(expectedInput, savedGeneration.InputFingerprint, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Saved Season generation input fingerprint does not match its catalog and settings.",
                    nameof(savedGeneration));
            }
        }

        var capturedRevision = seasonMap.Revision;
        var desiredFiles = BuildDesiredFiles(seasonMap, savedGeneration, cancellationToken);
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
        SaveAsync(seasonMap, savedGeneration: null, projectDirectory, cancellationToken);

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
        var catalog = LoadCatalog(definitions);
        var seasonMap = await LoadLayerAsync(
            definition,
            catalog,
            layerPath,
            cancellationToken).ConfigureAwait(false);
        var savedGeneration = hasGeneration
            ? await LoadGenerationAsync(
                definition,
                catalog,
                generationPath,
                cancellationToken).ConfigureAwait(false)
            : null;
        return new CampaignSeasonProjectLoadResult(
            seasonMap,
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
            new CampaignSeasonMap(definition, catalog),
            savedGeneration: null,
            Path.GetFullPath(sourceProjectDirectory),
            wasImplicitCompatibility: true);
    }

    private static IReadOnlyList<DesiredFile> BuildDesiredFiles(
        CampaignSeasonMap seasonMap,
        CampaignSeasonSavedGeneration? savedGeneration,
        CancellationToken cancellationToken)
    {
        var definitions = new SeasonDefinitionDocument
        {
            Version = DefinitionsVersion,
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
                WarmSeasonTemperatureCelsius = ToRecord(definition.Rule.WarmSeasonTemperatureCelsius),
                ColdSeasonTemperatureCelsius = ToRecord(definition.Rule.ColdSeasonTemperatureCelsius),
                AnnualTemperatureRangeCelsius = ToRecord(definition.Rule.AnnualTemperatureRangeCelsius),
                Moisture = ToRecord(definition.Rule.Moisture),
                Seasonality = ToRecord(definition.Rule.Seasonality),
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
            EnabledSeasonIds = saved.Settings.EnabledSeasonIds.ToArray(),
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
        if (seasonMap.OccurrenceCount > CampaignSeasonGenerator.MaximumCandidateOccurrenceCount)
        {
            throw new InvalidOperationException(
                $"Season persistence supports at most {CampaignSeasonGenerator.MaximumCandidateOccurrenceCount:N0} occurrences.");
        }

        var tileCount = checked((int)seasonMap.Definition.TileCount);
        var entries = seasonMap.GetMaterializedOccurrences()
            .OrderBy(entry => checked(entry.Y * seasonMap.Definition.TilesX + entry.X))
            .ThenBy(entry => seasonMap.Catalog.GetIndex(entry.Occurrence.SeasonId))
            .ToArray();
        var indexByteLength = checked(tileCount * LayerIndexRecordStride);
        var occurrenceByteLength = checked(entries.Length * LayerOccurrenceRecordStride);
        var bytes = new byte[checked(LayerHeaderSize + indexByteLength + occurrenceByteLength)];
        LayerMagic.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2), LayerVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10, 2), LayerIndexRecordStride);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12, 2), LayerOccurrenceRecordStride);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14, 2), 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16, 4), seasonMap.Definition.TilesX);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20, 4), seasonMap.Definition.TilesY);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24, 4), tileCount);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28, 4), entries.Length);
        Convert.FromHexString(CampaignSeasonSeed.GetCatalogIdFingerprint(seasonMap.Catalog))
            .CopyTo(bytes, 32);

        var entryIndex = 0;
        var occurrenceBase = LayerHeaderSize + indexByteLength;
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var first = entryIndex;
            while (entryIndex < entries.Length &&
                   checked(entries[entryIndex].Y * seasonMap.Definition.TilesX + entries[entryIndex].X) == tileIndex)
            {
                var entry = entries[entryIndex];
                var occurrenceOffset = occurrenceBase + (entryIndex * LayerOccurrenceRecordStride);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    bytes.AsSpan(occurrenceOffset, 2),
                    seasonMap.Catalog.GetIndex(entry.Occurrence.SeasonId));
                bytes[occurrenceOffset + 2] = entry.Occurrence.Locked ? (byte)1 : (byte)0;
                entryIndex++;
            }

            var indexOffset = LayerHeaderSize + (tileIndex * LayerIndexRecordStride);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(indexOffset, 4), checked((uint)first));
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(indexOffset + 4, 4),
                checked((uint)(entryIndex - first)));
        }

        return bytes;
    }

    private static CampaignSeasonCatalog LoadCatalog(SeasonDefinitionDocument document)
    {
        try
        {
            if (document.Version != DefinitionsVersion)
            {
                throw new WorldFormatException(
                    $"Season definitions version {document.Version} is unsupported; expected {DefinitionsVersion}.");
            }

            var records = RequireArray(document.Definitions, "definitions");
            var definitions = records.Select(ToDefinition).ToArray();
            var builtIns = definitions.Where(static value => value.BuiltIn)
                .Select(static value => value.Definition)
                .ToArray();
            var custom = definitions.Where(static value => !value.BuiltIn)
                .Select(static value => value.Definition)
                .ToArray();
            return new CampaignSeasonCatalog(custom, builtIns);
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new WorldFormatException($"Season definitions are invalid: {exception.Message}", exception);
        }
    }

    private static (CampaignSeasonDefinition Definition, bool BuiltIn) ToDefinition(
        SeasonDefinitionRecord record)
    {
        if (record.Rule is null)
        {
            throw new WorldFormatException($"Season definition '{record.Id}' is missing its rule.");
        }

        var rule = record.Rule;
        var definition = new CampaignSeasonDefinition(
            record.Id,
            record.Name,
            record.Fallback,
            record.Color,
            record.TintStrengthPercent,
            record.EffectIntensityPercent,
            new CampaignSeasonRule(
                latitudeDegrees: ToRange(rule.LatitudeDegrees),
                elevationMeters: ToRange(rule.ElevationMeters),
                temperatureCelsius: ToRange(rule.TemperatureCelsius),
                warmSeasonTemperatureCelsius: ToRange(rule.WarmSeasonTemperatureCelsius),
                coldSeasonTemperatureCelsius: ToRange(rule.ColdSeasonTemperatureCelsius),
                annualTemperatureRangeCelsius: ToRange(rule.AnnualTemperatureRangeCelsius),
                moisture: ToRange(rule.Moisture),
                seasonality: ToRange(rule.Seasonality),
                seaDistanceKilometers: ToRange(rule.SeaDistanceKilometers),
                lakeDistanceKilometers: ToRange(rule.LakeDistanceKilometers),
                riverDistanceKilometers: ToRange(rule.RiverDistanceKilometers),
                terrainIncludes: RequireArray(rule.TerrainIncludes, "terrainIncludes"),
                terrainExcludes: RequireArray(rule.TerrainExcludes, "terrainExcludes"),
                customTerrainIncludes: RequireArray(rule.CustomTerrainIncludes, "customTerrainIncludes"),
                customTerrainExcludes: RequireArray(rule.CustomTerrainExcludes, "customTerrainExcludes")));
        var defaultCatalog = new CampaignSeasonCatalog();
        if (defaultCatalog.IsBuiltIn(definition.Id) != record.BuiltIn)
        {
            throw new WorldFormatException(
                $"Season definition '{definition.Id}' has an incorrect built-in flag.");
        }

        return (definition, record.BuiltIn);
    }

    private static CampaignSeasonRange? ToRange(SeasonRangeRecord? range) =>
        range is null ? null : new CampaignSeasonRange(range.Minimum, range.Maximum);

    private static T[] RequireArray<T>(T[]? values, string name) =>
        values ?? throw new WorldFormatException($"Season data is missing required array '{name}'.");

    private static async Task<CampaignSeasonMap> LoadLayerAsync(
        CampaignWorldDefinition definition,
        CampaignSeasonCatalog catalog,
        string path,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new WorldFormatException($"Season layer file could not be read: {exception.Message}", exception);
        }

        if (bytes.Length < LayerHeaderSize || !bytes.AsSpan(0, 8).SequenceEqual(LayerMagic))
        {
            throw new WorldFormatException("Season layer file has an invalid or truncated KWSEASON header.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
        var indexStride = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10, 2));
        var occurrenceStride = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(12, 2));
        var reserved = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(14, 2));
        if (version != LayerVersion ||
            indexStride != LayerIndexRecordStride ||
            occurrenceStride != LayerOccurrenceRecordStride ||
            reserved != 0)
        {
            throw new WorldFormatException(
                $"Season layer version/stride {version}/{indexStride}/{occurrenceStride} is unsupported.");
        }

        var tilesX = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16, 4));
        var tilesY = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(20, 4));
        var tileCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24, 4));
        var occurrenceCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28, 4));
        if (tilesX != definition.TilesX ||
            tilesY != definition.TilesY ||
            tileCount != definition.TileCount ||
            occurrenceCount is < 0 or > CampaignSeasonGenerator.MaximumCandidateOccurrenceCount)
        {
            throw new WorldFormatException("Season layer dimensions or occurrence count do not match the campaign world.");
        }

        var expectedFingerprint = Convert.FromHexString(CampaignSeasonSeed.GetCatalogIdFingerprint(catalog));
        if (!bytes.AsSpan(32, 32).SequenceEqual(expectedFingerprint))
        {
            throw new WorldFormatException(
                "Season layer catalog fingerprint does not match season-definitions.json.");
        }

        var indexByteLength = checked(tileCount * LayerIndexRecordStride);
        var occurrenceByteLength = checked(occurrenceCount * LayerOccurrenceRecordStride);
        var expectedLength = checked(LayerHeaderSize + indexByteLength + occurrenceByteLength);
        if (bytes.Length != expectedLength)
        {
            throw new WorldFormatException(
                $"Season layer length {bytes.Length:N0} is invalid; expected {expectedLength:N0} bytes.");
        }

        var entries = new List<CampaignSeasonEntry>(occurrenceCount);
        var occurrenceBase = LayerHeaderSize + indexByteLength;
        var expectedFirst = 0u;
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var indexOffset = LayerHeaderSize + (tileIndex * LayerIndexRecordStride);
            var first = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(indexOffset, 4));
            var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(indexOffset + 4, 4));
            if (first != expectedFirst || first + count > occurrenceCount)
            {
                throw new WorldFormatException(
                    $"Season layer tile index {tileIndex} has a non-contiguous or out-of-range occurrence span.");
            }

            ushort? previousCatalogIndex = null;
            for (var local = 0u; local < count; local++)
            {
                var occurrenceIndex = checked((int)(first + local));
                var occurrenceOffset = occurrenceBase + (occurrenceIndex * LayerOccurrenceRecordStride);
                var catalogIndex = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(occurrenceOffset, 2));
                var flags = bytes[occurrenceOffset + 2];
                if (catalogIndex >= catalog.Definitions.Count || (flags & ~1) != 0)
                {
                    throw new WorldFormatException(
                        $"Season occurrence {occurrenceIndex} has an invalid catalog index or flags value.");
                }

                if (previousCatalogIndex is not null && catalogIndex <= previousCatalogIndex.Value)
                {
                    throw new WorldFormatException(
                        $"Season occurrences for tile index {tileIndex} are duplicated or not canonically ordered.");
                }

                previousCatalogIndex = catalogIndex;
                var x = tileIndex % definition.TilesX;
                var y = tileIndex / definition.TilesX;
                entries.Add(new CampaignSeasonEntry(
                    x,
                    y,
                    new CampaignSeasonOccurrence(
                        catalog.GetByIndex(catalogIndex).Id,
                        Locked: (flags & 1) != 0)));
            }

            expectedFirst = checked(first + count);
        }

        if (expectedFirst != occurrenceCount)
        {
            throw new WorldFormatException("Season layer index does not reference every occurrence record.");
        }

        var map = CampaignSeasonMap.CreateSnapshot(definition, catalog, entries);
        map.EnsureValid();
        return map;
    }

    private static async Task<CampaignSeasonSavedGeneration> LoadGenerationAsync(
        CampaignWorldDefinition definition,
        CampaignSeasonCatalog catalog,
        string path,
        CancellationToken cancellationToken)
    {
        var document = await ReadStrictAsync<SeasonGenerationDocument>(
            path,
            "Season generation file",
            cancellationToken).ConfigureAwait(false);
        try
        {
            if (document.Climate is null)
            {
                throw new WorldFormatException("Season generation file is missing climate settings.");
            }

            var climate = document.Climate;
            var settings = new CampaignSeasonGenerationSettings(
                document.SeasonSeed,
                document.SeedDerivedFromTerrain,
                document.CoverageMode,
                document.RegionalCenterLatitudeDegrees,
                document.AxialTiltDegrees,
                new CampaignSeasonClimateSettings(
                    climate.LapseRateCelsiusPerKilometer,
                    climate.SeaMaritimeStrength,
                    climate.SeaMaritimeRadiusKilometers,
                    climate.LakeMaritimeStrength,
                    climate.LakeMaritimeRadiusKilometers,
                    climate.MaritimeAmplitudeReduction,
                    climate.TemperatureNoiseCelsius,
                    climate.SeaMoistureStrength,
                    climate.SeaMoistureRadiusKilometers,
                    climate.LakeMoistureStrength,
                    climate.LakeMoistureRadiusKilometers,
                    climate.RiverMoistureStrength,
                    climate.RiverMoistureRadiusKilometers,
                    climate.RainShadowStrength,
                    climate.MoistureNoiseStrength,
                    climate.TemperatureNoiseWavelengthKilometers,
                    climate.MoistureNoiseWavelengthKilometers,
                    climate.RainShadowFetchKilometers,
                    climate.RainShadowReliefMeters,
                    climate.WindPerturbationDegrees),
                RequireArray(document.EnabledSeasonIds, "enabledSeasonIds"),
                document.SchemaVersion);
            settings.EnsureValid(catalog, definition);
            var saved = new CampaignSeasonSavedGeneration(
                settings,
                document.SourceTerrainFingerprint,
                document.InputFingerprint);
            var expectedInput = CampaignSeasonGenerationFingerprint.GetInputFingerprint(catalog, settings);
            if (!string.Equals(expectedInput, saved.InputFingerprint, StringComparison.Ordinal))
            {
                throw new WorldFormatException(
                    "Season generation input fingerprint does not match the saved catalog and settings.");
            }

            return saved;
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new WorldFormatException($"Season generation data is invalid: {exception.Message}", exception);
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
                "Season data changed while the project files were being prepared.");
        }
    }

    private sealed record DesiredFile(string FileName, byte[] Bytes);

    private sealed record StagedFile(string DestinationPath, string TemporaryPath);

    private sealed record SeasonDefinitionDocument
    {
        [JsonRequired]
        public int Version { get; init; }

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
        public SeasonRangeRecord? LatitudeDegrees { get; init; }

        public SeasonRangeRecord? ElevationMeters { get; init; }

        public SeasonRangeRecord? TemperatureCelsius { get; init; }

        public SeasonRangeRecord? WarmSeasonTemperatureCelsius { get; init; }

        public SeasonRangeRecord? ColdSeasonTemperatureCelsius { get; init; }

        public SeasonRangeRecord? AnnualTemperatureRangeCelsius { get; init; }

        public SeasonRangeRecord? Moisture { get; init; }

        public SeasonRangeRecord? Seasonality { get; init; }

        public SeasonRangeRecord? SeaDistanceKilometers { get; init; }

        public SeasonRangeRecord? LakeDistanceKilometers { get; init; }

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

        public double? RegionalCenterLatitudeDegrees { get; init; }

        [JsonRequired]
        public double AxialTiltDegrees { get; init; }

        [JsonRequired]
        public string[]? EnabledSeasonIds { get; init; }

        [JsonRequired]
        public string SourceTerrainFingerprint { get; init; } = string.Empty;

        [JsonRequired]
        public string InputFingerprint { get; init; } = string.Empty;

        [JsonRequired]
        public SeasonClimateRecord? Climate { get; init; }
    }

    private sealed record SeasonClimateRecord
    {
        [JsonRequired]
        public double LapseRateCelsiusPerKilometer { get; init; }

        [JsonRequired]
        public double SeaMaritimeStrength { get; init; }

        [JsonRequired]
        public double SeaMaritimeRadiusKilometers { get; init; }

        [JsonRequired]
        public double LakeMaritimeStrength { get; init; }

        [JsonRequired]
        public double LakeMaritimeRadiusKilometers { get; init; }

        [JsonRequired]
        public double MaritimeAmplitudeReduction { get; init; }

        [JsonRequired]
        public double TemperatureNoiseCelsius { get; init; }

        [JsonRequired]
        public double SeaMoistureStrength { get; init; }

        [JsonRequired]
        public double SeaMoistureRadiusKilometers { get; init; }

        [JsonRequired]
        public double LakeMoistureStrength { get; init; }

        [JsonRequired]
        public double LakeMoistureRadiusKilometers { get; init; }

        [JsonRequired]
        public double RiverMoistureStrength { get; init; }

        [JsonRequired]
        public double RiverMoistureRadiusKilometers { get; init; }

        [JsonRequired]
        public double RainShadowStrength { get; init; }

        [JsonRequired]
        public double MoistureNoiseStrength { get; init; }

        [JsonRequired]
        public double TemperatureNoiseWavelengthKilometers { get; init; }

        [JsonRequired]
        public double MoistureNoiseWavelengthKilometers { get; init; }

        [JsonRequired]
        public double RainShadowFetchKilometers { get; init; }

        [JsonRequired]
        public double RainShadowReliefMeters { get; init; }

        [JsonRequired]
        public double WindPerturbationDegrees { get; init; }
    }
}
