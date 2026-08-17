namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonClimateSettings
{
    public static CampaignSeasonClimateSettings EarthLike { get; } = new();

    public CampaignSeasonClimateSettings(
        double lapseRateCelsiusPerKilometer = 6.5,
        double seaMaritimeStrength = 0.70,
        double seaMaritimeRadiusKilometers = 650,
        double lakeMaritimeStrength = 0.25,
        double lakeMaritimeRadiusKilometers = 180,
        double maximumPhaseLagOrbitFraction = 0.08,
        double maritimeAmplitudeReduction = 0.55,
        double temperatureNoiseCelsius = 2.5,
        double seaMoistureStrength = 0.30,
        double seaMoistureRadiusKilometers = 700,
        double lakeMoistureStrength = 0.16,
        double lakeMoistureRadiusKilometers = 220,
        double riverMoistureStrength = 0.08,
        double riverMoistureRadiusKilometers = 80,
        double rainShadowStrength = 0.24,
        double moistureNoiseStrength = 0.10,
        double temperatureNoiseWavelengthKilometers = 1_600,
        double moistureNoiseWavelengthKilometers = 1_300,
        double rainShadowFetchKilometers = 700,
        double rainShadowReliefMeters = 1_800,
        double windPerturbationDegrees = 20)
    {
        LapseRateCelsiusPerKilometer = lapseRateCelsiusPerKilometer;
        SeaMaritimeStrength = seaMaritimeStrength;
        SeaMaritimeRadiusKilometers = seaMaritimeRadiusKilometers;
        LakeMaritimeStrength = lakeMaritimeStrength;
        LakeMaritimeRadiusKilometers = lakeMaritimeRadiusKilometers;
        MaximumPhaseLagOrbitFraction = maximumPhaseLagOrbitFraction;
        MaritimeAmplitudeReduction = maritimeAmplitudeReduction;
        TemperatureNoiseCelsius = temperatureNoiseCelsius;
        SeaMoistureStrength = seaMoistureStrength;
        SeaMoistureRadiusKilometers = seaMoistureRadiusKilometers;
        LakeMoistureStrength = lakeMoistureStrength;
        LakeMoistureRadiusKilometers = lakeMoistureRadiusKilometers;
        RiverMoistureStrength = riverMoistureStrength;
        RiverMoistureRadiusKilometers = riverMoistureRadiusKilometers;
        RainShadowStrength = rainShadowStrength;
        MoistureNoiseStrength = moistureNoiseStrength;
        TemperatureNoiseWavelengthKilometers = temperatureNoiseWavelengthKilometers;
        MoistureNoiseWavelengthKilometers = moistureNoiseWavelengthKilometers;
        RainShadowFetchKilometers = rainShadowFetchKilometers;
        RainShadowReliefMeters = rainShadowReliefMeters;
        WindPerturbationDegrees = windPerturbationDegrees;
        EnsureValid();
    }

    public double LapseRateCelsiusPerKilometer { get; }

    public double SeaMaritimeStrength { get; }

    public double SeaMaritimeRadiusKilometers { get; }

    public double LakeMaritimeStrength { get; }

    public double LakeMaritimeRadiusKilometers { get; }

    public double MaximumPhaseLagOrbitFraction { get; }

    public double MaritimeAmplitudeReduction { get; }

    public double TemperatureNoiseCelsius { get; }

    public double SeaMoistureStrength { get; }

    public double SeaMoistureRadiusKilometers { get; }

    public double LakeMoistureStrength { get; }

    public double LakeMoistureRadiusKilometers { get; }

    public double RiverMoistureStrength { get; }

    public double RiverMoistureRadiusKilometers { get; }

    public double RainShadowStrength { get; }

    public double MoistureNoiseStrength { get; }

    public double TemperatureNoiseWavelengthKilometers { get; }

    public double MoistureNoiseWavelengthKilometers { get; }

    public double RainShadowFetchKilometers { get; }

    public double RainShadowReliefMeters { get; }

    public double WindPerturbationDegrees { get; }

    public void EnsureValid()
    {
        EnsureInRange(LapseRateCelsiusPerKilometer, 0, 20, nameof(LapseRateCelsiusPerKilometer));
        EnsureInRange(SeaMaritimeStrength, 0, 2, nameof(SeaMaritimeStrength));
        EnsurePositive(SeaMaritimeRadiusKilometers, nameof(SeaMaritimeRadiusKilometers));
        EnsureInRange(LakeMaritimeStrength, 0, 2, nameof(LakeMaritimeStrength));
        EnsurePositive(LakeMaritimeRadiusKilometers, nameof(LakeMaritimeRadiusKilometers));
        EnsureInRange(MaximumPhaseLagOrbitFraction, 0, 0.25, nameof(MaximumPhaseLagOrbitFraction));
        EnsureInRange(MaritimeAmplitudeReduction, 0, 1, nameof(MaritimeAmplitudeReduction));
        EnsureInRange(TemperatureNoiseCelsius, 0, 30, nameof(TemperatureNoiseCelsius));
        EnsureInRange(SeaMoistureStrength, 0, 1, nameof(SeaMoistureStrength));
        EnsurePositive(SeaMoistureRadiusKilometers, nameof(SeaMoistureRadiusKilometers));
        EnsureInRange(LakeMoistureStrength, 0, 1, nameof(LakeMoistureStrength));
        EnsurePositive(LakeMoistureRadiusKilometers, nameof(LakeMoistureRadiusKilometers));
        EnsureInRange(RiverMoistureStrength, 0, 1, nameof(RiverMoistureStrength));
        EnsurePositive(RiverMoistureRadiusKilometers, nameof(RiverMoistureRadiusKilometers));
        EnsureInRange(RainShadowStrength, 0, 1, nameof(RainShadowStrength));
        EnsureInRange(MoistureNoiseStrength, 0, 1, nameof(MoistureNoiseStrength));
        EnsurePositive(TemperatureNoiseWavelengthKilometers, nameof(TemperatureNoiseWavelengthKilometers));
        EnsurePositive(MoistureNoiseWavelengthKilometers, nameof(MoistureNoiseWavelengthKilometers));
        EnsurePositive(RainShadowFetchKilometers, nameof(RainShadowFetchKilometers));
        EnsurePositive(RainShadowReliefMeters, nameof(RainShadowReliefMeters));
        EnsureInRange(WindPerturbationDegrees, 0, 45, nameof(WindPerturbationDegrees));
    }

    private static void EnsurePositive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{parameterName} must be finite and greater than zero.");
        }
    }

    private static void EnsureInRange(
        double value,
        double minimum,
        double maximum,
        string parameterName)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{parameterName} must be finite and from {minimum} through {maximum}.");
        }
    }
}
