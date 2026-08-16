using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Serialization;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceSerializationTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsEveryAuthoringField()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateWorldDefinition();
        var custom = CreateCustomDefinition("amber", "Amber", "amber", "#E6A53A");
        var catalog = new CampaignResourceCatalog([custom]);
        var map = new CampaignResourceMap(definition, catalog);
        map.Apply(
        [
            CampaignResourceMutation.Upsert(2, 1, new CampaignResourceOccurrence("amber", 83, true)),
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("fish", 41)),
            CampaignResourceMutation.Upsert(2, 1, new CampaignResourceOccurrence("iron-ore", 62)),
        ]);
        var settings = new CampaignResourceGenerationSettings(
            resourceSeed: -1_247_001,
            seedDerivedFromWorld: false,
            abundance: CampaignResourceAbundance.Custom,
            climate: CampaignResourceClimateProfile.Tropical,
            geology: CampaignResourceGeologyProfile.VolcanicArc,
            overrides:
            [
                new CampaignResourceGenerationOverride(
                    "iron-ore",
                    enabled: false,
                    coveragePercent: 19,
                    CampaignResourceRichness.Rich,
                    richnessBias: 17,
                    CampaignResourceConcentration.ManySmall,
                    mapPriority: 71),
                new CampaignResourceGenerationOverride(
                    "amber",
                    enabled: true,
                    coveragePercent: 4,
                    CampaignResourceRichness.Poor,
                    richnessBias: -9,
                    CampaignResourceConcentration.FewLarge,
                    mapPriority: 12),
            ]);

        await CampaignResourceProjectSerializer.SaveAsync(map, settings, temporary.Path);
        var loaded = await CampaignResourceProjectSerializer.LoadAsync(definition, temporary.Path);

        Assert.Equal(Path.GetFullPath(temporary.Path), loaded.SourceProjectDirectory);
        Assert.Equal(map.GetMaterializedOccurrences(), loaded.ResourceMap.GetMaterializedOccurrences());
        var loadedCustom = Assert.Single(loaded.ResourceMap.Catalog.CustomDefinitions);
        AssertDefinitionEqual(custom, loadedCustom);
        AssertSettingsEqual(settings, Assert.IsType<CampaignResourceGenerationSettings>(loaded.GenerationSettings));
        Assert.Equal(3, loaded.ResourceMap.OccurrenceCount);
        Assert.Equal(2, loaded.ResourceMap.MaterializedTileCount);
    }

    [Fact]
    public async Task Save_IsByteDeterministicAcrossInsertionOrders()
    {
        using var first = new TemporaryDirectory();
        using var second = new TemporaryDirectory();
        var definition = CreateWorldDefinition();
        var amberA = CreateCustomDefinition("amber", "Amber", "amber", "#E6A53A");
        var peatA = CreateCustomDefinition("peat", "Peat", "coal", "#51443B");
        var amberB = CreateCustomDefinition("amber", "Amber", "amber", "#E6A53A");
        var peatB = CreateCustomDefinition("peat", "Peat", "coal", "#51443B");
        var firstMap = new CampaignResourceMap(definition, new CampaignResourceCatalog([peatA, amberA]));
        var secondMap = new CampaignResourceMap(definition, new CampaignResourceCatalog([amberB, peatB]));
        firstMap.Apply(
        [
            CampaignResourceMutation.Upsert(2, 1, new CampaignResourceOccurrence("peat", 20)),
            CampaignResourceMutation.Upsert(0, 1, new CampaignResourceOccurrence("amber", 90, true)),
            CampaignResourceMutation.Upsert(0, 1, new CampaignResourceOccurrence("gold", 30)),
        ]);
        secondMap.Apply(
        [
            CampaignResourceMutation.Upsert(0, 1, new CampaignResourceOccurrence("gold", 30)),
            CampaignResourceMutation.Upsert(0, 1, new CampaignResourceOccurrence("amber", 90, true)),
            CampaignResourceMutation.Upsert(2, 1, new CampaignResourceOccurrence("peat", 20)),
        ]);
        var firstSettings = CreateSettings(
            new CampaignResourceGenerationOverride(
                "peat", true, 15, CampaignResourceRichness.Rich, 3,
                CampaignResourceConcentration.Balanced, 44),
            new CampaignResourceGenerationOverride(
                "amber", false, 0, CampaignResourceRichness.Poor, -2,
                CampaignResourceConcentration.FewLarge, 9));
        var secondSettings = CreateSettings(
            new CampaignResourceGenerationOverride(
                "amber", false, 0, CampaignResourceRichness.Poor, -2,
                CampaignResourceConcentration.FewLarge, 9),
            new CampaignResourceGenerationOverride(
                "peat", true, 15, CampaignResourceRichness.Rich, 3,
                CampaignResourceConcentration.Balanced, 44));

        await CampaignResourceProjectSerializer.SaveAsync(firstMap, firstSettings, first.Path);
        await CampaignResourceProjectSerializer.SaveAsync(secondMap, secondSettings, second.Path);

        foreach (var fileName in ResourceFileNames)
        {
            Assert.Equal(
                await File.ReadAllBytesAsync(Path.Combine(first.Path, fileName)),
                await File.ReadAllBytesAsync(Path.Combine(second.Path, fileName)));
        }

        var tileJson = await File.ReadAllTextAsync(
            Path.Combine(first.Path, CampaignResourceProjectSerializer.TilesFileName));
        Assert.True(tileJson.IndexOf("\"y\": 1", StringComparison.Ordinal) <
                    tileJson.LastIndexOf("\"y\": 1", StringComparison.Ordinal));
        Assert.True(tileJson.IndexOf("\"id\": \"amber\"", StringComparison.Ordinal) <
                    tileJson.IndexOf("\"id\": \"gold\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Load_MissingOptionalFilesReturnsBuiltInsNullSettingsAndEmptyMap()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateWorldDefinition();

        var loaded = await CampaignResourceProjectSerializer.LoadAsync(
            definition,
            Path.Combine(temporary.Path, CampaignWorldProjectSerializer.ManifestFileName));

        Assert.Empty(loaded.ResourceMap.Catalog.CustomDefinitions);
        Assert.Equal(CampaignResourceCatalog.BuiltInDefinitions.Count, loaded.ResourceMap.Catalog.Definitions.Count);
        Assert.Null(loaded.GenerationSettings);
        Assert.Equal(0, loaded.ResourceMap.OccurrenceCount);
        Assert.Equal(0, loaded.ResourceMap.Revision);
    }

    [Fact]
    public async Task Save_RemovesEveryStaleOptionalFileWhenStateIsAbsent()
    {
        using var temporary = new TemporaryDirectory();
        foreach (var fileName in ResourceFileNames)
        {
            await File.WriteAllTextAsync(Path.Combine(temporary.Path, fileName), "stale");
        }

        await CampaignResourceProjectSerializer.SaveAsync(
            new CampaignResourceMap(CreateWorldDefinition()),
            generationSettings: null,
            temporary.Path);

        foreach (var fileName in ResourceFileNames)
        {
            Assert.False(File.Exists(Path.Combine(temporary.Path, fileName)));
        }
    }

    [Fact]
    public async Task Save_InvalidCrossFileReferenceDoesNotMutateExistingFiles()
    {
        using var temporary = new TemporaryDirectory();
        var generationPath = Path.Combine(temporary.Path, CampaignResourceProjectSerializer.GenerationFileName);
        await File.WriteAllTextAsync(generationPath, "original generation bytes");
        var settings = CreateSettings(
            new CampaignResourceGenerationOverride(
                "unknown-resource", true, 10, CampaignResourceRichness.Balanced, 0,
                CampaignResourceConcentration.Balanced, 50));

        await Assert.ThrowsAsync<ArgumentException>(() => CampaignResourceProjectSerializer.SaveAsync(
            new CampaignResourceMap(CreateWorldDefinition()),
            settings,
            temporary.Path));

        Assert.Equal("original generation bytes", await File.ReadAllTextAsync(generationPath));
        Assert.Empty(Directory.EnumerateFiles(temporary.Path, "*.tmp"));
    }

    [Fact]
    public async Task Save_ReplacementFailureCleansEveryUniqueSiblingTemporaryFile()
    {
        using var temporary = new TemporaryDirectory();
        var definition = CreateWorldDefinition();
        var catalog = new CampaignResourceCatalog(
            [CreateCustomDefinition("amber", "Amber", "amber", "#E6A53A")]);
        var map = new CampaignResourceMap(definition, catalog);
        map.Upsert(0, 0, new CampaignResourceOccurrence("amber", 70, true));
        Directory.CreateDirectory(
            Path.Combine(temporary.Path, CampaignResourceProjectSerializer.DefinitionsFileName));

        var exception = await Record.ExceptionAsync(() => CampaignResourceProjectSerializer.SaveAsync(
            map,
            CreateSettings(),
            temporary.Path));

        Assert.True(exception is IOException or UnauthorizedAccessException);
        Assert.Empty(Directory.EnumerateFiles(temporary.Path, "*.tmp"));
        Assert.False(File.Exists(
            Path.Combine(temporary.Path, CampaignResourceProjectSerializer.GenerationFileName)));
        Assert.False(File.Exists(
            Path.Combine(temporary.Path, CampaignResourceProjectSerializer.TilesFileName)));
    }

    [Fact]
    public async Task Load_AllowsEnvironmentalMismatchBecauseItIsDiagnosticData()
    {
        using var temporary = new TemporaryDirectory();
        await WriteAsync(
            temporary.Path,
            CampaignResourceProjectSerializer.TilesFileName,
            ValidTilesDocument("{\"x\":0,\"y\":0,\"resources\":[{\"id\":\"fish\",\"potential\":75,\"locked\":false}]}"));

        var loaded = await CampaignResourceProjectSerializer.LoadAsync(CreateWorldDefinition(), temporary.Path);

        Assert.True(loaded.ResourceMap.TryGetOccurrence(0, 0, "fish", out var occurrence));
        Assert.Equal((byte)75, occurrence.Potential);
    }

    [Theory]
    [MemberData(nameof(InvalidDefinitionDocuments))]
    public async Task Load_RejectsMalformedOrInvalidDefinitionDocuments(string json)
    {
        using var temporary = new TemporaryDirectory();
        await WriteAsync(temporary.Path, CampaignResourceProjectSerializer.DefinitionsFileName, json);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignResourceProjectSerializer.LoadAsync(CreateWorldDefinition(), temporary.Path));
    }

    [Theory]
    [MemberData(nameof(InvalidGenerationDocuments))]
    public async Task Load_RejectsMalformedOrInvalidGenerationDocuments(string json)
    {
        using var temporary = new TemporaryDirectory();
        await WriteAsync(temporary.Path, CampaignResourceProjectSerializer.GenerationFileName, json);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignResourceProjectSerializer.LoadAsync(CreateWorldDefinition(), temporary.Path));
    }

    [Theory]
    [MemberData(nameof(InvalidTileDocuments))]
    public async Task Load_RejectsMalformedOrInvalidTileDocuments(string json)
    {
        using var temporary = new TemporaryDirectory();
        await WriteAsync(temporary.Path, CampaignResourceProjectSerializer.TilesFileName, json);

        await Assert.ThrowsAsync<WorldFormatException>(() =>
            CampaignResourceProjectSerializer.LoadAsync(CreateWorldDefinition(), temporary.Path));
    }

    [Fact]
    public async Task Load_ResolvesCustomDefinitionsBeforeSettingsAndOccurrences()
    {
        using var temporary = new TemporaryDirectory();
        await WriteAsync(
            temporary.Path,
            CampaignResourceProjectSerializer.DefinitionsFileName,
            ValidDefinitionsDocument(ValidDefinitionRecord("amber")));
        await WriteAsync(
            temporary.Path,
            CampaignResourceProjectSerializer.GenerationFileName,
            ValidGenerationDocument(
                "{\"resourceId\":\"amber\",\"enabled\":true,\"coveragePercent\":6," +
                "\"richness\":\"rich\",\"richnessBias\":7,\"concentration\":\"fewLarge\",\"mapPriority\":33}"));
        await WriteAsync(
            temporary.Path,
            CampaignResourceProjectSerializer.TilesFileName,
            ValidTilesDocument(
                "{\"x\":1,\"y\":1,\"resources\":[{\"id\":\"amber\",\"potential\":88,\"locked\":true}]}"));

        var loaded = await CampaignResourceProjectSerializer.LoadAsync(CreateWorldDefinition(), temporary.Path);

        Assert.True(loaded.ResourceMap.Catalog.Contains("amber"));
        Assert.Empty(loaded.ResourceMap.Catalog.Get("amber").Rules.AvoidedTerrainTags);
        Assert.Equal("amber", Assert.Single(loaded.GenerationSettings!.Overrides).ResourceId);
        Assert.True(loaded.ResourceMap.TryGetOccurrence(1, 1, "amber", out var occurrence));
        Assert.True(occurrence.Locked);
    }

    [Fact]
    public async Task Load_VersionTwoDefinitionsKeepAvoidanceAndDefaultHardExclusionsEmpty()
    {
        using var temporary = new TemporaryDirectory();
        await WriteAsync(
            temporary.Path,
            CampaignResourceProjectSerializer.DefinitionsFileName,
            ValidDefinitionsDocumentV2(ValidDefinitionRecordV2("amber")));

        var loaded = await CampaignResourceProjectSerializer.LoadAsync(
            CreateWorldDefinition(),
            temporary.Path);
        var amber = loaded.ResourceMap.Catalog.Get("amber");

        Assert.Equal(["arid"], amber.Rules.AvoidedTerrainTags);
        Assert.Empty(amber.Rules.ExcludedTerrainSurfaces);
    }

    public static IEnumerable<object[]> InvalidDefinitionDocuments()
    {
        yield return ["{}"];
        yield return ["{\"version\":4,\"definitions\":[]}"];
        yield return [ValidDefinitionsDocumentV2(ValidDefinitionRecord("amber"))];
        yield return [ValidDefinitionsDocumentV3(ValidDefinitionRecordV2("amber"))];
        yield return ["{\"version\":1,\"definitions\":[],\"unknown\":true}"];
        yield return ["{\"version\":1,\"version\":1,\"definitions\":[]}"];
        yield return [ValidDefinitionsDocument(ValidDefinitionRecord("gold"))];
        yield return [ValidDefinitionsDocument(
            ValidDefinitionRecord("amber") + "," + ValidDefinitionRecord("amber"))];
        yield return [ValidDefinitionsDocument(
            ValidDefinitionRecord("amber").Replace("\"medium\":\"land\"", "\"medium\":0", StringComparison.Ordinal))];
        yield return [ValidDefinitionsDocument(
            ValidDefinitionRecord("amber").Replace(
                "\"preferredTerrainTags\":[]",
                "\"preferredTerrainTags\":null",
                StringComparison.Ordinal))];
        yield return [ValidDefinitionsDocument(
            ValidDefinitionRecord("amber").Replace(
                "\"fieldWeights\":[]",
                "\"fieldWeights\":[{\"id\":\"moist\",\"weight\":1},{\"id\":\"moist\",\"weight\":2}]",
                StringComparison.Ordinal))];
        yield return [ValidDefinitionsDocument(
            ValidDefinitionRecord("amber").Replace(
                "\"elevationMeters\":null",
                "\"elevationMeters\":{\"minimum\":100,\"maximum\":-100}",
                StringComparison.Ordinal))];
    }

    public static IEnumerable<object[]> InvalidGenerationDocuments()
    {
        yield return ["{}"];
        yield return [ValidGenerationDocument().Replace("\"schemaVersion\":1", "\"schemaVersion\":2", StringComparison.Ordinal)];
        yield return [ValidGenerationDocument().Replace("\"abundance\":\"balanced\"", "\"abundance\":1", StringComparison.Ordinal)];
        yield return [ValidGenerationDocument().Replace("\"overrides\":[]", "\"overrides\":null", StringComparison.Ordinal)];
        yield return [ValidGenerationDocument(
            "{\"resourceId\":\"unknown-resource\",\"enabled\":true,\"coveragePercent\":6," +
            "\"richness\":\"rich\",\"richnessBias\":7,\"concentration\":\"fewLarge\",\"mapPriority\":33}")];
        var value =
            "{\"resourceId\":\"gold\",\"enabled\":true,\"coveragePercent\":6," +
            "\"richness\":\"rich\",\"richnessBias\":7,\"concentration\":\"fewLarge\",\"mapPriority\":33}";
        yield return [ValidGenerationDocument(value + "," + value)];
        yield return [ValidGenerationDocument(value.Replace("\"coveragePercent\":6", "\"coveragePercent\":101", StringComparison.Ordinal))];
        yield return [ValidGenerationDocument().Replace("\"geology\":\"autoMixed\"", "\"geology\":\"unknown\"", StringComparison.Ordinal)];
        yield return [ValidGenerationDocument().Replace("\"overrides\":[]", "\"overrides\":[],\"extra\":0", StringComparison.Ordinal)];
    }

    public static IEnumerable<object[]> InvalidTileDocuments()
    {
        yield return ["{}"];
        yield return ["{\"version\":2,\"tiles\":[]}"];
        yield return ["{\"version\":1,\"tiles\":null}"];
        yield return [ValidTilesDocument("{\"x\":0,\"y\":0,\"resources\":[]}")];
        yield return [ValidTilesDocument("{\"x\":3,\"y\":0,\"resources\":[{\"id\":\"gold\",\"potential\":50,\"locked\":false}]}")];
        yield return [ValidTilesDocument(
            "{\"x\":0,\"y\":0,\"resources\":[{\"id\":\"unknown-resource\",\"potential\":50,\"locked\":false}]}")];
        yield return [ValidTilesDocument(
            "{\"x\":0,\"y\":0,\"resources\":[{\"id\":\"gold\",\"potential\":0,\"locked\":false}]}")];
        yield return [ValidTilesDocument(
            "{\"x\":0,\"y\":0,\"resources\":[{\"id\":\"gold\",\"potential\":101,\"locked\":false}]}")];
        var tile = "{\"x\":0,\"y\":0,\"resources\":[{\"id\":\"gold\",\"potential\":50,\"locked\":false}]}";
        yield return [ValidTilesDocument(tile + "," + tile)];
        yield return [ValidTilesDocument(
            "{\"x\":0,\"y\":0,\"resources\":[" +
            "{\"id\":\"gold\",\"potential\":50,\"locked\":false}," +
            "{\"id\":\"gold\",\"potential\":60,\"locked\":true}]}")];
        yield return [ValidTilesDocument(tile).Replace("\"locked\":false", "\"locked\":false,\"locked\":true", StringComparison.Ordinal)];
        yield return [ValidTilesDocument(tile).Replace("\"tiles\":[", "\"tiles\":[],\"unknown\":[", StringComparison.Ordinal)];
    }

    private static readonly string[] ResourceFileNames =
    [
        CampaignResourceProjectSerializer.DefinitionsFileName,
        CampaignResourceProjectSerializer.GenerationFileName,
        CampaignResourceProjectSerializer.TilesFileName,
    ];

    private static CampaignWorldDefinition CreateWorldDefinition() =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: 15_000,
            worldHeightMeters: 10_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 20);

    private static CampaignResourceDefinition CreateCustomDefinition(
        string id,
        string name,
        string symbol,
        string color) =>
        new(
            id,
            name,
            CampaignResourceCategory.Finite,
            CampaignResourceDistributionProfile.SurfaceDeposit,
            CampaignResourceMedium.Land,
            symbol,
            color,
            mapPriority: 37,
            coveragePercent: 14,
            CampaignResourceRichness.Rich,
            CampaignResourceConcentration.ManySmall,
            new CampaignResourceRuleSet(
                CampaignResourceMedium.Land,
                elevationMeters: new CampaignResourceRange(-120, 1_800),
                grade: CampaignResourceRange.NonNegative(0.05, 0.8),
                waterDistanceKilometers: CampaignResourceRange.NonNegative(2, 90),
                regionScaleKilometers: CampaignResourceRange.NonNegative(8, 45),
                preferredTerrainTags: ["forest", "mineralized"],
                customTerrainIncludes: ["ancient-rock", "volcanic-soil"],
                customTerrainExcludes: ["marsh"],
                fieldWeights: new Dictionary<string, double>
                {
                    ["moisture"] = -0.75,
                    ["erosion"] = 2.25,
                },
                associationWeights: new Dictionary<string, double>
                {
                    ["gold"] = 1.5,
                    ["clay"] = -0.5,
                },
                avoidedTerrainTags: ["arid", "lowland"],
                excludedTerrainSurfaces:
                [
                    CampaignResourceSurfaceType.Desert,
                    CampaignResourceSurfaceType.Tundra,
                ]));

    private static CampaignResourceGenerationSettings CreateSettings(
        params CampaignResourceGenerationOverride[] overrides) =>
        new(
            90210,
            seedDerivedFromWorld: true,
            CampaignResourceAbundance.Balanced,
            CampaignResourceClimateProfile.AutoMixed,
            CampaignResourceGeologyProfile.FoldBelt,
            overrides);

    private static void AssertDefinitionEqual(
        CampaignResourceDefinition expected,
        CampaignResourceDefinition actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.DistributionProfile, actual.DistributionProfile);
        Assert.Equal(expected.Medium, actual.Medium);
        Assert.Equal(expected.SymbolId, actual.SymbolId);
        Assert.Equal(expected.ColorHex, actual.ColorHex);
        Assert.Equal(expected.MapPriority, actual.MapPriority);
        Assert.Equal(expected.CoveragePercent, actual.CoveragePercent);
        Assert.Equal(expected.Richness, actual.Richness);
        Assert.Equal(expected.Concentration, actual.Concentration);
        Assert.Equal(expected.Rules.ElevationMeters, actual.Rules.ElevationMeters);
        Assert.Equal(expected.Rules.Grade, actual.Rules.Grade);
        Assert.Equal(expected.Rules.WaterDistanceKilometers, actual.Rules.WaterDistanceKilometers);
        Assert.Equal(expected.Rules.RegionScaleKilometers, actual.Rules.RegionScaleKilometers);
        Assert.Equal(expected.Rules.PreferredTerrainTags, actual.Rules.PreferredTerrainTags);
        Assert.Equal(expected.Rules.AvoidedTerrainTags, actual.Rules.AvoidedTerrainTags);
        Assert.Equal(expected.Rules.ExcludedTerrainSurfaces, actual.Rules.ExcludedTerrainSurfaces);
        Assert.Equal(expected.Rules.CustomTerrainIncludes, actual.Rules.CustomTerrainIncludes);
        Assert.Equal(expected.Rules.CustomTerrainExcludes, actual.Rules.CustomTerrainExcludes);
        Assert.Equal(expected.Rules.FieldWeights, actual.Rules.FieldWeights);
        Assert.Equal(expected.Rules.AssociationWeights, actual.Rules.AssociationWeights);
    }

    private static void AssertSettingsEqual(
        CampaignResourceGenerationSettings expected,
        CampaignResourceGenerationSettings actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.ResourceSeed, actual.ResourceSeed);
        Assert.Equal(expected.SeedDerivedFromWorld, actual.SeedDerivedFromWorld);
        Assert.Equal(expected.Abundance, actual.Abundance);
        Assert.Equal(expected.Climate, actual.Climate);
        Assert.Equal(expected.Geology, actual.Geology);
        Assert.Equal(expected.Overrides.Count, actual.Overrides.Count);
        for (var index = 0; index < expected.Overrides.Count; index++)
        {
            var left = expected.Overrides[index];
            var right = actual.Overrides[index];
            Assert.Equal(left.ResourceId, right.ResourceId);
            Assert.Equal(left.Enabled, right.Enabled);
            Assert.Equal(left.CoveragePercent, right.CoveragePercent);
            Assert.Equal(left.Richness, right.Richness);
            Assert.Equal(left.RichnessBias, right.RichnessBias);
            Assert.Equal(left.Concentration, right.Concentration);
            Assert.Equal(left.MapPriority, right.MapPriority);
        }
    }

    private static string ValidDefinitionsDocument(string definitions) =>
        $"{{\"version\":1,\"definitions\":[{definitions}]}}";

    private static string ValidDefinitionsDocumentV2(string definitions) =>
        $"{{\"version\":2,\"definitions\":[{definitions}]}}";

    private static string ValidDefinitionsDocumentV3(string definitions) =>
        $"{{\"version\":3,\"definitions\":[{definitions}]}}";

    private static string ValidDefinitionRecord(string id) =>
        "{" +
        $"\"id\":\"{id}\",\"name\":\"Amber\"," +
        "\"category\":\"finite\",\"distributionProfile\":\"surfaceDeposit\",\"medium\":\"land\"," +
        "\"symbolId\":\"amber\",\"color\":\"#E6A53A\",\"mapPriority\":37,\"coveragePercent\":14," +
        "\"richness\":\"rich\",\"concentration\":\"manySmall\",\"rules\":{" +
        "\"elevationMeters\":null,\"grade\":null,\"waterDistanceKilometers\":null," +
        "\"regionScaleKilometers\":null,\"preferredTerrainTags\":[],\"customTerrainIncludes\":[]," +
        "\"customTerrainExcludes\":[],\"fieldWeights\":[],\"associationWeights\":[]}}";

    private static string ValidDefinitionRecordV2(string id) =>
        ValidDefinitionRecord(id).Replace(
            "\"preferredTerrainTags\":[]",
            "\"preferredTerrainTags\":[],\"avoidedTerrainTags\":[\"arid\"]",
            StringComparison.Ordinal);

    private static string ValidGenerationDocument(string overrides = "") =>
        "{\"schemaVersion\":1,\"resourceSeed\":42,\"seedDerivedFromWorld\":true," +
        "\"abundance\":\"balanced\",\"climate\":\"autoMixed\",\"geology\":\"autoMixed\"," +
        $"\"overrides\":[{overrides}]}}";

    private static string ValidTilesDocument(string tiles) =>
        $"{{\"version\":1,\"tiles\":[{tiles}]}}";

    private static Task WriteAsync(string directory, string fileName, string json) =>
        File.WriteAllTextAsync(Path.Combine(directory, fileName), json);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"KingdomCampaignResourceTests-{Guid.NewGuid():N}");
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
