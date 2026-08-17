using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Serialization;

public static class CampaignWorldRuntimeExporter
{
    public const string PackageExtension = ".kworld";
    public const string ManifestEntryName = "manifest.json";
    public const string TileDataEntryName = "tiles.bin";
    public const string ResourceIndexEntryName = "resource-index.bin";
    public const string ResourceRecordsEntryName = "resource-records.bin";
    public const string SeasonTilesEntryName = "season-tiles.bin";
    public const string FormatIdentifier = "kingdom-world-runtime";
    public const int FormatVersion = 1;
    public const int ResourceFormatVersion = 2;
    public const int SeasonFormatVersion = 3;
    public const int TileRecordSizeBytes = 4;
    public const int ResourceIndexRecordSizeBytes = 8;
    public const int ResourceRecordSizeBytes = 4;
    public const int SeasonRecordSizeBytes = 2;
    public const byte NoCustomTerrainIndex = byte.MaxValue;

    private const int ExportBufferSize = 64 * 1024;

    private static readonly DateTimeOffset StableArchiveTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task ExportAsync(
        CampaignWorld world,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        CampaignWorldDefinition.EnsureValid(world.Definition);
        if (!string.Equals(
                Path.GetExtension(packagePath),
                PackageExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Runtime world packages must use the '{PackageExtension}' extension.",
                nameof(packagePath));
        }

        var worldRevision = world.Revision;
        var fullPackagePath = Path.GetFullPath(packagePath);
        var packageDirectory = Path.GetDirectoryName(fullPackagePath)
            ?? throw new ArgumentException("Runtime package has no containing directory.", nameof(packagePath));
        Directory.CreateDirectory(packageDirectory);
        var temporaryPath = fullPackagePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            var customDefinitions = world.Tiles.CustomTerrainDefinitions
                .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
                .ToArray();
            var customIndices = customDefinitions
                .Select(static (definition, index) => (definition.Id, Index: checked((byte)index)))
                .ToDictionary(static item => item.Id, static item => item.Index, StringComparer.Ordinal);

            await using (var packageStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                ExportBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using (var archive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var tileEntry = archive.CreateEntry(TileDataEntryName, CompressionLevel.Optimal);
                    tileEntry.LastWriteTime = StableArchiveTimestamp;
                    string tileSha256;
                    await using (var tileStream = tileEntry.Open())
                    {
                        tileSha256 = await WriteDenseTileDataAsync(
                            world,
                            customIndices,
                            tileStream,
                            cancellationToken).ConfigureAwait(false);
                    }

                    EnsureWorldRevisionUnchanged(world, worldRevision);
                    var manifest = CreateManifest(world, customDefinitions, tileSha256);
                    var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                    manifestEntry.LastWriteTime = StableArchiveTimestamp;
                    await using var manifestStream = manifestEntry.Open();
                    await JsonSerializer.SerializeAsync(
                        manifestStream,
                        manifest,
                        JsonOptions,
                        cancellationToken).ConfigureAwait(false);
                }

                await packageStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureWorldRevisionUnchanged(world, worldRevision);
            File.Move(temporaryPath, fullPackagePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public static async Task ExportAsync(
        CampaignWorld world,
        CampaignResourceMap resources,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        CampaignWorldDefinition.EnsureValid(world.Definition);
        CampaignWorldDefinition.EnsureValid(resources.Definition);
        if (world.Definition != resources.Definition)
        {
            throw new ArgumentException(
                "The campaign resource map must use a value-equal world definition.",
                nameof(resources));
        }

        if (!string.Equals(
                Path.GetExtension(packagePath),
                PackageExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Runtime world packages must use the '{PackageExtension}' extension.",
                nameof(packagePath));
        }

        var resourceRevision = resources.Revision;
        var worldRevision = world.Revision;
        resources.EnsureValid();
        EnsureResourceRevisionUnchanged(resources, resourceRevision);
        cancellationToken.ThrowIfCancellationRequested();

        var resourceRecordCount = resources.OccurrenceCount;
        var tileByteLength = checked(world.Definition.TileCount * TileRecordSizeBytes);
        var resourceIndexByteLength = checked(world.Definition.TileCount * ResourceIndexRecordSizeBytes);
        var resourceRecordsByteLength = checked((long)resourceRecordCount * ResourceRecordSizeBytes);

        var customDefinitions = world.Tiles.CustomTerrainDefinitions
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        var customIndices = customDefinitions
            .Select(static (definition, index) => (definition.Id, Index: checked((byte)index)))
            .ToDictionary(static item => item.Id, static item => item.Index, StringComparer.Ordinal);
        var resourceDefinitions = resources.Catalog.Definitions
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        var resourceIndices = resourceDefinitions
            .Select(static (definition, index) => (definition.Id, Index: checked((ushort)index)))
            .ToDictionary(static item => item.Id, static item => item.Index, StringComparer.Ordinal);

        var fullPackagePath = Path.GetFullPath(packagePath);
        var packageDirectory = Path.GetDirectoryName(fullPackagePath)
            ?? throw new ArgumentException("Runtime package has no containing directory.", nameof(packagePath));
        Directory.CreateDirectory(packageDirectory);
        var temporaryPath = fullPackagePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var packageStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                ExportBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using (var archive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var tileEntry = archive.CreateEntry(TileDataEntryName, CompressionLevel.Optimal);
                    tileEntry.LastWriteTime = StableArchiveTimestamp;
                    string tileSha256;
                    await using (var tileStream = tileEntry.Open())
                    {
                        tileSha256 = await WriteDenseTileDataAsync(
                            world,
                            customIndices,
                            tileStream,
                            cancellationToken).ConfigureAwait(false);
                    }

                    EnsureResourceRevisionUnchanged(resources, resourceRevision);

                    var resourceIndexEntry = archive.CreateEntry(ResourceIndexEntryName, CompressionLevel.Optimal);
                    resourceIndexEntry.LastWriteTime = StableArchiveTimestamp;
                    RuntimeBinaryWriteResult resourceIndexResult;
                    await using (var resourceIndexStream = resourceIndexEntry.Open())
                    {
                        resourceIndexResult = await WriteResourceIndexDataAsync(
                            resources,
                            resourceIndexStream,
                            cancellationToken).ConfigureAwait(false);
                    }

                    EnsureResourceRevisionUnchanged(resources, resourceRevision);
                    if (resourceIndexResult.ReferencedRecordCount != resourceRecordCount)
                    {
                        throw new InvalidOperationException(
                            "The resource occurrence count changed while the runtime package was being exported.");
                    }

                    var resourceRecordsEntry = archive.CreateEntry(ResourceRecordsEntryName, CompressionLevel.Optimal);
                    resourceRecordsEntry.LastWriteTime = StableArchiveTimestamp;
                    RuntimeBinaryWriteResult resourceRecordsResult;
                    await using (var resourceRecordsStream = resourceRecordsEntry.Open())
                    {
                        resourceRecordsResult = await WriteResourceRecordsDataAsync(
                            resources,
                            resourceIndices,
                            resourceRecordsStream,
                            cancellationToken).ConfigureAwait(false);
                    }

                    EnsureResourceRevisionUnchanged(resources, resourceRevision);
                    if (resourceRecordsResult.ReferencedRecordCount != resourceRecordCount)
                    {
                        throw new InvalidOperationException(
                            "The resource occurrence count changed while the runtime package was being exported.");
                    }

                    EnsureWorldRevisionUnchanged(world, worldRevision);
                    var manifest = CreateManifestV2(
                        world,
                        customDefinitions,
                        resourceDefinitions,
                        tileSha256,
                        tileByteLength,
                        resourceIndexResult.Sha256,
                        resourceIndexByteLength,
                        resourceRecordsResult.Sha256,
                        resourceRecordsByteLength,
                        resourceRecordCount,
                        resources.Catalog);
                    var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                    manifestEntry.LastWriteTime = StableArchiveTimestamp;
                    await using var manifestStream = manifestEntry.Open();
                    await JsonSerializer.SerializeAsync(
                        manifestStream,
                        manifest,
                        JsonOptions,
                        cancellationToken).ConfigureAwait(false);
                }

                await packageStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureWorldRevisionUnchanged(world, worldRevision);
            EnsureResourceRevisionUnchanged(resources, resourceRevision);
            File.Move(temporaryPath, fullPackagePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public static async Task ExportAsync(
        CampaignWorld world,
        CampaignResourceMap resources,
        CampaignSeasonMap seasons,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(seasons);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        CampaignWorldDefinition.EnsureValid(world.Definition);
        CampaignWorldDefinition.EnsureValid(resources.Definition);
        CampaignWorldDefinition.EnsureValid(seasons.Definition);
        if (world.Definition != resources.Definition)
        {
            throw new ArgumentException(
                "The campaign resource map must use a value-equal world definition.",
                nameof(resources));
        }

        if (world.Definition != seasons.Definition)
        {
            throw new ArgumentException(
                "The campaign season map must use a value-equal world definition.",
                nameof(seasons));
        }

        if (!string.Equals(
                Path.GetExtension(packagePath),
                PackageExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Runtime world packages must use the '{PackageExtension}' extension.",
                nameof(packagePath));
        }

        var worldRevision = world.Revision;
        var resourceRevision = resources.Revision;
        var seasonRevision = seasons.Revision;
        resources.EnsureValid();
        seasons.EnsureValid();
        EnsureWorldRevisionUnchanged(world, worldRevision);
        EnsureResourceRevisionUnchanged(resources, resourceRevision);
        EnsureSeasonRevisionUnchanged(seasons, seasonRevision);
        cancellationToken.ThrowIfCancellationRequested();

        var resourceRecordCount = resources.OccurrenceCount;
        var tileByteLength = checked(world.Definition.TileCount * TileRecordSizeBytes);
        var resourceIndexByteLength = checked(
            world.Definition.TileCount * ResourceIndexRecordSizeBytes);
        var resourceRecordsByteLength = checked(
            (long)resourceRecordCount * ResourceRecordSizeBytes);
        var seasonByteLength = checked(world.Definition.TileCount * SeasonRecordSizeBytes);

        var customDefinitions = world.Tiles.CustomTerrainDefinitions
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        var customIndices = customDefinitions
            .Select(static (definition, index) => (definition.Id, Index: checked((byte)index)))
            .ToDictionary(static item => item.Id, static item => item.Index, StringComparer.Ordinal);
        var resourceDefinitions = resources.Catalog.Definitions
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        var resourceIndices = resourceDefinitions
            .Select(static (definition, index) => (definition.Id, Index: checked((ushort)index)))
            .ToDictionary(static item => item.Id, static item => item.Index, StringComparer.Ordinal);
        var seasonDefinitions = seasons.Catalog.Definitions.ToArray();

        var fullPackagePath = Path.GetFullPath(packagePath);
        var packageDirectory = Path.GetDirectoryName(fullPackagePath)
            ?? throw new ArgumentException("Runtime package has no containing directory.", nameof(packagePath));
        Directory.CreateDirectory(packageDirectory);
        var temporaryPath = fullPackagePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var packageStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                ExportBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using (var archive = new ZipArchive(packageStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var tileEntry = archive.CreateEntry(TileDataEntryName, CompressionLevel.Optimal);
                    tileEntry.LastWriteTime = StableArchiveTimestamp;
                    string tileSha256;
                    await using (var tileStream = tileEntry.Open())
                    {
                        tileSha256 = await WriteDenseTileDataAsync(
                            world,
                            customIndices,
                            tileStream,
                            cancellationToken).ConfigureAwait(false);
                    }

                    EnsureWorldRevisionUnchanged(world, worldRevision);
                    EnsureResourceRevisionUnchanged(resources, resourceRevision);
                    EnsureSeasonRevisionUnchanged(seasons, seasonRevision);

                    var resourceIndexEntry = archive.CreateEntry(
                        ResourceIndexEntryName,
                        CompressionLevel.Optimal);
                    resourceIndexEntry.LastWriteTime = StableArchiveTimestamp;
                    RuntimeBinaryWriteResult resourceIndexResult;
                    await using (var resourceIndexStream = resourceIndexEntry.Open())
                    {
                        resourceIndexResult = await WriteResourceIndexDataAsync(
                            resources,
                            resourceIndexStream,
                            cancellationToken).ConfigureAwait(false);
                    }

                    EnsureWorldRevisionUnchanged(world, worldRevision);
                    EnsureResourceRevisionUnchanged(resources, resourceRevision);
                    EnsureSeasonRevisionUnchanged(seasons, seasonRevision);
                    if (resourceIndexResult.ReferencedRecordCount != resourceRecordCount)
                    {
                        throw new InvalidOperationException(
                            "The resource occurrence count changed while the runtime package was being exported.");
                    }

                    var resourceRecordsEntry = archive.CreateEntry(
                        ResourceRecordsEntryName,
                        CompressionLevel.Optimal);
                    resourceRecordsEntry.LastWriteTime = StableArchiveTimestamp;
                    RuntimeBinaryWriteResult resourceRecordsResult;
                    await using (var resourceRecordsStream = resourceRecordsEntry.Open())
                    {
                        resourceRecordsResult = await WriteResourceRecordsDataAsync(
                            resources,
                            resourceIndices,
                            resourceRecordsStream,
                            cancellationToken).ConfigureAwait(false);
                    }

                    EnsureWorldRevisionUnchanged(world, worldRevision);
                    EnsureResourceRevisionUnchanged(resources, resourceRevision);
                    EnsureSeasonRevisionUnchanged(seasons, seasonRevision);
                    if (resourceRecordsResult.ReferencedRecordCount != resourceRecordCount)
                    {
                        throw new InvalidOperationException(
                            "The resource occurrence count changed while the runtime package was being exported.");
                    }

                    var seasonEntry = archive.CreateEntry(
                        SeasonTilesEntryName,
                        CompressionLevel.Optimal);
                    seasonEntry.LastWriteTime = StableArchiveTimestamp;
                    string seasonSha256;
                    await using (var seasonStream = seasonEntry.Open())
                    {
                        seasonSha256 = await WriteSeasonTileDataAsync(
                            seasons,
                            seasonStream,
                            cancellationToken).ConfigureAwait(false);
                    }

                    EnsureWorldRevisionUnchanged(world, worldRevision);
                    EnsureResourceRevisionUnchanged(resources, resourceRevision);
                    EnsureSeasonRevisionUnchanged(seasons, seasonRevision);
                    var manifest = CreateManifestV3(
                        world,
                        customDefinitions,
                        resourceDefinitions,
                        seasonDefinitions,
                        tileSha256,
                        tileByteLength,
                        resourceIndexResult.Sha256,
                        resourceIndexByteLength,
                        resourceRecordsResult.Sha256,
                        resourceRecordsByteLength,
                        resourceRecordCount,
                        seasonSha256,
                        seasonByteLength,
                        resources.Catalog,
                        seasons.Catalog);
                    var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                    manifestEntry.LastWriteTime = StableArchiveTimestamp;
                    await using var manifestStream = manifestEntry.Open();
                    await JsonSerializer.SerializeAsync(
                        manifestStream,
                        manifest,
                        JsonOptions,
                        cancellationToken).ConfigureAwait(false);
                }

                await packageStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureWorldRevisionUnchanged(world, worldRevision);
            EnsureResourceRevisionUnchanged(resources, resourceRevision);
            EnsureSeasonRevisionUnchanged(seasons, seasonRevision);
            File.Move(temporaryPath, fullPackagePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static async Task<string> WriteDenseTileDataAsync(
        CampaignWorld world,
        IReadOnlyDictionary<string, byte> customIndices,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ExportBufferSize];
        var bufferedBytes = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var y = 0; y < world.Definition.TilesY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < world.Definition.TilesX; x++)
            {
                if (bufferedBytes + TileRecordSizeBytes > buffer.Length)
                {
                    await FlushBufferAsync(
                        destination,
                        hash,
                        buffer,
                        bufferedBytes,
                        cancellationToken).ConfigureAwait(false);
                    bufferedBytes = 0;
                }

                var tile = world.Tiles.GetTile(x, y);
                buffer[bufferedBytes] = (byte)NormalizeLegacyType(tile.Type);
                buffer[bufferedBytes + 1] = GetCustomTerrainIndex(tile.CustomTerrainId, customIndices);
                BinaryPrimitives.WriteInt16LittleEndian(
                    buffer.AsSpan(bufferedBytes + 2, sizeof(short)),
                    tile.HeightMeters);
                bufferedBytes += TileRecordSizeBytes;
            }
        }

        if (bufferedBytes > 0)
        {
            await FlushBufferAsync(
                destination,
                hash,
                buffer,
                bufferedBytes,
                cancellationToken).ConfigureAwait(false);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task FlushBufferAsync(
        Stream destination,
        IncrementalHash hash,
        byte[] buffer,
        int count,
        CancellationToken cancellationToken)
    {
        hash.AppendData(buffer, 0, count);
        await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
    }

    private static async Task<RuntimeBinaryWriteResult> WriteResourceIndexDataAsync(
        CampaignResourceMap resources,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ExportBufferSize];
        var bufferedBytes = 0;
        uint firstRecordIndex = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var y = 0; y < resources.Definition.TilesY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < resources.Definition.TilesX; x++)
            {
                if (bufferedBytes + ResourceIndexRecordSizeBytes > buffer.Length)
                {
                    await FlushBufferAsync(
                        destination,
                        hash,
                        buffer,
                        bufferedBytes,
                        cancellationToken).ConfigureAwait(false);
                    bufferedBytes = 0;
                }

                var recordCount = checked((ushort)resources.GetOccurrences(x, y).Count);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    buffer.AsSpan(bufferedBytes, sizeof(uint)),
                    firstRecordIndex);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    buffer.AsSpan(bufferedBytes + sizeof(uint), sizeof(ushort)),
                    recordCount);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    buffer.AsSpan(bufferedBytes + sizeof(uint) + sizeof(ushort), sizeof(ushort)),
                    0);
                bufferedBytes += ResourceIndexRecordSizeBytes;
                firstRecordIndex = checked(firstRecordIndex + recordCount);
            }
        }

        if (bufferedBytes > 0)
        {
            await FlushBufferAsync(
                destination,
                hash,
                buffer,
                bufferedBytes,
                cancellationToken).ConfigureAwait(false);
        }

        return new RuntimeBinaryWriteResult(
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            firstRecordIndex);
    }

    private static async Task<RuntimeBinaryWriteResult> WriteResourceRecordsDataAsync(
        CampaignResourceMap resources,
        IReadOnlyDictionary<string, ushort> resourceIndices,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ExportBufferSize];
        var bufferedBytes = 0;
        long recordCount = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var y = 0; y < resources.Definition.TilesY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < resources.Definition.TilesX; x++)
            {
                foreach (var occurrence in resources.GetOccurrences(x, y))
                {
                    if (bufferedBytes + ResourceRecordSizeBytes > buffer.Length)
                    {
                        await FlushBufferAsync(
                            destination,
                            hash,
                            buffer,
                            bufferedBytes,
                            cancellationToken).ConfigureAwait(false);
                        bufferedBytes = 0;
                    }

                    if (!resourceIndices.TryGetValue(occurrence.ResourceId, out var catalogIndex))
                    {
                        throw new InvalidOperationException(
                            $"Resource occurrence references unknown catalog ID '{occurrence.ResourceId}'.");
                    }

                    BinaryPrimitives.WriteUInt16LittleEndian(
                        buffer.AsSpan(bufferedBytes, sizeof(ushort)),
                        catalogIndex);
                    buffer[bufferedBytes + sizeof(ushort)] = occurrence.Potential;
                    buffer[bufferedBytes + sizeof(ushort) + sizeof(byte)] = 0;
                    bufferedBytes += ResourceRecordSizeBytes;
                    recordCount = checked(recordCount + 1);
                }
            }
        }

        if (bufferedBytes > 0)
        {
            await FlushBufferAsync(
                destination,
                hash,
                buffer,
                bufferedBytes,
                cancellationToken).ConfigureAwait(false);
        }

        return new RuntimeBinaryWriteResult(
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            recordCount);
    }

    private static async Task<string> WriteSeasonTileDataAsync(
        CampaignSeasonMap seasons,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ExportBufferSize];
        var bufferedBytes = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (var y = 0; y < seasons.Definition.TilesY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < seasons.Definition.TilesX; x++)
            {
                if (bufferedBytes + SeasonRecordSizeBytes > buffer.Length)
                {
                    await FlushBufferAsync(
                        destination,
                        hash,
                        buffer,
                        bufferedBytes,
                        cancellationToken).ConfigureAwait(false);
                    bufferedBytes = 0;
                }

                var season = seasons.GetTile(x, y);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    buffer.AsSpan(bufferedBytes, sizeof(ushort)),
                    seasons.Catalog.GetIndex(season.SeasonId));
                bufferedBytes += SeasonRecordSizeBytes;
            }
        }

        if (bufferedBytes > 0)
        {
            await FlushBufferAsync(
                destination,
                hash,
                buffer,
                bufferedBytes,
                cancellationToken).ConfigureAwait(false);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static RuntimeWorldManifest CreateManifest(
        CampaignWorld world,
        IReadOnlyList<CampaignCustomTerrainDefinition> customDefinitions,
        string tileSha256)
    {
        var definition = world.Definition;
        return new RuntimeWorldManifest
        {
            Format = FormatIdentifier,
            Version = FormatVersion,
            World = new RuntimeWorldDimensions
            {
                WidthMeters = definition.WorldWidthMeters,
                HeightMeters = definition.WorldHeightMeters,
                CampaignTileSizeMeters = definition.CampaignTileSizeMeters,
                SeaLevelMeters = definition.SeaLevelMeters,
                MinimumHeightMeters = definition.MinimumHeightMeters,
                MaximumHeightMeters = definition.MaximumHeightMeters,
                DefaultTileHeightMeters = definition.DefaultTileHeightMeters,
            },
            Grid = new RuntimeGridLayout
            {
                TilesX = definition.TilesX,
                TilesY = definition.TilesY,
                TileCount = definition.TileCount,
                Origin = "northWest",
                XAxis = "east",
                YAxis = "south",
                TileOrder = "rowMajorYThenX",
            },
            TileRecord = new RuntimeTileRecordLayout
            {
                File = TileDataEntryName,
                RecordSizeBytes = TileRecordSizeBytes,
                ByteLength = checked(definition.TileCount * TileRecordSizeBytes),
                ByteOrder = "littleEndian",
                Sha256 = tileSha256,
                Fields =
                [
                    new RuntimeTileField("type", 0, "uint8", "tileTypes"),
                    new RuntimeTileField("customTerrainIndex", 1, "uint8", "customTerrain", NoCustomTerrainIndex),
                    new RuntimeTileField("heightMeters", 2, "int16", null),
                ],
            },
            TileTypes = Enum.GetValues<CampaignTileType>()
                .Where(static type => type is not (CampaignTileType.Water or CampaignTileType.Coastal))
                .Select(static type => new RuntimeTileType((byte)type, GetSerializedTypeName(type)))
                .ToArray(),
            CustomTerrain = customDefinitions
                .Select(static (definition, index) => new RuntimeCustomTerrain(
                    checked((byte)index),
                    definition.Id,
                    definition.Name,
                    (byte)definition.BaseType,
                    GetSerializedTypeName(definition.BaseType),
                    definition.ColorHex,
                    definition.GenerationSharePercent))
                .ToArray(),
        };
    }

    private static RuntimeWorldManifestV2 CreateManifestV2(
        CampaignWorld world,
        IReadOnlyList<CampaignCustomTerrainDefinition> customDefinitions,
        IReadOnlyList<CampaignResourceDefinition> resourceDefinitions,
        string tileSha256,
        long tileByteLength,
        string resourceIndexSha256,
        long resourceIndexByteLength,
        string resourceRecordsSha256,
        long resourceRecordsByteLength,
        long resourceRecordCount,
        CampaignResourceCatalog catalog)
    {
        var definition = world.Definition;
        return new RuntimeWorldManifestV2
        {
            Format = FormatIdentifier,
            Version = ResourceFormatVersion,
            World = new RuntimeWorldDimensions
            {
                WidthMeters = definition.WorldWidthMeters,
                HeightMeters = definition.WorldHeightMeters,
                CampaignTileSizeMeters = definition.CampaignTileSizeMeters,
                SeaLevelMeters = definition.SeaLevelMeters,
                MinimumHeightMeters = definition.MinimumHeightMeters,
                MaximumHeightMeters = definition.MaximumHeightMeters,
                DefaultTileHeightMeters = definition.DefaultTileHeightMeters,
            },
            Grid = new RuntimeGridLayout
            {
                TilesX = definition.TilesX,
                TilesY = definition.TilesY,
                TileCount = definition.TileCount,
                Origin = "northWest",
                XAxis = "east",
                YAxis = "south",
                TileOrder = "rowMajorYThenX",
            },
            TileRecord = new RuntimeTileRecordLayout
            {
                File = TileDataEntryName,
                RecordSizeBytes = TileRecordSizeBytes,
                ByteLength = tileByteLength,
                ByteOrder = "littleEndian",
                Sha256 = tileSha256,
                Fields =
                [
                    new RuntimeTileField("type", 0, "uint8", "tileTypes"),
                    new RuntimeTileField("customTerrainIndex", 1, "uint8", "customTerrain", NoCustomTerrainIndex),
                    new RuntimeTileField("heightMeters", 2, "int16", null),
                ],
            },
            TileTypes = Enum.GetValues<CampaignTileType>()
                .Where(static type => type is not (CampaignTileType.Water or CampaignTileType.Coastal))
                .Select(static type => new RuntimeTileType((byte)type, GetSerializedTypeName(type)))
                .ToArray(),
            CustomTerrain = customDefinitions
                .Select(static (customDefinition, index) => new RuntimeCustomTerrain(
                    checked((byte)index),
                    customDefinition.Id,
                    customDefinition.Name,
                    (byte)customDefinition.BaseType,
                    GetSerializedTypeName(customDefinition.BaseType),
                    customDefinition.ColorHex,
                    customDefinition.GenerationSharePercent))
                .ToArray(),
            Resources = new RuntimeResourceLayer
            {
                Catalog = resourceDefinitions
                    .Select((resourceDefinition, index) => new RuntimeResourceCatalogEntry(
                        checked((ushort)index),
                        resourceDefinition.Id,
                        resourceDefinition.Name,
                        GetSerializedResourceCategoryName(resourceDefinition.Category),
                        catalog.IsBuiltIn(resourceDefinition.Id)))
                    .ToArray(),
                IndexRecord = new RuntimeBinaryRecordLayout
                {
                    File = ResourceIndexEntryName,
                    RecordSizeBytes = ResourceIndexRecordSizeBytes,
                    RecordCount = definition.TileCount,
                    ByteLength = resourceIndexByteLength,
                    ByteOrder = "littleEndian",
                    Sha256 = resourceIndexSha256,
                    Fields =
                    [
                        new RuntimeBinaryField("firstRecordIndex", 0, "uint32", "resources.occurrenceRecord"),
                        new RuntimeBinaryField("recordCount", 4, "uint16"),
                        new RuntimeBinaryField("reserved", 6, "uint16", ConstantValue: 0),
                    ],
                },
                OccurrenceRecord = new RuntimeBinaryRecordLayout
                {
                    File = ResourceRecordsEntryName,
                    RecordSizeBytes = ResourceRecordSizeBytes,
                    RecordCount = resourceRecordCount,
                    ByteLength = resourceRecordsByteLength,
                    ByteOrder = "littleEndian",
                    Sha256 = resourceRecordsSha256,
                    Fields =
                    [
                        new RuntimeBinaryField("resourceCatalogIndex", 0, "uint16", "resources.catalog"),
                        new RuntimeBinaryField("potential", 2, "uint8"),
                        new RuntimeBinaryField("reserved", 3, "uint8", ConstantValue: 0),
                    ],
                },
            },
        };
    }

    private static RuntimeWorldManifestV3 CreateManifestV3(
        CampaignWorld world,
        IReadOnlyList<CampaignCustomTerrainDefinition> customDefinitions,
        IReadOnlyList<CampaignResourceDefinition> resourceDefinitions,
        IReadOnlyList<CampaignSeasonDefinition> seasonDefinitions,
        string tileSha256,
        long tileByteLength,
        string resourceIndexSha256,
        long resourceIndexByteLength,
        string resourceRecordsSha256,
        long resourceRecordsByteLength,
        long resourceRecordCount,
        string seasonSha256,
        long seasonByteLength,
        CampaignResourceCatalog resourceCatalog,
        CampaignSeasonCatalog seasonCatalog)
    {
        var definition = world.Definition;
        return new RuntimeWorldManifestV3
        {
            Format = FormatIdentifier,
            Version = SeasonFormatVersion,
            World = new RuntimeWorldDimensions
            {
                WidthMeters = definition.WorldWidthMeters,
                HeightMeters = definition.WorldHeightMeters,
                CampaignTileSizeMeters = definition.CampaignTileSizeMeters,
                SeaLevelMeters = definition.SeaLevelMeters,
                MinimumHeightMeters = definition.MinimumHeightMeters,
                MaximumHeightMeters = definition.MaximumHeightMeters,
                DefaultTileHeightMeters = definition.DefaultTileHeightMeters,
            },
            Grid = new RuntimeGridLayout
            {
                TilesX = definition.TilesX,
                TilesY = definition.TilesY,
                TileCount = definition.TileCount,
                Origin = "northWest",
                XAxis = "east",
                YAxis = "south",
                TileOrder = "rowMajorYThenX",
            },
            TileRecord = new RuntimeTileRecordLayout
            {
                File = TileDataEntryName,
                RecordSizeBytes = TileRecordSizeBytes,
                ByteLength = tileByteLength,
                ByteOrder = "littleEndian",
                Sha256 = tileSha256,
                Fields =
                [
                    new RuntimeTileField("type", 0, "uint8", "tileTypes"),
                    new RuntimeTileField(
                        "customTerrainIndex",
                        1,
                        "uint8",
                        "customTerrain",
                        NoCustomTerrainIndex),
                    new RuntimeTileField("heightMeters", 2, "int16", null),
                ],
            },
            TileTypes = Enum.GetValues<CampaignTileType>()
                .Where(static type => type is not (CampaignTileType.Water or CampaignTileType.Coastal))
                .Select(static type => new RuntimeTileType((byte)type, GetSerializedTypeName(type)))
                .ToArray(),
            CustomTerrain = customDefinitions
                .Select(static (customDefinition, index) => new RuntimeCustomTerrain(
                    checked((byte)index),
                    customDefinition.Id,
                    customDefinition.Name,
                    (byte)customDefinition.BaseType,
                    GetSerializedTypeName(customDefinition.BaseType),
                    customDefinition.ColorHex,
                    customDefinition.GenerationSharePercent))
                .ToArray(),
            Resources = new RuntimeResourceLayer
            {
                Catalog = resourceDefinitions
                    .Select((resourceDefinition, index) => new RuntimeResourceCatalogEntry(
                        checked((ushort)index),
                        resourceDefinition.Id,
                        resourceDefinition.Name,
                        GetSerializedResourceCategoryName(resourceDefinition.Category),
                        resourceCatalog.IsBuiltIn(resourceDefinition.Id)))
                    .ToArray(),
                IndexRecord = new RuntimeBinaryRecordLayout
                {
                    File = ResourceIndexEntryName,
                    RecordSizeBytes = ResourceIndexRecordSizeBytes,
                    RecordCount = definition.TileCount,
                    ByteLength = resourceIndexByteLength,
                    ByteOrder = "littleEndian",
                    Sha256 = resourceIndexSha256,
                    Fields =
                    [
                        new RuntimeBinaryField(
                            "firstRecordIndex",
                            0,
                            "uint32",
                            "resources.occurrenceRecord"),
                        new RuntimeBinaryField("recordCount", 4, "uint16"),
                        new RuntimeBinaryField("reserved", 6, "uint16", ConstantValue: 0),
                    ],
                },
                OccurrenceRecord = new RuntimeBinaryRecordLayout
                {
                    File = ResourceRecordsEntryName,
                    RecordSizeBytes = ResourceRecordSizeBytes,
                    RecordCount = resourceRecordCount,
                    ByteLength = resourceRecordsByteLength,
                    ByteOrder = "littleEndian",
                    Sha256 = resourceRecordsSha256,
                    Fields =
                    [
                        new RuntimeBinaryField(
                            "resourceCatalogIndex",
                            0,
                            "uint16",
                            "resources.catalog"),
                        new RuntimeBinaryField("potential", 2, "uint8"),
                        new RuntimeBinaryField("reserved", 3, "uint8", ConstantValue: 0),
                    ],
                },
            },
            Seasons = new RuntimeSeasonLayer
            {
                Catalog = seasonDefinitions
                    .Select((seasonDefinition, index) => new RuntimeSeasonCatalogEntry(
                        checked((ushort)index),
                        seasonDefinition.Id,
                        seasonDefinition.Name,
                        seasonCatalog.IsBuiltIn(seasonDefinition.Id),
                        GetSerializedSeasonName(seasonDefinition.Fallback),
                        seasonDefinition.ColorHex,
                        seasonDefinition.TintStrengthPercent,
                        seasonDefinition.EffectIntensityPercent))
                    .ToArray(),
                TileRecord = new RuntimeBinaryRecordLayout
                {
                    File = SeasonTilesEntryName,
                    RecordSizeBytes = SeasonRecordSizeBytes,
                    RecordCount = definition.TileCount,
                    ByteLength = seasonByteLength,
                    ByteOrder = "littleEndian",
                    Sha256 = seasonSha256,
                    Fields =
                    [
                        new RuntimeBinaryField(
                            "seasonCatalogIndex",
                            0,
                            "uint16",
                            "seasons.catalog"),
                    ],
                },
            },
        };
    }

    private static byte GetCustomTerrainIndex(
        string? customTerrainId,
        IReadOnlyDictionary<string, byte> customIndices)
    {
        if (customTerrainId is null)
        {
            return NoCustomTerrainIndex;
        }

        return customIndices.TryGetValue(customTerrainId, out var index)
            ? index
            : throw new InvalidOperationException(
                $"Tile references unknown custom terrain '{customTerrainId}'.");
    }

    private static CampaignTileType NormalizeLegacyType(CampaignTileType type) => type switch
    {
        CampaignTileType.Water => CampaignTileType.Sea,
        CampaignTileType.Coastal => CampaignTileType.Plains,
        _ => type,
    };

    private static string GetSerializedTypeName(CampaignTileType type) =>
        JsonNamingPolicy.CamelCase.ConvertName(type.ToString());

    private static string GetSerializedResourceCategoryName(CampaignResourceCategory category) =>
        JsonNamingPolicy.CamelCase.ConvertName(category.ToString());

    private static string GetSerializedSeasonName(CampaignBuiltInSeason season) =>
        JsonNamingPolicy.CamelCase.ConvertName(season.ToString());

    private static void EnsureResourceRevisionUnchanged(
        CampaignResourceMap resources,
        long expectedRevision)
    {
        if (resources.Revision != expectedRevision)
        {
            throw new InvalidOperationException(
                "The resource map changed while the runtime package was being exported.");
        }
    }

    private static void EnsureWorldRevisionUnchanged(
        CampaignWorld world,
        long expectedRevision)
    {
        if (world.Revision != expectedRevision)
        {
            throw new InvalidOperationException(
                "The campaign world changed while the runtime package was being exported.");
        }
    }

    private static void EnsureSeasonRevisionUnchanged(
        CampaignSeasonMap seasons,
        long expectedRevision)
    {
        if (seasons.Revision != expectedRevision)
        {
            throw new InvalidOperationException(
                "The season map changed while the runtime package was being exported.");
        }
    }

    private sealed class RuntimeWorldManifest
    {
        public required string Format { get; init; }

        public required int Version { get; init; }

        public required RuntimeWorldDimensions World { get; init; }

        public required RuntimeGridLayout Grid { get; init; }

        public required RuntimeTileRecordLayout TileRecord { get; init; }

        public required IReadOnlyList<RuntimeTileType> TileTypes { get; init; }

        public required IReadOnlyList<RuntimeCustomTerrain> CustomTerrain { get; init; }
    }

    private sealed class RuntimeWorldManifestV2
    {
        public required string Format { get; init; }

        public required int Version { get; init; }

        public required RuntimeWorldDimensions World { get; init; }

        public required RuntimeGridLayout Grid { get; init; }

        public required RuntimeTileRecordLayout TileRecord { get; init; }

        public required IReadOnlyList<RuntimeTileType> TileTypes { get; init; }

        public required IReadOnlyList<RuntimeCustomTerrain> CustomTerrain { get; init; }

        public required RuntimeResourceLayer Resources { get; init; }
    }

    private sealed class RuntimeWorldManifestV3
    {
        public required string Format { get; init; }

        public required int Version { get; init; }

        public required RuntimeWorldDimensions World { get; init; }

        public required RuntimeGridLayout Grid { get; init; }

        public required RuntimeTileRecordLayout TileRecord { get; init; }

        public required IReadOnlyList<RuntimeTileType> TileTypes { get; init; }

        public required IReadOnlyList<RuntimeCustomTerrain> CustomTerrain { get; init; }

        public required RuntimeResourceLayer Resources { get; init; }

        public required RuntimeSeasonLayer Seasons { get; init; }
    }

    private sealed class RuntimeWorldDimensions
    {
        public required long WidthMeters { get; init; }

        public required long HeightMeters { get; init; }

        public required int CampaignTileSizeMeters { get; init; }

        public required short SeaLevelMeters { get; init; }

        public required short MinimumHeightMeters { get; init; }

        public required short MaximumHeightMeters { get; init; }

        public required short DefaultTileHeightMeters { get; init; }
    }

    private sealed class RuntimeGridLayout
    {
        public required int TilesX { get; init; }

        public required int TilesY { get; init; }

        public required long TileCount { get; init; }

        public required string Origin { get; init; }

        public required string XAxis { get; init; }

        public required string YAxis { get; init; }

        public required string TileOrder { get; init; }
    }

    private sealed class RuntimeTileRecordLayout
    {
        public required string File { get; init; }

        public required int RecordSizeBytes { get; init; }

        public required long ByteLength { get; init; }

        public required string ByteOrder { get; init; }

        public required string Sha256 { get; init; }

        public required IReadOnlyList<RuntimeTileField> Fields { get; init; }
    }

    private sealed record RuntimeTileField(
        string Name,
        int OffsetBytes,
        string Storage,
        string? Mapping,
        int? NoneValue = null);

    private sealed record RuntimeTileType(byte Value, string Name);

    private sealed record RuntimeCustomTerrain(
        byte Index,
        string Id,
        string Name,
        byte BaseType,
        string BaseTypeName,
        string Color,
        int GenerationSharePercent);

    private sealed class RuntimeResourceLayer
    {
        public required IReadOnlyList<RuntimeResourceCatalogEntry> Catalog { get; init; }

        public required RuntimeBinaryRecordLayout IndexRecord { get; init; }

        public required RuntimeBinaryRecordLayout OccurrenceRecord { get; init; }
    }

    private sealed class RuntimeBinaryRecordLayout
    {
        public required string File { get; init; }

        public required int RecordSizeBytes { get; init; }

        public required long RecordCount { get; init; }

        public required long ByteLength { get; init; }

        public required string ByteOrder { get; init; }

        public required string Sha256 { get; init; }

        public required IReadOnlyList<RuntimeBinaryField> Fields { get; init; }
    }

    private sealed class RuntimeSeasonLayer
    {
        public required IReadOnlyList<RuntimeSeasonCatalogEntry> Catalog { get; init; }

        public required RuntimeBinaryRecordLayout TileRecord { get; init; }
    }

    private sealed record RuntimeBinaryField(
        string Name,
        int OffsetBytes,
        string Storage,
        string? Mapping = null,
        int? ConstantValue = null);

    private sealed record RuntimeResourceCatalogEntry(
        ushort Index,
        string Id,
        string Name,
        string Category,
        bool BuiltIn);

    private sealed record RuntimeSeasonCatalogEntry(
        ushort Index,
        string Id,
        string Name,
        bool BuiltIn,
        string Fallback,
        string Color,
        int TintStrengthPercent,
        int EffectIntensityPercent);

    private sealed record RuntimeBinaryWriteResult(
        string Sha256,
        long ReferencedRecordCount);
}
