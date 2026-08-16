namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceOccurrenceDiagnosticService
{
    private readonly CampaignResourceMap _resources;
    private readonly ICampaignResourceTerrainQuery _terrain;
    private IReadOnlyList<CampaignResourceOccurrenceDiagnostic>? _cachedDiagnostics;
    private IReadOnlyList<CampaignResourceOccurrenceDiagnostic>? _cachedWarnings;
    private long _cachedResourceRevision = -1;
    private long _cachedTerrainRevision = -1;

    public CampaignResourceOccurrenceDiagnosticService(
        CampaignResourceMap resources,
        ICampaignResourceTerrainQuery terrain)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        if (_resources.Definition != _terrain.Definition)
        {
            throw new ArgumentException(
                "Resource and terrain queries must describe the same campaign world definition.",
                nameof(terrain));
        }
    }

    public IReadOnlyList<CampaignResourceOccurrenceDiagnostic> GetDiagnostics()
    {
        EnsureCache();
        return _cachedDiagnostics!;
    }

    public IReadOnlyList<CampaignResourceOccurrenceDiagnostic> GetWarnings()
    {
        EnsureCache();
        return _cachedWarnings!;
    }

    private void EnsureCache()
    {
        var resourceRevision = _resources.Revision;
        var terrainRevision = _terrain.Revision;
        if (_cachedDiagnostics is not null &&
            _cachedResourceRevision == resourceRevision &&
            _cachedTerrainRevision == terrainRevision)
        {
            return;
        }

        var diagnostics = new List<CampaignResourceOccurrenceDiagnostic>(_resources.OccurrenceCount);
        foreach (var entry in _resources.GetMaterializedOccurrences())
        {
            var terrain = _terrain.GetSample(entry.X, entry.Y);
            terrain.EnsureValid();
            var definition = _resources.Catalog.Get(entry.Occurrence.ResourceId);
            var result = CampaignResourceDiagnosticEvaluator.Evaluate(definition, terrain);
            diagnostics.Add(new CampaignResourceOccurrenceDiagnostic(
                entry.X,
                entry.Y,
                entry.Occurrence,
                terrain,
                result));
        }

        var snapshot = Array.AsReadOnly(diagnostics.ToArray());
        _cachedDiagnostics = snapshot;
        _cachedWarnings = Array.AsReadOnly(snapshot.Where(static value => value.HasWarnings).ToArray());
        _cachedResourceRevision = resourceRevision;
        _cachedTerrainRevision = terrainRevision;
    }
}
