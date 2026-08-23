using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Serialization;
using Kingdom.World.Core.Terrain;

namespace Kingdom.World.Tests;

public sealed class CampaignWorldSerializationTests
{
    [Fact]
    public async Task VersionTwoRoundtrip_PreservesDefinitionTypeAndCentreHeight()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld();
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Sea, -200));
        world.Tiles.SetTile(1, 1, new CampaignTileData(CampaignTileType.Mountain, 1_750));

        await CampaignWorldProjectSerializer.SaveAsync(world, temporary.Path);
        var loaded = await CampaignWorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.False(loaded.WasConvertedFromLegacy);
        Assert.Equal(world.Definition, loaded.World.Definition);
        Assert.Equal(new CampaignTileData(CampaignTileType.Sea, -200), loaded.World.Tiles.GetTile(0, 0));
        Assert.Equal(new CampaignTileData(CampaignTileType.Mountain, 1_750), loaded.World.Tiles.GetTile(1, 1));
        Assert.Contains("\"version\": 2", await File.ReadAllTextAsync(
            Path.Combine(temporary.Path, CampaignWorldProjectSerializer.ManifestFileName)));
    }

    [Fact]
    public async Task VersionTwoLoad_RejectsAProjectAboveTheSharedEditableTileLimit()
    {
        using var temporary = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporary.Path, CampaignWorldProjectSerializer.ManifestFileName),
            """
            {
              "version": 2,
              "worldWidthMeters": 501000,
              "worldHeightMeters": 500000,
              "campaignTileSizeMeters": 1000,
              "seaLevelMeters": 0,
              "minimumHeightMeters": -1000,
              "maximumHeightMeters": 6000,
              "defaultTileHeightMeters": 0
            }
            """);

        var exception = await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignWorldProjectSerializer.LoadAsync(temporary.Path));

        Assert.Contains("250,000 editable tiles", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LegacyImport_PreflightsTheSharedEditableTileLimitBeforeReadingChunks()
    {
        using var temporary = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporary.Path, CampaignWorldProjectSerializer.ManifestFileName),
            """
            {
              "version": 1,
              "worldWidthMeters": 501000,
              "worldHeightMeters": 500000,
              "heightSamplesX": 502,
              "heightSamplesY": 501,
              "heightSampleSpacingMeters": 1000,
              "campaignTileSizeMeters": 1000,
              "seaLevelMeters": 0,
              "minimumElevationMeters": -1000,
              "maximumElevationMeters": 6000,
              "initialElevationMeters": 0,
              "chunkSize": 256
            }
            """);
        var chunks = Path.Combine(temporary.Path, WorldProjectSerializer.ChunkDirectoryName);
        Directory.CreateDirectory(chunks);
        await File.WriteAllBytesAsync(Path.Combine(chunks, "0_0.bin"), [0x00]);

        var exception = await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignWorldProjectSerializer.LoadAsync(temporary.Path));

        Assert.Contains("250,000 editable tiles", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Chunk 0_0.bin", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VersionTwoRoundtrip_PreservesOriginalLandWaterShoreAndRiverTypes()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld(5, 2);
        world.Tiles.SetTiles(
        [
            new CampaignTileEntry(0, 0, new CampaignTileData(CampaignTileType.Sea, -100)),
            new CampaignTileEntry(1, 0, new CampaignTileData(CampaignTileType.Lake, 15)),
            new CampaignTileEntry(2, 0, new CampaignTileData(CampaignTileType.Beach, 20)),
            new CampaignTileEntry(3, 0, new CampaignTileData(CampaignTileType.Forest, 25)),
            new CampaignTileEntry(4, 0, new CampaignTileData(CampaignTileType.Steppe, 60)),
            new CampaignTileEntry(0, 1, new CampaignTileData(CampaignTileType.Cliff, 200)),
            new CampaignTileEntry(1, 1, new CampaignTileData(CampaignTileType.River, 30)),
            new CampaignTileEntry(2, 1, new CampaignTileData(CampaignTileType.LargeRiver, 35)),
            new CampaignTileEntry(3, 1, new CampaignTileData(CampaignTileType.River, 40)),
            new CampaignTileEntry(4, 1, new CampaignTileData(CampaignTileType.Desert, 80)),
        ]);

        await CampaignWorldProjectSerializer.SaveAsync(world, temporary.Path);
        var loaded = await CampaignWorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.Equal(CampaignTileType.Sea, loaded.World.Tiles.GetTile(0, 0).Type);
        Assert.Equal(CampaignTileType.Lake, loaded.World.Tiles.GetTile(1, 0).Type);
        Assert.Equal(CampaignTileType.Beach, loaded.World.Tiles.GetTile(2, 0).Type);
        Assert.Equal(CampaignTileType.Forest, loaded.World.Tiles.GetTile(3, 0).Type);
        Assert.Equal(CampaignTileType.Steppe, loaded.World.Tiles.GetTile(4, 0).Type);
        Assert.Equal(CampaignTileType.Cliff, loaded.World.Tiles.GetTile(0, 1).Type);
        Assert.Equal(CampaignTileType.River, loaded.World.Tiles.GetTile(1, 1).Type);
        Assert.Equal(CampaignTileType.LargeRiver, loaded.World.Tiles.GetTile(2, 1).Type);
        Assert.Equal(
            RiverConnections.East | RiverConnections.West,
            loaded.World.Tiles.GetRiverConnections(2, 1));
        Assert.Equal(CampaignTileType.Desert, loaded.World.Tiles.GetTile(4, 1).Type);
        Assert.Contains(
            "\"type\": \"largeRiver\"",
            await File.ReadAllTextAsync(Path.Combine(
                temporary.Path,
                CampaignWorldProjectSerializer.CampaignTileFileName)));
        Assert.Contains(
            "\"type\": \"steppe\"",
            await File.ReadAllTextAsync(Path.Combine(
                temporary.Path,
                CampaignWorldProjectSerializer.CampaignTileFileName)));
    }

    [Fact]
    public async Task VersionTwoRoundtrip_PreservesCustomLandDefinitionsAndTileIdentity()
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
        world.Tiles.SetTile(
            1,
            1,
            new CampaignTileData(CampaignTileType.Plains, 275, "farmland"));

        await CampaignWorldProjectSerializer.SaveAsync(world, temporary.Path);
        var loaded = await CampaignWorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.Equal([farmland], loaded.World.Tiles.CustomTerrainDefinitions);
        Assert.Equal(
            new CampaignTileData(CampaignTileType.Plains, 275, "farmland"),
            loaded.World.Tiles.GetTile(1, 1));
        Assert.Contains(
            "\"customTerrainId\": \"farmland\"",
            await File.ReadAllTextAsync(Path.Combine(
                temporary.Path,
                CampaignWorldProjectSerializer.CampaignTileFileName)));
        Assert.Contains(
            "\"id\": \"farmland\"",
            await File.ReadAllTextAsync(Path.Combine(
                temporary.Path,
                CampaignWorldProjectSerializer.CustomTerrainFileName)));
    }

    [Fact]
    public async Task VersionTwoRoundtrip_PreservesExplicitRiverJunction()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld(3, 3);
        world.Tiles.SetTiles(
        [
            new CampaignTileEntry(1, 1, new CampaignTileData(CampaignTileType.RiverJunction, 20)),
            new CampaignTileEntry(1, 2, new CampaignTileData(CampaignTileType.River, 30)),
            new CampaignTileEntry(0, 1, new CampaignTileData(CampaignTileType.River, 20)),
            new CampaignTileEntry(2, 1, new CampaignTileData(CampaignTileType.LargeRiver, 20)),
        ]);

        await CampaignWorldProjectSerializer.SaveAsync(world, temporary.Path);
        var loaded = await CampaignWorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.Equal(CampaignTileType.RiverJunction, loaded.World.Tiles.GetTile(1, 1).Type);
        Assert.Equal(3, Enum.GetValues<RiverConnections>()
            .Where(static connection => connection != RiverConnections.None)
            .Count(connection => loaded.World.Tiles.GetRiverConnections(1, 1).HasFlag(connection)));
        Assert.Contains(
            "\"type\": \"riverJunction\"",
            await File.ReadAllTextAsync(Path.Combine(
                temporary.Path,
                CampaignWorldProjectSerializer.CampaignTileFileName)));
    }

    [Fact]
    public async Task EarlyVersionTwoWaterType_LoadsAsSea()
    {
        using var temporary = new TemporaryDirectory();
        await SaveEmptyWorldAsync(temporary.Path);
        await WriteTileFileAsync(
            temporary.Path,
            """
            {
              "version": 2,
              "tiles": [
                { "x": 0, "y": 0, "type": "water", "heightMeters": -50 }
              ]
            }
            """);

        var loaded = await CampaignWorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.Equal(
            new CampaignTileData(CampaignTileType.Sea, -50),
            loaded.World.Tiles.GetTile(0, 0));
    }

    [Fact]
    public async Task LegacyVersionTwoCoastal_LoadsAsPlainsAndReportsNormalization()
    {
        using var temporary = new TemporaryDirectory();
        await SaveEmptyWorldAsync(temporary.Path);
        await WriteTileFileAsync(
            temporary.Path,
            """
            {
              "version": 2,
              "tiles": [
                { "x": 0, "y": 0, "type": "coastal", "heightMeters": 125 }
              ]
            }
            """);

        var loaded = await CampaignWorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.Equal(1, loaded.NormalizedLegacyCoastalTileCount);
        Assert.Equal(
            new CampaignTileData(CampaignTileType.Plains, 125),
            loaded.World.Tiles.GetTile(0, 0));

        await CampaignWorldProjectSerializer.SaveAsync(loaded.World, temporary.Path);
        var savedTiles = await File.ReadAllTextAsync(Path.Combine(
            temporary.Path,
            CampaignWorldProjectSerializer.CampaignTileFileName));
        Assert.DoesNotContain("coastal", savedTiles, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"type\": \"plains\"", savedTiles);
    }

    [Fact]
    public async Task LoadRejectsRiverWithThreeCardinalExits()
    {
        using var temporary = new TemporaryDirectory();
        await CampaignWorldProjectSerializer.SaveAsync(CreateWorld(3, 3), temporary.Path);
        await WriteTileFileAsync(
            temporary.Path,
            """
            {
              "version": 2,
              "tiles": [
                { "x": 1, "y": 1, "type": "river", "heightMeters": 20 },
                { "x": 1, "y": 0, "type": "river", "heightMeters": 20 },
                { "x": 2, "y": 1, "type": "river", "heightMeters": 20 },
                { "x": 1, "y": 2, "type": "river", "heightMeters": 20 }
              ]
            }
            """);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignWorldProjectSerializer.LoadAsync(temporary.Path));
    }

    [Fact]
    public async Task MissingTileFile_LoadsImplicitDefaultTiles()
    {
        using var temporary = new TemporaryDirectory();
        var world = CreateWorld(defaultHeight: 125);
        await CampaignWorldProjectSerializer.SaveAsync(world, temporary.Path);
        File.Delete(Path.Combine(temporary.Path, CampaignWorldProjectSerializer.CampaignTileFileName));

        var loaded = await CampaignWorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.Equal(new CampaignTileData(CampaignTileType.Unassigned, 125), loaded.World.Tiles.GetTile(0, 0));
        Assert.Equal(0, loaded.World.Tiles.MaterializedTileCount);
    }

    [Fact]
    public async Task LoadRejectsDuplicateTileRecords()
    {
        using var temporary = new TemporaryDirectory();
        await SaveEmptyWorldAsync(temporary.Path);
        await WriteTileFileAsync(
            temporary.Path,
            """
            {
              "version": 2,
              "tiles": [
                { "x": 0, "y": 0, "type": "plains", "heightMeters": 100 },
                { "x": 0, "y": 0, "type": "forest", "heightMeters": 200 }
              ]
            }
            """);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignWorldProjectSerializer.LoadAsync(temporary.Path));
    }

    [Fact]
    public async Task LoadRejectsTileOutsideGrid()
    {
        using var temporary = new TemporaryDirectory();
        await SaveEmptyWorldAsync(temporary.Path);
        await WriteTileFileAsync(
            temporary.Path,
            """
            {
              "version": 2,
              "tiles": [
                { "x": 2, "y": 0, "type": "plains", "heightMeters": 100 }
              ]
            }
            """);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignWorldProjectSerializer.LoadAsync(temporary.Path));
    }

    [Fact]
    public async Task LoadRejectsUnknownTileType()
    {
        using var temporary = new TemporaryDirectory();
        await SaveEmptyWorldAsync(temporary.Path);
        await WriteTileFileAsync(
            temporary.Path,
            """
            {
              "version": 2,
              "tiles": [
                { "x": 0, "y": 0, "type": "volcano", "heightMeters": 100 }
              ]
            }
            """);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignWorldProjectSerializer.LoadAsync(temporary.Path));
    }

    [Fact]
    public async Task LoadRejectsHeightOutsideConfiguredRange()
    {
        using var temporary = new TemporaryDirectory();
        await SaveEmptyWorldAsync(temporary.Path);
        await WriteTileFileAsync(
            temporary.Path,
            """
            {
              "version": 2,
              "tiles": [
                { "x": 0, "y": 0, "type": "mountain", "heightMeters": 7000 }
              ]
            }
            """);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignWorldProjectSerializer.LoadAsync(temporary.Path));
    }

    [Fact]
    public async Task LegacyImport_AveragesOwnedSamplesCopiesTypesAndLeavesSourceUnchanged()
    {
        using var temporary = new TemporaryDirectory();
        var definition = WorldDefinition.Create(
            worldWidthMeters: 80,
            worldHeightMeters: 40,
            heightSampleSpacingMeters: 10,
            campaignTileSizeMeters: 40,
            seaLevelMeters: 0,
            minimumElevationMeters: -1_000,
            maximumElevationMeters: 6_000,
            chunkSize: 4,
            initialElevationMeters: 0);
        var legacy = new WorldTerrain(definition);
        for (var y = 0; y < definition.HeightSamplesY; y++)
        {
            for (var x = 0; x < definition.HeightSamplesX; x++)
            {
                legacy.SetHeight(x, y, x < 4 ? (short)100 : (short)300);
            }
        }

        legacy.CampaignTiles.SetTileType(0, 0, CampaignTileType.Plains);
        legacy.CampaignTiles.SetTileType(1, 0, CampaignTileType.Forest);
        await WorldProjectSerializer.SaveAsync(legacy, temporary.Path);
        var manifestPath = Path.Combine(temporary.Path, WorldProjectSerializer.ManifestFileName);
        var originalManifest = await File.ReadAllTextAsync(manifestPath);

        var result = await CampaignWorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.True(result.WasConvertedFromLegacy);
        Assert.Equal(new CampaignTileData(CampaignTileType.Plains, 100), result.World.Tiles.GetTile(0, 0));
        Assert.Equal(new CampaignTileData(CampaignTileType.Forest, 300), result.World.Tiles.GetTile(1, 0));
        Assert.Equal(originalManifest, await File.ReadAllTextAsync(manifestPath));
    }

    private static CampaignWorld CreateWorld(short defaultHeight = 0) => CreateWorld(2, 2, defaultHeight);

    private static CampaignWorld CreateWorld(int tilesX, int tilesY, short defaultHeight = 0) =>
        new(CampaignWorldDefinition.Create(
            worldWidthMeters: tilesX * 5_000L,
            worldHeightMeters: tilesY * 5_000L,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: defaultHeight));

    private static Task SaveEmptyWorldAsync(string path) =>
        CampaignWorldProjectSerializer.SaveAsync(CreateWorld(), path);

    private static Task WriteTileFileAsync(string path, string contents) =>
        File.WriteAllTextAsync(Path.Combine(path, CampaignWorldProjectSerializer.CampaignTileFileName), contents);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"KingdomCampaignWorldTests-{Guid.NewGuid():N}");
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
