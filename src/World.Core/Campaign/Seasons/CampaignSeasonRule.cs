namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonRule
{
    public const int MaximumTerrainFilterCount = 64;

    public static CampaignSeasonRule Unrestricted { get; } = new();

    private readonly HashSet<CampaignTileType> _terrainIncludes;
    private readonly HashSet<CampaignTileType> _terrainExcludes;
    private readonly HashSet<string> _customTerrainIncludes;
    private readonly HashSet<string> _customTerrainExcludes;

    public CampaignSeasonRule(
        CampaignSeasonRange? latitudeDegrees = null,
        CampaignSeasonRange? elevationMeters = null,
        CampaignSeasonRange? temperatureCelsius = null,
        CampaignSeasonRange? moisture = null,
        CampaignSeasonRange? seasonalIntensity = null,
        CampaignSeasonRange? seasonalTendency = null,
        CampaignSeasonRange? seaDistanceKilometers = null,
        CampaignSeasonRange? lakeDistanceKilometers = null,
        CampaignSeasonRange? riverDistanceKilometers = null,
        IEnumerable<CampaignTileType>? terrainIncludes = null,
        IEnumerable<CampaignTileType>? terrainExcludes = null,
        IEnumerable<string>? customTerrainIncludes = null,
        IEnumerable<string>? customTerrainExcludes = null)
    {
        LatitudeDegrees = latitudeDegrees;
        ElevationMeters = elevationMeters;
        TemperatureCelsius = temperatureCelsius;
        Moisture = moisture;
        SeasonalIntensity = seasonalIntensity;
        SeasonalTendency = seasonalTendency;
        SeaDistanceKilometers = seaDistanceKilometers;
        LakeDistanceKilometers = lakeDistanceKilometers;
        RiverDistanceKilometers = riverDistanceKilometers;
        TerrainIncludes = CopyTerrainTypes(terrainIncludes, nameof(terrainIncludes));
        TerrainExcludes = CopyTerrainTypes(terrainExcludes, nameof(terrainExcludes));
        CustomTerrainIncludes = CopyIdentifiers(customTerrainIncludes, nameof(customTerrainIncludes));
        CustomTerrainExcludes = CopyIdentifiers(customTerrainExcludes, nameof(customTerrainExcludes));
        _terrainIncludes = TerrainIncludes.ToHashSet();
        _terrainExcludes = TerrainExcludes.ToHashSet();
        _customTerrainIncludes = CustomTerrainIncludes.ToHashSet(StringComparer.Ordinal);
        _customTerrainExcludes = CustomTerrainExcludes.ToHashSet(StringComparer.Ordinal);
        EnsureValid();
    }

    public CampaignSeasonRange? LatitudeDegrees { get; }

    public CampaignSeasonRange? ElevationMeters { get; }

    public CampaignSeasonRange? TemperatureCelsius { get; }

    public CampaignSeasonRange? Moisture { get; }

    public CampaignSeasonRange? SeasonalIntensity { get; }

    public CampaignSeasonRange? SeasonalTendency { get; }

    public CampaignSeasonRange? SeaDistanceKilometers { get; }

    public CampaignSeasonRange? LakeDistanceKilometers { get; }

    public CampaignSeasonRange? RiverDistanceKilometers { get; }

    public IReadOnlyList<CampaignTileType> TerrainIncludes { get; }

    public IReadOnlyList<CampaignTileType> TerrainExcludes { get; }

    public IReadOnlyList<string> CustomTerrainIncludes { get; }

    public IReadOnlyList<string> CustomTerrainExcludes { get; }

    public void EnsureValid()
    {
        LatitudeDegrees?.EnsureValid(nameof(LatitudeDegrees), -90, 90);
        ElevationMeters?.EnsureValid(nameof(ElevationMeters));
        TemperatureCelsius?.EnsureValid(nameof(TemperatureCelsius), -273.15);
        Moisture?.EnsureValid(nameof(Moisture), 0, 1);
        SeasonalIntensity?.EnsureValid(nameof(SeasonalIntensity), -1, 1);
        SeasonalTendency?.EnsureValid(nameof(SeasonalTendency), -1, 1);
        SeaDistanceKilometers?.EnsureValid(nameof(SeaDistanceKilometers), 0);
        LakeDistanceKilometers?.EnsureValid(nameof(LakeDistanceKilometers), 0);
        RiverDistanceKilometers?.EnsureValid(nameof(RiverDistanceKilometers), 0);

        var terrainConflict = _terrainExcludes.FirstOrDefault(_terrainIncludes.Contains);
        if (_terrainIncludes.Contains(terrainConflict) && _terrainExcludes.Contains(terrainConflict))
        {
            throw new ArgumentException(
                $"Terrain '{terrainConflict}' cannot be both included and excluded.",
                nameof(TerrainExcludes));
        }

        var customConflict = _customTerrainExcludes.FirstOrDefault(_customTerrainIncludes.Contains);
        if (customConflict is not null)
        {
            throw new ArgumentException(
                $"Custom terrain '{customConflict}' cannot be both included and excluded.",
                nameof(CustomTerrainExcludes));
        }
    }

    public bool AllowsTerrain(CampaignTileType terrainType, string? customTerrainId = null)
    {
        EnsureCanonicalTerrainType(terrainType, nameof(terrainType));
        if (customTerrainId is not null &&
            !CampaignSeasonDefinition.IsValidPortableIdentifier(
                customTerrainId,
                CampaignCustomTerrainDefinition.MaximumIdentifierLength))
        {
            throw new ArgumentException(
                "Custom terrain ID is not a valid portable identifier.",
                nameof(customTerrainId));
        }

        return AllowsTerrainValidated(terrainType, customTerrainId);
    }

    internal bool AllowsTerrainValidated(CampaignTileType terrainType, string? customTerrainId)
    {
        if (customTerrainId is not null && _customTerrainExcludes.Contains(customTerrainId))
        {
            return false;
        }

        if (customTerrainId is not null && _customTerrainIncludes.Contains(customTerrainId))
        {
            return true;
        }

        if (_terrainExcludes.Contains(terrainType))
        {
            return false;
        }

        var hasWhitelist = _terrainIncludes.Count > 0 || _customTerrainIncludes.Count > 0;
        return !hasWhitelist || _terrainIncludes.Contains(terrainType);
    }

    private static IReadOnlyList<CampaignTileType> CopyTerrainTypes(
        IEnumerable<CampaignTileType>? values,
        string parameterName)
    {
        var copy = values?.ToArray() ?? [];
        if (copy.Length > MaximumTerrainFilterCount)
        {
            throw new ArgumentException(
                $"A season rule can contain at most {MaximumTerrainFilterCount} values in {parameterName}.",
                parameterName);
        }

        var unique = new HashSet<CampaignTileType>();
        foreach (var value in copy)
        {
            EnsureCanonicalTerrainType(value, parameterName);
            if (!unique.Add(value))
            {
                throw new ArgumentException(
                    $"Terrain '{value}' appears more than once in {parameterName}.",
                    parameterName);
            }
        }

        Array.Sort(copy);
        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<string> CopyIdentifiers(
        IEnumerable<string>? values,
        string parameterName)
    {
        var copy = values?.ToArray() ?? [];
        if (copy.Length > MaximumTerrainFilterCount)
        {
            throw new ArgumentException(
                $"A season rule can contain at most {MaximumTerrainFilterCount} values in {parameterName}.",
                parameterName);
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in copy)
        {
            if (!CampaignSeasonDefinition.IsValidPortableIdentifier(
                    value,
                    CampaignCustomTerrainDefinition.MaximumIdentifierLength))
            {
                throw new ArgumentException(
                    $"Custom terrain value '{value}' is not a valid portable ID.",
                    parameterName);
            }

            if (!unique.Add(value))
            {
                throw new ArgumentException(
                    $"Custom terrain value '{value}' appears more than once in {parameterName}.",
                    parameterName);
            }
        }

        Array.Sort(copy, StringComparer.Ordinal);
        return Array.AsReadOnly(copy);
    }

    private static void EnsureCanonicalTerrainType(CampaignTileType value, string parameterName)
    {
        if (!Enum.IsDefined(value) || value is CampaignTileType.Water or CampaignTileType.Coastal)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Season terrain filters require a canonical campaign tile type.");
        }
    }
}
