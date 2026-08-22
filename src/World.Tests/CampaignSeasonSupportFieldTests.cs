using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonSupportFieldTests
{
    [Fact]
    public void WholeGlobe_UsesTileCenterLongitudeLatitudeAndAnnualSeasonality()
    {
        var definition = CreateDefinition(4, 2, 100_000);
        var support = Build(
            definition,
            UniformSamples(definition, Land()),
            new CampaignSeasonGenerationSettings(17_029));

        var northWest = support.GetSample(0, 0);
        var southEast = support.GetSample(3, 1);

        Assert.Equal(-135, northWest.LongitudeDegrees!.Value, precision: 6);
        Assert.Equal(45, northWest.LatitudeDegrees, precision: 6);
        Assert.Equal(135, southEast.LongitudeDegrees!.Value, precision: 6);
        Assert.Equal(-45, southEast.LatitudeDegrees, precision: 6);
        Assert.True(northWest.Seasonality > 0);
        Assert.Equal(northWest.Seasonality, southEast.Seasonality, precision: 6);
        Assert.True(northWest.WarmSeasonTemperatureCelsius > northWest.TemperatureCelsius);
        Assert.True(northWest.ColdSeasonTemperatureCelsius < northWest.TemperatureCelsius);
    }

    [Fact]
    public void RegionalCoverage_HasNoInventedLongitudeAndAppliesExactElevationLapseRate()
    {
        var definition = CreateDefinition(2, 1, 1_000);
        var samples = new[]
        {
            Land(elevation: 0),
            Land(elevation: 1_000),
        };
        var settings = RegionalSettings(
            centerLatitude: 0,
            climate: new CampaignSeasonClimateSettings(temperatureNoiseCelsius: 0));
        var support = Build(definition, samples, settings);
        var low = support.GetSample(0, 0);
        var high = support.GetSample(1, 0);

        Assert.Null(low.LongitudeDegrees);
        Assert.Equal(6.5, low.TemperatureCelsius - high.TemperatureCelsius, precision: 4);
    }

    [Fact]
    public void WaterDistances_AreExactPhysicalKilometersAndMissingSourcesStayInfinite()
    {
        var definition = CreateDefinition(3, 1, 100_000);
        var samples = new[]
        {
            Sea(),
            Land(),
            River(),
        };
        var support = Build(definition, samples, RegionalSettings(0));
        var middle = support.GetSample(1, 0);

        Assert.Equal(100, middle.SeaDistanceKilometers, precision: 8);
        Assert.Equal(100, middle.RiverDistanceKilometers, precision: 8);
        Assert.True(double.IsPositiveInfinity(middle.LakeDistanceKilometers));
    }

    [Fact]
    public void MaritimeInfluenceReducesInlandAndSeaMoistureInfluenceDecays()
    {
        var definition = CreateDefinition(10, 1, 100_000);
        var samples = UniformSamples(definition, Land());
        samples[0] = Sea();
        var settings = RegionalSettings(
            0,
            new CampaignSeasonClimateSettings(
                temperatureNoiseCelsius: 0,
                moistureNoiseStrength: 0,
                rainShadowStrength: 0));
        var support = Build(definition, samples, settings);
        var coast = support.GetSample(0, 0);
        var inland = support.GetSample(9, 0);

        Assert.True(coast.MaritimeInfluence > inland.MaritimeInfluence);
        Assert.True(coast.Moisture > inland.Moisture);
    }

    [Fact]
    public void WesterlyRainShadowRespondsToPhysicalUpwindRelief()
    {
        var definition = CreateDefinition(4, 1, 100_000);
        var samples = new[]
        {
            Land(),
            Land(elevation: 3_000),
            Land(),
            Land(),
        };
        var climate = new CampaignSeasonClimateSettings(
            temperatureNoiseCelsius: 0,
            moistureNoiseStrength: 0,
            windPerturbationDegrees: 0);
        var support = Build(definition, samples, RegionalSettings(45, climate));

        Assert.Equal(0, support.GetSample(0, 0).RainShadow, precision: 6);
        Assert.True(support.GetSample(2, 0).RainShadow > 0.5);
        Assert.True(support.GetSample(2, 0).Moisture < support.GetSample(0, 0).Moisture);
    }

    [Fact]
    public void Build_IsDeterministicAndHonorsPreCancellation()
    {
        var definition = CreateDefinition(8, 6, 20_000);
        var samples = UniformSamples(definition, Land());
        var settings = RegionalSettings(20);
        var first = Build(definition, samples, settings);
        var second = Build(definition, samples, settings);

        for (var y = 0; y < definition.TilesY; y++)
        {
            for (var x = 0; x < definition.TilesX; x++)
            {
                Assert.Equal(first.GetSample(x, y), second.GetSample(x, y));
            }
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var terrain = Capture(definition, samples).Terrain;
        Assert.Throws<OperationCanceledException>(() =>
            CampaignSeasonSupportFields.Build(terrain, settings, cancellation.Token));
    }

    [Fact]
    public void ZeroTiltRemovesAnnualTemperatureSeasonality()
    {
        var definition = CreateDefinition(2, 1, 100_000);
        var settings = new CampaignSeasonGenerationSettings(
            17,
            coverageMode: CampaignSeasonCoverageMode.Regional,
            regionalCenterLatitudeDegrees: 70,
            axialTiltDegrees: 0,
            climate: new CampaignSeasonClimateSettings(temperatureNoiseCelsius: 0));
        var sample = Build(definition, UniformSamples(definition, Land()), settings)
            .GetSample(0, 0);

        Assert.Equal(0, sample.Seasonality, precision: 12);
        Assert.Equal(0, sample.AnnualTemperatureRangeCelsius, precision: 12);
        Assert.Equal(sample.TemperatureCelsius, sample.WarmSeasonTemperatureCelsius, precision: 12);
        Assert.Equal(sample.TemperatureCelsius, sample.ColdSeasonTemperatureCelsius, precision: 12);
    }

    [Fact]
    public void RiverRaisesMoistureWithoutReceivingMaritimeThermalInertia()
    {
        var definition = CreateDefinition(9, 1, 100_000);
        var samples = UniformSamples(definition, Land());
        samples[0] = River();
        var climate = new CampaignSeasonClimateSettings(
            temperatureNoiseCelsius: 0,
            moistureNoiseStrength: 0,
            rainShadowStrength: 0);
        var support = Build(definition, samples, RegionalSettings(0, climate));
        var river = support.GetSample(0, 0);
        var distant = support.GetSample(8, 0);

        Assert.Equal(0, river.MaritimeInfluence, precision: 12);
        Assert.Equal(0, distant.MaritimeInfluence, precision: 12);
        Assert.Equal(river.TemperatureCelsius, distant.TemperatureCelsius, precision: 6);
        Assert.True(river.Moisture > distant.Moisture);
    }

    [Fact]
    public void PhysicalClimateIsConsistentAcrossFiveAndTwentyKilometerTiles()
    {
        var fineDefinition = CreateDefinition(80, 80, 5_000);
        var coarseDefinition = CreateDefinition(20, 20, 20_000);
        var climate = new CampaignSeasonClimateSettings(
            temperatureNoiseCelsius: 0,
            moistureNoiseStrength: 0,
            rainShadowStrength: 0);
        var settings = RegionalSettings(30, climate);
        var fine = Build(
            fineDefinition,
            UniformSamples(fineDefinition, Land(elevation: 400)),
            settings);
        var coarse = Build(
            coarseDefinition,
            UniformSamples(coarseDefinition, Land(elevation: 400)),
            settings);
        var fineSample = fine.GetSample(40, 41);
        var coarseSample = coarse.GetSample(10, 10);

        Assert.InRange(
            Math.Abs(fineSample.LatitudeDegrees - coarseSample.LatitudeDegrees),
            0,
            0.03);
        Assert.InRange(
            Math.Abs(fineSample.TemperatureCelsius - coarseSample.TemperatureCelsius),
            0,
            0.15);
        Assert.InRange(Math.Abs(fineSample.Moisture - coarseSample.Moisture), 0, 0.01);
    }

    [Fact]
    public void WholeGlobeTemperatureNoiseIsContinuousAcrossTheLongitudeSeam()
    {
        var definition = CreateDefinition(360, 2, 100_000);
        var climate = new CampaignSeasonClimateSettings(
            temperatureNoiseCelsius: 10,
            moistureNoiseStrength: 0,
            rainShadowStrength: 0);
        var settings = new CampaignSeasonGenerationSettings(
            17_029,
            axialTiltDegrees: 0,
            climate: climate);
        var support = Build(
            definition,
            UniformSamples(definition, Land()),
            settings);
        var seamDelta = Math.Abs(
            support.GetSample(0, 0).TemperatureCelsius -
            support.GetSample(359, 0).TemperatureCelsius);
        var ordinaryNeighborMaximum = Enumerable.Range(0, 359)
            .Max(x => Math.Abs(
                support.GetSample(x, 0).TemperatureCelsius -
                support.GetSample(x + 1, 0).TemperatureCelsius));

        Assert.InRange(seamDelta, 0, ordinaryNeighborMaximum * 1.25);
    }

    private static CampaignSeasonSupportFields Build(
        CampaignWorldDefinition definition,
        CampaignSeasonTerrainSample[] samples,
        CampaignSeasonGenerationSettings settings) =>
        CampaignSeasonSupportFields.Build(Capture(definition, samples).Terrain, settings);

    private static CampaignSeasonGenerationSource Capture(
        CampaignWorldDefinition definition,
        CampaignSeasonTerrainSample[] samples)
    {
        var catalog = new CampaignSeasonCatalog();
        return CampaignSeasonGenerationSource.Capture(
            new ArrayTerrainQuery(definition, samples),
            new CampaignSeasonMap(definition, catalog));
    }

    private static CampaignSeasonGenerationSettings RegionalSettings(
        double centerLatitude,
        CampaignSeasonClimateSettings? climate = null) =>
        new(
            17_029,
            coverageMode: CampaignSeasonCoverageMode.Regional,
            regionalCenterLatitudeDegrees: centerLatitude,
            climate: climate);

    private static CampaignSeasonTerrainSample[] UniformSamples(
        CampaignWorldDefinition definition,
        CampaignSeasonTerrainSample sample) =>
        Enumerable.Repeat(sample, checked((int)definition.TileCount)).ToArray();

    private static CampaignSeasonTerrainSample Land(short elevation = 0) =>
        new(CampaignTileType.Plains, null, elevation, CampaignSeasonWaterFeatures.None);

    private static CampaignSeasonTerrainSample Sea() =>
        new(CampaignTileType.Sea, null, -20, CampaignSeasonWaterFeatures.Sea);

    private static CampaignSeasonTerrainSample River() =>
        new(CampaignTileType.River, null, 0, CampaignSeasonWaterFeatures.River);

    private static CampaignWorldDefinition CreateDefinition(int width, int height, int tileMeters) =>
        CampaignWorldDefinition.Create(
            (long)width * tileMeters,
            (long)height * tileMeters,
            tileMeters,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

    private sealed class ArrayTerrainQuery : ICampaignSeasonTerrainQuery
    {
        private readonly CampaignSeasonTerrainSample[] _samples;

        public ArrayTerrainQuery(
            CampaignWorldDefinition definition,
            CampaignSeasonTerrainSample[] samples)
        {
            Definition = definition;
            _samples = samples;
        }

        public CampaignWorldDefinition Definition { get; }

        public long Revision => 0;

        public CampaignSeasonTerrainSample GetSample(int x, int y) =>
            _samples[(y * Definition.TilesX) + x];
    }
}
