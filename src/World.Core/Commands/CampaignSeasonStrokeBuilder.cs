using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Core.Commands;

public sealed class CampaignSeasonStrokeBuilder
{
    private readonly CampaignSeasonMap _seasons;
    private readonly Dictionary<CampaignTileCoordinate, MutableChange> _changes = [];
    private bool _closed;

    public CampaignSeasonStrokeBuilder(CampaignSeasonMap seasons)
    {
        _seasons = seasons ?? throw new ArgumentNullException(nameof(seasons));
    }

    public int TouchedTileCount => _changes.Count;

    public bool IsClosed => _closed;

    public void SetTile(CampaignTileCoordinate coordinate, CampaignSeasonTile tile) =>
        SetTile(coordinate.X, coordinate.Y, tile);

    public void SetTile(int x, int y, CampaignSeasonTile tile)
    {
        EnsureOpen();
        var beforeCurrentEdit = _seasons.GetTile(x, y);
        _seasons.SetTile(x, y, tile);
        Capture(x, y, beforeCurrentEdit, tile);
    }

    public void Paint(
        CampaignTileCoordinate coordinate,
        string seasonId,
        bool locked = true) =>
        SetTile(coordinate, new CampaignSeasonTile(seasonId, locked));

    public void ResetToDefault(CampaignTileCoordinate coordinate, bool locked = false) =>
        SetTile(coordinate, new CampaignSeasonTile(_seasons.DefaultSeasonId, locked));

    public void SetLocked(CampaignTileCoordinate coordinate, bool locked)
    {
        var current = _seasons.GetTile(coordinate.X, coordinate.Y);
        SetTile(coordinate, current with { Locked = locked });
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
            new CampaignSeasonMutation(change.X, change.Y, change.Before)));
    }

    private void Capture(
        int x,
        int y,
        CampaignSeasonTile beforeCurrentEdit,
        CampaignSeasonTile after)
    {
        var coordinate = new CampaignTileCoordinate(x, y);
        if (_changes.TryGetValue(coordinate, out var existing))
        {
            existing.After = after;
            return;
        }

        if (beforeCurrentEdit == after)
        {
            return;
        }

        _changes.Add(coordinate, new MutableChange(x, y, beforeCurrentEdit, after));
    }

    private IEnumerable<CampaignSeasonChange> GetOrderedChanges() =>
        _changes.Values
            .Select(static change => new CampaignSeasonChange(
                change.X,
                change.Y,
                change.Before,
                change.After))
            .OrderBy(static change => change.Y)
            .ThenBy(static change => change.X);

    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_closed, this);

    private sealed class MutableChange(
        int x,
        int y,
        CampaignSeasonTile before,
        CampaignSeasonTile after)
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public CampaignSeasonTile Before { get; } = before;

        public CampaignSeasonTile After { get; set; } = after;
    }
}
