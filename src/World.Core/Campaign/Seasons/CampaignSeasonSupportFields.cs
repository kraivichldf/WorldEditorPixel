using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

public readonly record struct CampaignSeasonSupportSample(
    double? LongitudeDegrees,
    double LatitudeDegrees,
    double Seasonality,
    double TemperatureCelsius,
    double WarmSeasonTemperatureCelsius,
    double ColdSeasonTemperatureCelsius,
    double AnnualTemperatureRangeCelsius,
    double Moisture,
    double MaritimeInfluence,
    double RainShadow,
    double SeaDistanceKilometers,
    double LakeDistanceKilometers,
    double RiverDistanceKilometers);

/// <summary>
/// Immutable Earth-like support fields used to explain and reproduce a Season Occurrence candidate.
/// These diagnostics are derived and are not project authority.
/// </summary>
public sealed class CampaignSeasonSupportFields
{
    private readonly float[]? _longitudeDegrees;
    private readonly float[] _latitudeDegrees;
    private readonly float[] _seasonality;
    private readonly float[] _temperatureCelsius;
    private readonly float[] _warmSeasonTemperatureCelsius;
    private readonly float[] _coldSeasonTemperatureCelsius;
    private readonly float[] _annualTemperatureRangeCelsius;
    private readonly float[] _moisture;
    private readonly float[] _maritimeInfluence;
    private readonly float[] _rainShadow;
    private readonly double[] _seaDistanceKilometers;
    private readonly double[] _lakeDistanceKilometers;
    private readonly double[] _riverDistanceKilometers;

    private CampaignSeasonSupportFields(
        CampaignSeasonTerrainSnapshot terrain,
        CampaignSeasonGenerationSettings settings,
        float[]? longitudeDegrees,
        float[] latitudeDegrees,
        float[] seasonality,
        float[] temperatureCelsius,
        float[] warmSeasonTemperatureCelsius,
        float[] coldSeasonTemperatureCelsius,
        float[] annualTemperatureRangeCelsius,
        float[] moisture,
        float[] maritimeInfluence,
        float[] rainShadow,
        double[] seaDistanceKilometers,
        double[] lakeDistanceKilometers,
        double[] riverDistanceKilometers)
    {
        Terrain = terrain;
        Settings = settings;
        _longitudeDegrees = longitudeDegrees;
        _latitudeDegrees = latitudeDegrees;
        _seasonality = seasonality;
        _temperatureCelsius = temperatureCelsius;
        _warmSeasonTemperatureCelsius = warmSeasonTemperatureCelsius;
        _coldSeasonTemperatureCelsius = coldSeasonTemperatureCelsius;
        _annualTemperatureRangeCelsius = annualTemperatureRangeCelsius;
        _moisture = moisture;
        _maritimeInfluence = maritimeInfluence;
        _rainShadow = rainShadow;
        _seaDistanceKilometers = seaDistanceKilometers;
        _lakeDistanceKilometers = lakeDistanceKilometers;
        _riverDistanceKilometers = riverDistanceKilometers;
    }

    public CampaignSeasonTerrainSnapshot Terrain { get; }

    public CampaignSeasonGenerationSettings Settings { get; }

    public CampaignSeasonSupportSample GetSample(int x, int y)
    {
        var index = GetIndex(x, y);
        return new CampaignSeasonSupportSample(
            _longitudeDegrees is null ? null : _longitudeDegrees[index],
            _latitudeDegrees[index],
            _seasonality[index],
            _temperatureCelsius[index],
            _warmSeasonTemperatureCelsius[index],
            _coldSeasonTemperatureCelsius[index],
            _annualTemperatureRangeCelsius[index],
            _moisture[index],
            _maritimeInfluence[index],
            _rainShadow[index],
            _seaDistanceKilometers[index],
            _lakeDistanceKilometers[index],
            _riverDistanceKilometers[index]);
    }

    public static CampaignSeasonSupportFields Build(
        CampaignSeasonTerrainSnapshot terrain,
        CampaignSeasonGenerationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);
        CampaignWorldDefinition.EnsureValid(terrain.Definition);
        settings.EnsureCoverageValid(terrain.Definition);
        settings.Climate.EnsureValid();
        cancellationToken.ThrowIfCancellationRequested();

        var definition = terrain.Definition;
        var width = definition.TilesX;
        var height = definition.TilesY;
        var count = checked((int)definition.TileCount);
        var tileKilometers = definition.CampaignTileSizeMeters / 1_000d;
        var worldWidthKilometers = definition.WorldWidthMeters / 1_000d;
        var worldHeightKilometers = definition.WorldHeightMeters / 1_000d;
        var samples = terrain.AsSpan();
        var distanceField = new CampaignResourceDistanceField(
            width,
            height,
            definition.CampaignTileSizeMeters,
            (x, y) => ToResourceWaterSources(terrain.Samples[(y * width) + x].WaterFeatures),
            cancellationToken);

        var longitude = settings.CoverageMode == CampaignSeasonCoverageMode.WholeGlobe
            ? new float[count]
            : null;
        var latitude = new float[count];
        var seasonality = new float[count];
        var temperature = new float[count];
        var warmSeasonTemperature = new float[count];
        var coldSeasonTemperature = new float[count];
        var annualTemperatureRange = new float[count];
        var moisture = new float[count];
        var maritime = new float[count];
        var rainShadow = new float[count];
        var seaDistance = new double[count];
        var lakeDistance = new double[count];
        var riverDistance = new double[count];
        var climate = settings.Climate;
        var earthTiltSine = Math.Sin(DegreesToRadians(CampaignSeasonGenerationSettings.EarthAxialTiltDegrees));
        var tiltScale = earthTiltSine <= double.Epsilon
            ? 0
            : Math.Sin(DegreesToRadians(settings.AxialTiltDegrees)) / earthTiltSine;

        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var yKilometers = (y + 0.5) * tileKilometers;
            var latitudeDegrees = GetLatitudeDegrees(
                settings,
                definition,
                y,
                yKilometers,
                worldHeightKilometers);
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                var xKilometers = (x + 0.5) * tileKilometers;
                if (longitude is not null)
                {
                    longitude[index] = (float)(-180 + (360 * (x + 0.5) / width));
                }

                latitude[index] = (float)latitudeDegrees;
                var distances = distanceField.GetDistances(x, y);
                seaDistance[index] = distances.Sea;
                lakeDistance[index] = distances.Lake;
                riverDistance[index] = distances.River;
                var maritimeValue = Clamp01(
                    (climate.SeaMaritimeStrength * DistanceInfluence(
                        distances.Sea,
                        climate.SeaMaritimeRadiusKilometers)) +
                    (climate.LakeMaritimeStrength * DistanceInfluence(
                        distances.Lake,
                        climate.LakeMaritimeRadiusKilometers)));
                maritime[index] = (float)maritimeValue;
                var amplitudeScale = 1 - (climate.MaritimeAmplitudeReduction * maritimeValue);

                var temperatureNoise = GetRegionalNoise(
                    xKilometers,
                    yKilometers,
                    worldWidthKilometers,
                    settings.CoverageMode,
                    OffsetSeed(settings.SeasonSeed, 7_003),
                    climate.TemperatureNoiseWavelengthKilometers,
                    0.52,
                    0.31,
                    0.17);
                var absoluteLatitude = Math.Abs(latitudeDegrees);
                var latitudeMeanCelsius = 30 - (0.42 * absoluteLatitude);
                var continentalAmplitudeCelsius =
                    (2 + (20 * Math.Pow(absoluteLatitude / 90, 1.35))) * tiltScale;
                var heightAboveSeaKilometers = Math.Max(
                    0,
                    samples[index].ElevationMeters - definition.SeaLevelMeters) / 1_000d;
                var meanTemperature =
                    latitudeMeanCelsius +
                    (-climate.LapseRateCelsiusPerKilometer * heightAboveSeaKilometers) +
                    (climate.TemperatureNoiseCelsius * temperatureNoise);
                var amplitudeCelsius = Math.Max(0, continentalAmplitudeCelsius * amplitudeScale);
                var annualRangeCelsius = 2 * amplitudeCelsius;
                temperature[index] = (float)meanTemperature;
                warmSeasonTemperature[index] = (float)(meanTemperature + amplitudeCelsius);
                coldSeasonTemperature[index] = (float)(meanTemperature - amplitudeCelsius);
                annualTemperatureRange[index] = (float)annualRangeCelsius;
                seasonality[index] = (float)Clamp01(annualRangeCelsius / 40);
            }
        }

        BuildRainShadow(
            terrain,
            settings,
            latitude,
            rainShadow,
            tileKilometers,
            worldWidthKilometers,
            cancellationToken);

        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var yKilometers = (y + 0.5) * tileKilometers;
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                var xKilometers = (x + 0.5) * tileKilometers;
                var absoluteLatitude = Math.Abs(latitude[index]);
                var latitudeMoisture =
                    0.42 +
                    (0.30 * Math.Exp(-Math.Pow(absoluteLatitude / 16, 2))) -
                    (0.24 * Math.Exp(-Math.Pow((absoluteLatitude - 28) / 10, 2))) +
                    (0.10 * Math.Exp(-Math.Pow((absoluteLatitude - 55) / 16, 2)));
                var moistureNoise = GetRegionalNoise(
                    xKilometers,
                    yKilometers,
                    worldWidthKilometers,
                    settings.CoverageMode,
                    OffsetSeed(settings.SeasonSeed, 17_011),
                    climate.MoistureNoiseWavelengthKilometers,
                    0.58,
                    0.29,
                    0.13);
                moisture[index] = (float)Clamp01(
                    latitudeMoisture +
                    (climate.SeaMoistureStrength * DistanceInfluence(
                        seaDistance[index],
                        climate.SeaMoistureRadiusKilometers)) +
                    (climate.LakeMoistureStrength * DistanceInfluence(
                        lakeDistance[index],
                        climate.LakeMoistureRadiusKilometers)) +
                    (climate.RiverMoistureStrength * DistanceInfluence(
                        riverDistance[index],
                        climate.RiverMoistureRadiusKilometers)) -
                    (climate.RainShadowStrength * rainShadow[index]) +
                    (climate.MoistureNoiseStrength * moistureNoise));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new CampaignSeasonSupportFields(
            terrain,
            settings,
            longitude,
            latitude,
            seasonality,
            temperature,
            warmSeasonTemperature,
            coldSeasonTemperature,
            annualTemperatureRange,
            moisture,
            maritime,
            rainShadow,
            seaDistance,
            lakeDistance,
            riverDistance);
    }

    private static void BuildRainShadow(
        CampaignSeasonTerrainSnapshot terrain,
        CampaignSeasonGenerationSettings settings,
        IReadOnlyList<float> latitude,
        float[] destination,
        double tileKilometers,
        double worldWidthKilometers,
        CancellationToken cancellationToken)
    {
        var definition = terrain.Definition;
        var width = definition.TilesX;
        var height = definition.TilesY;
        var samples = terrain.AsSpan();
        var climate = settings.Climate;
        const int sampleCount = 8;
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var yKilometers = (y + 0.5) * tileKilometers;
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                if (samples[index].TerrainType is CampaignTileType.Sea or CampaignTileType.Lake)
                {
                    destination[index] = 0;
                    continue;
                }

                var xKilometers = (x + 0.5) * tileKilometers;
                var absoluteLatitude = Math.Abs(latitude[index]);
                var baseDirectionRadians = absoluteLatitude is < 30 or >= 60
                    ? Math.PI
                    : 0;
                var perturbation = GetRegionalNoise(
                    xKilometers,
                    yKilometers,
                    worldWidthKilometers,
                    settings.CoverageMode,
                    OffsetSeed(settings.SeasonSeed, 29_011),
                    2_500,
                    1,
                    0,
                    0) * DegreesToRadians(climate.WindPerturbationDegrees);
                var travelDirectionX = Math.Cos(baseDirectionRadians + perturbation);
                var travelDirectionY = Math.Sin(baseDirectionRadians + perturbation);
                var centerHeight = Math.Max(
                    definition.SeaLevelMeters,
                    samples[index].ElevationMeters);
                var maximumBarrier = 0d;
                for (var step = 1; step <= sampleCount; step++)
                {
                    var distance = climate.RainShadowFetchKilometers * step / sampleCount;
                    var upwindXKilometers = Math.Clamp(
                        xKilometers - (travelDirectionX * distance),
                        0,
                        Math.Max(0, definition.WorldWidthMeters / 1_000d - double.Epsilon));
                    var upwindYKilometers = Math.Clamp(
                        yKilometers - (travelDirectionY * distance),
                        0,
                        Math.Max(0, definition.WorldHeightMeters / 1_000d - double.Epsilon));
                    var sampleX = Math.Clamp((int)(upwindXKilometers / tileKilometers), 0, width - 1);
                    var sampleY = Math.Clamp((int)(upwindYKilometers / tileKilometers), 0, height - 1);
                    var upwindHeight = Math.Max(
                        definition.SeaLevelMeters,
                        samples[(sampleY * width) + sampleX].ElevationMeters);
                    var distanceWeight = Math.Exp(-distance / climate.RainShadowFetchKilometers);
                    maximumBarrier = Math.Max(
                        maximumBarrier,
                        Math.Max(0, upwindHeight - centerHeight) * distanceWeight);
                }

                destination[index] = (float)Clamp01(maximumBarrier / climate.RainShadowReliefMeters);
            }
        }
    }

    private static double GetLatitudeDegrees(
        CampaignSeasonGenerationSettings settings,
        CampaignWorldDefinition definition,
        int y,
        double yKilometers,
        double worldHeightKilometers) =>
        settings.CoverageMode == CampaignSeasonCoverageMode.WholeGlobe
            ? 90 - (180 * (y + 0.5) / definition.TilesY)
            : settings.RegionalCenterLatitudeDegrees!.Value +
              ((worldHeightKilometers / 2) - yKilometers) /
              CampaignSeasonGenerationSettings.KilometersPerLatitudeDegree;

    private static double GetRegionalNoise(
        double xKilometers,
        double yKilometers,
        double worldWidthKilometers,
        CampaignSeasonCoverageMode coverageMode,
        int seed,
        double primaryWavelengthKilometers,
        double primaryWeight,
        double middleWeight,
        double detailWeight)
    {
        var totalWeight = primaryWeight + middleWeight + detailWeight;
        var value = primaryWeight == 0
            ? 0
            : primaryWeight * SampleNoise(
                xKilometers,
                yKilometers,
                worldWidthKilometers,
                coverageMode,
                seed,
                primaryWavelengthKilometers,
                2);
        if (middleWeight != 0)
        {
            value += middleWeight * SampleNoise(
                xKilometers,
                yKilometers,
                worldWidthKilometers,
                coverageMode,
                OffsetSeed(seed, 1_009),
                primaryWavelengthKilometers * 0.3125,
                2);
        }

        if (detailWeight != 0)
        {
            value += detailWeight * SampleNoise(
                xKilometers,
                yKilometers,
                worldWidthKilometers,
                coverageMode,
                OffsetSeed(seed, 2_023),
                primaryWavelengthKilometers * 0.10,
                1);
        }

        return Math.Clamp(value / Math.Max(totalWeight, double.Epsilon), -1, 1);
    }

    private static double SampleNoise(
        double xKilometers,
        double yKilometers,
        double worldWidthKilometers,
        CampaignSeasonCoverageMode coverageMode,
        int seed,
        double wavelengthKilometers,
        int octaves)
    {
        if (coverageMode != CampaignSeasonCoverageMode.WholeGlobe)
        {
            return CampaignTerrainNoise.Fractal(
                xKilometers,
                yKilometers,
                seed,
                wavelengthKilometers,
                octaves);
        }

        var normalizedX = Math.Clamp(xKilometers / worldWidthKilometers, 0, 1);
        var blend = normalizedX * normalizedX * (3 - (2 * normalizedX));
        var current = CampaignTerrainNoise.Fractal(
            xKilometers,
            yKilometers,
            seed,
            wavelengthKilometers,
            octaves);
        var wrapped = CampaignTerrainNoise.Fractal(
            xKilometers - worldWidthKilometers,
            yKilometers,
            seed,
            wavelengthKilometers,
            octaves);
        return Math.Clamp(((1 - blend) * current) + (blend * wrapped), -1, 1);
    }

    private int GetIndex(int x, int y)
    {
        if ((uint)x >= (uint)Terrain.Definition.TilesX ||
            (uint)y >= (uint)Terrain.Definition.TilesY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Season support coordinate ({x}, {y}) is outside the campaign grid.");
        }

        return (y * Terrain.Definition.TilesX) + x;
    }

    private static CampaignResourceWaterSources ToResourceWaterSources(
        CampaignSeasonWaterFeatures features)
    {
        var sources = CampaignResourceWaterSources.None;
        if (features.HasFlag(CampaignSeasonWaterFeatures.Sea))
        {
            sources |= CampaignResourceWaterSources.Sea;
        }

        if (features.HasFlag(CampaignSeasonWaterFeatures.Lake))
        {
            sources |= CampaignResourceWaterSources.Lake;
        }

        if (features.HasFlag(CampaignSeasonWaterFeatures.River))
        {
            sources |= CampaignResourceWaterSources.River;
        }

        return sources;
    }

    private static double DistanceInfluence(double distanceKilometers, double radiusKilometers) =>
        double.IsPositiveInfinity(distanceKilometers)
            ? 0
            : Math.Exp(-distanceKilometers / radiusKilometers);

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    private static int OffsetSeed(int seed, int offset) => unchecked(seed + offset);
}
