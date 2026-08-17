using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonGeneratorTests
{
    [Fact]
    public void Generate_UsesOrderedFirstMatchAndCustomCatchAllWithoutQuotas()
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
            priorityIds: [north.Id, south.Id]);
        var source = Capture(definition, catalog, map, Land());

        var result = CampaignSeasonGenerator.Generate(
            source,
            catalog,
            settings,
            CampaignSeasonGenerationScope.All);

        Assert.All(result.CandidateMap.GetTiles(new CampaignTileArea(0, 0, 3, 0)),
            entry => Assert.Equal(north.Id, entry.Tile.SeasonId));
        Assert.All(result.CandidateMap.GetTiles(new CampaignTileArea(0, 1, 3, 1)),
            entry => Assert.Equal(south.Id, entry.Tile.SeasonId));
        Assert.Equal(8, result.ChangedTileCount);
        Assert.Equal(50, Report(result, north.Id).CandidateCoveragePercent, precision: 8);
        Assert.Equal(50, Report(result, south.Id).CandidateCoveragePercent, precision: 8);
        Assert.Null(Report(result, north.Id).ZeroReason);
    }

    [Fact]
    public void Generate_ReportsLowerPriorityShadowingAndManualOnlyDefinitions()
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
                priorityIds: [first.Id, final.Id]),
            CampaignSeasonGenerationScope.All);

        var firstReport = Report(result, first.Id);
        var finalReport = Report(result, final.Id);
        var manualReport = Report(result, manual.Id);
        Assert.Equal(6, firstReport.EnvironmentalMatchCount);
        Assert.Equal(6, firstReport.PriorityWinCount);
        Assert.Equal(6, finalReport.EnvironmentalMatchCount);
        Assert.Equal(6, finalReport.ShadowedMatchCount);
        Assert.Equal(0, finalReport.CandidateTileCount);
        Assert.Contains("higher-priority", finalReport.ZeroReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(manualReport.GenerationEnabled);
        Assert.Contains("Manual-paint-only", manualReport.ZeroReason, StringComparison.Ordinal);
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
        map.Paint(0, 0, CampaignSeasonCatalog.SummerId, locked: true);
        map.Paint(3, 1, CampaignSeasonCatalog.AutumnId, locked: false);
        var sourceRevision = map.Revision;
        var result = CampaignSeasonGenerator.Generate(
            Capture(definition, catalog, map, Land()),
            catalog,
            new CampaignSeasonGenerationSettings(
                17_029,
                priorityIds: [north.Id, south.Id]),
            CampaignSeasonGenerationScope.ForArea(new CampaignTileArea(0, 0, 1, 1)));

        Assert.Equal(
            new CampaignSeasonTile(CampaignSeasonCatalog.SummerId, Locked: true),
            result.CandidateMap.GetTile(0, 0));
        Assert.Equal(north.Id, result.CandidateMap.GetTile(1, 0).SeasonId);
        Assert.Equal(south.Id, result.CandidateMap.GetTile(0, 1).SeasonId);
        Assert.Equal(CampaignSeasonCatalog.AutumnId, result.CandidateMap.GetTile(3, 1).SeasonId);
        Assert.Equal(1, Report(result, north.Id).LockedOverrideCount);
        Assert.Equal(sourceRevision, map.Revision);
        Assert.Equal(CampaignSeasonCatalog.AutumnId, map.GetTile(3, 1).SeasonId);
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
                priorityIds: [impossible.Id, final.Id]),
            CampaignSeasonGenerationScope.All);

        var report = Report(result, impossible.Id);
        Assert.Equal(0, report.EnvironmentalMatchCount);
        Assert.Equal(0, report.CandidateTileCount);
        Assert.Contains("No tile passed", report.ZeroReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(definition.TileCount, Report(result, final.Id).CandidateTileCount);
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

        Assert.Equal(first.CandidateMap.GetAllTiles(), second.CandidateMap.GetAllTiles());
        Assert.Equal(first.Reports, second.Reports);
        Assert.Equal(0, map.Revision);
        Assert.All(map.GetAllTiles(), entry =>
            Assert.Equal(CampaignSeasonCatalog.SpringId, entry.Tile.SeasonId));
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
        map.Paint(0, 0, CampaignSeasonCatalog.WinterId, locked: true);
        var source = CampaignSeasonGenerationSource.Capture(
            new CampaignSeasonTerrainQueryV2(world),
            map);

        world.Tiles.SetTile(0, 0, new CampaignTileData(CampaignTileType.Desert, 900));
        map.Paint(0, 0, CampaignSeasonCatalog.SummerId, locked: false);
        Assert.Equal(CampaignTileType.Plains, source.Terrain.GetSample(0, 0).TerrainType);
        Assert.Equal(100, source.Terrain.GetSample(0, 0).ElevationMeters);
        Assert.Equal(
            new CampaignSeasonTile(CampaignSeasonCatalog.WinterId, Locked: true),
            source.CurrentTiles[0]);

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
        map.Paint(0, 0, CampaignSeasonCatalog.WinterId);
        Assert.False(second.IsCurrent(query, map));

        var third = CampaignSeasonGenerator.Generate(
            CampaignSeasonGenerationSource.Capture(query, map),
            catalog,
            new CampaignSeasonGenerationSettings(1),
            CampaignSeasonGenerationScope.All);
        third.CandidateMap.Paint(0, 0, CampaignSeasonCatalog.SummerId);
        Assert.False(third.IsCurrent(query, map));
    }

    [Fact]
    public void Generate_CoversTheRepresentative140By140CampaignGridExactlyOnce()
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
            19_600,
            result.Reports
                .Where(static report => report.GenerationEnabled)
                .Sum(static report => report.CandidateTileCount));
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
                CampaignSeasonCatalog.AutumnId,
                CampaignSeasonCatalog.SummerId,
            },
            id => Assert.True(Report(result, id).CandidateTileCount > 0, id));
    }

    [Fact]
    public void Generate_ZeroTiltRemovesSpringAndAutumnButRetainsColdAndWarmBands()
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

        Assert.Equal(0, Report(result, CampaignSeasonCatalog.SpringId).CandidateTileCount);
        Assert.Equal(0, Report(result, CampaignSeasonCatalog.AutumnId).CandidateTileCount);
        Assert.True(Report(result, CampaignSeasonCatalog.WinterId).CandidateTileCount > 0);
        Assert.True(Report(result, CampaignSeasonCatalog.SummerId).CandidateTileCount > 0);
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
