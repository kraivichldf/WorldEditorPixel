using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Core.Commands;

public sealed class CampaignResourceStrokeBuilder
{
    private readonly CampaignResourceMap _resources;
    private readonly Dictionary<ResourceIdentity, MutableChange> _changes = [];
    private bool _closed;

    public CampaignResourceStrokeBuilder(CampaignResourceMap resources)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public int TouchedOccurrenceCount => _changes.Count;

    public bool IsClosed => _closed;

    public void Upsert(
        CampaignTileCoordinate coordinate,
        CampaignResourceOccurrence occurrence) =>
        Upsert(coordinate.X, coordinate.Y, occurrence);

    public void Upsert(int x, int y, CampaignResourceOccurrence occurrence)
    {
        EnsureOpen();
        var resourceId = occurrence.ResourceId;
        var beforeCurrentEdit = GetOccurrenceOrNull(x, y, resourceId);
        _resources.Upsert(x, y, occurrence);
        Capture(x, y, resourceId, beforeCurrentEdit, occurrence);
    }

    public void Remove(CampaignTileCoordinate coordinate, string resourceId) =>
        Remove(coordinate.X, coordinate.Y, resourceId);

    public void Remove(int x, int y, string resourceId)
    {
        EnsureOpen();
        var beforeCurrentEdit = GetOccurrenceOrNull(x, y, resourceId);
        _resources.Remove(x, y, resourceId);
        Capture(x, y, resourceId, beforeCurrentEdit, after: null);
    }

    public CampaignResourceEditCommand Complete(string description)
    {
        EnsureOpen();
        _closed = true;
        return new CampaignResourceEditCommand(
            _resources,
            description,
            GetOrderedChanges());
    }

    public void Cancel()
    {
        EnsureOpen();
        _closed = true;
        _resources.Apply(GetOrderedChanges().Select(static change =>
            change.Before is { } occurrence
                ? CampaignResourceMutation.Upsert(change.X, change.Y, occurrence)
                : CampaignResourceMutation.Remove(change.X, change.Y, change.ResourceId)));
    }

    private CampaignResourceOccurrence? GetOccurrenceOrNull(
        int x,
        int y,
        string resourceId) =>
        _resources.TryGetOccurrence(x, y, resourceId, out var occurrence)
            ? occurrence
            : null;

    private void Capture(
        int x,
        int y,
        string resourceId,
        CampaignResourceOccurrence? beforeCurrentEdit,
        CampaignResourceOccurrence? after)
    {
        var identity = new ResourceIdentity(x, y, resourceId);
        if (_changes.TryGetValue(identity, out var existing))
        {
            existing.After = after;
            return;
        }

        if (beforeCurrentEdit == after)
        {
            return;
        }

        _changes.Add(
            identity,
            new MutableChange(x, y, resourceId, beforeCurrentEdit, after));
    }

    private IEnumerable<CampaignResourceChange> GetOrderedChanges() =>
        _changes.Values
            .Select(static change => new CampaignResourceChange(
                change.X,
                change.Y,
                change.ResourceId,
                change.Before,
                change.After))
            .OrderBy(static change => change.Y)
            .ThenBy(static change => change.X)
            .ThenBy(static change => change.ResourceId, StringComparer.Ordinal);

    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_closed, this);

    private readonly record struct ResourceIdentity(int X, int Y, string ResourceId);

    private sealed class MutableChange(
        int x,
        int y,
        string resourceId,
        CampaignResourceOccurrence? before,
        CampaignResourceOccurrence? after)
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public string ResourceId { get; } = resourceId;

        public CampaignResourceOccurrence? Before { get; } = before;

        public CampaignResourceOccurrence? After { get; set; } = after;
    }
}
