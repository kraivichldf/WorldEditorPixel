namespace Kingdom.World.Core.Campaign.Resources;

public sealed record CampaignResourceGenerationReport(
    string ResourceId,
    int EligibleTileCount,
    int RequestedTileCount,
    int ActualOccurrenceCount,
    int GeneratedOccurrenceCount,
    int RegionCount,
    double MeanPotential,
    byte MaximumPotential,
    int PreservedLockCount,
    int OverTargetLockCount,
    double EffectiveCoveragePercent,
    double ActualCoveragePercent,
    string? ShortfallReason,
    IReadOnlyList<string> Warnings);

public sealed class CampaignResourceGenerationResult
{
    public const int MaximumCandidateOccurrenceCount = 2_000_000;

    public CampaignResourceGenerationResult(
        CampaignResourceMap candidateMap,
        CampaignResourceGenerationSettings settings,
        CampaignResourceGenerationScope scope,
        IEnumerable<CampaignResourceGenerationReport> reports,
        long sourceTerrainRevision,
        long sourceResourceRevision)
    {
        CandidateMap = candidateMap ?? throw new ArgumentNullException(nameof(candidateMap));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(scope);
        candidateMap.EnsureValid();
        if (candidateMap.OccurrenceCount > MaximumCandidateOccurrenceCount)
        {
            throw new CampaignResourceGenerationLimitException(candidateMap.OccurrenceCount);
        }

        settings.EnsureValid(candidateMap.Catalog);
        scope.EnsureValid(candidateMap.Catalog);
        Scope = scope;
        var reportCopy = (reports ?? throw new ArgumentNullException(nameof(reports)))
            .Select(static report => report with
            {
                Warnings = Array.AsReadOnly(report.Warnings.ToArray()),
            })
            .OrderBy(static report => report.ResourceId, StringComparer.Ordinal)
            .ToArray();
        var duplicateReport = reportCopy
            .GroupBy(static report => report.ResourceId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateReport is not null)
        {
            throw new ArgumentException(
                $"Resource generation report '{duplicateReport.Key}' appears more than once.",
                nameof(reports));
        }

        if (reportCopy.Any(report => !candidateMap.Catalog.Contains(report.ResourceId)))
        {
            throw new ArgumentException("Resource generation reports contain an unknown resource ID.", nameof(reports));
        }

        Reports = Array.AsReadOnly(reportCopy);
        SourceTerrainRevision = sourceTerrainRevision;
        SourceResourceRevision = sourceResourceRevision;
        CandidateResourceRevision = candidateMap.Revision;
    }

    public CampaignResourceMap CandidateMap { get; }

    public CampaignResourceGenerationSettings Settings { get; }

    public CampaignResourceGenerationScope Scope { get; }

    public IReadOnlyList<CampaignResourceGenerationReport> Reports { get; }

    public long SourceTerrainRevision { get; }

    public long SourceResourceRevision { get; }

    public long CandidateResourceRevision { get; }

    public bool IsCurrent(long terrainRevision, long resourceRevision) =>
        CandidateMap.Revision == CandidateResourceRevision &&
        terrainRevision == SourceTerrainRevision &&
        resourceRevision == SourceResourceRevision;

    public bool IsCurrent(
        ICampaignResourceTerrainQuery terrainQuery,
        CampaignResourceMap resourceMap)
    {
        ArgumentNullException.ThrowIfNull(terrainQuery);
        ArgumentNullException.ThrowIfNull(resourceMap);
        return CandidateMap.Revision == CandidateResourceRevision &&
            terrainQuery.Definition == CandidateMap.Definition &&
            resourceMap.Definition == CandidateMap.Definition &&
            ReferenceEquals(resourceMap.Catalog, CandidateMap.Catalog) &&
            IsCurrent(terrainQuery.Revision, resourceMap.Revision);
    }
}

public sealed class CampaignResourceGenerationLimitException : InvalidOperationException
{
    public CampaignResourceGenerationLimitException(int occurrenceCount)
        : base(
            $"The candidate would contain {occurrenceCount:N0} occurrences, above the " +
            $"{CampaignResourceGenerationResult.MaximumCandidateOccurrenceCount:N0} occurrence limit. " +
            "Narrow the scope or lower independent resource coverage.")
    {
        OccurrenceCount = occurrenceCount;
    }

    public int OccurrenceCount { get; }
}
