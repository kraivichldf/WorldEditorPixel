using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Core.Commands;

public sealed class CampaignSeasonStrokeBuilder
{
    private readonly CampaignSeasonMap _seasons;
    private readonly Dictionary<SeasonIdentity, MutableChange> _changes = [];
    private bool _closed;

    public CampaignSeasonStrokeBuilder(CampaignSeasonMap seasons)
    {
        _seasons = seasons ?? throw new ArgumentNullException(nameof(seasons));
    }

    public int TouchedOccurrenceCount => _changes.Count;

    public int TouchedTileCount => _changes.Keys
        .Select(static identity => (identity.X, identity.Y))
        .Distinct()
        .Count();

    public bool IsClosed => _closed;

    public void Upsert(
        CampaignTileCoordinate coordinate,
        string seasonId,
        bool locked = true) =>
        Upsert(coordinate.X, coordinate.Y, new CampaignSeasonOccurrence(seasonId, locked));

    public void Upsert(int x, int y, CampaignSeasonOccurrence occurrence)
    {
        EnsureOpen();
        var beforeCurrentEdit = GetOccurrenceOrNull(x, y, occurrence.SeasonId);
        _seasons.Upsert(x, y, occurrence);
        Capture(x, y, occurrence.SeasonId, beforeCurrentEdit, occurrence);
    }

    public void Remove(CampaignTileCoordinate coordinate, string seasonId) =>
        Remove(coordinate.X, coordinate.Y, seasonId);

    public void Remove(int x, int y, string seasonId)
    {
        EnsureOpen();
        var beforeCurrentEdit = GetOccurrenceOrNull(x, y, seasonId);
        _seasons.Remove(x, y, seasonId);
        Capture(x, y, seasonId, beforeCurrentEdit, after: null);
    }

    public void SetLocked(CampaignTileCoordinate coordinate, string seasonId, bool locked)
    {
        EnsureOpen();
        var beforeCurrentEdit = GetOccurrenceOrNull(coordinate.X, coordinate.Y, seasonId);
        if (beforeCurrentEdit is not { } occurrence)
        {
            return;
        }

        var after = occurrence with { Locked = locked };
        _seasons.Upsert(coordinate.X, coordinate.Y, after);
        Capture(coordinate.X, coordinate.Y, seasonId, beforeCurrentEdit, after);
    }

    public CampaignSeasonEditCommand Complete(string description)
    {
        EnsureOpen();
        _closed = true;
        return new CampaignSeasonEditCommand(_seasons, description, GetOrderedChanges());
    }

    public void Cancel()
    {
        EnsureOpen();
        _closed = true;
        _seasons.Apply(GetOrderedChanges().Select(static change =>
            change.Before is { } occurrence
                ? CampaignSeasonMutation.Upsert(change.X, change.Y, occurrence)
                : CampaignSeasonMutation.Remove(change.X, change.Y, change.SeasonId)));
    }

    private CampaignSeasonOccurrence? GetOccurrenceOrNull(
        int x,
        int y,
        string seasonId) =>
        _seasons.TryGetOccurrence(x, y, seasonId, out var occurrence)
            ? occurrence
            : null;

    private void Capture(
        int x,
        int y,
        string seasonId,
        CampaignSeasonOccurrence? beforeCurrentEdit,
        CampaignSeasonOccurrence? after)
    {
        var identity = new SeasonIdentity(x, y, seasonId);
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
            new MutableChange(x, y, seasonId, beforeCurrentEdit, after));
    }

    private IEnumerable<CampaignSeasonChange> GetOrderedChanges() =>
        _changes.Values
            .Select(static change => new CampaignSeasonChange(
                change.X,
                change.Y,
                change.SeasonId,
                change.Before,
                change.After))
            .OrderBy(static change => change.Y)
            .ThenBy(static change => change.X)
            .ThenBy(static change => change.SeasonId, StringComparer.Ordinal);

    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_closed, this);

    private readonly record struct SeasonIdentity(int X, int Y, string SeasonId);

    private sealed class MutableChange(
        int x,
        int y,
        string seasonId,
        CampaignSeasonOccurrence? before,
        CampaignSeasonOccurrence? after)
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public string SeasonId { get; } = seasonId;

        public CampaignSeasonOccurrence? Before { get; } = before;

        public CampaignSeasonOccurrence? After { get; set; } = after;
    }
}
