using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

/// <summary>
/// Produces canonical content fingerprints for accepted Season generation diagnostics.
/// Revisions guard one live preview; fingerprints explain staleness after save and reopen.
/// </summary>
public static class CampaignSeasonGenerationFingerprint
{
    public static string GetSourceTerrainFingerprint(CampaignSeasonTerrainSnapshot terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        CampaignWorldDefinition.EnsureValid(terrain.Definition);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendDefinition(hash, terrain.Definition);
        foreach (var sample in terrain.Samples)
        {
            AppendInt32(hash, (int)sample.TerrainType);
            AppendNullableString(hash, sample.CustomTerrainId);
            AppendInt32(hash, sample.ElevationMeters);
            AppendInt32(hash, (int)sample.WaterFeatures);
        }

        return Finish(hash);
    }

    public static string GetInputFingerprint(
        CampaignSeasonCatalog catalog,
        CampaignSeasonGenerationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid(catalog);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendInt32(hash, settings.SchemaVersion);
        AppendInt32(hash, settings.SeasonSeed);
        AppendBoolean(hash, settings.SeedDerivedFromTerrain);
        AppendInt32(hash, (int)settings.CoverageMode);
        AppendNullableDouble(hash, settings.RegionalCenterLatitudeDegrees);
        AppendDouble(hash, settings.AxialTiltDegrees);
        AppendClimate(hash, settings.Climate);
        AppendInt32(hash, settings.EnabledSeasonIds.Count);
        foreach (var seasonId in settings.EnabledSeasonIds)
        {
            AppendString(hash, seasonId);
            AppendRule(hash, catalog.Get(seasonId).Rule);
        }

        return Finish(hash);
    }

    private static void AppendDefinition(
        IncrementalHash hash,
        CampaignWorldDefinition definition)
    {
        AppendInt32(hash, definition.Version);
        AppendInt64(hash, definition.WorldWidthMeters);
        AppendInt64(hash, definition.WorldHeightMeters);
        AppendInt32(hash, definition.CampaignTileSizeMeters);
        AppendInt32(hash, definition.SeaLevelMeters);
        AppendInt32(hash, definition.MinimumHeightMeters);
        AppendInt32(hash, definition.MaximumHeightMeters);
        AppendInt32(hash, definition.DefaultTileHeightMeters);
    }

    private static void AppendClimate(
        IncrementalHash hash,
        CampaignSeasonClimateSettings climate)
    {
        AppendDouble(hash, climate.LapseRateCelsiusPerKilometer);
        AppendDouble(hash, climate.SeaMaritimeStrength);
        AppendDouble(hash, climate.SeaMaritimeRadiusKilometers);
        AppendDouble(hash, climate.LakeMaritimeStrength);
        AppendDouble(hash, climate.LakeMaritimeRadiusKilometers);
        AppendDouble(hash, climate.MaritimeAmplitudeReduction);
        AppendDouble(hash, climate.TemperatureNoiseCelsius);
        AppendDouble(hash, climate.SeaMoistureStrength);
        AppendDouble(hash, climate.SeaMoistureRadiusKilometers);
        AppendDouble(hash, climate.LakeMoistureStrength);
        AppendDouble(hash, climate.LakeMoistureRadiusKilometers);
        AppendDouble(hash, climate.RiverMoistureStrength);
        AppendDouble(hash, climate.RiverMoistureRadiusKilometers);
        AppendDouble(hash, climate.RainShadowStrength);
        AppendDouble(hash, climate.MoistureNoiseStrength);
        AppendDouble(hash, climate.TemperatureNoiseWavelengthKilometers);
        AppendDouble(hash, climate.MoistureNoiseWavelengthKilometers);
        AppendDouble(hash, climate.RainShadowFetchKilometers);
        AppendDouble(hash, climate.RainShadowReliefMeters);
        AppendDouble(hash, climate.WindPerturbationDegrees);
    }

    private static void AppendRule(IncrementalHash hash, CampaignSeasonRule rule)
    {
        AppendRange(hash, rule.LatitudeDegrees);
        AppendRange(hash, rule.ElevationMeters);
        AppendRange(hash, rule.TemperatureCelsius);
        AppendRange(hash, rule.WarmSeasonTemperatureCelsius);
        AppendRange(hash, rule.ColdSeasonTemperatureCelsius);
        AppendRange(hash, rule.AnnualTemperatureRangeCelsius);
        AppendRange(hash, rule.Moisture);
        AppendRange(hash, rule.Seasonality);
        AppendRange(hash, rule.SeaDistanceKilometers);
        AppendRange(hash, rule.LakeDistanceKilometers);
        AppendRange(hash, rule.RiverDistanceKilometers);
        AppendInt32(hash, rule.TerrainIncludes.Count);
        foreach (var value in rule.TerrainIncludes)
        {
            AppendInt32(hash, (int)value);
        }

        AppendInt32(hash, rule.TerrainExcludes.Count);
        foreach (var value in rule.TerrainExcludes)
        {
            AppendInt32(hash, (int)value);
        }

        AppendStrings(hash, rule.CustomTerrainIncludes);
        AppendStrings(hash, rule.CustomTerrainExcludes);
    }

    private static void AppendRange(IncrementalHash hash, CampaignSeasonRange? range)
    {
        AppendBoolean(hash, range.HasValue);
        if (range is { } value)
        {
            AppendDouble(hash, value.Minimum);
            AppendDouble(hash, value.Maximum);
        }
    }

    private static void AppendStrings(IncrementalHash hash, IReadOnlyList<string> values)
    {
        AppendInt32(hash, values.Count);
        foreach (var value in values)
        {
            AppendString(hash, value);
        }
    }

    private static void AppendNullableString(IncrementalHash hash, string? value)
    {
        AppendBoolean(hash, value is not null);
        if (value is not null)
        {
            AppendString(hash, value);
        }
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendNullableDouble(IncrementalHash hash, double? value)
    {
        AppendBoolean(hash, value.HasValue);
        if (value.HasValue)
        {
            AppendDouble(hash, value.Value);
        }
    }

    private static void AppendDouble(IncrementalHash hash, double value) =>
        AppendInt64(hash, BitConverter.DoubleToInt64Bits(value));

    private static void AppendBoolean(IncrementalHash hash, bool value) =>
        hash.AppendData([value ? (byte)1 : (byte)0]);

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static string Finish(IncrementalHash hash) =>
        Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
}
