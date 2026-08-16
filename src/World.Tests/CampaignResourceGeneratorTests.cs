using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceGeneratorTests
{
    [Fact]
    public void Generate_DefaultCopperHasMeaningfulOpportunityOnAContinentalWorld()
    {
        var definition = CreateDefinition(140, 140);
        var terrain = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.Continent,
                Seed: 17_029,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.Balanced));
        var world = new CampaignWorld(definition);
        world.Tiles.SetTiles(terrain.Tiles);
        var catalog = new CampaignResourceCatalog();
        var source = CampaignResourceGenerationSource.Capture(
            new CampaignResourceTerrainQueryV2(world),
            new CampaignResourceMap(definition, catalog));
        var settings = new CampaignResourceGenerationSettings(
            CampaignResourceSeed.FromTerrainSeed(17_029));
        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            settings,
            CampaignResourceGenerationScope.All);
        var copper = result.Reports.Single(static report => report.ResourceId == "copper-ore");

        Assert.True(copper.RequestedTileCount > 0);
        Assert.InRange(
            copper.GeneratedOccurrenceCount,
            Math.Max(1, copper.RequestedTileCount / 2),
            copper.RequestedTileCount);
    }

    [Theory]
    [InlineData(CampaignMapGenerationPreset.Continent)]
    [InlineData(CampaignMapGenerationPreset.Island)]
    [InlineData(CampaignMapGenerationPreset.Archipelago)]
    [InlineData(CampaignMapGenerationPreset.EastCoast)]
    [InlineData(CampaignMapGenerationPreset.WestCoast)]
    [InlineData(CampaignMapGenerationPreset.NorthCoast)]
    [InlineData(CampaignMapGenerationPreset.SouthCoast)]
    [InlineData(CampaignMapGenerationPreset.InlandSea)]
    [InlineData(CampaignMapGenerationPreset.LandOnly)]
    public void Generate_DefaultResourcesKeepSpawnOpportunityAcrossWorldPresets(
        CampaignMapGenerationPreset preset)
    {
        var definition = CreateDefinition(140, 140);
        var catalog = new CampaignResourceCatalog();
        var terrain = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                preset,
                Seed: 17_029,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.Balanced));
        var world = new CampaignWorld(definition);
        world.Tiles.SetTiles(terrain.Tiles);
        var result = new CampaignResourceGenerator().Generate(
            CampaignResourceGenerationSource.Capture(
                new CampaignResourceTerrainQueryV2(world),
                new CampaignResourceMap(definition, catalog)),
            catalog,
            new CampaignResourceGenerationSettings(
                CampaignResourceSeed.FromTerrainSeed(17_029)),
            CampaignResourceGenerationScope.All);
        var missing = result.Reports
            .Where(static report =>
                report.RequestedTileCount > 0 && report.GeneratedOccurrenceCount == 0)
            .Select(static report => report.ResourceId)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Generate_IsDeterministicAndDoesNotMutateCapturedSource()
    {
        var definition = CreateDefinition(18, 14);
        var custom = CreateCustom("test-resource", coverage: 32);
        var catalog = new CampaignResourceCatalog([custom]);
        var map = new CampaignResourceMap(definition, catalog);
        var query = UniformQuery(definition, Land());
        var source = CampaignResourceGenerationSource.Capture(query, map);
        var settings = new CampaignResourceGenerationSettings(17_029);
        var generator = new CampaignResourceGenerator();

        var first = generator.Generate(
            source,
            catalog,
            settings,
            CampaignResourceGenerationScope.ForResource(custom.Id));
        var second = generator.Generate(
            source,
            catalog,
            settings,
            CampaignResourceGenerationScope.ForResource(custom.Id));

        Assert.Equal(
            first.CandidateMap.GetMaterializedOccurrences(),
            second.CandidateMap.GetMaterializedOccurrences());
        Assert.Equal(
            first.Reports.Select(ComparableReport),
            second.Reports.Select(ComparableReport));
        Assert.Empty(map.GetMaterializedOccurrences());
        Assert.Equal(0, map.Revision);
        Assert.True(first.IsCurrent(query, map));
    }

    [Fact]
    public void Generate_ResourceScopePreservesOutsideAndLocksWhileZeroCoverageRemovesUnlocked()
    {
        var definition = CreateDefinition(6, 2);
        var target = CreateCustom("target-resource", coverage: 40);
        var outside = CreateCustom("outside-resource", coverage: 40);
        var catalog = new CampaignResourceCatalog([target, outside]);
        var map = new CampaignResourceMap(definition, catalog);
        var locked = new CampaignResourceOccurrence(target.Id, 83, Locked: true);
        var outsideOccurrence = new CampaignResourceOccurrence(outside.Id, 27, Locked: false);
        map.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, locked),
            CampaignResourceMutation.Upsert(1, 0, new CampaignResourceOccurrence(target.Id, 45)),
            CampaignResourceMutation.Upsert(2, 0, outsideOccurrence),
        ]);
        var settings = new CampaignResourceGenerationSettings(
            91,
            overrides:
            [
                Override(target.Id, coverage: 0),
            ]);
        var query = UniformQuery(definition, Land());
        var source = CampaignResourceGenerationSource.Capture(query, map);

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            settings,
            CampaignResourceGenerationScope.ForResource(target.Id));

        Assert.True(result.CandidateMap.TryGetOccurrence(0, 0, target.Id, out var retained));
        Assert.Equal(locked, retained);
        Assert.False(result.CandidateMap.TryGetOccurrence(1, 0, target.Id, out _));
        Assert.True(result.CandidateMap.TryGetOccurrence(2, 0, outside.Id, out var outsideRetained));
        Assert.Equal(outsideOccurrence, outsideRetained);
        var report = Assert.Single(result.Reports);
        Assert.Equal(0, report.RequestedTileCount);
        Assert.Equal(0, report.GeneratedOccurrenceCount);
        Assert.Equal(1, report.PreservedLockCount);
        Assert.Contains("0%", report.ShortfallReason, StringComparison.Ordinal);
        Assert.Equal(3, map.OccurrenceCount);
    }

    [Fact]
    public void Generate_DisabledResourceIsLocksOnlyEvenWithPositiveCoverage()
    {
        var definition = CreateDefinition(6, 2);
        var target = CreateCustom("target-resource", coverage: 80);
        var catalog = new CampaignResourceCatalog([target]);
        var map = new CampaignResourceMap(definition, catalog);
        map.Apply(
        [
            CampaignResourceMutation.Upsert(
                0,
                0,
                new CampaignResourceOccurrence(target.Id, 83, Locked: true)),
            CampaignResourceMutation.Upsert(
                1,
                0,
                new CampaignResourceOccurrence(target.Id, 45)),
        ]);
        var settings = new CampaignResourceGenerationSettings(
            91,
            overrides:
            [
                new CampaignResourceGenerationOverride(
                    target.Id,
                    enabled: false,
                    coveragePercent: 80,
                    CampaignResourceRichness.Balanced,
                    richnessBias: 0,
                    CampaignResourceConcentration.Balanced,
                    mapPriority: 50),
            ]);
        var source = CampaignResourceGenerationSource.Capture(
            UniformQuery(definition, Land()),
            map);

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            settings,
            CampaignResourceGenerationScope.ForResource(target.Id));
        var report = Assert.Single(result.Reports);

        Assert.Equal(1, result.CandidateMap.OccurrenceCount);
        Assert.Equal(0, report.RequestedTileCount);
        Assert.Equal(0, report.GeneratedOccurrenceCount);
        Assert.Contains("disabled", report.ShortfallReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_CategoryScopeReplacesOnlyThatCategory()
    {
        var definition = CreateDefinition(6, 2);
        var finite = CreateCustom("finite-custom", coverage: 80);
        var renewable = CreateCustom(
            "renewable-custom",
            coverage: 80,
            category: CampaignResourceCategory.Renewable);
        var catalog = new CampaignResourceCatalog([finite, renewable]);
        var map = new CampaignResourceMap(definition, catalog);
        var renewableOccurrence = new CampaignResourceOccurrence(renewable.Id, 66);
        map.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence(finite.Id, 55)),
            CampaignResourceMutation.Upsert(1, 0, renewableOccurrence),
        ]);
        var disabledFinite = catalog.Definitions
            .Where(static definition => definition.Category == CampaignResourceCategory.Finite)
            .Select(definition => new CampaignResourceGenerationOverride(
                definition.Id,
                enabled: false,
                coveragePercent: definition.CoveragePercent,
                definition.Richness,
                richnessBias: 0,
                definition.Concentration,
                definition.MapPriority))
            .ToArray();
        var settings = new CampaignResourceGenerationSettings(91, overrides: disabledFinite);
        var source = CampaignResourceGenerationSource.Capture(
            UniformQuery(definition, Land()),
            map);

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            settings,
            CampaignResourceGenerationScope.ForCategory(CampaignResourceCategory.Finite));

        Assert.False(result.CandidateMap.TryGetOccurrence(0, 0, finite.Id, out _));
        Assert.True(result.CandidateMap.TryGetOccurrence(1, 0, renewable.Id, out var retained));
        Assert.Equal(renewableOccurrence, retained);
        Assert.All(
            result.Reports,
            report => Assert.Equal(
                CampaignResourceCategory.Finite,
                catalog.Get(report.ResourceId).Category));
    }

    [Fact]
    public void GenerationScope_SelectionCanonicalizesIdsAndUsesValueEquality()
    {
        var first = CampaignResourceGenerationScope.ForResources(["timber", "clay", "timber"]);
        var second = CampaignResourceGenerationScope.ForResources(["clay", "timber"]);
        var catalog = new CampaignResourceCatalog();

        Assert.Equal(["clay", "timber"], first.ResourceIds);
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.True(first.Includes(catalog.Get("timber")));
        Assert.False(first.Includes(catalog.Get("gold")));
        first.EnsureValid(catalog);
    }

    [Fact]
    public void GenerationScope_SelectionRejectsEmptyInvalidAndUnknownIds()
    {
        Assert.Throws<ArgumentException>(() => CampaignResourceGenerationScope.ForResources([]));
        Assert.Throws<ArgumentException>(() => CampaignResourceGenerationScope.ForResources(["Invalid ID"]));
        Assert.Throws<ArgumentException>(() => CampaignResourceGenerationScope.ForResources([null!]));

        var unknown = CampaignResourceGenerationScope.ForResources(["not-installed"]);
        Assert.Throws<ArgumentException>(() => unknown.EnsureValid(new CampaignResourceCatalog()));
    }

    [Fact]
    public void Generate_SelectionReplacesOnlyIncludedResourcesAndPreservesExcludedDisabledResource()
    {
        var definition = CreateDefinition(8, 2);
        var includedOff = CreateCustom("included-off", coverage: 80);
        var includedOn = CreateCustom("included-on", coverage: 80);
        var excluded = CreateCustom("excluded-resource", coverage: 80);
        var catalog = new CampaignResourceCatalog([includedOff, includedOn, excluded]);
        var map = new CampaignResourceMap(definition, catalog);
        var excludedOccurrence = new CampaignResourceOccurrence(excluded.Id, 37);
        map.Apply(
        [
            CampaignResourceMutation.Upsert(
                0,
                0,
                new CampaignResourceOccurrence(includedOff.Id, 62)),
            CampaignResourceMutation.Upsert(1, 0, excludedOccurrence),
        ]);
        var settings = new CampaignResourceGenerationSettings(
            91,
            overrides:
            [
                new CampaignResourceGenerationOverride(
                    includedOff.Id,
                    enabled: false,
                    includedOff.CoveragePercent,
                    includedOff.Richness,
                    richnessBias: 0,
                    includedOff.Concentration,
                    includedOff.MapPriority),
                new CampaignResourceGenerationOverride(
                    excluded.Id,
                    enabled: false,
                    excluded.CoveragePercent,
                    excluded.Richness,
                    richnessBias: 0,
                    excluded.Concentration,
                    excluded.MapPriority),
            ]);
        var scope = CampaignResourceGenerationScope.ForResources([includedOn.Id, includedOff.Id]);

        var result = new CampaignResourceGenerator().Generate(
            CampaignResourceGenerationSource.Capture(UniformQuery(definition, Land()), map),
            catalog,
            settings,
            scope);

        Assert.False(result.CandidateMap.TryGetOccurrence(0, 0, includedOff.Id, out _));
        Assert.True(result.CandidateMap.TryGetOccurrence(1, 0, excluded.Id, out var retained));
        Assert.Equal(excludedOccurrence, retained);
        Assert.Equal([includedOff.Id, includedOn.Id], result.Reports.Select(static report => report.ResourceId));
        Assert.True(result.Reports.Single(report => report.ResourceId == includedOn.Id).GeneratedOccurrenceCount > 0);
        Assert.Equal(2, map.OccurrenceCount);
    }

    [Theory]
    [InlineData(CampaignResourceAbundance.Sparse, 2, 30)]
    [InlineData(CampaignResourceAbundance.Balanced, 3, 50)]
    [InlineData(CampaignResourceAbundance.Abundant, 5, 75)]
    [InlineData(CampaignResourceAbundance.Custom, 3, 50)]
    public void Generate_AppliesCoverageMultiplierBeforeFloor(
        CampaignResourceAbundance abundance,
        int expectedTarget,
        double expectedEffectiveCoverage)
    {
        var definition = CreateDefinition(7, 1);
        var custom = CreateCustom("target-resource", coverage: 50);
        var catalog = new CampaignResourceCatalog([custom]);
        var map = new CampaignResourceMap(definition, catalog);
        var source = CampaignResourceGenerationSource.Capture(UniformQuery(definition, Land()), map);
        var settings = new CampaignResourceGenerationSettings(44, abundance: abundance);

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            settings,
            CampaignResourceGenerationScope.ForResource(custom.Id));

        var report = Assert.Single(result.Reports);
        Assert.Equal(expectedTarget, report.RequestedTileCount);
        Assert.Equal(expectedEffectiveCoverage, report.EffectiveCoveragePercent);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(3, 0, 1)]
    public void Generate_LocksCountAgainstUpperTargetAndCanRemainAboveIt(
        int lockCount,
        int expectedGenerated,
        int expectedOverTarget)
    {
        var definition = CreateDefinition(10, 1);
        var custom = CreateCustom("target-resource", coverage: 20);
        var catalog = new CampaignResourceCatalog([custom]);
        var map = new CampaignResourceMap(definition, catalog);
        for (var x = 0; x < lockCount; x++)
        {
            map.Upsert(x, 0, new CampaignResourceOccurrence(custom.Id, (byte)(70 + x), Locked: true));
        }

        var source = CampaignResourceGenerationSource.Capture(UniformQuery(definition, Land()), map);
        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(44),
            CampaignResourceGenerationScope.ForResource(custom.Id));
        var report = Assert.Single(result.Reports);

        Assert.Equal(2, report.RequestedTileCount);
        Assert.Equal(expectedGenerated, report.GeneratedOccurrenceCount);
        Assert.Equal(lockCount, report.PreservedLockCount);
        Assert.Equal(expectedOverTarget, report.OverTargetLockCount);
        for (var x = 0; x < lockCount; x++)
        {
            Assert.True(result.CandidateMap.TryGetOccurrence(x, 0, custom.Id, out var occurrence));
            Assert.True(occurrence.Locked);
            Assert.Equal(70 + x, occurrence.Potential);
        }
    }

    [Fact]
    public void Generate_UnsupportedCustomFactorsProduceLocksOnlyAndListEveryId()
    {
        var definition = CreateDefinition(8, 2);
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            preferredTerrainTags: ["unsupported-tag"],
            fieldWeights: new Dictionary<string, double>
            {
                ["unsupported-field"] = 0,
            },
            associationWeights: new Dictionary<string, double>
            {
                ["unsupported-association"] = -1,
            },
            avoidedTerrainTags: ["unsupported-avoid"]);
        var custom = CreateCustom("custom-resource", coverage: 75, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var map = new CampaignResourceMap(definition, catalog);
        map.Apply(
        [
            CampaignResourceMutation.Upsert(
                0,
                0,
                new CampaignResourceOccurrence(custom.Id, 72, Locked: true)),
            CampaignResourceMutation.Upsert(
                1,
                0,
                new CampaignResourceOccurrence(custom.Id, 55, Locked: false)),
        ]);
        var source = CampaignResourceGenerationSource.Capture(UniformQuery(definition, Land()), map);

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(8),
            CampaignResourceGenerationScope.ForResource(custom.Id));
        var report = Assert.Single(result.Reports);

        Assert.Equal(1, result.CandidateMap.OccurrenceCount);
        Assert.Equal(0, report.GeneratedOccurrenceCount);
        Assert.Contains("unsupported", report.ShortfallReason, StringComparison.OrdinalIgnoreCase);
        var warning = Assert.Single(report.Warnings);
        Assert.Contains("unsupported-association", warning, StringComparison.Ordinal);
        Assert.Contains("unsupported-avoid", warning, StringComparison.Ordinal);
        Assert.Contains("unsupported-field", warning, StringComparison.Ordinal);
        Assert.Contains("unsupported-tag", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryBuiltInPreferredTagIsSupported()
    {
        var unsupported = CampaignResourceCatalog.BuiltInDefinitions
            .SelectMany(static definition => definition.Rules.PreferredTerrainTags)
            .Distinct(StringComparer.Ordinal)
            .Where(tag => !CampaignResourceSupportFieldIds.IsSupported(tag))
            .ToArray();

        Assert.Empty(unsupported);
    }

    [Fact]
    public void EveryBuiltInAvoidedTagIsSupported()
    {
        var unsupported = CampaignResourceCatalog.BuiltInDefinitions
            .SelectMany(static definition => definition.Rules.AvoidedTerrainTags)
            .Distinct(StringComparer.Ordinal)
            .Where(tag => !CampaignResourceSupportFieldIds.IsSupported(tag))
            .ToArray();

        Assert.Empty(unsupported);
        Assert.Contains(
            CampaignResourceCatalog.BuiltInDefinitions,
            static definition => definition.Rules.AvoidedTerrainTags.Count > 0);
    }

    [Fact]
    public void FishCanGenerateAcrossProductiveSeaAndLakeCells()
    {
        var definition = CreateDefinition(10, 2);
        var catalog = new CampaignResourceCatalog();
        var samples = new CampaignResourceTerrainSample[20];
        for (var x = 0; x < 10; x++)
        {
            samples[x] = Water(
                CampaignResourceSurfaceType.Sea,
                seaDistance: 0,
                lakeDistance: double.PositiveInfinity);
            samples[10 + x] = Water(CampaignResourceSurfaceType.Lake, seaDistance: 5, lakeDistance: 0);
        }

        var map = new CampaignResourceMap(definition, catalog);
        var source = CampaignResourceGenerationSource.Capture(new TestTerrainQuery(definition, samples), map);
        var settings = new CampaignResourceGenerationSettings(
            17,
            overrides:
            [
                Override("fish", coverage: 100),
            ]);

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            settings,
            CampaignResourceGenerationScope.ForResource("fish"));
        var entries = result.CandidateMap.GetMaterializedOccurrences();

        Assert.Contains(entries, static entry => entry.Y == 0);
        Assert.Contains(entries, static entry => entry.Y == 1);
        Assert.Equal(20, Assert.Single(result.Reports).RequestedTileCount);
    }

    [Fact]
    public void CalculatePotential_AppliesExactRichnessBiasAndAbundanceShifts()
    {
        var poor = new CampaignResourceEffectiveGenerationSettings(
            Enabled: true,
            CoveragePercent: 40,
            CampaignResourceRichness.Poor,
            RichnessBias: -30,
            CampaignResourceConcentration.Balanced,
            MapPriority: 50);
        var rich = poor with
        {
            Richness = CampaignResourceRichness.Rich,
            RichnessBias = 30,
        };

        Assert.Equal(
            7,
            CampaignResourceGenerator.CalculatePotential(
                suitability: 0.65,
                admissionFloor: 0.30,
                distanceToCoreKilometers: 0,
                radiusKilometers: 80,
                detail: 0.4,
                CampaignResourceAbundance.Sparse,
                poor));
        Assert.Equal(
            100,
            CampaignResourceGenerator.CalculatePotential(
                suitability: 0.65,
                admissionFloor: 0.30,
                distanceToCoreKilometers: 0,
                radiusKilometers: 80,
                detail: 0.4,
                CampaignResourceAbundance.Abundant,
                rich));
    }

    [Fact]
    public void CaptureRejectsRevisionChangesAndReadsTerrainRowMajor()
    {
        var definition = CreateDefinition(3, 2);
        var query = UniformQuery(definition, Land());
        query.MutateRevisionAtCall = 4;
        var map = new CampaignResourceMap(definition, new CampaignResourceCatalog());

        Assert.Throws<InvalidOperationException>(() =>
            CampaignResourceGenerationSource.Capture(query, map));
        Assert.Equal(
            [(0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)],
            query.ReadCoordinates);
    }

    [Fact]
    public void CaptureRejectsResourceMapChangesDuringTerrainCopy()
    {
        var definition = CreateDefinition(3, 2);
        var catalog = new CampaignResourceCatalog();
        var map = new CampaignResourceMap(definition, catalog);
        var query = UniformQuery(definition, Land());
        query.OnRead = call =>
        {
            if (call == 3)
            {
                map.Upsert(0, 0, new CampaignResourceOccurrence("gold", 80, Locked: true));
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
            CampaignResourceGenerationSource.Capture(query, map));
    }

    [Fact]
    public void GenerateHonorsPreCanceledTokenAndCandidateMutationInvalidatesResult()
    {
        var definition = CreateDefinition(8, 8);
        var custom = CreateCustom("test-resource", coverage: 20);
        var catalog = new CampaignResourceCatalog([custom]);
        var map = new CampaignResourceMap(definition, catalog);
        var query = UniformQuery(definition, Land());
        var source = CampaignResourceGenerationSource.Capture(query, map);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var generator = new CampaignResourceGenerator();

        Assert.Throws<OperationCanceledException>(() => generator.Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(9),
            CampaignResourceGenerationScope.ForResource(custom.Id),
            cancellation.Token));
        Assert.Equal(0, map.Revision);

        var result = generator.Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(9),
            CampaignResourceGenerationScope.ForResource(custom.Id));
        Assert.True(result.IsCurrent(query, map));
        result.CandidateMap.Upsert(
            0,
            0,
            new CampaignResourceOccurrence(custom.Id, 1, Locked: false));
        Assert.False(result.IsCurrent(query, map));
    }

    [Fact]
    public void ResourceSeedFallbackIsStableAndChangesWithAuthoritativeTerrain()
    {
        var definition = CreateDefinition(3, 2);
        var catalog = new CampaignResourceCatalog();
        var map = new CampaignResourceMap(definition, catalog);
        var first = CampaignResourceGenerationSource.Capture(UniformQuery(definition, Land()), map);
        var second = CampaignResourceGenerationSource.Capture(UniformQuery(definition, Land()), map);
        var changedSamples = Enumerable.Repeat(Land(), 6).ToArray();
        changedSamples[5] = Land(elevation: 20);
        var changed = CampaignResourceGenerationSource.Capture(
            new TestTerrainQuery(definition, changedSamples),
            map);

        Assert.Equal(CampaignResourceSeed.FromCurrentWorld(first), CampaignResourceSeed.FromCurrentWorld(second));
        Assert.NotEqual(CampaignResourceSeed.FromCurrentWorld(first), CampaignResourceSeed.FromCurrentWorld(changed));
        Assert.Equal(CampaignResourceSeed.FromTerrainSeed(123), CampaignResourceSeed.FromTerrainSeed(123));
    }

    [Fact]
    public void Generate_IsIndependentOfCatalogConstructionOrderAndScopeEvaluation()
    {
        var definition = CreateDefinition(12, 10);
        var alpha = CreateCustom("alpha-resource", coverage: 28);
        var beta = CreateCustom("beta-resource", coverage: 37);
        var firstCatalog = new CampaignResourceCatalog([alpha, beta]);
        var secondCatalog = new CampaignResourceCatalog([beta, alpha]);
        var firstSource = CampaignResourceGenerationSource.Capture(
            UniformQuery(definition, Land()),
            new CampaignResourceMap(definition, firstCatalog));
        var secondSource = CampaignResourceGenerationSource.Capture(
            UniformQuery(definition, Land()),
            new CampaignResourceMap(definition, secondCatalog));
        var settings = new CampaignResourceGenerationSettings(5_411);
        var generator = new CampaignResourceGenerator();

        var all = generator.Generate(
            firstSource,
            firstCatalog,
            settings,
            CampaignResourceGenerationScope.All);
        var alphaOnly = generator.Generate(
            firstSource,
            firstCatalog,
            settings,
            CampaignResourceGenerationScope.ForResource(alpha.Id));
        var reordered = generator.Generate(
            secondSource,
            secondCatalog,
            settings,
            CampaignResourceGenerationScope.ForResource(alpha.Id));

        Assert.Equal(
            alphaOnly.CandidateMap.GetMaterializedOccurrences(),
            all.CandidateMap.GetMaterializedOccurrences()
                .Where(entry => entry.Occurrence.ResourceId == alpha.Id));
        Assert.Equal(
            alphaOnly.CandidateMap.GetMaterializedOccurrences(),
            reordered.CandidateMap.GetMaterializedOccurrences());
    }

    [Fact]
    public void Generate_DoesNotForceRequestedQuotaBelowAdmissionFloor()
    {
        var definition = CreateDefinition(10, 10);
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            preferredTerrainTags: ["freshwater"]);
        var custom = CreateCustom("dry-water-resource", coverage: 80, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var source = CampaignResourceGenerationSource.Capture(
            UniformQuery(definition, Land()),
            new CampaignResourceMap(definition, catalog));

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(77),
            CampaignResourceGenerationScope.ForResource(custom.Id));
        var report = Assert.Single(result.Reports);

        Assert.Equal(100, report.EligibleTileCount);
        Assert.Equal(80, report.RequestedTileCount);
        Assert.Equal(0, report.GeneratedOccurrenceCount);
        Assert.Empty(result.CandidateMap.GetMaterializedOccurrences());
        Assert.Contains("no unsuitable cells were forced", report.ShortfallReason, StringComparison.Ordinal);
    }

    [Fact]
    public void SuitabilityAppliesInclusiveHardRangesBeforeSoftFactors()
    {
        var definition = CreateDefinition(3, 1);
        var samples = new[]
        {
            Land(elevation: 0),
            Land(elevation: 10),
            Land(elevation: 11),
        };
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            elevationMeters: new CampaignResourceRange(0, 10));
        var custom = CreateCustom("range-resource", coverage: 50, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var source = CampaignResourceGenerationSource.Capture(
            new TestTerrainQuery(definition, samples),
            new CampaignResourceMap(definition, catalog));
        var settings = new CampaignResourceGenerationSettings(4);
        var support = CampaignResourceSupportFields.Build(source.Terrain, settings);

        var evaluation = CampaignResourceSuitabilityEvaluator.Evaluate(
            custom,
            source.Terrain,
            support);

        Assert.Equal(2, evaluation.EligibleTileCount);
        Assert.True(evaluation.IsEligible(0, 0));
        Assert.True(evaluation.IsEligible(1, 0));
        Assert.False(evaluation.IsEligible(2, 0));
    }

    [Fact]
    public void CustomTerrainIncludeWhitelistRefinesCustomCellsWithoutRejectingPlainTerrain()
    {
        var definition = CreateDefinition(3, 1);
        var samples = new[]
        {
            Land(),
            Land() with { CustomTerrainId = "orchard" },
            Land() with { CustomTerrainId = "quarry" },
        };
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            customTerrainIncludes: ["orchard"]);
        var custom = CreateCustom("custom-affinity", coverage: 50, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var source = CampaignResourceGenerationSource.Capture(
            new TestTerrainQuery(definition, samples),
            new CampaignResourceMap(definition, catalog));
        var settings = new CampaignResourceGenerationSettings(4);
        var support = CampaignResourceSupportFields.Build(source.Terrain, settings);

        var evaluation = CampaignResourceSuitabilityEvaluator.Evaluate(
            custom,
            source.Terrain,
            support);

        Assert.Equal(2, evaluation.EligibleTileCount);
        Assert.True(evaluation.IsEligible(0, 0));
        Assert.True(evaluation.IsEligible(1, 0));
        Assert.False(evaluation.IsEligible(2, 0));
    }

    [Fact]
    public void PreferredTagsAreAlternativeCuesRatherThanConjunctiveRequirements()
    {
        var definition = CreateDefinition(1, 1);
        var riverLand = Land() with
        {
            RiverDistanceKilometers = 0,
            RiverFeatures = CampaignResourceRiverFeatures.Present,
        };
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            preferredTerrainTags: ["lake", "river"]);
        var custom = CreateCustom("alternative-cue-resource", coverage: 50, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var source = CampaignResourceGenerationSource.Capture(
            new TestTerrainQuery(definition, [riverLand]),
            new CampaignResourceMap(definition, catalog));
        var support = CampaignResourceSupportFields.Build(
            source.Terrain,
            new CampaignResourceGenerationSettings(4));

        var evaluation = CampaignResourceSuitabilityEvaluator.Evaluate(
            custom,
            source.Terrain,
            support);

        Assert.InRange(evaluation.GetSuitability(0, 0), 0.779f, 0.781f);
    }

    [Fact]
    public void AvoidedTagsPenalizeWhenAnyAlternativeCueIsStrong()
    {
        var definition = CreateDefinition(1, 1);
        var riverLand = Land() with
        {
            RiverDistanceKilometers = 0,
            RiverFeatures = CampaignResourceRiverFeatures.Present,
        };
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            avoidedTerrainTags: ["lake", "river"]);
        var custom = CreateCustom("alternative-aversion-resource", coverage: 50, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var source = CampaignResourceGenerationSource.Capture(
            new TestTerrainQuery(definition, [riverLand]),
            new CampaignResourceMap(definition, catalog));
        var support = CampaignResourceSupportFields.Build(
            source.Terrain,
            new CampaignResourceGenerationSettings(4));

        var evaluation = CampaignResourceSuitabilityEvaluator.Evaluate(
            custom,
            source.Terrain,
            support);

        Assert.InRange(evaluation.GetSuitability(0, 0), 0.339f, 0.341f);
    }

    [Fact]
    public void NegativeExplicitWeightInvertsTheSupportResponse()
    {
        var definition = CreateDefinition(2, 1);
        var wet = Land() with
        {
            RiverDistanceKilometers = 0,
            RiverFeatures = CampaignResourceRiverFeatures.Present,
        };
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            fieldWeights: new Dictionary<string, double>
            {
                ["freshwater"] = -1,
            });
        var custom = CreateCustom("inverse-resource", coverage: 50, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var source = CampaignResourceGenerationSource.Capture(
            new TestTerrainQuery(definition, [Land(), wet]),
            new CampaignResourceMap(definition, catalog));
        var settings = new CampaignResourceGenerationSettings(4);
        var support = CampaignResourceSupportFields.Build(source.Terrain, settings);

        var evaluation = CampaignResourceSuitabilityEvaluator.Evaluate(
            custom,
            source.Terrain,
            support);

        Assert.True(evaluation.GetSuitability(0, 0) > 0.99);
        Assert.True(evaluation.GetSuitability(1, 0) < 0.001);
    }

    [Fact]
    public void AvoidedFactorPenalizesStrongResponseWithoutMakingTheTileIneligible()
    {
        var definition = CreateDefinition(2, 1);
        var wet = Land() with
        {
            RiverDistanceKilometers = 0,
            RiverFeatures = CampaignResourceRiverFeatures.Present,
        };
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            avoidedTerrainTags: ["freshwater"]);
        var custom = CreateCustom("dry-preferring-resource", coverage: 50, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var source = CampaignResourceGenerationSource.Capture(
            new TestTerrainQuery(definition, [Land(), wet]),
            new CampaignResourceMap(definition, catalog));
        var support = CampaignResourceSupportFields.Build(
            source.Terrain,
            new CampaignResourceGenerationSettings(4));

        var evaluation = CampaignResourceSuitabilityEvaluator.Evaluate(
            custom,
            source.Terrain,
            support);

        Assert.True(evaluation.IsEligible(0, 0));
        Assert.True(evaluation.IsEligible(1, 0));
        Assert.True(evaluation.GetSuitability(0, 0) > 0.99);
        Assert.InRange(evaluation.GetSuitability(1, 0), 0.119f, 0.121f);
    }

    [Fact]
    public void Generate_UsesAvoidedFactorToPreferOtherwiseEquivalentTiles()
    {
        var definition = CreateDefinition(10, 1);
        var samples = Enumerable.Repeat(Land(), 10).ToArray();
        for (var x = 5; x < samples.Length; x++)
        {
            samples[x] = samples[x] with
            {
                RiverDistanceKilometers = 0,
                RiverFeatures = CampaignResourceRiverFeatures.Present,
            };
        }

        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            avoidedTerrainTags: ["freshwater"]);
        var custom = CreateCustom("dry-preferring-resource", coverage: 20, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var source = CampaignResourceGenerationSource.Capture(
            new TestTerrainQuery(definition, samples),
            new CampaignResourceMap(definition, catalog));

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(17_029),
            CampaignResourceGenerationScope.ForResource(custom.Id));

        var generated = result.CandidateMap.GetMaterializedOccurrences();
        Assert.NotEmpty(generated);
        Assert.All(generated, static entry => Assert.InRange(entry.X, 0, 4));
        Assert.All(generated, static entry => Assert.False(entry.Occurrence.Locked));
    }

    [Fact]
    public void Generate_NeverPlacesUnlockedOccurrencesOnHardExcludedSurfaces()
    {
        var definition = CreateDefinition(10, 1);
        var samples = Enumerable.Repeat(Land(), 10).ToArray();
        for (var x = 5; x < samples.Length; x++)
        {
            samples[x] = samples[x] with { Surface = CampaignResourceSurfaceType.Desert };
        }

        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            excludedTerrainSurfaces: [CampaignResourceSurfaceType.Desert]);
        var custom = CreateCustom("desert-excluding-resource", coverage: 100, rules: rules);
        var catalog = new CampaignResourceCatalog([custom]);
        var source = CampaignResourceGenerationSource.Capture(
            new TestTerrainQuery(definition, samples),
            new CampaignResourceMap(definition, catalog));

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(17_029),
            CampaignResourceGenerationScope.ForResource(custom.Id));
        var report = Assert.Single(result.Reports);

        Assert.Equal(5, report.EligibleTileCount);
        Assert.Equal(5, report.RequestedTileCount);
        Assert.NotEmpty(result.CandidateMap.GetMaterializedOccurrences());
        Assert.All(
            result.CandidateMap.GetMaterializedOccurrences(),
            static entry => Assert.InRange(entry.X, 0, 4));
    }

    [Fact]
    public void ClimateSupportFieldsSampleAtPhysicalKilometerScale()
    {
        var fineDefinition = CreateDefinition(30, 30, tileMeters: 1_000);
        var coarseDefinition = CreateDefinition(10, 10, tileMeters: 3_000);
        var catalog = new CampaignResourceCatalog();
        var fineSource = CampaignResourceGenerationSource.Capture(
            UniformQuery(fineDefinition, Land(elevation: 250)),
            new CampaignResourceMap(fineDefinition, catalog));
        var coarseSource = CampaignResourceGenerationSource.Capture(
            UniformQuery(coarseDefinition, Land(elevation: 250)),
            new CampaignResourceMap(coarseDefinition, catalog));
        var settings = new CampaignResourceGenerationSettings(31_337);
        var fine = CampaignResourceSupportFields.Build(fineSource.Terrain, settings);
        var coarse = CampaignResourceSupportFields.Build(coarseSource.Terrain, settings);

        for (var y = 0; y < coarseDefinition.TilesY; y++)
        {
            for (var x = 0; x < coarseDefinition.TilesX; x++)
            {
                var fineX = (3 * x) + 1;
                var fineY = (3 * y) + 1;
                Assert.Equal(coarse.GetValue("temperature", x, y), fine.GetValue("temperature", fineX, fineY), 6);
                Assert.Equal(coarse.GetValue("moisture", x, y), fine.GetValue("moisture", fineX, fineY), 6);
            }
        }
    }

    [Fact]
    public void SupportFieldsPreserveSignedTectonicTangentsForVeinOrientation()
    {
        var definition = CreateDefinition(30, 30, tileMeters: 2_000);
        var catalog = new CampaignResourceCatalog();
        var source = CampaignResourceGenerationSource.Capture(
            UniformQuery(definition, Land()),
            new CampaignResourceMap(definition, catalog));

        var support = CampaignResourceSupportFields.Build(
            source.Terrain,
            new CampaignResourceGenerationSettings(17_029));

        Assert.All(support.BoundaryTangentX, static value => Assert.InRange(value, -1, 1));
        Assert.All(support.BoundaryTangentY, static value => Assert.InRange(value, -1, 1));
        Assert.True(
            support.BoundaryTangentX.Any(static value => value < 0) ||
            support.BoundaryTangentY.Any(static value => value < 0));
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void Generate_CompletesStandardNineteenThousandSixHundredTileWorldForAllResources()
    {
        var definition = CreateDefinition(140, 140);
        var catalog = new CampaignResourceCatalog();
        var map = new CampaignResourceMap(definition, catalog);
        var source = CampaignResourceGenerationSource.Capture(
            UniformQuery(definition, Land()),
            map);

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(17_029),
            CampaignResourceGenerationScope.All);

        Assert.Equal(catalog.Definitions.Count, result.Reports.Count);
        Assert.InRange(
            result.CandidateMap.OccurrenceCount,
            1,
            CampaignResourceGenerationResult.MaximumCandidateOccurrenceCount);
        Assert.Empty(map.GetMaterializedOccurrences());
    }

    [Fact]
    public void Generate_CoarseTilesStillGrowManySmallRegionsBeyondTheirCores()
    {
        var definition = CreateDefinition(60, 60, tileMeters: 20_000);
        var resource = new CampaignResourceDefinition(
            "coarse-surface-deposit",
            "Coarse Surface Deposit",
            CampaignResourceCategory.Finite,
            CampaignResourceDistributionProfile.SurfaceDeposit,
            CampaignResourceMedium.Land,
            "stone",
            "#777777",
            mapPriority: 50,
            coveragePercent: 35,
            CampaignResourceRichness.Balanced,
            CampaignResourceConcentration.ManySmall,
            new CampaignResourceRuleSet(CampaignResourceMedium.Land));
        var catalog = new CampaignResourceCatalog([resource]);
        var source = CampaignResourceGenerationSource.Capture(
            UniformQuery(definition, Land()),
            new CampaignResourceMap(definition, catalog));

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(17_029),
            CampaignResourceGenerationScope.ForResource(resource.Id));
        var report = Assert.Single(result.Reports);

        Assert.InRange(
            report.GeneratedOccurrenceCount,
            report.RequestedTileCount / 2,
            report.RequestedTileCount);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void Generate_CompletesTwoHundredFiftyThousandTileWorldForSelectedResource()
    {
        var definition = CreateDefinition(500, 500, tileMeters: 1_000);
        var resource = CreateCustom("large-world-resource", coverage: 5);
        var catalog = new CampaignResourceCatalog([resource]);
        var map = new CampaignResourceMap(definition, catalog);
        var source = CampaignResourceGenerationSource.Capture(
            UniformQuery(definition, Land()),
            map);

        var result = new CampaignResourceGenerator().Generate(
            source,
            catalog,
            new CampaignResourceGenerationSettings(31_337),
            CampaignResourceGenerationScope.ForResource(resource.Id));

        var report = Assert.Single(result.Reports);
        Assert.Equal(250_000, report.EligibleTileCount);
        Assert.InRange(report.GeneratedOccurrenceCount, 1, report.RequestedTileCount);
        Assert.Equal(report.ActualOccurrenceCount, result.CandidateMap.OccurrenceCount);
        Assert.Empty(map.GetMaterializedOccurrences());
    }

    [Fact]
    public void CandidateOccurrenceLimitAllowsBoundaryAndRejectsOneAbove()
    {
        CampaignResourceGenerator.EnsureCandidateLimit(
            CampaignResourceGenerationResult.MaximumCandidateOccurrenceCount);

        var exception = Assert.Throws<CampaignResourceGenerationLimitException>(() =>
            CampaignResourceGenerator.EnsureCandidateLimit(
                CampaignResourceGenerationResult.MaximumCandidateOccurrenceCount + 1L));
        Assert.Equal(2_000_001, exception.OccurrenceCount);
        Assert.Contains("Narrow the scope", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CampaignResourceDistributionProfile.Field, 80, 0.30)]
    [InlineData(CampaignResourceDistributionProfile.Vein, 35, 0.40)]
    [InlineData(CampaignResourceDistributionProfile.Basin, 65, 0.42)]
    [InlineData(CampaignResourceDistributionProfile.SurfaceDeposit, 25, 0.38)]
    [InlineData(CampaignResourceDistributionProfile.Aquatic, 70, 0.30)]
    public void DistributionProfilesUseFixedPhysicalDefaultsAndAdmissionFloors(
        CampaignResourceDistributionProfile profile,
        double expectedRadiusKilometers,
        double expectedAdmissionFloor)
    {
        var medium = profile == CampaignResourceDistributionProfile.Aquatic
            ? CampaignResourceMedium.Water
            : CampaignResourceMedium.Land;
        var definition = new CampaignResourceDefinition(
            "profile-resource",
            "Profile Resource",
            CampaignResourceCategory.Finite,
            profile,
            medium,
            "ore",
            "#887766",
            mapPriority: 50,
            coveragePercent: 10,
            CampaignResourceRichness.Balanced,
            CampaignResourceConcentration.Balanced);
        var effective = new CampaignResourceEffectiveGenerationSettings(
            Enabled: true,
            CoveragePercent: 10,
            CampaignResourceRichness.Balanced,
            RichnessBias: 0,
            CampaignResourceConcentration.Balanced,
            MapPriority: 50);

        Assert.Equal(
            expectedRadiusKilometers,
            CampaignResourceGenerator.GetRegionRadiusKilometers(definition, effective, tileKilometers: 5));
        Assert.Equal(expectedAdmissionFloor, CampaignResourceGenerator.GetAdmissionFloor(profile));
    }

    [Theory]
    [InlineData(CampaignResourceConcentration.FewLarge, 128)]
    [InlineData(CampaignResourceConcentration.Balanced, 80)]
    [InlineData(CampaignResourceConcentration.ManySmall, 48)]
    public void ConcentrationScalesPhysicalRegionRadius(
        CampaignResourceConcentration concentration,
        double expectedRadiusKilometers)
    {
        var definition = CreateCustom("profile-resource", coverage: 10);
        var effective = new CampaignResourceEffectiveGenerationSettings(
            Enabled: true,
            CoveragePercent: 10,
            CampaignResourceRichness.Balanced,
            RichnessBias: 0,
            concentration,
            MapPriority: 50);

        Assert.Equal(
            expectedRadiusKilometers,
            CampaignResourceGenerator.GetRegionRadiusKilometers(definition, effective, tileKilometers: 5));
    }

    private static object ComparableReport(CampaignResourceGenerationReport report) => new
    {
        report.ResourceId,
        report.EligibleTileCount,
        report.RequestedTileCount,
        report.ActualOccurrenceCount,
        report.GeneratedOccurrenceCount,
        report.RegionCount,
        report.MeanPotential,
        report.MaximumPotential,
        report.PreservedLockCount,
        report.OverTargetLockCount,
        report.EffectiveCoveragePercent,
        report.ActualCoveragePercent,
        report.ShortfallReason,
        Warnings = string.Join("\n", report.Warnings),
    };

    private static CampaignWorldDefinition CreateDefinition(int tilesX, int tilesY, int tileMeters = 5_000) =>
        CampaignWorldDefinition.Create(
            tilesX * (long)tileMeters,
            tilesY * (long)tileMeters,
            tileMeters,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

    private static CampaignResourceDefinition CreateCustom(
        string id,
        int coverage,
        CampaignResourceRuleSet? rules = null,
        CampaignResourceCategory category = CampaignResourceCategory.Finite) =>
        new(
            id,
            id.Replace('-', ' '),
            category,
            CampaignResourceDistributionProfile.Field,
            CampaignResourceMedium.Land,
            "ore",
            "#887766",
            mapPriority: 50,
            coverage,
            CampaignResourceRichness.Balanced,
            CampaignResourceConcentration.Balanced,
            rules);

    private static CampaignResourceGenerationOverride Override(string id, int coverage) =>
        new(
            id,
            enabled: true,
            coverage,
            CampaignResourceRichness.Balanced,
            richnessBias: 0,
            CampaignResourceConcentration.Balanced,
            mapPriority: 50);

    private static TestTerrainQuery UniformQuery(
        CampaignWorldDefinition definition,
        CampaignResourceTerrainSample sample) =>
        new(definition, Enumerable.Repeat(sample, checked((int)definition.TileCount)).ToArray());

    private static CampaignResourceTerrainSample Land(short elevation = 0) =>
        new(
            CampaignResourceTerrainKind.Land,
            CampaignResourceSurfaceType.Grassland,
            CampaignResourceTerrainForm.Flat,
            CustomTerrainId: null,
            elevation,
            MaximumCardinalGrade: 0,
            SeaDistanceKilometers: double.PositiveInfinity,
            LakeDistanceKilometers: double.PositiveInfinity,
            RiverDistanceKilometers: double.PositiveInfinity,
            CampaignResourceRiverFeatures.None,
            CampaignResourceCoastFlags.None);

    private static CampaignResourceTerrainSample Water(
        CampaignResourceSurfaceType surface,
        double seaDistance,
        double lakeDistance) =>
        new(
            CampaignResourceTerrainKind.Water,
            surface,
            CampaignResourceTerrainForm.Flat,
            CustomTerrainId: null,
            ElevationMeters: 0,
            MaximumCardinalGrade: 0,
            seaDistance,
            lakeDistance,
            RiverDistanceKilometers: double.PositiveInfinity,
            CampaignResourceRiverFeatures.None,
            CampaignResourceCoastFlags.CoastalWater);

    private sealed class TestTerrainQuery : ICampaignResourceTerrainQuery
    {
        private readonly CampaignResourceTerrainSample[] _samples;
        private int _callCount;

        public TestTerrainQuery(
            CampaignWorldDefinition definition,
            CampaignResourceTerrainSample[] samples)
        {
            Definition = definition;
            _samples = samples;
        }

        public CampaignWorldDefinition Definition { get; }

        public long Revision { get; private set; }

        public int? MutateRevisionAtCall { get; set; }

        public Action<int>? OnRead { get; set; }

        public List<(int X, int Y)> ReadCoordinates { get; } = [];

        public CampaignResourceTerrainSample GetSample(int x, int y)
        {
            ReadCoordinates.Add((x, y));
            _callCount++;
            OnRead?.Invoke(_callCount);
            if (_callCount == MutateRevisionAtCall)
            {
                Revision++;
            }

            return _samples[(y * Definition.TilesX) + x];
        }
    }
}
