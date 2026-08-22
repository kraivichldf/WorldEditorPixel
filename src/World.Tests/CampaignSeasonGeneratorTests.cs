using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonGeneratorTests
{
    [Fact]
    public void Generate_EvaluatesDefinitionsIndependentlyAndKeepsOverlaps()
    {
        var definition = CreateDefinition(4, 2);
        var north = Custom(
            "northern-season",
            new CampaignSeasonRule(latitudeDegrees: new CampaignSeasonRange(0, 90)));
        var south = Custom("southern-season", CampaignSeasonRule.Unrestricted);
        var catalog = new CampaignSeasonCatalog([north, south]);
        var map = new CampaignSeasonMap(definition, catalog);
        var settings = new CampaignSeasonGenerationSettings(
            17_029,
            enabledSeasonIds: [north.Id, south.Id]);
        var source = Capture(definition, catalog, map, Land());

        var result = CampaignSeasonGenerator.Generate(
            source,
            catalog,
            settings,
            CampaignSeasonGenerationScope.All);

        for (var x = 0; x < definition.TilesX; x++)
        {
            Assert.True(result.CandidateMap.TryGetOccurrence(x, 0, north.Id, out _));
            Assert.True(result.CandidateMap.TryGetOccurrence(x, 0, south.Id, out _));
            Assert.False(result.CandidateMap.TryGetOccurrence(x, 1, north.Id, out _));
            Assert.True(result.CandidateMap.TryGetOccurrence(x, 1, south.Id, out _));
        }

        Assert.Equal(12, result.ChangedIdentityCount);
        Assert.Equal(50, Report(result, north.Id).CandidateCoveragePercent, precision: 8);
        Assert.Equal(100, Report(result, south.Id).CandidateCoveragePercent, precision: 8);
        Assert.Null(Report(result, north.Id).ZeroReason);
    }

    [Fact]
    public void Generate_AllowsEveryMatchingDefinitionAndPreservesExcludedDefinitions()
    {
        var definition = CreateDefinition(3, 2);
        var first = Custom("first-season", CampaignSeasonRule.Unrestricted);
        var final = Custom("final-season", CampaignSeasonRule.Unrestricted);
        var manual = Custom("manual-season", CampaignSeasonRule.Unrestricted);
        var catalog = new CampaignSeasonCatalog([first, final, manual]);
        var map = new CampaignSeasonMap(definition, catalog);
        var result = CampaignSeasonGenerator.Generate(
            Capture(definition, catalog, map, Land()),
            catalog,
            new CampaignSeasonGenerationSettings(
                91,
                enabledSeasonIds: [first.Id, final.Id]),
            CampaignSeasonGenerationScope.All);

        var firstReport = Report(result, first.Id);
        var finalReport = Report(result, final.Id);
        var manualReport = Report(result, manual.Id);
        Assert.Equal(6, firstReport.EnvironmentalMatchCount);
        Assert.Equal(6, firstReport.CandidateOccurrenceCount);
        Assert.Equal(6, finalReport.EnvironmentalMatchCount);
        Assert.Equal(6, finalReport.CandidateOccurrenceCount);
        Assert.Equal(12, result.CandidateMap.OccurrenceCount);
        Assert.False(manualReport.Selected);
        Assert.Contains("Excluded", manualReport.ZeroReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_PreservesLocksAndEverythingOutsideRectangularScope()
    {
        var definition = CreateDefinition(4, 2);
        var north = Custom(
            "northern-season",
            new CampaignSeasonRule(latitudeDegrees: new CampaignSeasonRange(0, 90)));
        var south = Custom("southern-season", CampaignSeasonRule.Unrestricted);
        var catalog = new CampaignSeasonCatalog([north, south]);
        var map = new CampaignSeasonMap(definition, catalog);
        map.Upsert(0, 0, new(CampaignSeasonCatalog.SummerId, Locked: true));
        map.Upsert(0, 1, new(north.Id, Locked: true));
        map.Upsert(3, 1, new(CampaignSeasonCatalog.FallId));
        var sourceRevision = map.Revision;
        var result = CampaignSeasonGenerator.Generate(
            Capture(definition, catalog, map, Land()),
            catalog,
            new CampaignSeasonGenerationSettings(
                17_029,
                enabledSeasonIds: [north.Id, south.Id]),
            CampaignSeasonGenerationScope.ForArea(new CampaignTileArea(0, 0, 1, 1)));

        Assert.True(result.CandidateMap.TryGetOccurrence(0, 0, CampaignSeasonCatalog.SummerId, out var summer));
        Assert.True(summer.Locked);
        Assert.True(result.CandidateMap.TryGetOccurrence(0, 0, north.Id, out _));
        Assert.True(result.CandidateMap.TryGetOccurrence(0, 0, south.Id, out _));
        Assert.True(result.CandidateMap.TryGetOccurrence(1, 0, north.Id, out _));
        Assert.True(result.CandidateMap.TryGetOccurrence(0, 1, north.Id, out var lockedNorth));
        Assert.True(lockedNorth.Locked);
        Assert.True(result.CandidateMap.TryGetOccurrence(0, 1, south.Id, out _));
        Assert.True(result.CandidateMap.TryGetOccurrence(3, 1, CampaignSeasonCatalog.FallId, out _));
        Assert.Equal(1, Report(result, north.Id).PreservedLockCount);
        Assert.Equal(sourceRevision, map.Revision);
        Assert.True(map.TryGetOccurrence(3, 1, CampaignSeasonCatalog.FallId, out _));
    }

    [Fact]
    public void Generate_ExplainsGeographicZeroWithoutForcingAPlacement()
    {
        var definition = CreateDefinition(4, 2);
        var impossible = Custom(
            "polar-pin",
            new CampaignSeasonRule(latitudeDegrees: new CampaignSeasonRange(89.9, 90)));
        var final = Custom("final-season", CampaignSeasonRule.Unrestricted);
        var catalog = new CampaignSeasonCatalog([impossible, final]);
        var result = CampaignSeasonGenerator.Generate(
            Capture(definition, catalog, new CampaignSeasonMap(definition, catalog), Land()),
            catalog,
            new CampaignSeasonGenerationSettings(
                17_029,
                enabledSeasonIds: [impossible.Id, final.Id]),
            CampaignSeasonGenerationScope.All);

        var report = Report(result, impossible.Id);
        Assert.Equal(0, report.EnvironmentalMatchCount);
        Assert.Equal(0, report.CandidateOccurrenceCount);
        Assert.Contains("No tile passed", report.ZeroReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(definition.TileCount, Report(result, final.Id).CandidateOccurrenceCount);
    }

    [Fact]
    public void Generate_IsDeterministicAndNeverMutatesCapturedAuthority()
    {
        var definition = CreateDefinition(12, 8);
        var catalog = new CampaignSeasonCatalog();
        var map = new CampaignSeasonMap(definition, catalog);
        var source = Capture(definition, catalog, map, Land(elevation: 240));
        var settings = new CampaignSeasonGenerationSettings(17_029);

        var first = CampaignSeasonGenerator.Generate(
            source,
            catalog,
            settings,
            CampaignSeasonGenerationScope.All);
        var second = CampaignSeasonGenerator.Generate(
            source,
            catalog,
            settings,
            CampaignSeasonGenerationScope.All);

        Assert.Equal(
            first.CandidateMap.GetMaterializedOccurrences(),
            second.CandidateMap.GetMaterializedOccurrences());
        Assert.Equal(first.Reports, second.Reports);
        Assert.Equal(0, map.Revision);
        Assert.Empty(map.GetMaterializedOccurrences());
    }

    [Fact]
    public void Generate_RejectsCatalogIdentityMismatchAndHonorsCancellation()
    {
        var definition = CreateDefinition(4, 2);
        var catalog = new CampaignSeasonCatalog();
        var source = Capture(
            definition,
            catalog,
            new CampaignSeasonMap(definition, catalog),
            Land());
        Assert.Throws<ArgumentException>(() => CampaignSeasonGenerator.Generate(
            source,
            new CampaignSeasonCatalog(),
            new CampaignSeasonGenerationSettings(1),
            CampaignSeasonGenerationScope.All));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => CampaignSeasonGenerator.Generate(
            source,
            catalog,
            new CampaignSeasonGenerationSettings(1),
            CampaignSeasonGenerationScope.All,
            cancellation.Token));
    }

    [Fact]
    public void Capture_IsIndependentAndDetectsTerrainRevisionDrift()
    {
        var definition = CreateDefinition(2, 1);
        var catalog = new CampaignSeasonCatalog();
        var world = new CampaignWorld(definition);
        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Plains, 100));
        var map = new CampaignSeasonMap(definition, catalog);
        map.Upsert(0, 0, new(CampaignSeasonCatalog.WinterId, Locked: true));
        var source = CampaignSeasonGenerationSource.Capture(
            new CampaignSeasonTerrainQueryV2(world),
            map);

        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Desert, 900));
        map.Upsert(0, 0, new(CampaignSeasonCatalog.SummerId));
        Assert.Equal(CampaignTileType.Plains, source.Terrain.GetSample(0, 0).TerrainType);
        Assert.Equal(100, source.Terrain.GetSample(0, 0).ElevationMeters);
        Assert.Equal(
            new CampaignSeasonEntry(
                0,
                0,
                new CampaignSeasonOccurrence(CampaignSeasonCatalog.WinterId, Locked: true)),
            Assert.Single(source.CurrentEntries));

        var drifting = new DriftingTerrainQuery(definition, Land());
        Assert.Throws<InvalidOperationException>(() =>
            CampaignSeasonGenerationSource.Capture(
                drifting,
                new CampaignSeasonMap(definition, catalog)));
    }

    [Fact]
    public void Result_BecomesStaleWhenTerrainSeasonOrCandidateChanges()
    {
        var definition = CreateDefinition(4, 2);
        var catalog = new CampaignSeasonCatalog();
        var world = new CampaignWorld(definition);
        var map = new CampaignSeasonMap(definition, catalog);
        var query = new CampaignSeasonTerrainQueryV2(world);
        var result = CampaignSeasonGenerator.Generate(
            CampaignSeasonGenerationSource.Capture(query, map),
            catalog,
            new CampaignSeasonGenerationSettings(1),
            CampaignSeasonGenerationScope.All);
        Assert.True(result.IsCurrent(query, map));

        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Plains, 10));
        Assert.False(result.IsCurrent(query, map));

        var second = CampaignSeasonGenerator.Generate(
            CampaignSeasonGenerationSource.Capture(query, map),
            catalog,
            new CampaignSeasonGenerationSettings(1),
            CampaignSeasonGenerationScope.All);
        map.Upsert(0, 0, new(CampaignSeasonCatalog.WinterId));
        Assert.False(second.IsCurrent(query, map));

        var third = CampaignSeasonGenerator.Generate(
            CampaignSeasonGenerationSource.Capture(query, map),
            catalog,
            new CampaignSeasonGenerationSettings(1),
            CampaignSeasonGenerationScope.All);
        var firstEntry = third.CandidateMap.GetMaterializedOccurrences()[0];
        third.CandidateMap.SetLocked(
            firstEntry.X,
            firstEntry.Y,
            firstEntry.Occurrence.SeasonId,
            !firstEntry.Occurrence.Locked);
        Assert.False(third.IsCurrent(query, map));
    }

    [Fact]
    public void Generate_CoversRepresentative140By140GridWithIndependentOccurrences()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var catalog = new CampaignSeasonCatalog();
        var map = new CampaignSeasonMap(definition, catalog);
        var result = CampaignSeasonGenerator.Generate(
            Capture(definition, catalog, map, Land(elevation: 180)),
            catalog,
            new CampaignSeasonGenerationSettings(17_029),
            CampaignSeasonGenerationScope.All);

        Assert.Equal(19_600, result.CandidateMap.TileCount);
        Assert.Equal(
            result.CandidateMap.OccurrenceCount,
            result.Reports
                .Where(static report => report.Selected)
                .Sum(static report => report.CandidateOccurrenceCount));
        Assert.True(result.CandidateMap.OccurrenceCount >= 19_600);
        Assert.Empty(result.CandidateMap.Validate());
    }

    [Fact]
    public void Generate_DefaultRulesProduceEarthLikeFourSeasonBands()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 4_000_000,
            worldHeightMeters: 36_000_000,
            campaignTileSizeMeters: 500_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var catalog = new CampaignSeasonCatalog();
        var climate = new CampaignSeasonClimateSettings(
            temperatureNoiseCelsius: 0,
            moistureNoiseStrength: 0,
            rainShadowStrength: 0);
        var result = CampaignSeasonGenerator.Generate(
            Capture(
                definition,
                catalog,
                new CampaignSeasonMap(definition, catalog),
                Land()),
            catalog,
            new CampaignSeasonGenerationSettings(
                CampaignSeasonSeed.FromTerrainSeed(17_029),
                climate: climate),
            CampaignSeasonGenerationScope.All);

        Assert.All(
            new[]
            {
                CampaignSeasonCatalog.WinterId,
                CampaignSeasonCatalog.SpringId,
                CampaignSeasonCatalog.FallId,
                CampaignSeasonCatalog.SummerId,
            },
            id => Assert.True(Report(result, id).CandidateOccurrenceCount > 0, id));
    }

    [Fact]
    public void Generate_ZeroTiltRemovesSeasonsThatRequireAnnualSeasonality()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 4_000_000,
            worldHeightMeters: 36_000_000,
            campaignTileSizeMeters: 500_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var catalog = new CampaignSeasonCatalog();
        var result = CampaignSeasonGenerator.Generate(
            Capture(
                definition,
                catalog,
                new CampaignSeasonMap(definition, catalog),
                Land()),
            catalog,
            new CampaignSeasonGenerationSettings(
                17_029,
                axialTiltDegrees: 0,
                climate: new CampaignSeasonClimateSettings(
                    temperatureNoiseCelsius: 0,
                    moistureNoiseStrength: 0,
                    rainShadowStrength: 0)),
            CampaignSeasonGenerationScope.All);

        Assert.Equal(0, Report(result, CampaignSeasonCatalog.SpringId).CandidateOccurrenceCount);
        Assert.Equal(0, Report(result, CampaignSeasonCatalog.FallId).CandidateOccurrenceCount);
        Assert.Equal(0, Report(result, CampaignSeasonCatalog.WinterId).CandidateOccurrenceCount);
        Assert.True(Report(result, CampaignSeasonCatalog.SummerId).CandidateOccurrenceCount > 0);
    }

    private static CampaignSeasonGenerationReport Report(
        CampaignSeasonGenerationResult result,
        string id) =>
        result.Reports.Single(report => string.Equals(report.SeasonId, id, StringComparison.Ordinal));

    private static CampaignSeasonDefinition Custom(string id, CampaignSeasonRule rule) =>
        new(
            id,
            id.Replace('-', ' '),
            CampaignBuiltInSeason.Spring,
            "#6688AA",
            tintStrengthPercent: 40,
            effectIntensityPercent: 40,
            rule);

    private static CampaignSeasonGenerationSource Capture(
        CampaignWorldDefinition definition,
        CampaignSeasonCatalog catalog,
        CampaignSeasonMap map,
        CampaignSeasonTerrainSample sample) =>
        CampaignSeasonGenerationSource.Capture(
            new UniformTerrainQuery(definition, sample),
            map);

    private static CampaignSeasonTerrainSample Land(short elevation = 0) =>
        new(CampaignTileType.Plains, null, elevation, CampaignSeasonWaterFeatures.None);

    private static CampaignWorldDefinition CreateDefinition(int width, int height) =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: (long)width * 100_000,
            worldHeightMeters: (long)height * 100_000,
            campaignTileSizeMeters: 100_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

    private sealed class UniformTerrainQuery : ICampaignSeasonTerrainQuery
    {
        private readonly CampaignSeasonTerrainSample _sample;

        public UniformTerrainQuery(
            CampaignWorldDefinition definition,
            CampaignSeasonTerrainSample sample)
        {
            Definition = definition;
            _sample = sample;
        }

        public CampaignWorldDefinition Definition { get; }

        public long Revision => 0;

        public CampaignSeasonTerrainSample GetSample(int x, int y) => _sample;
    }

    private sealed class DriftingTerrainQuery : ICampaignSeasonTerrainQuery
    {
        private readonly CampaignSeasonTerrainSample _sample;
        private int _reads;

        public DriftingTerrainQuery(
            CampaignWorldDefinition definition,
            CampaignSeasonTerrainSample sample)
        {
            Definition = definition;
            _sample = sample;
        }

        public CampaignWorldDefinition Definition { get; }

        public long Revision => _reads == 0 ? 0 : 1;

        public CampaignSeasonTerrainSample GetSample(int x, int y)
        {
            _reads++;
            return _sample;
        }
    }
}
