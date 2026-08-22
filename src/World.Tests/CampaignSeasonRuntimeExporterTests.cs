using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Serialization;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonRuntimeExporterTests
{
    [Fact]
    public async Task Export_WritesVersionThreeOccurrenceIndexAndRecordsWithoutAuthoringLocks()
    {
        using var temporary = new TemporaryDirectory();
        var world = new CampaignWorld(CreateDefinition());
        var resources = new CampaignResourceMap(CreateDefinition());
        var catalog = new CampaignSeasonCatalog([CreateCustomSeason()]);
        var seasons = new CampaignSeasonMap(CreateDefinition(), catalog);
        seasons.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("spring")),
            CampaignSeasonMutation.Upsert(0, 0, new("summer")),
            CampaignSeasonMutation.Upsert(0, 0, new("fall", Locked: true)),
            CampaignSeasonMutation.Upsert(1, 0, new("monsoon")),
            CampaignSeasonMutation.Upsert(0, 1, new("spring", Locked: true)),
            CampaignSeasonMutation.Upsert(0, 1, new("winter")),
        ]);
        var packagePath = Path.Combine(temporary.Path, "runtime-v3.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, seasons, packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        Assert.Equal(
        [
            CampaignWorldRuntimeExporter.TileDataEntryName,
            CampaignWorldRuntimeExporter.ResourceIndexEntryName,
            CampaignWorldRuntimeExporter.ResourceRecordsEntryName,
            CampaignWorldRuntimeExporter.SeasonIndexEntryName,
            CampaignWorldRuntimeExporter.SeasonRecordsEntryName,
            CampaignWorldRuntimeExporter.ManifestEntryName,
        ], archive.Entries.Select(static entry => entry.FullName));

        var indexBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.SeasonIndexEntryName)!);
        var recordBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.SeasonRecordsEntryName)!);
        Assert.Equal(4 * CampaignWorldRuntimeExporter.SeasonIndexRecordSizeBytes, indexBytes.Length);
        Assert.Equal(6 * CampaignWorldRuntimeExporter.SeasonRecordSizeBytes, recordBytes.Length);
        Assert.Equal((0u, (ushort)3), ReadSeasonSpan(indexBytes, 0));
        Assert.Equal((3u, (ushort)1), ReadSeasonSpan(indexBytes, 1));
        Assert.Equal((4u, (ushort)2), ReadSeasonSpan(indexBytes, 2));
        Assert.Equal((6u, (ushort)0), ReadSeasonSpan(indexBytes, 3));
        Assert.Equal(
        [
            (ushort)0,
            (ushort)2,
            (ushort)3,
            (ushort)1,
            (ushort)2,
            (ushort)4,
        ], ReadSeasonIndexes(recordBytes));

        using var manifest = JsonDocument.Parse(await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!));
        var root = manifest.RootElement;
        Assert.Equal(CampaignWorldRuntimeExporter.SeasonFormatVersion,
            root.GetProperty("version").GetInt32());
        var seasonLayer = root.GetProperty("seasons");
        Assert.Equal(CampaignWorldRuntimeExporter.SeasonIndexEntryName,
            seasonLayer.GetProperty("indexRecord").GetProperty("file").GetString());
        Assert.Equal(CampaignWorldRuntimeExporter.SeasonRecordsEntryName,
            seasonLayer.GetProperty("occurrenceRecord").GetProperty("file").GetString());
        Assert.Equal(6,
            seasonLayer.GetProperty("occurrenceRecord").GetProperty("recordCount").GetInt64());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(recordBytes)).ToLowerInvariant(),
            seasonLayer.GetProperty("occurrenceRecord").GetProperty("sha256").GetString());
        Assert.Equal(catalog.Definitions.Select(static value => value.Id).Order(StringComparer.Ordinal),
            seasonLayer.GetProperty("catalog").EnumerateArray()
                .Select(static entry => entry.GetProperty("id").GetString()));

        var manifestText = root.GetRawText();
        Assert.DoesNotContain("locked", manifestText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("priority", manifestText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("seasonSeed", manifestText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_KeepsVersionTwoTerrainAndResourceStreamsByteCompatible()
    {
        using var temporary = new TemporaryDirectory();
        var world = new CampaignWorld(CreateDefinition());
        world.Tiles.SetTile(1, 0, new(CampaignTileType.Hills, 375));
        var resources = new CampaignResourceMap(CreateDefinition());
        resources.Upsert(0, 0, new("gold", 51));
        resources.Upsert(1, 1, new("timber", 82));
        var seasons = new CampaignSeasonMap(CreateDefinition());
        seasons.Upsert(1, 1, new("winter"));
        var versionTwoPath = Path.Combine(temporary.Path, "v2.kworld");
        var versionThreePath = Path.Combine(temporary.Path, "v3.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, versionTwoPath);
        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, seasons, versionThreePath);

        using var versionTwo = ZipFile.OpenRead(versionTwoPath);
        using var versionThree = ZipFile.OpenRead(versionThreePath);
        foreach (var fileName in new[]
                 {
                     CampaignWorldRuntimeExporter.TileDataEntryName,
                     CampaignWorldRuntimeExporter.ResourceIndexEntryName,
                     CampaignWorldRuntimeExporter.ResourceRecordsEntryName,
                 })
        {
            Assert.Equal(
                await ReadEntryBytesAsync(versionTwo.GetEntry(fileName)!),
                await ReadEntryBytesAsync(versionThree.GetEntry(fileName)!));
        }
    }

    [Fact]
    public async Task Export_IsDeterministicAcrossCatalogInsertionMutationOrderAndLockState()
    {
        using var temporary = new TemporaryDirectory();
        var world = new CampaignWorld(CreateDefinition());
        var resources = new CampaignResourceMap(CreateDefinition());
        var firstCatalog = new CampaignSeasonCatalog(
            [CreateCustomSeason("wet-season"), CreateCustomSeason()]);
        var secondCatalog = new CampaignSeasonCatalog(
            [CreateCustomSeason(), CreateCustomSeason("wet-season")]);
        var first = new CampaignSeasonMap(CreateDefinition(), firstCatalog);
        var second = new CampaignSeasonMap(CreateDefinition(), secondCatalog);
        first.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("monsoon", Locked: true)),
            CampaignSeasonMutation.Upsert(0, 0, new("spring")),
            CampaignSeasonMutation.Upsert(1, 1, new("wet-season")),
        ]);
        second.Apply(
        [
            CampaignSeasonMutation.Upsert(1, 1, new("wet-season", Locked: true)),
            CampaignSeasonMutation.Upsert(0, 0, new("spring", Locked: true)),
            CampaignSeasonMutation.Upsert(0, 0, new("monsoon")),
        ]);
        var firstPath = Path.Combine(temporary.Path, "first.kworld");
        var secondPath = Path.Combine(temporary.Path, "second.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, first, firstPath);
        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, second, secondPath);

        Assert.Equal(await File.ReadAllBytesAsync(firstPath), await File.ReadAllBytesAsync(secondPath));
    }

    [Fact]
    public async Task Export_DefinitionMismatchPreservesExistingDestination()
    {
        using var temporary = new TemporaryDirectory();
        var packagePath = Path.Combine(temporary.Path, "existing.kworld");
        var original = new byte[] { 7, 6, 5, 4 };
        await File.WriteAllBytesAsync(packagePath, original);
        var mismatch = CampaignWorldDefinition.Create(
            15_000,
            10_000,
            5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CampaignWorldRuntimeExporter.ExportAsync(
                new CampaignWorld(CreateDefinition()),
                new CampaignResourceMap(CreateDefinition()),
                new CampaignSeasonMap(mismatch),
                packagePath));

        Assert.Equal(original, await File.ReadAllBytesAsync(packagePath));
        Assert.Equal([packagePath], Directory.EnumerateFiles(temporary.Path));
    }

    [Fact]
    public async Task Export_PreCancelledOperationPreservesDestinationAndCleansTemporaryFile()
    {
        using var temporary = new TemporaryDirectory();
        var packagePath = Path.Combine(temporary.Path, "existing.kworld");
        var original = new byte[] { 9, 8, 7 };
        await File.WriteAllBytesAsync(packagePath, original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CampaignWorldRuntimeExporter.ExportAsync(
                new CampaignWorld(CreateDefinition()),
                new CampaignResourceMap(CreateDefinition()),
                new CampaignSeasonMap(CreateDefinition()),
                packagePath,
                cancellation.Token));

        Assert.Equal(original, await File.ReadAllBytesAsync(packagePath));
        Assert.Equal([packagePath], Directory.EnumerateFiles(temporary.Path));
    }

    private static (uint First, ushort Count) ReadSeasonSpan(byte[] bytes, int tileIndex)
    {
        var offset = tileIndex * CampaignWorldRuntimeExporter.SeasonIndexRecordSizeBytes;
        return (
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 4, 2)));
    }

    private static ushort[] ReadSeasonIndexes(byte[] bytes) =>
        Enumerable.Range(0, bytes.Length / CampaignWorldRuntimeExporter.SeasonRecordSizeBytes)
            .Select(index => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(
                index * CampaignWorldRuntimeExporter.SeasonRecordSizeBytes,
                2)))
            .ToArray();

    private static CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            10_000,
            10_000,
            5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

    private static CampaignSeasonDefinition CreateCustomSeason(string id = "monsoon") =>
        new(
            id,
            id == "monsoon" ? "Monsoon" : "Wet Season",
            CampaignBuiltInSeason.Summer,
            "#467A9C",
            64,
            81,
            new CampaignSeasonRule(moisture: new(0.6, 1)));

    private static async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry)
    {
        await using var stream = entry.Open();
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"WorldEditorPixel-SeasonRuntime-{Guid.NewGuid():N}");
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
