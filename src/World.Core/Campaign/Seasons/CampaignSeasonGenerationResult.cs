namespace Kingdom.World.Core.Campaign.Seasons;

public sealed record CampaignSeasonGenerationReport(
    string SeasonId,
    bool Selected,
    int ScopeTileCount,
    int CurrentOccurrenceCount,
    int EnvironmentalMatchCount,
    int AddedOccurrenceCount,
    int RemovedOccurrenceCount,
    int RetainedUnlockedCount,
    int PreservedLockCount,
    int CandidateOccurrenceCount,
    double CandidateCoveragePercent,
    string? ZeroReason,
    IReadOnlyList<string> Warnings)
;

public sealed class CampaignSeasonGenerationResult
{
    public CampaignSeasonGenerationResult(
        CampaignSeasonMap candidateMap,
        CampaignSeasonGenerationSettings settings,
        CampaignSeasonGenerationScope scope,
        CampaignSeasonSupportFields supportFields,
        IEnumerable<CampaignSeasonGenerationReport> reports,
        int changedIdentityCount,
        long sourceTerrainRevision,
        long sourceSeasonRevision)
    {
        CandidateMap = candidateMap ?? throw new ArgumentNullException(nameof(candidateMap));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        SupportFields = supportFields ?? throw new ArgumentNullException(nameof(supportFields));
        candidateMap.EnsureValid();
        settings.EnsureValid(candidateMap.Catalog, candidateMap.Definition);
        scope.EnsureValid(candidateMap.Definition);
        if (supportFields.Terrain.Definition != candidateMap.Definition)
        {
            throw new ArgumentException(
                "Season support fields and candidate map must use the same world definition.",
                nameof(supportFields));
        }

        if (changedIdentityCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedIdentityCount));
        }

        var reportCopy = (reports ?? throw new ArgumentNullException(nameof(reports)))
            .Select(static report => report with
            {
                Warnings = Array.AsReadOnly(report.Warnings.ToArray()),
            })
            .OrderBy(report => candidateMap.Catalog.GetIndex(report.SeasonId))
            .ToArray();
        var duplicate = reportCopy
            .GroupBy(static report => report.SeasonId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Season generation report '{duplicate.Key}' appears more than once.",
                nameof(reports));
        }

        if (reportCopy.Any(report => !candidateMap.Catalog.Contains(report.SeasonId)))
        {
            throw new ArgumentException(
                "Season generation reports contain an unknown season ID.",
                nameof(reports));
        }

        Reports = Array.AsReadOnly(reportCopy);
        ChangedIdentityCount = changedIdentityCount;
        SourceTerrainRevision = sourceTerrainRevision;
        SourceSeasonRevision = sourceSeasonRevision;
        CandidateSeasonRevision = candidateMap.Revision;
    }

    public CampaignSeasonMap CandidateMap { get; }

    public CampaignSeasonGenerationSettings Settings { get; }

    public CampaignSeasonGenerationScope Scope { get; }

    public CampaignSeasonSupportFields SupportFields { get; }

    public IReadOnlyList<CampaignSeasonGenerationReport> Reports { get; }

    public int ChangedIdentityCount { get; }

    public long SourceTerrainRevision { get; }

    public long SourceSeasonRevision { get; }

    public long CandidateSeasonRevision { get; }

    public bool IsCurrent(long terrainRevision, long seasonRevision) =>
        CandidateMap.Revision == CandidateSeasonRevision &&
        terrainRevision == SourceTerrainRevision &&
        seasonRevision == SourceSeasonRevision;

    public bool IsCurrent(
        ICampaignSeasonTerrainQuery terrainQuery,
        CampaignSeasonMap seasonMap)
    {
        ArgumentNullException.ThrowIfNull(terrainQuery);
        ArgumentNullException.ThrowIfNull(seasonMap);
        return
            CandidateMap.Revision == CandidateSeasonRevision &&
            terrainQuery.Definition == CandidateMap.Definition &&
            seasonMap.Definition == CandidateMap.Definition &&
            ReferenceEquals(seasonMap.Catalog, CandidateMap.Catalog) &&
            IsCurrent(terrainQuery.Revision, seasonMap.Revision);
    }
}
