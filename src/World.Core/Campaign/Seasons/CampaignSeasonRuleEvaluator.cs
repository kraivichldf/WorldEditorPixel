namespace Kingdom.World.Core.Campaign.Seasons;

public static class CampaignSeasonRuleEvaluator
{
    public static bool Matches(
        CampaignSeasonRule rule,
        CampaignSeasonTerrainSample terrain,
        CampaignSeasonSupportSample support)
    {
        ArgumentNullException.ThrowIfNull(rule);
        rule.EnsureValid();
        terrain.EnsureValid();
        return MatchesValidated(rule, terrain, support);
    }

    internal static bool MatchesValidated(
        CampaignSeasonRule rule,
        CampaignSeasonTerrainSample terrain,
        CampaignSeasonSupportSample support)
    {
        return
            rule.AllowsTerrainValidated(terrain.TerrainType, terrain.CustomTerrainId) &&
            Contains(rule.LatitudeDegrees, support.LatitudeDegrees) &&
            Contains(rule.ElevationMeters, terrain.ElevationMeters) &&
            Contains(rule.TemperatureCelsius, support.TemperatureCelsius) &&
            Contains(rule.WarmSeasonTemperatureCelsius, support.WarmSeasonTemperatureCelsius) &&
            Contains(rule.ColdSeasonTemperatureCelsius, support.ColdSeasonTemperatureCelsius) &&
            Contains(rule.AnnualTemperatureRangeCelsius, support.AnnualTemperatureRangeCelsius) &&
            Contains(rule.Moisture, support.Moisture) &&
            Contains(rule.Seasonality, support.Seasonality) &&
            Contains(rule.SeaDistanceKilometers, support.SeaDistanceKilometers) &&
            Contains(rule.LakeDistanceKilometers, support.LakeDistanceKilometers) &&
            Contains(rule.RiverDistanceKilometers, support.RiverDistanceKilometers);
    }

    private static bool Contains(CampaignSeasonRange? range, double value) =>
        range is null || range.Value.Contains(value);
}
