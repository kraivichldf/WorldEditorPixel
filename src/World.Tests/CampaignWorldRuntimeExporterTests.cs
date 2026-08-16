using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Serialization;

namespace Kingdom.World.Tests;

public sealed class CampaignWorldRuntimeExporterTests
{
    [Fact]
    public async Task Export_WritesSelfDescribingDenseLittleEndianPackage()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        var farmland = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#91A85A",
            GenerationSharePercent: 30);
        world.Tiles.SetCustomTerrainDefinitions([farmland]);
        world.Tiles.SetTiles(
        [
            new CampaignTileEntry(0, 0, new CampaignTileData(CampaignTileType.Sea, -200)),
            new CampaignTileEntry(1, 0, new CampaignTileData(CampaignTileType.Plains, 275, "farmland")),
            new CampaignTileEntry(1, 1, new CampaignTileData(CampaignTileType.Mountain, 1_750)),
        ]);
        var packagePath = Path.Combine(temporary.Path, "runtime.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        Assert.Equal(2, archive.Entries.Count);
        var tileBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.TileDataEntryName)!);
        var manifestBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!);
        using var manifest = JsonDocument.Parse(manifestBytes);
        var root = manifest.RootElement;

        Assert.Equal(CampaignWorldRuntimeExporter.FormatIdentifier, root.GetProperty("format").GetString());
        Assert.Equal(CampaignWorldRuntimeExporter.FormatVersion, root.GetProperty("version").GetInt32());
        Assert.Equal(10_000, root.GetProperty("world").GetProperty("widthMeters").GetInt64());
        Assert.Equal(2, root.GetProperty("grid").GetProperty("tilesX").GetInt32());
        Assert.Equal(2, root.GetProperty("grid").GetProperty("tilesY").GetInt32());
        Assert.Equal("northWest", root.GetProperty("grid").GetProperty("origin").GetString());
        Assert.Equal("rowMajorYThenX", root.GetProperty("grid").GetProperty("tileOrder").GetString());
        Assert.Equal(4, root.GetProperty("tileRecord").GetProperty("recordSizeBytes").GetInt32());
        Assert.Equal(16, root.GetProperty("tileRecord").GetProperty("byteLength").GetInt64());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(tileBytes)).ToLowerInvariant(),
            root.GetProperty("tileRecord").GetProperty("sha256").GetString());

        var customTerrain = Assert.Single(root.GetProperty("customTerrain").EnumerateArray());
        Assert.Equal(0, customTerrain.GetProperty("index").GetByte());
        Assert.Equal("farmland", customTerrain.GetProperty("id").GetString());
        Assert.Equal("#91A85A", customTerrain.GetProperty("color").GetString());
        Assert.DoesNotContain(
            root.GetProperty("tileTypes").EnumerateArray(),
            type => type.GetProperty("value").GetByte() == (byte)CampaignTileType.Coastal);

        Assert.Equal(16, tileBytes.Length);
        AssertTileRecord(tileBytes, 0, CampaignTileType.Sea, byte.MaxValue, -200);
        AssertTileRecord(tileBytes, 1, CampaignTileType.Plains, 0, 275);
        AssertTileRecord(tileBytes, 2, CampaignTileType.Unassigned, byte.MaxValue, 0);
        AssertTileRecord(tileBytes, 3, CampaignTileType.Mountain, byte.MaxValue, 1_750);
    }

    [Fact]
    public async Task Export_SameWorldProducesIdenticalPackageBytes()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Forest, 320));
        var firstPath = Path.Combine(temporary.Path, "first.kworld");
        var secondPath = Path.Combine(temporary.Path, "second.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, firstPath);
        await CampaignWorldRuntimeExporter.ExportAsync(world, secondPath);

        Assert.Equal(await File.ReadAllBytesAsync(firstPath), await File.ReadAllBytesAsync(secondPath));
    }

    [Fact]
    public async Task Export_PreservesLargeRiverTypeAndManifestMapping()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.LargeRiver, 45));
        var packagePath = Path.Combine(temporary.Path, "large-river.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        var tileBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.TileDataEntryName)!);
        var manifestBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!);
        using var manifest = JsonDocument.Parse(manifestBytes);

        AssertTileRecord(tileBytes, 0, CampaignTileType.LargeRiver, byte.MaxValue, 45);
        Assert.Contains(
            manifest.RootElement.GetProperty("tileTypes").EnumerateArray(),
            type => type.GetProperty("value").GetByte() == (byte)CampaignTileType.LargeRiver &&
                    type.GetProperty("name").GetString() == "largeRiver");
    }

    [Fact]
    public async Task Export_PreservesRiverJunctionTypeAndManifestMapping()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.RiverJunction, 45));
        var packagePath = Path.Combine(temporary.Path, "river-junction.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        var tileBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.TileDataEntryName)!);
        var manifestBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!);
        using var manifest = JsonDocument.Parse(manifestBytes);

        AssertTileRecord(tileBytes, 0, CampaignTileType.RiverJunction, byte.MaxValue, 45);
        Assert.Contains(
            manifest.RootElement.GetProperty("tileTypes").EnumerateArray(),
            type => type.GetProperty("value").GetByte() == (byte)CampaignTileType.RiverJunction &&
                    type.GetProperty("name").GetString() == "riverJunction");
    }

    [Fact]
    public async Task Export_PreservesAppendedSteppeTypeAndManifestMapping()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Steppe, 85));
        var packagePath = Path.Combine(temporary.Path, "steppe.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        var tileBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.TileDataEntryName)!);
        var manifestBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!);
        using var manifest = JsonDocument.Parse(manifestBytes);

        Assert.Equal(15, (byte)CampaignTileType.Steppe);
        AssertTileRecord(tileBytes, 0, CampaignTileType.Steppe, byte.MaxValue, 85);
        Assert.Contains(
            manifest.RootElement.GetProperty("tileTypes").EnumerateArray(),
            type => type.GetProperty("value").GetByte() == (byte)CampaignTileType.Steppe &&
                    type.GetProperty("name").GetString() == "steppe");
    }

    [Fact]
    public async Task Export_RejectsAnAmbiguousFileExtension()
    {
        using var temporary = new TemporaryDirectory();
        var path = Path.Combine(temporary.Path, "runtime.raw");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CampaignWorldRuntimeExporter.ExportAsync(CreateWorld(), path));

        Assert.Contains(CampaignWorldRuntimeExporter.PackageExtension, exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Export_PreCancelledVersionOnePreservesDestinationAndLeavesNoTemporaryPackage()
    {
        using var temporary = new TemporaryDirectory();
        var packagePath = Path.Combine(temporary.Path, "existing-v1.kworld");
        var originalBytes = new byte[] { 7, 6, 5, 4 };
        await File.WriteAllBytesAsync(packagePath, originalBytes);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CampaignWorldRuntimeExporter.ExportAsync(
                CreateWorld(),
                packagePath,
                cancellation.Token));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(packagePath));
        Assert.Equal([packagePath], Directory.EnumerateFiles(temporary.Path));
    }

    [Fact]
    public async Task ExportWithResources_WritesVersionTwoBinaryContractAndChecksums()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        world.Tiles.SetTiles(
        [
            new CampaignTileEntry(0, 0, new CampaignTileData(CampaignTileType.Forest, 220)),
            new CampaignTileEntry(0, 1, new CampaignTileData(CampaignTileType.Hills, 480)),
        ]);
        var amber = CreateCustomResource("amber-resin", "Amber Resin", CampaignResourceCategory.Finite);
        var resources = new CampaignResourceMap(
            CreateDefinition(),
            new CampaignResourceCatalog([amber]));
        resources.Apply(
        [
            CampaignResourceMutation.Upsert(1, 1, new CampaignResourceOccurrence("fish", 61)),
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("timber", 54)),
            CampaignResourceMutation.Upsert(0, 1, new CampaignResourceOccurrence("amber-resin", 87)),
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("gold", 33, Locked: true)),
        ]);
        var packagePath = Path.Combine(temporary.Path, "runtime-v2.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        Assert.Equal(
        [
            CampaignWorldRuntimeExporter.TileDataEntryName,
            CampaignWorldRuntimeExporter.ResourceIndexEntryName,
            CampaignWorldRuntimeExporter.ResourceRecordsEntryName,
            CampaignWorldRuntimeExporter.ManifestEntryName,
        ], archive.Entries.Select(static entry => entry.FullName));
        Assert.All(
            archive.Entries,
            static entry => Assert.Equal(new DateTime(1980, 1, 1), entry.LastWriteTime.DateTime));

        var tileBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.TileDataEntryName)!);
        var indexBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ResourceIndexEntryName)!);
        var recordBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ResourceRecordsEntryName)!);
        var manifestBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!);
        using var manifest = JsonDocument.Parse(manifestBytes);
        var root = manifest.RootElement;
        var resourceLayer = root.GetProperty("resources");
        var catalog = resourceLayer.GetProperty("catalog").EnumerateArray().ToArray();

        Assert.Equal(CampaignWorldRuntimeExporter.ResourceFormatVersion, root.GetProperty("version").GetInt32());
        Assert.Equal(17, catalog.Length);
        Assert.Equal(
            catalog.Select(static entry => entry.GetProperty("id").GetString()).Order(StringComparer.Ordinal),
            catalog.Select(static entry => entry.GetProperty("id").GetString()));
        Assert.Equal(Enumerable.Range(0, catalog.Length), catalog.Select(static entry => entry.GetProperty("index").GetInt32()));

        var amberCatalog = catalog.Single(static entry =>
            entry.GetProperty("id").GetString() == "amber-resin");
        Assert.Equal(
            ["index", "id", "name", "category", "builtIn"],
            amberCatalog.EnumerateObject().Select(static property => property.Name));
        Assert.Equal("Amber Resin", amberCatalog.GetProperty("name").GetString());
        Assert.Equal("finite", amberCatalog.GetProperty("category").GetString());
        Assert.False(amberCatalog.GetProperty("builtIn").GetBoolean());
        var timberCatalog = catalog.Single(static entry =>
            entry.GetProperty("id").GetString() == "timber");
        Assert.Equal("renewable", timberCatalog.GetProperty("category").GetString());
        Assert.True(timberCatalog.GetProperty("builtIn").GetBoolean());

        Assert.Equal(16, tileBytes.Length);
        Assert.Equal(32, indexBytes.Length);
        Assert.Equal(16, recordBytes.Length);
        AssertResourceIndexRecord(indexBytes, 0, firstRecordIndex: 0, recordCount: 2);
        AssertResourceIndexRecord(indexBytes, 1, firstRecordIndex: 2, recordCount: 0);
        AssertResourceIndexRecord(indexBytes, 2, firstRecordIndex: 2, recordCount: 1);
        AssertResourceIndexRecord(indexBytes, 3, firstRecordIndex: 3, recordCount: 1);
        AssertResourceRecord(recordBytes, 0, GetCatalogIndex(catalog, "gold"), potential: 33);
        AssertResourceRecord(recordBytes, 1, GetCatalogIndex(catalog, "timber"), potential: 54);
        AssertResourceRecord(recordBytes, 2, GetCatalogIndex(catalog, "amber-resin"), potential: 87);
        AssertResourceRecord(recordBytes, 3, GetCatalogIndex(catalog, "fish"), potential: 61);

        var indexLayout = resourceLayer.GetProperty("indexRecord");
        AssertBinaryLayout(
            indexLayout,
            CampaignWorldRuntimeExporter.ResourceIndexEntryName,
            CampaignWorldRuntimeExporter.ResourceIndexRecordSizeBytes,
            recordCount: 4,
            indexBytes);
        Assert.Equal(
            [0, 4, 6],
            indexLayout.GetProperty("fields").EnumerateArray()
                .Select(static field => field.GetProperty("offsetBytes").GetInt32()));
        Assert.Equal(
            0,
            indexLayout.GetProperty("fields")[2].GetProperty("constantValue").GetInt32());

        var occurrenceLayout = resourceLayer.GetProperty("occurrenceRecord");
        AssertBinaryLayout(
            occurrenceLayout,
            CampaignWorldRuntimeExporter.ResourceRecordsEntryName,
            CampaignWorldRuntimeExporter.ResourceRecordSizeBytes,
            recordCount: 4,
            recordBytes);
        Assert.Equal(
            [0, 2, 3],
            occurrenceLayout.GetProperty("fields").EnumerateArray()
                .Select(static field => field.GetProperty("offsetBytes").GetInt32()));
        Assert.Equal(
            0,
            occurrenceLayout.GetProperty("fields")[2].GetProperty("constantValue").GetInt32());
    }

    [Fact]
    public async Task ExportWithResources_EmptyMapWritesDenseZeroIndexAndEmptyRecords()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        var resources = new CampaignResourceMap(CreateDefinition());
        var packagePath = Path.Combine(temporary.Path, "empty-resources.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, packagePath);

        using var archive = ZipFile.OpenRead(packagePath);
        var indexBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ResourceIndexEntryName)!);
        var recordBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ResourceRecordsEntryName)!);
        var manifestBytes = await ReadEntryBytesAsync(
            archive.GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!);
        using var manifest = JsonDocument.Parse(manifestBytes);

        Assert.Equal(32, indexBytes.Length);
        for (var recordIndex = 0; recordIndex < 4; recordIndex++)
        {
            AssertResourceIndexRecord(indexBytes, recordIndex, firstRecordIndex: 0, recordCount: 0);
        }

        Assert.Empty(recordBytes);
        var resourceLayer = manifest.RootElement.GetProperty("resources");
        Assert.Equal(4, resourceLayer.GetProperty("indexRecord").GetProperty("recordCount").GetInt64());
        Assert.Equal(0, resourceLayer.GetProperty("occurrenceRecord").GetProperty("recordCount").GetInt64());
        Assert.Equal(0, resourceLayer.GetProperty("occurrenceRecord").GetProperty("byteLength").GetInt64());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant(),
            resourceLayer.GetProperty("occurrenceRecord").GetProperty("sha256").GetString());
    }

    [Fact]
    public async Task ExportWithResources_IsDeterministicAcrossInsertionOrderAndLockState()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        var amber = CreateCustomResource("amber-resin", "Amber Resin", CampaignResourceCategory.Finite);
        var herbs = CreateCustomResource("medicinal-herbs", "Medicinal Herbs", CampaignResourceCategory.Renewable);
        var first = new CampaignResourceMap(
            CreateDefinition(),
            new CampaignResourceCatalog([herbs, amber]));
        var second = new CampaignResourceMap(
            CreateDefinition(),
            new CampaignResourceCatalog([amber, herbs]));
        first.Apply(
        [
            CampaignResourceMutation.Upsert(1, 1, new CampaignResourceOccurrence("medicinal-herbs", 70)),
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("timber", 45, Locked: true)),
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("amber-resin", 82)),
        ]);
        second.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("amber-resin", 82, Locked: true)),
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("timber", 45)),
            CampaignResourceMutation.Upsert(1, 1, new CampaignResourceOccurrence("medicinal-herbs", 70, Locked: true)),
        ]);
        var firstPath = Path.Combine(temporary.Path, "first.kworld");
        var secondPath = Path.Combine(temporary.Path, "second.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, first, firstPath);
        await CampaignWorldRuntimeExporter.ExportAsync(world, second, secondPath);

        Assert.Equal(await File.ReadAllBytesAsync(firstPath), await File.ReadAllBytesAsync(secondPath));
    }

    [Fact]
    public async Task ExportWithResources_KeepsVersionOnePackageAndTerrainBytesCompatible()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Forest, 320));
        var resources = new CampaignResourceMap(CreateDefinition());
        resources.Upsert(1, 0, new CampaignResourceOccurrence("timber", 75));
        var versionOnePath = Path.Combine(temporary.Path, "version-one.kworld");
        var versionTwoPath = Path.Combine(temporary.Path, "version-two.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, versionOnePath);
        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, versionTwoPath);

        using var versionOne = ZipFile.OpenRead(versionOnePath);
        using var versionTwo = ZipFile.OpenRead(versionTwoPath);
        Assert.Equal(
            [
                CampaignWorldRuntimeExporter.TileDataEntryName,
                CampaignWorldRuntimeExporter.ManifestEntryName,
            ], versionOne.Entries.Select(static entry => entry.FullName));
        Assert.Equal(
            await ReadEntryBytesAsync(versionOne.GetEntry(CampaignWorldRuntimeExporter.TileDataEntryName)!),
            await ReadEntryBytesAsync(versionTwo.GetEntry(CampaignWorldRuntimeExporter.TileDataEntryName)!));
        using var manifest = JsonDocument.Parse(await ReadEntryBytesAsync(
            versionOne.GetEntry(CampaignWorldRuntimeExporter.ManifestEntryName)!));
        Assert.Equal(CampaignWorldRuntimeExporter.FormatVersion, manifest.RootElement.GetProperty("version").GetInt32());
        Assert.False(manifest.RootElement.TryGetProperty("resources", out _));
    }

    [Fact]
    public async Task ExportWithResources_AcceptsAValueEqualDefinitionInstance()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        var resources = new CampaignResourceMap(CreateDefinition());
        var packagePath = Path.Combine(temporary.Path, "value-equal.kworld");

        await CampaignWorldRuntimeExporter.ExportAsync(world, resources, packagePath);

        Assert.True(File.Exists(packagePath));
    }

    [Fact]
    public async Task ExportWithResources_DefinitionMismatchPreservesExistingDestination()
    {
        using var temporary = new TemporaryDirectory();
        var packagePath = Path.Combine(temporary.Path, "existing.kworld");
        var originalBytes = new byte[] { 11, 22, 33, 44 };
        await File.WriteAllBytesAsync(packagePath, originalBytes);
        var mismatchedResources = new CampaignResourceMap(CampaignWorldDefinition.Create(
            worldWidthMeters: 15_000,
            worldHeightMeters: 10_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CampaignWorldRuntimeExporter.ExportAsync(CreateWorld(), mismatchedResources, packagePath));

        Assert.Contains("value-equal", exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(packagePath));
        Assert.Equal([packagePath], Directory.EnumerateFiles(temporary.Path));
    }

    [Fact]
    public async Task ExportWithResources_PreCancelledExportPreservesDestinationAndLeavesNoTemporaryPackage()
    {
        using var temporary = new TemporaryDirectory();
        var packagePath = Path.Combine(temporary.Path, "existing.kworld");
        var originalBytes = new byte[] { 99, 88, 77 };
        await File.WriteAllBytesAsync(packagePath, originalBytes);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CampaignWorldRuntimeExporter.ExportAsync(
                CreateWorld(),
                new CampaignResourceMap(CreateDefinition()),
                packagePath,
                cancellation.Token));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(packagePath));
        Assert.Equal([packagePath], Directory.EnumerateFiles(temporary.Path));
    }

    [Fact]
    public async Task ExportWithResources_CheckedByteLengthsRejectOverflowBeforeWriting()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: int.MaxValue,
            worldHeightMeters: int.MaxValue,
            campaignTileSizeMeters: 1,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var packagePath = Path.Combine(temporary.Path, "overflow.kworld");

        await Assert.ThrowsAsync<OverflowException>(() =>
            CampaignWorldRuntimeExporter.ExportAsync(
                new CampaignWorld(definition),
                new CampaignResourceMap(definition),
                packagePath));

        Assert.False(File.Exists(packagePath));
        Assert.Empty(Directory.EnumerateFiles(temporary.Path));
    }

    private static CampaignWorld CreateWorld() => new(CreateDefinition());

    private static CampaignWorldDefinition CreateDefinition() => CampaignWorldDefinition.Create(
        worldWidthMeters: 10_000,
        worldHeightMeters: 10_000,
        campaignTileSizeMeters: 5_000,
        seaLevelMeters: 0,
        minimumHeightMeters: -1_000,
        maximumHeightMeters: 6_000);

    private static CampaignResourceDefinition CreateCustomResource(
        string id,
        string name,
        CampaignResourceCategory category) =>
        new(
            id,
            name,
            category,
            CampaignResourceDistributionProfile.Field,
            CampaignResourceMedium.Land,
            "resource",
            "#735A91",
            mapPriority: 50,
            coveragePercent: 10,
            CampaignResourceRichness.Balanced,
            CampaignResourceConcentration.Balanced);

    private static async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry)
    {
        await using var stream = entry.Open();
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }

    private static void AssertTileRecord(
        byte[] bytes,
        int recordIndex,
        CampaignTileType expectedType,
        byte expectedCustomTerrainIndex,
        short expectedHeight)
    {
        var offset = recordIndex * CampaignWorldRuntimeExporter.TileRecordSizeBytes;
        Assert.Equal((byte)expectedType, bytes[offset]);
        Assert.Equal(expectedCustomTerrainIndex, bytes[offset + 1]);
        Assert.Equal(expectedHeight, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset + 2, 2)));
    }

    private static ushort GetCatalogIndex(IEnumerable<JsonElement> catalog, string resourceId) =>
        catalog.Single(entry => entry.GetProperty("id").GetString() == resourceId)
            .GetProperty("index")
            .GetUInt16();

    private static void AssertResourceIndexRecord(
        byte[] bytes,
        int recordIndex,
        uint firstRecordIndex,
        ushort recordCount)
    {
        var offset = recordIndex * CampaignWorldRuntimeExporter.ResourceIndexRecordSizeBytes;
        Assert.Equal(
            firstRecordIndex,
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint))));
        Assert.Equal(
            recordCount,
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + sizeof(uint), sizeof(ushort))));
        Assert.Equal(
            0,
            BinaryPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(offset + sizeof(uint) + sizeof(ushort), sizeof(ushort))));
    }

    private static void AssertResourceRecord(
        byte[] bytes,
        int recordIndex,
        ushort catalogIndex,
        byte potential)
    {
        var offset = recordIndex * CampaignWorldRuntimeExporter.ResourceRecordSizeBytes;
        Assert.Equal(
            catalogIndex,
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort))));
        Assert.Equal(potential, bytes[offset + sizeof(ushort)]);
        Assert.Equal(0, bytes[offset + sizeof(ushort) + sizeof(byte)]);
    }

    private static void AssertBinaryLayout(
        JsonElement layout,
        string expectedFile,
        int expectedRecordSize,
        long recordCount,
        byte[] streamBytes)
    {
        Assert.Equal(expectedFile, layout.GetProperty("file").GetString());
        Assert.Equal(expectedRecordSize, layout.GetProperty("recordSizeBytes").GetInt32());
        Assert.Equal(recordCount, layout.GetProperty("recordCount").GetInt64());
        Assert.Equal(streamBytes.LongLength, layout.GetProperty("byteLength").GetInt64());
        Assert.Equal("littleEndian", layout.GetProperty("byteOrder").GetString());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(streamBytes)).ToLowerInvariant(),
            layout.GetProperty("sha256").GetString());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"KingdomRuntimeExportTests-{Guid.NewGuid():N}");
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
