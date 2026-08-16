namespace Kingdom.World.Core.Campaign.Resources;

public enum CampaignResourceDiagnosticCode
{
    TerrainUnassigned = 0,
    MediumRequiresLand = 1,
    MediumRequiresWater = 2,
    ElevationBelowMinimum = 3,
    ElevationAboveMaximum = 4,
    GradeBelowMinimum = 5,
    GradeAboveMaximum = 6,
    WaterDistanceBelowMinimum = 7,
    WaterDistanceAboveMaximum = 8,
    CustomTerrainNotIncluded = 9,
    CustomTerrainExcluded = 10,
    TerrainSurfaceExcluded = 11,
}

public readonly record struct CampaignResourceDiagnosticIssue(
    CampaignResourceDiagnosticCode Code,
    string Message);

public enum CampaignResourceUnevaluatedFactor
{
    ClimateProfile = 0,
    GeologyProfile = 1,
    PreferredTerrainTags = 2,
    FieldWeights = 3,
    AssociationWeights = 4,
    DistributionShape = 5,
    RegionScale = 6,
    FinalGeneratorSuitability = 7,
    AvoidedTerrainTags = 8,
}

public sealed class CampaignResourceDiagnosticResult
{
    internal CampaignResourceDiagnosticResult(
        IEnumerable<CampaignResourceDiagnosticIssue> issues,
        IEnumerable<CampaignResourceUnevaluatedFactor>? unevaluatedFactors = null)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = Array.AsReadOnly(issues
            .OrderBy(static issue => issue.Code)
            .ToArray());
        UnevaluatedFactors = Array.AsReadOnly((unevaluatedFactors ?? [])
            .Distinct()
            .OrderBy(static factor => factor)
            .ToArray());
    }

    public IReadOnlyList<CampaignResourceDiagnosticIssue> Issues { get; }

    public bool HasWarnings => Issues.Count > 0;

    public IReadOnlyList<CampaignResourceUnevaluatedFactor> UnevaluatedFactors { get; }

    public bool HasUnevaluatedFactors => UnevaluatedFactors.Count > 0;
}

public sealed class CampaignResourceOccurrenceDiagnostic
{
    internal CampaignResourceOccurrenceDiagnostic(
        int x,
        int y,
        CampaignResourceOccurrence occurrence,
        CampaignResourceTerrainSample terrain,
        CampaignResourceDiagnosticResult result)
    {
        X = x;
        Y = y;
        Occurrence = occurrence;
        Terrain = terrain;
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public int X { get; }

    public int Y { get; }

    public string ResourceId => Occurrence.ResourceId;

    public CampaignResourceOccurrence Occurrence { get; }

    public CampaignResourceTerrainSample Terrain { get; }

    public CampaignResourceDiagnosticResult Result { get; }

    public IReadOnlyList<CampaignResourceDiagnosticIssue> Issues => Result.Issues;

    public bool HasWarnings => Result.HasWarnings;
}
