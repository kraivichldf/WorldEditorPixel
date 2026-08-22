using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonGenerationSettings
{
    public const int CurrentSchemaVersion = 1;

    public const int MaximumEnabledDefinitionCount = 256;

    public const double EarthAxialTiltDegrees = 23.44;

    public const double EarthMeanRadiusKilometers = 6_371.0088;

    public static readonly double KilometersPerLatitudeDegree =
        Math.PI * EarthMeanRadiusKilometers / 180;

    private static readonly string[] DefaultEnabledIds =
    [
        CampaignSeasonCatalog.FallId,
        CampaignSeasonCatalog.SpringId,
        CampaignSeasonCatalog.SummerId,
        CampaignSeasonCatalog.WinterId,
    ];

    private readonly HashSet<string> _enabledIdSet;

    public CampaignSeasonGenerationSettings(
        int seasonSeed,
        bool seedDerivedFromTerrain = true,
        CampaignSeasonCoverageMode coverageMode = CampaignSeasonCoverageMode.WholeGlobe,
        double? regionalCenterLatitudeDegrees = null,
        double axialTiltDegrees = EarthAxialTiltDegrees,
        CampaignSeasonClimateSettings? climate = null,
        IEnumerable<string>? enabledSeasonIds = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        SchemaVersion = schemaVersion;
        SeasonSeed = seasonSeed;
        SeedDerivedFromTerrain = seedDerivedFromTerrain;
        CoverageMode = coverageMode;
        RegionalCenterLatitudeDegrees = regionalCenterLatitudeDegrees;
        AxialTiltDegrees = axialTiltDegrees;
        Climate = climate ?? CampaignSeasonClimateSettings.EarthLike;
        var enabledCopy = (enabledSeasonIds ?? DefaultEnabledIds)
            .Order(StringComparer.Ordinal)
            .ToArray();
        EnabledSeasonIds = Array.AsReadOnly(enabledCopy);
        _enabledIdSet = enabledCopy.ToHashSet(StringComparer.Ordinal);
        EnsureBasicSettingsValid();
    }

    public int SchemaVersion { get; }

    public int SeasonSeed { get; }

    public bool SeedDerivedFromTerrain { get; }

    public CampaignSeasonCoverageMode CoverageMode { get; }

    public double? RegionalCenterLatitudeDegrees { get; }

    public double AxialTiltDegrees { get; }

    public CampaignSeasonClimateSettings Climate { get; }

    public IReadOnlyList<string> EnabledSeasonIds { get; }

    public static IReadOnlyList<string> DefaultEnabledSeasonIds => Array.AsReadOnly(DefaultEnabledIds);

    public bool IsGenerationEnabled(string seasonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        return _enabledIdSet.Contains(seasonId);
    }

    public IReadOnlyList<CampaignSeasonDefinition> GetEnabledDefinitions(
        CampaignSeasonCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Definitions
            .Where(definition => _enabledIdSet.Contains(definition.Id))
            .ToArray();
    }

    public void EnsureValid(
        CampaignSeasonCatalog catalog,
        CampaignWorldDefinition? definition = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        EnsureBasicSettingsValid();
        foreach (var seasonId in EnabledSeasonIds)
        {
            if (!catalog.Contains(seasonId))
            {
                throw new ArgumentException(
                    $"Enabled season selection references unknown season '{seasonId}'.",
                    nameof(catalog));
            }
        }

        if (definition is not null)
        {
            CampaignWorldDefinition.EnsureValid(definition);
            EnsureCoverageFits(definition);
        }
    }

    public (double MinimumLatitude, double MaximumLatitude) GetRegionalLatitudeSpan(
        CampaignWorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CampaignWorldDefinition.EnsureValid(definition);
        if (CoverageMode != CampaignSeasonCoverageMode.Regional ||
            RegionalCenterLatitudeDegrees is not { } center)
        {
            throw new InvalidOperationException(
                "A regional latitude span is available only for Regional season coverage.");
        }

        var halfSpanDegrees = definition.WorldHeightMeters / 1_000d /
            (2 * KilometersPerLatitudeDegree);
        return (center - halfSpanDegrees, center + halfSpanDegrees);
    }

    internal void EnsureCoverageValid(CampaignWorldDefinition definition) =>
        EnsureCoverageFits(definition);

    private void EnsureBasicSettingsValid()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SchemaVersion),
                SchemaVersion,
                $"Season generation settings version must be {CurrentSchemaVersion}.");
        }

        if (!Enum.IsDefined(CoverageMode))
        {
            throw new ArgumentOutOfRangeException(nameof(CoverageMode), CoverageMode, "Unknown season coverage mode.");
        }

        if (!double.IsFinite(AxialTiltDegrees) || AxialTiltDegrees is < 0 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AxialTiltDegrees),
                AxialTiltDegrees,
                "Season axial tilt must be finite and from 0 through 90 degrees.");
        }

        if (CoverageMode == CampaignSeasonCoverageMode.WholeGlobe &&
            RegionalCenterLatitudeDegrees is not null)
        {
            throw new ArgumentException(
                "Whole-globe season coverage cannot define a regional centre latitude.",
                nameof(RegionalCenterLatitudeDegrees));
        }

        if (CoverageMode == CampaignSeasonCoverageMode.Regional &&
            (RegionalCenterLatitudeDegrees is not { } center ||
             !double.IsFinite(center) ||
             center is < -90 or > 90))
        {
            throw new ArgumentOutOfRangeException(
                nameof(RegionalCenterLatitudeDegrees),
                RegionalCenterLatitudeDegrees,
                "Regional season coverage requires a finite centre latitude from -90 through 90 degrees.");
        }

        if (EnabledSeasonIds.Count == 0)
        {
            throw new ArgumentException(
                "Season generation must include at least one definition.",
                nameof(EnabledSeasonIds));
        }

        if (EnabledSeasonIds.Count > MaximumEnabledDefinitionCount)
        {
            throw new ArgumentException(
                $"Season generation can include at most {MaximumEnabledDefinitionCount} definitions.",
                nameof(EnabledSeasonIds));
        }

        if (_enabledIdSet.Count != EnabledSeasonIds.Count)
        {
            throw new ArgumentException(
                "Enabled season selection cannot contain the same definition more than once.",
                nameof(EnabledSeasonIds));
        }

        for (var index = 0; index < EnabledSeasonIds.Count; index++)
        {
            if (!CampaignSeasonDefinition.IsValidIdentifier(EnabledSeasonIds[index]))
            {
                throw new ArgumentException(
                    $"Enabled season selection contains an invalid season ID at index {index}.",
                    nameof(EnabledSeasonIds));
            }
        }

        Climate.EnsureValid();
    }

    private void EnsureCoverageFits(CampaignWorldDefinition definition)
    {
        if (CoverageMode != CampaignSeasonCoverageMode.Regional)
        {
            return;
        }

        var (minimum, maximum) = GetRegionalLatitudeSpan(definition);
        const double tolerance = 1e-10;
        if (minimum < -90 - tolerance || maximum > 90 + tolerance)
        {
            throw new ArgumentException(
                $"Regional season coverage spans {minimum:F3} through {maximum:F3} degrees and crosses a pole. " +
                "Move the centre latitude or use Whole-globe coverage.",
                nameof(definition));
        }
    }
}
