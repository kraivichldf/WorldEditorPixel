namespace Kingdom.World.Core.Campaign.Resources;

public enum CampaignResourceTerrainKind
{
    Unassigned = 0,
    Land = 1,
    Water = 2,
}

public enum CampaignResourceSurfaceType
{
    Unassigned = 0,
    Grassland = 1,
    Forest = 2,
    Desert = 3,
    Wetland = 4,
    Tundra = 5,
    BarrenRock = 6,
    Sea = 7,
    Lake = 8,
}

public enum CampaignResourceTerrainForm
{
    Flat = 0,
    Rolling = 1,
    Hills = 2,
    Mountain = 3,
    Cliff = 4,
}

[Flags]
public enum CampaignResourceRiverFeatures
{
    None = 0,
    Present = 1 << 0,
    Large = 1 << 1,
    Junction = 1 << 2,
}

[Flags]
public enum CampaignResourceCoastFlags
{
    None = 0,
    AdjacentSea = 1 << 0,
    AdjacentLake = 1 << 1,
    CoastalWater = 1 << 2,
    BeachShore = 1 << 3,
    CliffShore = 1 << 4,
}

public readonly record struct CampaignResourceTerrainSample(
    CampaignResourceTerrainKind Kind,
    CampaignResourceSurfaceType Surface,
    CampaignResourceTerrainForm Form,
    string? CustomTerrainId,
    short ElevationMeters,
    double MaximumCardinalGrade,
    double SeaDistanceKilometers,
    double LakeDistanceKilometers,
    double RiverDistanceKilometers,
    CampaignResourceRiverFeatures RiverFeatures,
    CampaignResourceCoastFlags CoastFlags)
{
    public double NearestWaterDistanceKilometers => Math.Min(
        SeaDistanceKilometers,
        Math.Min(LakeDistanceKilometers, RiverDistanceKilometers));

    public bool HasRiver => RiverFeatures.HasFlag(CampaignResourceRiverFeatures.Present);

    public bool IsAdjacentToSea => CoastFlags.HasFlag(CampaignResourceCoastFlags.AdjacentSea);

    public bool IsAdjacentToLake => CoastFlags.HasFlag(CampaignResourceCoastFlags.AdjacentLake);

    public void EnsureValid()
    {
        if (!Enum.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown normalized terrain kind.");
        }

        if (!Enum.IsDefined(Surface))
        {
            throw new ArgumentOutOfRangeException(nameof(Surface), Surface, "Unknown normalized resource surface.");
        }

        if (!Enum.IsDefined(Form))
        {
            throw new ArgumentOutOfRangeException(nameof(Form), Form, "Unknown normalized terrain form.");
        }

        const CampaignResourceRiverFeatures allRiverFeatures =
            CampaignResourceRiverFeatures.Present |
            CampaignResourceRiverFeatures.Large |
            CampaignResourceRiverFeatures.Junction;
        if ((RiverFeatures & ~allRiverFeatures) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RiverFeatures),
                RiverFeatures,
                "Unknown normalized River features.");
        }

        if ((RiverFeatures & (CampaignResourceRiverFeatures.Large | CampaignResourceRiverFeatures.Junction)) != 0 &&
            !HasRiver)
        {
            throw new ArgumentException(
                "Large and junction River features require River presence.",
                nameof(RiverFeatures));
        }

        const CampaignResourceCoastFlags allCoastFlags =
            CampaignResourceCoastFlags.AdjacentSea |
            CampaignResourceCoastFlags.AdjacentLake |
            CampaignResourceCoastFlags.CoastalWater |
            CampaignResourceCoastFlags.BeachShore |
            CampaignResourceCoastFlags.CliffShore;
        if ((CoastFlags & ~allCoastFlags) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CoastFlags), CoastFlags, "Unknown normalized coast flags.");
        }

        var expectedKind = Surface switch
        {
            CampaignResourceSurfaceType.Unassigned => CampaignResourceTerrainKind.Unassigned,
            CampaignResourceSurfaceType.Sea or CampaignResourceSurfaceType.Lake =>
                CampaignResourceTerrainKind.Water,
            _ => CampaignResourceTerrainKind.Land,
        };
        if (Kind != expectedKind)
        {
            throw new ArgumentException(
                $"Normalized surface {Surface} requires terrain kind {expectedKind}, not {Kind}.",
                nameof(Kind));
        }

        if (Kind == CampaignResourceTerrainKind.Unassigned &&
            (RiverFeatures != CampaignResourceRiverFeatures.None || CoastFlags != CampaignResourceCoastFlags.None))
        {
            throw new ArgumentException(
                "Unassigned terrain cannot carry River or coast metadata.",
                nameof(Kind));
        }

        if (HasRiver && Kind != CampaignResourceTerrainKind.Land)
        {
            throw new ArgumentException("River metadata requires assigned land.", nameof(RiverFeatures));
        }

        var hasCoastalWater = CoastFlags.HasFlag(CampaignResourceCoastFlags.CoastalWater);
        var hasAdjacentSea = CoastFlags.HasFlag(CampaignResourceCoastFlags.AdjacentSea);
        var hasAdjacentLake = CoastFlags.HasFlag(CampaignResourceCoastFlags.AdjacentLake);
        var hasBeach = CoastFlags.HasFlag(CampaignResourceCoastFlags.BeachShore);
        var hasCliff = CoastFlags.HasFlag(CampaignResourceCoastFlags.CliffShore);
        if (hasCoastalWater && Kind != CampaignResourceTerrainKind.Water)
        {
            throw new ArgumentException("Coastal-water metadata requires a water cell.", nameof(CoastFlags));
        }

        if ((hasAdjacentSea || hasAdjacentLake || hasBeach || hasCliff) &&
            Kind != CampaignResourceTerrainKind.Land)
        {
            throw new ArgumentException(
                "Adjacent-water and shore-style metadata require a land cell.",
                nameof(CoastFlags));
        }

        if ((hasBeach || hasCliff) && !(hasAdjacentSea || hasAdjacentLake))
        {
            throw new ArgumentException(
                "Beach and cliff shore metadata require at least one adjacent Sea or Lake edge.",
                nameof(CoastFlags));
        }

        if (CustomTerrainId is not null)
        {
            if (Kind != CampaignResourceTerrainKind.Land ||
                !CampaignResourceDefinition.IsValidIdentifier(CustomTerrainId))
            {
                throw new ArgumentException(
                    "A normalized custom terrain ID must be a valid portable ID on assigned land.",
                    nameof(CustomTerrainId));
            }
        }

        if (!double.IsFinite(MaximumCardinalGrade) || MaximumCardinalGrade < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumCardinalGrade),
                MaximumCardinalGrade,
                "Maximum cardinal grade must be finite and non-negative.");
        }

        EnsureValidDistance(SeaDistanceKilometers, nameof(SeaDistanceKilometers));
        EnsureValidDistance(LakeDistanceKilometers, nameof(LakeDistanceKilometers));
        EnsureValidDistance(RiverDistanceKilometers, nameof(RiverDistanceKilometers));
    }

    private static void EnsureValidDistance(double value, string name)
    {
        if (double.IsNaN(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                "Normalized water distance must be non-negative or positive infinity when no source exists.");
        }
    }
}
