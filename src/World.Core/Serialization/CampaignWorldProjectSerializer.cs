using System.Text.Json;
using System.Text.Json.Serialization;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Terrain;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Serialization;

public static class CampaignWorldProjectSerializer
{
    public const string ManifestFileName = WorldProjectSerializer.ManifestFileName;
    public const string CampaignTileFileName = WorldProjectSerializer.CampaignTileFileName;
    public const string CustomTerrainFileName = "custom-terrain.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.Strict,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static async Task SaveAsync(
        CampaignWorld world,
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        CampaignWorldDefinition.EnsureValid(world.Definition);

        var fullProjectPath = Path.GetFullPath(projectDirectory);
        Directory.CreateDirectory(fullProjectPath);

        await SaveCustomTerrainDefinitionsAsync(
            world.Tiles.CustomTerrainDefinitions,
            fullProjectPath,
            cancellationToken).ConfigureAwait(false);

        var entries = world.Tiles.GetMaterializedTiles()
            .OrderBy(static entry => entry.Y)
            .ThenBy(static entry => entry.X)
            .Select(static entry => new CampaignTileRecord
            {
                X = entry.X,
                Y = entry.Y,
                Type = NormalizeLegacyType(entry.Data.Type),
                HeightMeters = entry.Data.HeightMeters,
                CustomTerrainId = entry.Data.CustomTerrainId,
            })
            .ToArray();
        var tileDocument = new CampaignTileDocument
        {
            Version = CampaignTileDocument.CurrentVersion,
            Tiles = entries,
        };

        var tilePath = Path.Combine(fullProjectPath, CampaignTileFileName);
        await WriteAtomicallyAsync(
            tilePath,
            JsonSerializer.SerializeToUtf8Bytes(tileDocument, JsonOptions),
            cancellationToken).ConfigureAwait(false);

        var manifestPath = Path.Combine(fullProjectPath, ManifestFileName);
        await WriteAtomicallyAsync(
            manifestPath,
            JsonSerializer.SerializeToUtf8Bytes(world.Definition, JsonOptions),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CampaignWorldLoadResult> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullPath = Path.GetFullPath(projectPath);
        var manifestPath = Directory.Exists(fullPath)
            ? Path.Combine(fullPath, ManifestFileName)
            : fullPath;
        var projectDirectory = Path.GetDirectoryName(manifestPath)
            ?? throw new WorldFormatException("World manifest has no containing directory.");

        if (!File.Exists(manifestPath))
        {
            throw new WorldFormatException($"World manifest was not found: {manifestPath}");
        }

        var version = await ReadVersionAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        if (version == WorldDefinition.CurrentVersion)
        {
            return await ConvertLegacyAsync(manifestPath, projectDirectory, cancellationToken).ConfigureAwait(false);
        }

        if (version != CampaignWorldDefinition.CurrentVersion)
        {
            throw new WorldFormatException(
                $"World format version {version} is unsupported; expected 1 or {CampaignWorldDefinition.CurrentVersion}.");
        }

        var definition = await LoadDefinitionAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        var customTerrainDefinitions = await LoadCustomTerrainDefinitionsAsync(
            projectDirectory,
            cancellationToken).ConfigureAwait(false);
        var world = new CampaignWorld(definition, customTerrainDefinitions);
        var normalizedCoastalTileCount = await LoadTilesAsync(
            world.Tiles,
            projectDirectory,
            cancellationToken).ConfigureAwait(false);
        return new CampaignWorldLoadResult(
            world,
            false,
            projectDirectory,
            normalizedCoastalTileCount);
    }

    public static string GetProjectDirectory(string projectPath) =>
        WorldProjectSerializer.GetProjectDirectory(projectPath);

    private static async Task<int> ReadVersionAsync(string manifestPath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("version", out var versionElement) ||
                !versionElement.TryGetInt32(out var version))
            {
                throw new WorldFormatException("World manifest has no valid integer version.");
            }

            return version;
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            throw new WorldFormatException($"World manifest is invalid: {exception.Message}", exception);
        }
    }

    private static async Task<CampaignWorldDefinition> LoadDefinitionAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            var definition = await JsonSerializer.DeserializeAsync<CampaignWorldDefinition>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false)
                ?? throw new WorldFormatException("World manifest is empty.");
            CampaignWorldDefinition.EnsureValid(definition);
            return definition;
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or WorldValidationException or OverflowException)
        {
            throw new WorldFormatException($"World manifest is invalid: {exception.Message}", exception);
        }
    }

    private static async Task<int> LoadTilesAsync(
        CampaignTileMap tiles,
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(projectDirectory, CampaignTileFileName);
        if (!File.Exists(path))
        {
            return 0;
        }

        CampaignTileDocument document;
        try
        {
            await using var stream = File.OpenRead(path);
            document = await JsonSerializer.DeserializeAsync<CampaignTileDocument>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false)
                ?? throw new WorldFormatException("Campaign tile file is empty.");
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or NotSupportedException)
        {
            throw new WorldFormatException($"Campaign tile file is invalid: {exception.Message}", exception);
        }

        if (document.Version != CampaignTileDocument.CurrentVersion)
        {
            throw new WorldFormatException(
                $"Campaign tile file version {document.Version} is unsupported; expected {CampaignTileDocument.CurrentVersion}.");
        }

        var seen = new HashSet<long>();
        var loadedEntries = new List<CampaignTileEntry>(document.Tiles.Length);
        var normalizedCoastalTileCount = 0;
        foreach (var tile in document.Tiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(tile.Type))
            {
                throw new WorldFormatException(
                    $"Campaign tile ({tile.X}, {tile.Y}) has unknown type '{tile.Type}'.");
            }

            if (!tiles.IsValidCoordinate(tile.X, tile.Y))
            {
                throw new WorldFormatException(
                    $"Campaign tile ({tile.X}, {tile.Y}) lies outside 0..{tiles.Definition.TilesX - 1}, " +
                    $"0..{tiles.Definition.TilesY - 1}.");
            }

            if (tile.HeightMeters < tiles.Definition.MinimumHeightMeters ||
                tile.HeightMeters > tiles.Definition.MaximumHeightMeters)
            {
                throw new WorldFormatException(
                    $"Campaign tile ({tile.X}, {tile.Y}) height {tile.HeightMeters} is outside " +
                    $"{tiles.Definition.MinimumHeightMeters}..{tiles.Definition.MaximumHeightMeters} metres.");
            }

            var key = ((long)tile.Y << 32) | (uint)tile.X;
            if (!seen.Add(key))
            {
                throw new WorldFormatException($"Campaign tile ({tile.X}, {tile.Y}) is stored more than once.");
            }

            normalizedCoastalTileCount += tile.Type == CampaignTileType.Coastal ? 1 : 0;
            var data = new CampaignTileData(
                NormalizeLegacyType(tile.Type),
                tile.HeightMeters,
                tile.CustomTerrainId);
            if (data == tiles.DefaultTile)
            {
                throw new WorldFormatException(
                    $"Campaign tile ({tile.X}, {tile.Y}) redundantly stores the implicit default value.");
            }

            loadedEntries.Add(new CampaignTileEntry(tile.X, tile.Y, data));
        }

        try
        {
            tiles.SetTiles(loadedEntries);
        }
        catch (CampaignTileTopologyException exception)
        {
            throw new WorldFormatException($"Campaign tile file has invalid river topology: {exception.Message}", exception);
        }
        catch (ArgumentException exception)
        {
            throw new WorldFormatException($"Campaign tile file has invalid custom terrain data: {exception.Message}", exception);
        }

        return normalizedCoastalTileCount;
    }

    private static async Task<CampaignWorldLoadResult> ConvertLegacyAsync(
        string manifestPath,
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        CampaignWorldDefinition definition;
        try
        {
            var legacyDefinition = await WorldProjectSerializer.LoadDefinitionAsync(
                manifestPath,
                cancellationToken).ConfigureAwait(false);
            definition = CreateLegacyCampaignDefinition(legacyDefinition);
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (WorldValidationException exception)
        {
            throw new WorldFormatException($"Legacy world cannot be converted: {exception.Message}", exception);
        }

        WorldTerrain legacy;
        try
        {
            legacy = await WorldProjectSerializer.LoadAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or WorldValidationException)
        {
            throw new WorldFormatException($"Legacy world could not be imported: {exception.Message}", exception);
        }

        var world = new CampaignWorld(definition);
        var normalizedCoastalTileCount = 0;
        for (var tileY = 0; tileY < definition.TilesY; tileY++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var tileX = 0; tileX < definition.TilesX; tileX++)
            {
                var legacyType = legacy.CampaignTiles.GetTileType(tileX, tileY);
                normalizedCoastalTileCount += legacyType == CampaignTileType.Coastal ? 1 : 0;
                var type = NormalizeLegacyType(legacyType);
                var height = GetLegacyTileAverage(legacy, tileX, tileY);
                var data = new CampaignTileData(type, height);
                if (data != world.Tiles.DefaultTile)
                {
                    world.Tiles.SetTile(tileX, tileY, data);
                }
            }
        }

        return new CampaignWorldLoadResult(world, true, projectDirectory, normalizedCoastalTileCount);
    }

    private static CampaignWorldDefinition CreateLegacyCampaignDefinition(WorldDefinition old)
    {
        if (old.WorldWidthMeters % old.CampaignTileSizeMeters != 0 ||
            old.WorldHeightMeters % old.CampaignTileSizeMeters != 0)
        {
            throw new WorldFormatException(
                "Legacy world dimensions must divide exactly by campaign tile size before they can be imported.");
        }

        return CampaignWorldDefinition.Create(
            old.WorldWidthMeters,
            old.WorldHeightMeters,
            old.CampaignTileSizeMeters,
            old.SeaLevelMeters,
            old.MinimumElevationMeters,
            old.MaximumElevationMeters,
            old.InitialElevationMeters);
    }

    private static short GetLegacyTileAverage(WorldTerrain legacy, int tileX, int tileY)
    {
        var definition = legacy.Definition;
        var tileSize = (long)definition.CampaignTileSizeMeters;
        var startMetersX = tileX * tileSize;
        var startMetersY = tileY * tileSize;
        var endMetersX = startMetersX + tileSize;
        var endMetersY = startMetersY + tileSize;
        var startSampleX = CeilingDivide(startMetersX, definition.HeightSampleSpacingMeters);
        var startSampleY = CeilingDivide(startMetersY, definition.HeightSampleSpacingMeters);
        var endSampleX = tileX == definition.CampaignTilesX - 1
            ? definition.HeightSamplesX
            : CeilingDivide(endMetersX, definition.HeightSampleSpacingMeters);
        var endSampleY = tileY == definition.CampaignTilesY - 1
            ? definition.HeightSamplesY
            : CeilingDivide(endMetersY, definition.HeightSampleSpacingMeters);

        long total = 0;
        long count = 0;
        for (var sampleY = startSampleY; sampleY < endSampleY; sampleY++)
        {
            for (var sampleX = startSampleX; sampleX < endSampleX; sampleX++)
            {
                total += legacy.GetHeight(sampleX, sampleY);
                count++;
            }
        }

        if (count == 0)
        {
            return SampleLegacyAtTileCenter(legacy, tileX, tileY);
        }

        return (short)Math.Clamp(
            Math.Round((double)total / count, MidpointRounding.AwayFromZero),
            definition.MinimumElevationMeters,
            definition.MaximumElevationMeters);
    }

    private static short SampleLegacyAtTileCenter(WorldTerrain legacy, int tileX, int tileY)
    {
        var definition = legacy.Definition;
        var centerMetersX = (tileX + 0.5) * definition.CampaignTileSizeMeters;
        var centerMetersY = (tileY + 0.5) * definition.CampaignTileSizeMeters;
        var sampleX = centerMetersX / definition.HeightSampleSpacingMeters;
        var sampleY = centerMetersY / definition.HeightSampleSpacingMeters;
        var x0 = Math.Clamp((int)Math.Floor(sampleX), 0, definition.HeightSamplesX - 1);
        var y0 = Math.Clamp((int)Math.Floor(sampleY), 0, definition.HeightSamplesY - 1);
        var x1 = Math.Min(x0 + 1, definition.HeightSamplesX - 1);
        var y1 = Math.Min(y0 + 1, definition.HeightSamplesY - 1);
        var fractionX = sampleX - Math.Floor(sampleX);
        var fractionY = sampleY - Math.Floor(sampleY);
        var top = Lerp(legacy.GetHeight(x0, y0), legacy.GetHeight(x1, y0), fractionX);
        var bottom = Lerp(legacy.GetHeight(x0, y1), legacy.GetHeight(x1, y1), fractionX);
        return (short)Math.Clamp(
            Math.Round(Lerp(top, bottom, fractionY), MidpointRounding.AwayFromZero),
            definition.MinimumElevationMeters,
            definition.MaximumElevationMeters);
    }

    private static async Task WriteAtomicallyAsync(
        string destination,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var temporary = destination + ".tmp";
        await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, destination, true);
    }

    private static int CeilingDivide(long value, int divisor) =>
        checked((int)((value + divisor - 1) / divisor));

    private static double Lerp(double left, double right, double amount) =>
        left + (right - left) * amount;

    private static CampaignTileType NormalizeLegacyType(CampaignTileType type) => type switch
    {
        CampaignTileType.Water => CampaignTileType.Sea,
        CampaignTileType.Coastal => CampaignTileType.Plains,
        _ => type,
    };

    private static async Task SaveCustomTerrainDefinitionsAsync(
        IReadOnlyList<CampaignCustomTerrainDefinition> definitions,
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        var customTerrainPath = Path.Combine(projectDirectory, CustomTerrainFileName);
        if (definitions.Count == 0)
        {
            if (File.Exists(customTerrainPath))
            {
                File.Delete(customTerrainPath);
            }

            return;
        }

        var document = new CampaignCustomTerrainDocument
        {
            Version = CampaignCustomTerrainDocument.CurrentVersion,
            Types = definitions
                .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
                .Select(static definition => new CampaignCustomTerrainRecord
                {
                    Id = definition.Id,
                    Name = definition.Name,
                    BaseType = definition.BaseType,
                    Color = definition.ColorHex,
                    GenerationSharePercent = definition.GenerationSharePercent,
                })
                .ToArray(),
        };
        await WriteAtomicallyAsync(
            customTerrainPath,
            JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<CampaignCustomTerrainDefinition>> LoadCustomTerrainDefinitionsAsync(
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        var customTerrainPath = Path.Combine(projectDirectory, CustomTerrainFileName);
        if (!File.Exists(customTerrainPath))
        {
            return [];
        }

        CampaignCustomTerrainDocument document;
        try
        {
            await using var stream = File.OpenRead(customTerrainPath);
            document = await JsonSerializer.DeserializeAsync<CampaignCustomTerrainDocument>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false)
                ?? throw new WorldFormatException("Custom terrain file is empty.");
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or NotSupportedException)
        {
            throw new WorldFormatException($"Custom terrain file is invalid: {exception.Message}", exception);
        }

        if (document.Version != CampaignCustomTerrainDocument.CurrentVersion)
        {
            throw new WorldFormatException(
                $"Custom terrain file version {document.Version} is unsupported; expected {CampaignCustomTerrainDocument.CurrentVersion}.");
        }

        try
        {
            return CampaignCustomTerrainDefinition.ValidateAll(document.Types.Select(static type =>
                new CampaignCustomTerrainDefinition(
                    type.Id,
                    type.Name,
                    type.BaseType,
                    type.Color,
                    type.GenerationSharePercent)));
        }
        catch (ArgumentException exception)
        {
            throw new WorldFormatException($"Custom terrain file is invalid: {exception.Message}", exception);
        }
    }

    private sealed record CampaignTileDocument
    {
        public const int CurrentVersion = 2;

        [JsonRequired]
        public int Version { get; init; }

        [JsonRequired]
        public CampaignTileRecord[] Tiles { get; init; } = [];
    }

    private sealed record CampaignTileRecord
    {
        [JsonRequired]
        public int X { get; init; }

        [JsonRequired]
        public int Y { get; init; }

        [JsonRequired]
        public CampaignTileType Type { get; init; }

        [JsonRequired]
        public short HeightMeters { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CustomTerrainId { get; init; }
    }

    private sealed record CampaignCustomTerrainDocument
    {
        public const int CurrentVersion = 1;

        [JsonRequired]
        public int Version { get; init; }

        [JsonRequired]
        public CampaignCustomTerrainRecord[] Types { get; init; } = [];
    }

    private sealed record CampaignCustomTerrainRecord
    {
        [JsonRequired]
        public string Id { get; init; } = string.Empty;

        [JsonRequired]
        public string Name { get; init; } = string.Empty;

        [JsonRequired]
        public CampaignTileType BaseType { get; init; }

        [JsonRequired]
        public string Color { get; init; } = string.Empty;

        [JsonRequired]
        public int GenerationSharePercent { get; init; }
    }
}
