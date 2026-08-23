using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Chunks;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Terrain;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Serialization;

public static class WorldProjectSerializer
{
    public const string ManifestFileName = "world.json";
    public const string ChunkDirectoryName = "chunks";
    public const string CampaignTileFileName = "campaign-tiles.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.Strict,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static async Task SaveAsync(
        WorldTerrain terrain,
        string projectDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        WorldDefinitionValidator.EnsureValid(terrain.Definition);

        var fullProjectPath = Path.GetFullPath(projectDirectory);
        var chunkDirectory = Path.Combine(fullProjectPath, ChunkDirectoryName);
        Directory.CreateDirectory(fullProjectPath);
        Directory.CreateDirectory(chunkDirectory);

        var chunks = new List<TerrainChunk>(terrain.GetAllocatedChunks());
        chunks.Sort(static (left, right) =>
        {
            var y = left.ChunkY.CompareTo(right.ChunkY);
            return y != 0 ? y : left.ChunkX.CompareTo(right.ChunkX);
        });

        var expectedChunkFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = GetChunkFileName(chunk.ChunkX, chunk.ChunkY);
            expectedChunkFiles.Add(fileName);

            var destination = Path.Combine(chunkDirectory, fileName);
            var temporary = destination + ".tmp";
            var bytes = EncodeChunk(chunk.Samples);
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, destination, true);
        }

        foreach (var existingPath in Directory.EnumerateFiles(chunkDirectory, "*.bin", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(existingPath);
            if (TryParseChunkFileName(fileName, out _, out _) && !expectedChunkFiles.Contains(fileName))
            {
                File.Delete(existingPath);
            }
        }

        await SaveCampaignTilesAsync(terrain.CampaignTiles, fullProjectPath, cancellationToken).ConfigureAwait(false);

        var manifestPath = Path.Combine(fullProjectPath, ManifestFileName);
        var temporaryManifestPath = manifestPath + ".tmp";
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(terrain.Definition, JsonOptions);
        await File.WriteAllBytesAsync(temporaryManifestPath, manifestBytes, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryManifestPath, manifestPath, true);
    }

    public static async Task<WorldTerrain> LoadAsync(
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

        var definition = await LoadDefinitionAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        var terrain = new WorldTerrain(definition);
        var chunkDirectory = Path.Combine(projectDirectory, ChunkDirectoryName);
        if (Directory.Exists(chunkDirectory))
        {
            var paths = Directory.EnumerateFiles(chunkDirectory, "*.bin", SearchOption.TopDirectoryOnly).ToList();
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var chunkPath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(chunkPath);
                if (!TryParseChunkFileName(fileName, out var chunkX, out var chunkY))
                {
                    throw new WorldFormatException($"Invalid chunk file name: {fileName}");
                }

                (int Width, int Height) dimensions;
                try
                {
                    dimensions = terrain.GetChunkDimensions(chunkX, chunkY);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    throw new WorldFormatException($"Chunk {fileName} lies outside the world.", exception);
                }

                var bytes = await File.ReadAllBytesAsync(chunkPath, cancellationToken).ConfigureAwait(false);
                var expectedByteLength = checked(dimensions.Width * dimensions.Height * sizeof(short));
                if (bytes.Length != expectedByteLength)
                {
                    throw new WorldFormatException(
                        $"Chunk {fileName} has {bytes.Length} bytes; expected {expectedByteLength}.");
                }

                var samples = DecodeChunk(bytes);
                for (var index = 0; index < samples.Length; index++)
                {
                    var elevation = samples[index];
                    if (elevation < definition.MinimumElevationMeters || elevation > definition.MaximumElevationMeters)
                    {
                        throw new WorldFormatException(
                            $"Chunk {fileName} contains elevation {elevation} at sample index {index}; " +
                            $"allowed range is {definition.MinimumElevationMeters}..{definition.MaximumElevationMeters}.");
                    }
                }

                terrain.LoadChunk(TerrainChunk.FromSamples(chunkX, chunkY, dimensions.Width, dimensions.Height, samples));
            }
        }

        await LoadCampaignTilesAsync(terrain.CampaignTiles, projectDirectory, cancellationToken).ConfigureAwait(false);

        return terrain;
    }

    internal static async Task<WorldDefinition> LoadDefinitionAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            var definition = await JsonSerializer.DeserializeAsync<WorldDefinition>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new WorldFormatException("World manifest is empty.");
            WorldDefinitionValidator.EnsureValid(definition);
            return definition;
        }
        catch (WorldFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or IOException or WorldValidationException)
        {
            throw new WorldFormatException($"World manifest is invalid: {exception.Message}", exception);
        }
    }

    public static string GetProjectDirectory(string projectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        var fullPath = Path.GetFullPath(projectPath);
        if (Directory.Exists(fullPath))
        {
            return fullPath;
        }

        return Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Project path has no containing directory.", nameof(projectPath));
    }

    private static async Task SaveCampaignTilesAsync(
        CampaignTileLayer layer,
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        var tiles = layer.GetAssignedTiles().ToList();
        tiles.Sort(static (left, right) =>
        {
            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        });

        var document = new CampaignTileDocument
        {
            Version = CampaignTileDocument.CurrentVersion,
            Tiles = tiles.Select(static tile => new CampaignTileRecord
            {
                X = tile.X,
                Y = tile.Y,
                Type = tile.Type,
            }).ToArray(),
        };
        var destination = Path.Combine(projectDirectory, CampaignTileFileName);
        var temporary = destination + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, destination, true);
    }

    private static async Task LoadCampaignTilesAsync(
        CampaignTileLayer layer,
        string projectDirectory,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(projectDirectory, CampaignTileFileName);
        if (!File.Exists(path))
        {
            return;
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
        foreach (var tile in document.Tiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tile.Type == CampaignTileType.Unassigned || !Enum.IsDefined(tile.Type))
            {
                throw new WorldFormatException(
                    $"Campaign tile ({tile.X}, {tile.Y}) has invalid sparse type '{tile.Type}'.");
            }

            if (!layer.IsValidCoordinate(tile.X, tile.Y))
            {
                throw new WorldFormatException(
                    $"Campaign tile ({tile.X}, {tile.Y}) lies outside 0..{layer.TilesX - 1}, 0..{layer.TilesY - 1}.");
            }

            var key = ((long)tile.Y << 32) | (uint)tile.X;
            if (!seen.Add(key))
            {
                throw new WorldFormatException($"Campaign tile ({tile.X}, {tile.Y}) is assigned more than once.");
            }

            layer.SetTileType(tile.X, tile.Y, tile.Type);
        }
    }

    private static byte[] EncodeChunk(ReadOnlySpan<short> samples)
    {
        var bytes = new byte[checked(samples.Length * sizeof(short))];
        for (var index = 0; index < samples.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * sizeof(short), sizeof(short)), samples[index]);
        }

        return bytes;
    }

    private static short[] DecodeChunk(ReadOnlySpan<byte> bytes)
    {
        var samples = new short[bytes.Length / sizeof(short)];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = BinaryPrimitives.ReadInt16LittleEndian(
                bytes.Slice(index * sizeof(short), sizeof(short)));
        }

        return samples;
    }

    private static string GetChunkFileName(int chunkX, int chunkY) =>
        string.Create(CultureInfo.InvariantCulture, $"{chunkX}_{chunkY}.bin");

    private static bool TryParseChunkFileName(string fileName, out int chunkX, out int chunkY)
    {
        chunkX = 0;
        chunkY = 0;
        if (!fileName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = fileName.AsSpan(0, fileName.Length - 4);
        var separator = stem.IndexOf('_');
        if (separator <= 0 || separator == stem.Length - 1 || stem[(separator + 1)..].Contains('_'))
        {
            return false;
        }

        return int.TryParse(stem[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out chunkX) &&
               int.TryParse(stem[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out chunkY);
    }

    private sealed record CampaignTileDocument
    {
        public const int CurrentVersion = 1;

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
    }
}
