using System.Text.Json;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Serialization;

namespace Kingdom.World.Tests;

public sealed class CampaignWorldJsonExporterTests
{
    [Fact]
    public async Task Export_WritesOneSelfContainedEngineJsonFile()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateDefinition();
        var orchard = new CampaignCustomTerrainDefinition(
            "orchard",
            "Orchard",
            CampaignTileType.Forest,
            "#447744");
        var world = new CampaignWorld(definition, [orchard]);
        world.Tiles.SetTiles(
        [
            new CampaignTileEntry(
                0,
                0,
                new CampaignTileData(CampaignTileType.Forest, 220, orchard.Id)),
            new CampaignTileEntry(1, 1, new CampaignTileData(CampaignTileType.Sea, -180)),
        ]);
        var amber = CreateCustomResource("amber-resin", "Amber Resin");
        var resources = new CampaignResourceMap(
            definition,
            new CampaignResourceCatalog([amber]));
        resources.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, new("timber", 54, Locked: true)),
            CampaignResourceMutation.Upsert(0, 0, new("amber-resin", 87)),
            CampaignResourceMutation.Upsert(1, 1, new("fish", 61)),
        ]);
        var monsoon = CreateCustomSeason("monsoon", "Monsoon");
        var seasons = new CampaignSeasonMap(
            definition,
            new CampaignSeasonCatalog([monsoon]));
        seasons.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("summer")),
            CampaignSeasonMutation.Upsert(0, 0, new("spring", Locked: true)),
            CampaignSeasonMutation.Upsert(0, 0, new("fall")),
            CampaignSeasonMutation.Upsert(1, 1, new("monsoon")),
        ]);
        var jsonPath = Path.Combine(temporary.Path, "campaign.world.json");

        await CampaignWorldJsonExporter.ExportAsync(world, resources, seasons, jsonPath);

        Assert.Equal([jsonPath], Directory.EnumerateFiles(temporary.Path));
        await using var stream = File.OpenRead(jsonPath);
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        Assert.Equal(
        [
            "format",
            "version",
            "world",
            "grid",
            "tileTypes",
            "customTerrain",
            "resources",
            "seasons",
            "tiles",
        ], root.EnumerateObject().Select(static property => property.Name));
        Assert.Equal(CampaignWorldJsonExporter.FormatIdentifier, root.GetProperty("format").GetString());
        Assert.Equal(CampaignWorldJsonExporter.FormatVersion, root.GetProperty("version").GetInt32());

        var worldData = root.GetProperty("world");
        Assert.Equal(10_000, worldData.GetProperty("widthMeters").GetInt64());
        Assert.Equal(5_000, worldData.GetProperty("campaignTileSizeMeters").GetInt32());
        var grid = root.GetProperty("grid");
        Assert.Equal(2, grid.GetProperty("tilesX").GetInt32());
        Assert.Equal(2, grid.GetProperty("tilesY").GetInt32());
        Assert.Equal(4, grid.GetProperty("tileCount").GetInt64());
        Assert.Equal("northWest", grid.GetProperty("origin").GetString());
        Assert.Equal("rowMajorYThenX", grid.GetProperty("order").GetString());

        var tileTypes = root.GetProperty("tileTypes").EnumerateArray().ToArray();
        Assert.Contains(tileTypes, static item =>
            item.GetProperty("value").GetInt32() == (byte)CampaignTileType.Steppe &&
            item.GetProperty("id").GetString() == "steppe");
        Assert.DoesNotContain(tileTypes, static item =>
            item.GetProperty("id").GetString() is "water" or "coastal");
        var customTerrain = Assert.Single(root.GetProperty("customTerrain").EnumerateArray());
        Assert.Equal("orchard", customTerrain.GetProperty("id").GetString());
        Assert.Equal("forest", customTerrain.GetProperty("baseTerrainType").GetString());

        var resourceLayer = root.GetProperty("resources");
        Assert.Equal(3, resourceLayer.GetProperty("occurrenceCount").GetInt32());
        var resourceCatalog = resourceLayer.GetProperty("catalog").EnumerateArray().ToArray();
        Assert.Equal(
            resourceCatalog.Select(static item => item.GetProperty("id").GetString()).Order(StringComparer.Ordinal),
            resourceCatalog.Select(static item => item.GetProperty("id").GetString()));
        Assert.False(resourceCatalog.Single(static item =>
            item.GetProperty("id").GetString() == "amber-resin").GetProperty("builtIn").GetBoolean());

        var seasonLayer = root.GetProperty("seasons");
        Assert.Equal(4, seasonLayer.GetProperty("occurrenceCount").GetInt32());
        var monsoonCatalog = seasonLayer.GetProperty("catalog").EnumerateArray().Single(static item =>
            item.GetProperty("id").GetString() == "monsoon");
        Assert.Equal("summer", monsoonCatalog.GetProperty("fallback").GetString());
        Assert.False(monsoonCatalog.GetProperty("builtIn").GetBoolean());

        var tiles = root.GetProperty("tiles").EnumerateArray().ToArray();
        Assert.Equal(4, tiles.Length);
        Assert.Equal(
            [(0, 0), (1, 0), (0, 1), (1, 1)],
            tiles.Select(static tile =>
                (tile.GetProperty("x").GetInt32(), tile.GetProperty("y").GetInt32())));
        var firstTile = tiles[0];
        Assert.Equal("forest", firstTile.GetProperty("terrainType").GetString());
        Assert.Equal("orchard", firstTile.GetProperty("customTerrainId").GetString());
        Assert.Equal(220, firstTile.GetProperty("heightMeters").GetInt32());
        Assert.Equal(
            ["amber-resin", "timber"],
            firstTile.GetProperty("resources").EnumerateArray()
                .Select(static item => item.GetProperty("id").GetString()));
        Assert.Equal(
            [87, 54],
            firstTile.GetProperty("resources").EnumerateArray()
                .Select(static item => item.GetProperty("potential").GetInt32()));
        Assert.Equal(
            ["fall", "spring", "summer"],
            firstTile.GetProperty("seasons").EnumerateArray()
                .Select(static item => item.GetString()));
        Assert.True(tiles[1].GetProperty("customTerrainId").ValueKind == JsonValueKind.Null);
        Assert.DoesNotContain("locked", root.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_IsDeterministicAcrossInsertionOrderAndAuthoringLocks()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateDefinition();
        var world = new CampaignWorld(definition);
        var amber = CreateCustomResource("amber-resin", "Amber Resin");
        var herbs = CreateCustomResource("medicinal-herbs", "Medicinal Herbs");
        var firstResources = new CampaignResourceMap(
            definition,
            new CampaignResourceCatalog([herbs, amber]));
        var secondResources = new CampaignResourceMap(
            definition,
            new CampaignResourceCatalog([amber, herbs]));
        firstResources.Apply(
        [
            CampaignResourceMutation.Upsert(1, 1, new("medicinal-herbs", 70)),
            CampaignResourceMutation.Upsert(0, 0, new("timber", 45, Locked: true)),
            CampaignResourceMutation.Upsert(0, 0, new("amber-resin", 82)),
        ]);
        secondResources.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, new("amber-resin", 82, Locked: true)),
            CampaignResourceMutation.Upsert(0, 0, new("timber", 45)),
            CampaignResourceMutation.Upsert(1, 1, new("medicinal-herbs", 70, Locked: true)),
        ]);

        var monsoon = CreateCustomSeason("monsoon", "Monsoon");
        var wet = CreateCustomSeason("wet-season", "Wet Season");
        var firstSeasons = new CampaignSeasonMap(
            definition,
            new CampaignSeasonCatalog([wet, monsoon]));
        var secondSeasons = new CampaignSeasonMap(
            definition,
            new CampaignSeasonCatalog([monsoon, wet]));
        firstSeasons.Apply(
        [
            CampaignSeasonMutation.Upsert(1, 1, new("wet-season")),
            CampaignSeasonMutation.Upsert(0, 0, new("spring", Locked: true)),
            CampaignSeasonMutation.Upsert(0, 0, new("monsoon")),
        ]);
        secondSeasons.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("monsoon", Locked: true)),
            CampaignSeasonMutation.Upsert(0, 0, new("spring")),
            CampaignSeasonMutation.Upsert(1, 1, new("wet-season", Locked: true)),
        ]);
        var firstPath = Path.Combine(temporary.Path, "first.json");
        var secondPath = Path.Combine(temporary.Path, "second.json");

        await CampaignWorldJsonExporter.ExportAsync(
            world,
            firstResources,
            firstSeasons,
            firstPath);
        await CampaignWorldJsonExporter.ExportAsync(
            world,
            secondResources,
            secondSeasons,
            secondPath);

        Assert.Equal(await File.ReadAllBytesAsync(firstPath), await File.ReadAllBytesAsync(secondPath));
    }

    [Fact]
    public async Task Export_RemainsValidAcrossBoundedAsyncFlushes()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 2_049_000,
            worldHeightMeters: 1_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var world = new CampaignWorld(definition);
        var jsonPath = Path.Combine(temporary.Path, "flush-boundary.world.json");

        await CampaignWorldJsonExporter.ExportAsync(
            world,
            new CampaignResourceMap(definition),
            new CampaignSeasonMap(definition),
            jsonPath);

        await using var stream = File.OpenRead(jsonPath);
        using var document = await JsonDocument.ParseAsync(stream);
        var tiles = document.RootElement.GetProperty("tiles").EnumerateArray().ToArray();
        Assert.Equal(2_049, tiles.Length);
        Assert.Equal(2_048, tiles[^1].GetProperty("x").GetInt32());
        Assert.Equal(0, tiles[^1].GetProperty("y").GetInt32());
    }

    [Fact]
    public async Task Export_PreCancelledRequestPreservesDestinationAndCleansTemporaryFile()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateDefinition();
        var world = new CampaignWorld(definition);
        var resources = new CampaignResourceMap(definition);
        var seasons = new CampaignSeasonMap(definition);
        var jsonPath = Path.Combine(temporary.Path, "existing.json");
        var originalBytes = "original"u8.ToArray();
        await File.WriteAllBytesAsync(jsonPath, originalBytes);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CampaignWorldJsonExporter.ExportAsync(
                world,
                resources,
                seasons,
                jsonPath,
                cancellation.Token));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(jsonPath));
        Assert.Equal([jsonPath], Directory.EnumerateFiles(temporary.Path));
    }

    [Fact]
    public async Task Export_RejectsWrongExtensionAndMismatchedLayersWithoutCreatingFiles()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateDefinition();
        var world = new CampaignWorld(definition);
        var resources = new CampaignResourceMap(definition);
        var seasons = new CampaignSeasonMap(definition);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            CampaignWorldJsonExporter.ExportAsync(
                world,
                resources,
                seasons,
                Path.Combine(temporary.Path, "world.txt")));

        var mismatch = CampaignWorldDefinition.Create(
            20_000,
            10_000,
            5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CampaignWorldJsonExporter.ExportAsync(
                world,
                new CampaignResourceMap(mismatch),
                seasons,
                Path.Combine(temporary.Path, "world.json")));

        Assert.Empty(Directory.EnumerateFiles(temporary.Path));
    }

    private static CampaignWorldDefinition CreateDefinition() => CampaignWorldDefinition.Create(
        worldWidthMeters: 10_000,
        worldHeightMeters: 10_000,
        campaignTileSizeMeters: 5_000,
        seaLevelMeters: 0,
        minimumHeightMeters: -1_000,
        maximumHeightMeters: 6_000);

    private static CampaignResourceDefinition CreateCustomResource(string id, string name) =>
        new(
            id,
            name,
            CampaignResourceCategory.Finite,
            CampaignResourceDistributionProfile.Field,
            CampaignResourceMedium.Land,
            "resource",
            "#735A91",
            mapPriority: 50,
            coveragePercent: 10,
            CampaignResourceRichness.Balanced,
            CampaignResourceConcentration.Balanced);

    private static CampaignSeasonDefinition CreateCustomSeason(string id, string name) =>
        new(
            id,
            name,
            CampaignBuiltInSeason.Summer,
            "#467A9C",
            tintStrengthPercent: 64,
            effectIntensityPercent: 81);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"WorldEditorPixel-JsonRuntime-{Guid.NewGuid():N}");
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
