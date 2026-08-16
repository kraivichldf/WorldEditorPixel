using System.Text.Json;
using System.Text.Json.Serialization;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Serialization;

public static class CampaignResourceProjectSerializer
{
    public const string DefinitionsFileName = "resource-definitions.json";

    public const string GenerationFileName = "resource-generation.json";

    public const string TilesFileName = "resource-tiles.json";

    private const int MinimumDefinitionsVersion = 1;

    private const int DefinitionsVersion = 3;

    private const int TilesVersion = 1;

    private static readonly string[] OptionalFileNames =
    [
        DefinitionsFileName,
        GenerationFileName,
        TilesFileName,
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
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? generationSettings,
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceMap);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        CampaignWorldDefinition.EnsureValid(resourceMap.Definition);
        resourceMap.EnsureValid();
        generationSettings?.EnsureValid(resourceMap.Catalog);

        var desiredFiles = BuildDesiredFiles(resourceMap, generationSettings);
        var fullProjectPath = Path.GetFullPath(projectDirectory);
        Directory.CreateDirectory(fullProjectPath);

        var stagedFiles = new List<StagedFile>(desiredFiles.Count);
        try
        {
            foreach (var desiredFile in desiredFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.Combine(fullProjectPath, desiredFile.FileName);
                var temporary = destination + $".{Guid.NewGuid():N}.tmp";
                stagedFiles.Add(new StagedFile(destination, temporary));
                await File.WriteAllBytesAsync(temporary, desiredFile.Bytes, cancellationToken)
                    .ConfigureAwait(false);
            }

            // From this boundary onward the per-file commits are intentionally non-cancellable.
            // Cancellation can therefore never interrupt one atomic replacement halfway through.
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var stagedFile in stagedFiles)
            {
                File.Move(stagedFile.TemporaryPath, stagedFile.DestinationPath, overwrite: true);
            }

            var desiredNames = desiredFiles
                .Select(static file => file.FileName)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var staleFileName in OptionalFileNames.Reverse())
            {
                if (!desiredNames.Contains(staleFileName))
                {
                    File.Delete(Path.Combine(fullProjectPath, staleFileName));
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

    public static async Task<CampaignResourceProjectLoadResult> LoadAsync(
        CampaignWorldDefinition definition,
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        CampaignWorldDefinition.EnsureValid(definition);
        var projectDirectory = CampaignWorldProjectSerializer.GetProjectDirectory(projectPath);

        var catalog = await LoadCatalogAsync(projectDirectory, cancellationToken).ConfigureAwait(false);
        var generationSettings = await LoadGenerationSettingsAsync(
            catalog,
            projectDirectory,
            cancellationToken).ConfigureAwait(false);
        var resourceMap = await LoadResourceMapAsync(
            definition,
            catalog,
            projectDirectory,
            cancellationToken).ConfigureAwait(false);

        return new CampaignResourceProjectLoadResult(resourceMap, generationSettings, projectDirectory);
    }

    private static IReadOnlyList<DesiredFile> BuildDesiredFiles(
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? generationSettings)
    {
        var desiredFiles = new List<DesiredFile>(capacity: 3);
        if (resourceMap.Catalog.CustomDefinitions.Count > 0)
        {
            var definitions = new ResourceDefinitionDocument
            {
                Version = DefinitionsVersion,
                Definitions = resourceMap.Catalog.CustomDefinitions
                    .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
                    .Select(ToRecord)
                    .ToArray(),
            };
            desiredFiles.Add(CreateDesiredFile(DefinitionsFileName, definitions));
        }

        if (generationSettings is not null)
        {
            var generation = new ResourceGenerationDocument
            {
                SchemaVersion = generationSettings.SchemaVersion,
                ResourceSeed = generationSettings.ResourceSeed,
                SeedDerivedFromWorld = generationSettings.SeedDerivedFromWorld,
                Abundance = generationSettings.Abundance,
                Climate = generationSettings.Climate,
                Geology = generationSettings.Geology,
                Overrides = generationSettings.Overrides
                    .OrderBy(static value => value.ResourceId, StringComparer.Ordinal)
                    .Select(static value => new ResourceGenerationOverrideRecord
                    {
                        ResourceId = value.ResourceId,
                        Enabled = value.Enabled,
                        CoveragePercent = value.CoveragePercent,
                        Richness = value.Richness,
                        RichnessBias = value.RichnessBias,
                        Concentration = value.Concentration,
                        MapPriority = value.MapPriority,
                    })
                    .ToArray(),
            };
            desiredFiles.Add(CreateDesiredFile(GenerationFileName, generation));
        }

        if (resourceMap.OccurrenceCount > 0)
        {
            var tiles = resourceMap.GetMaterializedOccurrences()
                .GroupBy(static entry => (entry.X, entry.Y))
                .OrderBy(static group => group.Key.Y)
                .ThenBy(static group => group.Key.X)
                .Select(static group => new ResourceTileRecord
                {
                    X = group.Key.X,
                    Y = group.Key.Y,
                    Resources = group
                        .OrderBy(static entry => entry.Occurrence.ResourceId, StringComparer.Ordinal)
                        .Select(static entry => new ResourceOccurrenceRecord
                        {
                            Id = entry.Occurrence.ResourceId,
                            Potential = entry.Occurrence.Potential,
                            Locked = entry.Occurrence.Locked,
                        })
                        .ToArray(),
                })
                .ToArray();
            desiredFiles.Add(CreateDesiredFile(
                TilesFileName,
                new ResourceTileDocument { Version = TilesVersion, Tiles = tiles }));
        }

        return desiredFiles;
    }

    private static DesiredFile CreateDesiredFile<T>(string fileName, T document)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        ValidateCanonicalBytes<T>(bytes, fileName);
        return new DesiredFile(fileName, bytes);
    }

    private static ResourceDefinitionRecord ToRecord(CampaignResourceDefinition definition) =>
        new()
        {
            Id = definition.Id,
            Name = definition.Name,
            Category = definition.Category,
            DistributionProfile = definition.DistributionProfile,
            Medium = definition.Medium,
            SymbolId = definition.SymbolId,
            Color = definition.ColorHex,
            MapPriority = definition.MapPriority,
            CoveragePercent = definition.CoveragePercent,
            Richness = definition.Richness,
            Concentration = definition.Concentration,
            Rules = new ResourceRuleRecord
            {
                ElevationMeters = ToRecord(definition.Rules.ElevationMeters),
                Grade = ToRecord(definition.Rules.Grade),
                WaterDistanceKilometers = ToRecord(definition.Rules.WaterDistanceKilometers),
                RegionScaleKilometers = ToRecord(definition.Rules.RegionScaleKilometers),
                PreferredTerrainTags = definition.Rules.PreferredTerrainTags
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                AvoidedTerrainTags = definition.Rules.AvoidedTerrainTags
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                ExcludedTerrainSurfaces = definition.Rules.ExcludedTerrainSurfaces.ToArray(),
                CustomTerrainIncludes = definition.Rules.CustomTerrainIncludes
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                CustomTerrainExcludes = definition.Rules.CustomTerrainExcludes
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                FieldWeights = ToWeightRecords(definition.Rules.FieldWeights),
                AssociationWeights = ToWeightRecords(definition.Rules.AssociationWeights),
            },
        };

    private static ResourceRangeRecord? ToRecord(CampaignResourceRange? range) =>
        range is { } value
            ? new ResourceRangeRecord { Minimum = value.Minimum, Maximum = value.Maximum }
            : null;

    private static ResourceWeightRecord[] ToWeightRecords(IReadOnlyDictionary<string, double> weights) =>
        weights
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new ResourceWeightRecord { Id = pair.Key, Weight = pair.Value })
            .ToArray();

    private static async Task<CampaignResourceCatalog> LoadCatalogAsync(
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(projectDirectory, DefinitionsFileName);
        if (!File.Exists(path))
        {
            return new CampaignResourceCatalog();
        }

        var document = await ReadStrictAsync<ResourceDefinitionDocument>(
            path,
            "Resource definition file",
            cancellationToken).ConfigureAwait(false);
        if (document.Version is < MinimumDefinitionsVersion or > DefinitionsVersion)
        {
            throw new WorldFormatException(
                $"Resource definition file version {document.Version} is unsupported; expected " +
                $"{MinimumDefinitionsVersion} through {DefinitionsVersion}.");
        }

        if (document.Definitions is null)
        {
            throw new WorldFormatException("Resource definition file has a null definitions list.");
        }

        try
        {
            var definitions = document.Definitions
                .Select(record => ToDefinition(record, document.Version))
                .ToArray();
            return new CampaignResourceCatalog(definitions);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NullReferenceException)
        {
            throw new WorldFormatException($"Resource definition file is invalid: {exception.Message}", exception);
        }
    }

    private static CampaignResourceDefinition ToDefinition(
        ResourceDefinitionRecord record,
        int definitionsVersion)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Rules is null)
        {
            throw new ArgumentException($"Resource definition '{record.Id}' has null rules.");
        }

        var rules = new CampaignResourceRuleSet(
            record.Medium,
            ToRange(record.Rules.ElevationMeters),
            ToRange(record.Rules.Grade),
            ToRange(record.Rules.WaterDistanceKilometers),
            ToRange(record.Rules.RegionScaleKilometers),
            RequireArray(record.Rules.PreferredTerrainTags, "preferred terrain tags"),
            RequireArray(record.Rules.CustomTerrainIncludes, "custom terrain includes"),
            RequireArray(record.Rules.CustomTerrainExcludes, "custom terrain excludes"),
            ToWeightDictionary(record.Rules.FieldWeights, "field weights"),
            ToWeightDictionary(record.Rules.AssociationWeights, "association weights"),
            avoidedTerrainTags: definitionsVersion >= 2
                ? RequireArray(record.Rules.AvoidedTerrainTags, "avoided terrain tags")
                : [],
            excludedTerrainSurfaces: definitionsVersion >= 3
                ? RequireArray(record.Rules.ExcludedTerrainSurfaces, "excluded terrain surfaces")
                : []);
        return new CampaignResourceDefinition(
            record.Id,
            record.Name,
            record.Category,
            record.DistributionProfile,
            record.Medium,
            record.SymbolId,
            record.Color,
            record.MapPriority,
            record.CoveragePercent,
            record.Richness,
            record.Concentration,
            rules);
    }

    private static CampaignResourceRange? ToRange(ResourceRangeRecord? range) =>
        range is null ? null : new CampaignResourceRange(range.Minimum, range.Maximum);

    private static T[] RequireArray<T>(T[]? values, string description) =>
        values ?? throw new ArgumentException($"Resource rule {description} cannot be null.");

    private static IReadOnlyDictionary<string, double> ToWeightDictionary(
        ResourceWeightRecord[]? records,
        string description)
    {
        if (records is null)
        {
            throw new ArgumentException($"Resource rule {description} cannot be null.");
        }

        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (record is null)
            {
                throw new ArgumentException($"Resource rule {description} cannot contain null entries.");
            }

            if (!result.TryAdd(record.Id, record.Weight))
            {
                throw new ArgumentException(
                    $"Resource weight '{record.Id}' appears more than once in {description}.");
            }
        }

        return result;
    }

    private static async Task<CampaignResourceGenerationSettings?> LoadGenerationSettingsAsync(
        CampaignResourceCatalog catalog,
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(projectDirectory, GenerationFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var document = await ReadStrictAsync<ResourceGenerationDocument>(
            path,
            "Resource generation file",
            cancellationToken).ConfigureAwait(false);
        if (document.SchemaVersion != CampaignResourceGenerationSettings.CurrentSchemaVersion)
        {
            throw new WorldFormatException(
                $"Resource generation schema version {document.SchemaVersion} is unsupported; " +
                $"expected {CampaignResourceGenerationSettings.CurrentSchemaVersion}.");
        }

        if (document.Overrides is null)
        {
            throw new WorldFormatException("Resource generation file has a null overrides list.");
        }

        try
        {
            var overrides = document.Overrides.Select(static value =>
            {
                ArgumentNullException.ThrowIfNull(value);
                return new CampaignResourceGenerationOverride(
                    value.ResourceId,
                    value.Enabled,
                    value.CoveragePercent,
                    value.Richness,
                    value.RichnessBias,
                    value.Concentration,
                    value.MapPriority);
            });
            var settings = new CampaignResourceGenerationSettings(
                document.ResourceSeed,
                document.SeedDerivedFromWorld,
                document.Abundance,
                document.Climate,
                document.Geology,
                overrides,
                document.SchemaVersion);
            settings.EnsureValid(catalog);
            return settings;
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException)
        {
            throw new WorldFormatException($"Resource generation file is invalid: {exception.Message}", exception);
        }
    }

    private static async Task<CampaignResourceMap> LoadResourceMapAsync(
        CampaignWorldDefinition definition,
        CampaignResourceCatalog catalog,
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        var resourceMap = new CampaignResourceMap(definition, catalog);
        var path = Path.Combine(projectDirectory, TilesFileName);
        if (!File.Exists(path))
        {
            return resourceMap;
        }

        var document = await ReadStrictAsync<ResourceTileDocument>(
            path,
            "Resource tile file",
            cancellationToken).ConfigureAwait(false);
        if (document.Version != TilesVersion)
        {
            throw new WorldFormatException(
                $"Resource tile file version {document.Version} is unsupported; expected {TilesVersion}.");
        }

        if (document.Tiles is null)
        {
            throw new WorldFormatException("Resource tile file has a null tiles list.");
        }

        var seenTiles = new HashSet<long>();
        var mutations = new List<CampaignResourceMutation>();
        foreach (var tile in document.Tiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tile is null)
            {
                throw new WorldFormatException("Resource tile file cannot contain null tile records.");
            }

            if (!resourceMap.IsValidCoordinate(tile.X, tile.Y))
            {
                throw new WorldFormatException(
                    $"Resource tile ({tile.X}, {tile.Y}) lies outside 0..{definition.TilesX - 1}, " +
                    $"0..{definition.TilesY - 1}.");
            }

            var tileKey = ((long)tile.Y << 32) | (uint)tile.X;
            if (!seenTiles.Add(tileKey))
            {
                throw new WorldFormatException(
                    $"Resource tile ({tile.X}, {tile.Y}) is stored more than once.");
            }

            if (tile.Resources is null || tile.Resources.Length == 0)
            {
                throw new WorldFormatException(
                    $"Resource tile ({tile.X}, {tile.Y}) redundantly stores no resources.");
            }

            var seenResourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in tile.Resources)
            {
                if (record is null)
                {
                    throw new WorldFormatException(
                        $"Resource tile ({tile.X}, {tile.Y}) cannot contain null resource records.");
                }

                if (!seenResourceIds.Add(record.Id))
                {
                    throw new WorldFormatException(
                        $"Resource '{record.Id}' at ({tile.X}, {tile.Y}) is stored more than once.");
                }

                if (!catalog.Contains(record.Id))
                {
                    throw new WorldFormatException(
                        $"Resource tile ({tile.X}, {tile.Y}) references unknown resource '{record.Id}'.");
                }

                var occurrence = new CampaignResourceOccurrence(record.Id, record.Potential, record.Locked);
                try
                {
                    occurrence.EnsureValid();
                }
                catch (ArgumentException exception)
                {
                    throw new WorldFormatException(
                        $"Resource '{record.Id}' at ({tile.X}, {tile.Y}) is invalid: {exception.Message}",
                        exception);
                }

                mutations.Add(CampaignResourceMutation.Upsert(tile.X, tile.Y, occurrence));
            }
        }

        try
        {
            resourceMap.Apply(mutations);
        }
        catch (ArgumentException exception)
        {
            throw new WorldFormatException($"Resource tile file is invalid: {exception.Message}", exception);
        }

        return resourceMap;
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
                throw new InvalidOperationException($"Canonical {fileName} serialization produced no document.");
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
                {
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
                }

            case JsonValueKind.Array:
                {
                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        ValidateNoDuplicateProperties(item, $"{path}[{index}]");
                        index++;
                    }

                    break;
                }
        }
    }

    private sealed record DesiredFile(string FileName, byte[] Bytes);

    private sealed record StagedFile(string DestinationPath, string TemporaryPath);

    private sealed record ResourceDefinitionDocument
    {
        [JsonRequired]
        public int Version { get; init; }

        [JsonRequired]
        public ResourceDefinitionRecord[]? Definitions { get; init; }
    }

    private sealed record ResourceDefinitionRecord
    {
        [JsonRequired]
        public string Id { get; init; } = string.Empty;

        [JsonRequired]
        public string Name { get; init; } = string.Empty;

        [JsonRequired]
        public CampaignResourceCategory Category { get; init; }

        [JsonRequired]
        public CampaignResourceDistributionProfile DistributionProfile { get; init; }

        [JsonRequired]
        public CampaignResourceMedium Medium { get; init; }

        [JsonRequired]
        public string SymbolId { get; init; } = string.Empty;

        [JsonRequired]
        public string Color { get; init; } = string.Empty;

        [JsonRequired]
        public int MapPriority { get; init; }

        [JsonRequired]
        public int CoveragePercent { get; init; }

        [JsonRequired]
        public CampaignResourceRichness Richness { get; init; }

        [JsonRequired]
        public CampaignResourceConcentration Concentration { get; init; }

        [JsonRequired]
        public ResourceRuleRecord? Rules { get; init; }
    }

    private sealed record ResourceRuleRecord
    {
        [JsonRequired]
        public ResourceRangeRecord? ElevationMeters { get; init; }

        [JsonRequired]
        public ResourceRangeRecord? Grade { get; init; }

        [JsonRequired]
        public ResourceRangeRecord? WaterDistanceKilometers { get; init; }

        [JsonRequired]
        public ResourceRangeRecord? RegionScaleKilometers { get; init; }

        [JsonRequired]
        public string[]? PreferredTerrainTags { get; init; }

        public string[]? AvoidedTerrainTags { get; init; }

        public CampaignResourceSurfaceType[]? ExcludedTerrainSurfaces { get; init; }

        [JsonRequired]
        public string[]? CustomTerrainIncludes { get; init; }

        [JsonRequired]
        public string[]? CustomTerrainExcludes { get; init; }

        [JsonRequired]
        public ResourceWeightRecord[]? FieldWeights { get; init; }

        [JsonRequired]
        public ResourceWeightRecord[]? AssociationWeights { get; init; }
    }

    private sealed record ResourceRangeRecord
    {
        [JsonRequired]
        public double Minimum { get; init; }

        [JsonRequired]
        public double Maximum { get; init; }
    }

    private sealed record ResourceWeightRecord
    {
        [JsonRequired]
        public string Id { get; init; } = string.Empty;

        [JsonRequired]
        public double Weight { get; init; }
    }

    private sealed record ResourceGenerationDocument
    {
        [JsonRequired]
        public int SchemaVersion { get; init; }

        [JsonRequired]
        public int ResourceSeed { get; init; }

        [JsonRequired]
        public bool SeedDerivedFromWorld { get; init; }

        [JsonRequired]
        public CampaignResourceAbundance Abundance { get; init; }

        [JsonRequired]
        public CampaignResourceClimateProfile Climate { get; init; }

        [JsonRequired]
        public CampaignResourceGeologyProfile Geology { get; init; }

        [JsonRequired]
        public ResourceGenerationOverrideRecord[]? Overrides { get; init; }
    }

    private sealed record ResourceGenerationOverrideRecord
    {
        [JsonRequired]
        public string ResourceId { get; init; } = string.Empty;

        [JsonRequired]
        public bool Enabled { get; init; }

        [JsonRequired]
        public int CoveragePercent { get; init; }

        [JsonRequired]
        public CampaignResourceRichness Richness { get; init; }

        [JsonRequired]
        public int RichnessBias { get; init; }

        [JsonRequired]
        public CampaignResourceConcentration Concentration { get; init; }

        [JsonRequired]
        public int MapPriority { get; init; }
    }

    private sealed record ResourceTileDocument
    {
        [JsonRequired]
        public int Version { get; init; }

        [JsonRequired]
        public ResourceTileRecord[]? Tiles { get; init; }
    }

    private sealed record ResourceTileRecord
    {
        [JsonRequired]
        public int X { get; init; }

        [JsonRequired]
        public int Y { get; init; }

        [JsonRequired]
        public ResourceOccurrenceRecord[]? Resources { get; init; }
    }

    private sealed record ResourceOccurrenceRecord
    {
        [JsonRequired]
        public string Id { get; init; } = string.Empty;

        [JsonRequired]
        public byte Potential { get; init; }

        [JsonRequired]
        public bool Locked { get; init; }
    }
}
