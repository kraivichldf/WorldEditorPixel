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
    public async Task Export_WritesVersionThreeDenseSeasonContractWithoutAuthoringState()
    {
        using var temporary = new TemporaryDirectory();
        var world = new CampaignWorld(CreateDefinition());
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Forest, 240));
        var resources = new CampaignResourceMap(CreateDefinition());
        resources.Upsert(1, 1, new CampaignResourceOccurrence("timber", 73, Locked: true));
        var catalog = new CampaignSeasonCatalog([CreateCustomSeason()]);
        var seasons = new CampaignSeasonMap(CreateDefinition(), catalog);
        seasons.Apply(
        [
            new CampaignSeasonMutation(0, 0, new CampaignSeasonTile("winter", Locked: true)),
            new CampaignSeasonMutation(1, 0, new CampaignSeasonTile("monsoon")),
            new CampaignSeasonMutation(0, 1, new CampaignSeasonTile("spring", Locked: true)),
            new CampaignSeasonMutation(1, 1, new CampaignSeasonTile("autumn")),
        ]);
        var packagePath = Path.Combine(temporary.Path, "runtime-v3.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(
            world,
            resources,
            seasons,
            packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        Assert.Equal(
        [
            CampaignWorldRuntimeExporter.TileDataEntryName,
            CampaignWorldRuntimeExporter.ResourceIndexEntryName,
            CampaignWorldRuntimeExporter.ResourceRecordsEntryName,
            CampaignWorldRuntimeExporter.SeasonTilesEntryName,
            CampaignWorldRuntimeExporter.ManifestEntryName,
        ], archive.Entries.Select(static entry => entry.FullName));
        Assert.All(
            archive.Entries,
            static entry => Assert.Equal(new DateTime(1980, 1, 1), entry.LastWriteTime.DateTime));

        var seasonBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.SeasonTilesEntryName)!);
        Assert.Equal(8, seasonBytes.Length);
        Assert.Equal(catalog.GetIndex("winter"), ReadSeasonIndex(seasonBytes, 0));
        Assert.Equal(catalog.GetIndex("monsoon"), ReadSeasonIndex(seasonBytes, 1));
        Assert.Equal(catalog.GetIndex("spring"), ReadSeasonIndex(seasonBytes, 2));
        Assert.Equal(catalog.GetIndex("autumn"), ReadSeasonIndex(seasonBytes, 3));

        using var manifest = JsonDocument.Parse(await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!));
        var root = manifest.RootElement;
        Assert.Equal(
            CampaignWorldRuntimeExporter.SeasonFormatVersion,
            root.GetProperty("version").GetInt32());
        var seasonLayer = root.GetProperty("seasons");
        var runtimeCatalog = seasonLayer.GetProperty("catalog").EnumerateArray().ToArray();
        Assert.Equal(catalog.Definitions.Select(static value => value.Id),
            runtimeCatalog.Select(static entry => entry.GetProperty("id").GetString()));
        Assert.Equal(
            Enumerable.Range(0, runtimeCatalog.Length),
            runtimeCatalog.Select(static entry => entry.GetProperty("index").GetInt32()));
        var monsoon = runtimeCatalog.Single(static entry =>
            entry.GetProperty("id").GetString() == "monsoon");
        Assert.Equal(
        [
            "index",
            "id",
            "name",
            "builtIn",
            "fallback",
            "color",
            "tintStrengthPercent",
            "effectIntensityPercent",
        ], monsoon.EnumerateObject().Select(static property => property.Name));
        Assert.False(monsoon.GetProperty("builtIn").GetBoolean());
        Assert.Equal("summer", monsoon.GetProperty("fallback").GetString());
        Assert.Equal("#467A9C", monsoon.GetProperty("color").GetString());
        Assert.Equal(64, monsoon.GetProperty("tintStrengthPercent").GetInt32());
        Assert.Equal(81, monsoon.GetProperty("effectIntensityPercent").GetInt32());

        var layout = seasonLayer.GetProperty("tileRecord");
        Assert.Equal(
            CampaignWorldRuntimeExporter.SeasonTilesEntryName,
            layout.GetProperty("file").GetString());
        Assert.Equal(
            CampaignWorldRuntimeExporter.SeasonRecordSizeBytes,
            layout.GetProperty("recordSizeBytes").GetInt32());
        Assert.Equal(4, layout.GetProperty("recordCount").GetInt64());
        Assert.Equal(seasonBytes.LongLength, layout.GetProperty("byteLength").GetInt64());
        Assert.Equal("littleEndian", layout.GetProperty("byteOrder").GetString());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(seasonBytes)).ToLowerInvariant(),
            layout.GetProperty("sha256").GetString());
        var field = Assert.Single(layout.GetProperty("fields").EnumerateArray());
        Assert.Equal("seasonCatalogIndex", field.GetProperty("name").GetString());
        Assert.Equal(0, field.GetProperty("offsetBytes").GetInt32());
        Assert.Equal("uint16", field.GetProperty("storage").GetString());
        Assert.Equal("seasons.catalog", field.GetProperty("mapping").GetString());

        var manifestText = root.GetRawText();
        Assert.DoesNotContain("locked", manifestText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("priorityIds", manifestText, StringComparison.Ordinal);
        Assert.DoesNotContain("seasonSeed", manifestText, StringComparison.Ordinal);
        Assert.DoesNotContain("temperatureCelsius", manifestText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_KeepsAllVersionTwoBinaryStreamsByteCompatible()
    {
        using var temporary = new TemporaryDirectory();
        var world = new CampaignWorld(CreateDefinition());
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Hills, 375));
        var resources = new CampaignResourceMap(CreateDefinition());
        resources.Apply(
        [
            CampaignResourceMutation.Upsert(
                0,
                0,
                new CampaignResourceOccurrence("gold", 51)),
            CampaignResourceMutation.Upsert(
                1,
                1,
                new CampaignResourceOccurrence("timber", 82)),
        ]);
        var seasons = new CampaignSeasonMap(CreateDefinition());
        seasons.Paint(1, 1, "winter");
        var versionTwoPath = Path.Combine(temporary.Path, "v2.kworld");
        var versionThreePath = Path.Combine(temporary.Path, "v3.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(
            world,
            resources,
            versionTwoPath);
        await CampaignWorldRuntimeExporter.ExportAsync(
            world,
            resources,
            seasons,
            versionThreePath);

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
    public async Task Export_IsDeterministicAcrossCatalogConstructionAndLockState()
    {
        using var temporary = new TemporaryDirectory();
        var world = new CampaignWorld(CreateDefinition());
        var resources = new CampaignResourceMap(CreateDefinition());
        var firstCatalog = new CampaignSeasonCatalog(
        [
            CreateCustomSeason("wet-season"),
            CreateCustomSeason(),
        ]);
        var secondCatalog = new CampaignSeasonCatalog(
        [
            CreateCustomSeason(),
            CreateCustomSeason("wet-season"),
        ]);
        var first = new CampaignSeasonMap(CreateDefinition(), firstCatalog);
        var second = new CampaignSeasonMap(CreateDefinition(), secondCatalog);
        first.Apply(
        [
            new CampaignSeasonMutation(0, 0, new CampaignSeasonTile("monsoon", true)),
            new CampaignSeasonMutation(1, 1, new CampaignSeasonTile("wet-season")),
        ]);
        second.Apply(
        [
            new CampaignSeasonMutation(1, 1, new CampaignSeasonTile("wet-season", true)),
            new CampaignSeasonMutation(0, 0, new CampaignSeasonTile("monsoon")),
        ]);
        var firstPath = Path.Combine(temporary.Path, "first.kworld");
        var secondPath = Path.Combine(temporary.Path, "second.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, first, firstPath);
        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, second, secondPath);

        Assert.Equal(
            await File.ReadAllBytesAsync(firstPath),
            await File.ReadAllBytesAsync(secondPath));
    }

    [Fact]
    public async Task Export_DefinitionMismatchPreservesExistingDestination()
    {
        using var temporary = new TemporaryDirectory();
        var packagePath = Path.Combine(temporary.Path, "existing.kworld");
        var original = new byte[] { 7, 6, 5, 4 };
        await File.WriteAllBytesAsync(packagePath, original);
        var mismatch = CampaignWorldDefinition.Create(
            worldWidthMeters: 15_000,
            worldHeightMeters: 10_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CampaignWorldRuntimeExporter.ExportAsync(
                new CampaignWorld(CreateDefinition()),
                new CampaignResourceMap(CreateDefinition()),
                new CampaignSeasonMap(mismatch),
                packagePath));

        Assert.Contains("value-equal", exception.Message, StringComparison.Ordinal);
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

    private static CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: 10_000,
            worldHeightMeters: 10_000,
            campaignTileSizeMeters: 5_000,
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
            new CampaignSeasonRule(
                moisture: new CampaignSeasonRange(0.6, 1)));

    private static ushort ReadSeasonIndex(byte[] bytes, int recordIndex) =>
        BinaryPrimitives.ReadUInt16LittleEndian(
            bytes.AsSpan(
                recordIndex * CampaignWorldRuntimeExporter.SeasonRecordSizeBytes,
                sizeof(ushort)));

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
                $"KingdomCampaignSeasonRuntimeTests-{Guid.NewGuid():N}");
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
