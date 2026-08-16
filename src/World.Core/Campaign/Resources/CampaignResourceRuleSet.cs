using System.Collections.ObjectModel;

namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceRuleSet
{
    public const int MaximumNamedRuleCount = 64;

    public const double MaximumAbsoluteWeight = 10;

    public static CampaignResourceRuleSet Land { get; } = new(CampaignResourceMedium.Land);

    public static CampaignResourceRuleSet Water { get; } = new(CampaignResourceMedium.Water);

    public CampaignResourceRuleSet(
        CampaignResourceMedium medium,
        CampaignResourceRange? elevationMeters = null,
        CampaignResourceRange? grade = null,
        CampaignResourceRange? waterDistanceKilometers = null,
        CampaignResourceRange? regionScaleKilometers = null,
        IEnumerable<string>? preferredTerrainTags = null,
        IEnumerable<string>? customTerrainIncludes = null,
        IEnumerable<string>? customTerrainExcludes = null,
        IReadOnlyDictionary<string, double>? fieldWeights = null,
        IReadOnlyDictionary<string, double>? associationWeights = null,
        IEnumerable<string>? avoidedTerrainTags = null,
        IEnumerable<CampaignResourceSurfaceType>? excludedTerrainSurfaces = null)
    {
        Medium = medium;
        ElevationMeters = elevationMeters;
        Grade = grade;
        WaterDistanceKilometers = waterDistanceKilometers;
        RegionScaleKilometers = regionScaleKilometers;
        PreferredTerrainTags = CopyIdentifiers(preferredTerrainTags, nameof(preferredTerrainTags));
        AvoidedTerrainTags = CopyIdentifiers(avoidedTerrainTags, nameof(avoidedTerrainTags));
        ExcludedTerrainSurfaces = CopySurfaces(excludedTerrainSurfaces);
        CustomTerrainIncludes = CopyIdentifiers(customTerrainIncludes, nameof(customTerrainIncludes));
        CustomTerrainExcludes = CopyIdentifiers(customTerrainExcludes, nameof(customTerrainExcludes));
        FieldWeights = CopyWeights(fieldWeights, nameof(fieldWeights));
        AssociationWeights = CopyWeights(associationWeights, nameof(associationWeights));
        EnsureValid();
    }

    public CampaignResourceMedium Medium { get; }

    public CampaignResourceRange? ElevationMeters { get; }

    public CampaignResourceRange? Grade { get; }

    public CampaignResourceRange? WaterDistanceKilometers { get; }

    public CampaignResourceRange? RegionScaleKilometers { get; }

    public IReadOnlyList<string> PreferredTerrainTags { get; }

    /// <summary>
    /// Supported terrain factors that softly reduce suitability where their response is strong.
    /// Avoidance never makes a tile ineligible; use hard medium, range, or custom-terrain rules for bans.
    /// </summary>
    public IReadOnlyList<string> AvoidedTerrainTags { get; }

    /// <summary>
    /// Normalized base surfaces that fail hard eligibility for this resource.
    /// Existing manual/locked occurrences remain authoritative and are diagnosed rather than deleted.
    /// </summary>
    public IReadOnlyList<CampaignResourceSurfaceType> ExcludedTerrainSurfaces { get; }

    public IReadOnlyList<string> CustomTerrainIncludes { get; }

    public IReadOnlyList<string> CustomTerrainExcludes { get; }

    public IReadOnlyDictionary<string, double> FieldWeights { get; }

    public IReadOnlyDictionary<string, double> AssociationWeights { get; }

    public void EnsureValid()
    {
        if (!Enum.IsDefined(Medium))
        {
            throw new ArgumentOutOfRangeException(nameof(Medium), Medium, "Unknown resource medium.");
        }

        ElevationMeters?.EnsureValid(name: nameof(ElevationMeters));
        Grade?.EnsureValid(requireNonNegative: true, name: nameof(Grade));
        WaterDistanceKilometers?.EnsureValid(
            requireNonNegative: true,
            name: nameof(WaterDistanceKilometers));
        RegionScaleKilometers?.EnsureValid(
            requireNonNegative: true,
            name: nameof(RegionScaleKilometers));
        if (RegionScaleKilometers is { Minimum: <= 0 })
        {
            throw new ArgumentOutOfRangeException(
                nameof(RegionScaleKilometers),
                "Resource region scale must be greater than zero kilometres.");
        }

        var included = CustomTerrainIncludes.ToHashSet(StringComparer.Ordinal);
        var conflict = CustomTerrainExcludes.FirstOrDefault(included.Contains);
        if (conflict is not null)
        {
            throw new ArgumentException(
                $"Custom terrain '{conflict}' cannot be both included and excluded.",
                nameof(CustomTerrainExcludes));
        }

        var preferred = PreferredTerrainTags.ToHashSet(StringComparer.Ordinal);
        var contradictoryAffinity = AvoidedTerrainTags.FirstOrDefault(preferred.Contains);
        if (contradictoryAffinity is not null)
        {
            throw new ArgumentException(
                $"Terrain factor '{contradictoryAffinity}' cannot be both preferred and avoided.",
                nameof(AvoidedTerrainTags));
        }
    }

    private static IReadOnlyList<string> CopyIdentifiers(
        IEnumerable<string>? values,
        string parameterName)
    {
        var copy = values?.ToArray() ?? [];
        if (copy.Length > MaximumNamedRuleCount)
        {
            throw new ArgumentException(
                $"A resource rule can contain at most {MaximumNamedRuleCount} values in {parameterName}.",
                parameterName);
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in copy)
        {
            if (!CampaignResourceDefinition.IsValidIdentifier(value))
            {
                throw new ArgumentException(
                    $"Resource rule value '{value}' is not a valid portable ID.",
                    parameterName);
            }

            if (!unique.Add(value))
            {
                throw new ArgumentException(
                    $"Resource rule value '{value}' appears more than once in {parameterName}.",
                    parameterName);
            }
        }

        Array.Sort(copy, StringComparer.Ordinal);
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyDictionary<string, double> CopyWeights(
        IReadOnlyDictionary<string, double>? values,
        string parameterName)
    {
        var copy = new SortedDictionary<string, double>(StringComparer.Ordinal);
        if (values is null)
        {
            return new ReadOnlyDictionary<string, double>(copy);
        }

        if (values.Count > MaximumNamedRuleCount)
        {
            throw new ArgumentException(
                $"A resource rule can contain at most {MaximumNamedRuleCount} weights in {parameterName}.",
                parameterName);
        }

        foreach (var (key, value) in values)
        {
            if (!CampaignResourceDefinition.IsValidIdentifier(key))
            {
                throw new ArgumentException(
                    $"Resource weight key '{key}' is not a valid portable ID.",
                    parameterName);
            }

            if (!double.IsFinite(value) || Math.Abs(value) > MaximumAbsoluteWeight)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"Resource weights must be finite and between {-MaximumAbsoluteWeight} and {MaximumAbsoluteWeight}.");
            }

            copy.Add(key, value);
        }

        return new ReadOnlyDictionary<string, double>(copy);
    }

    private static IReadOnlyList<CampaignResourceSurfaceType> CopySurfaces(
        IEnumerable<CampaignResourceSurfaceType>? values)
    {
        var copy = values?.ToArray() ?? [];
        var unique = new HashSet<CampaignResourceSurfaceType>();
        foreach (var value in copy)
        {
            if (!Enum.IsDefined(value) || value == CampaignResourceSurfaceType.Unassigned)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(values),
                    value,
                    "A hard-excluded resource surface must be an assigned normalized surface.");
            }

            if (!unique.Add(value))
            {
                throw new ArgumentException(
                    $"Resource surface '{value}' appears more than once in the hard-exclusion list.",
                    nameof(values));
            }
        }

        Array.Sort(copy);
        return Array.AsReadOnly(copy);
    }
}
