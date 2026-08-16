using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Core.Commands;

public sealed class CampaignTileStrokeBuilder
{
    private readonly CampaignTileLayer _layer;
    private readonly Dictionary<long, MutableChange> _changes = [];
    private bool _completed;

    public CampaignTileStrokeBuilder(CampaignTileLayer layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }

    public int ChangedTileCount => _changes.Count;

    public void ApplyTile(CampaignTileCoordinate coordinate, CampaignTileType type)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        var key = GetKey(coordinate.X, coordinate.Y);
        if (_changes.TryGetValue(key, out var existing))
        {
            existing.After = type;
            _layer.SetTileType(coordinate.X, coordinate.Y, type);
            return;
        }

        var before = _layer.GetTileType(coordinate.X, coordinate.Y);
        if (before == type)
        {
            return;
        }

        _changes.Add(key, new MutableChange(coordinate.X, coordinate.Y, before, type));
        _layer.SetTileType(coordinate.X, coordinate.Y, type);
    }

    public CampaignTileEditCommand Complete(string description)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        _completed = true;

        var changes = new List<CampaignTileChange>(_changes.Count);
        foreach (var change in _changes.Values)
        {
            if (change.Before != change.After)
            {
                changes.Add(new CampaignTileChange(change.X, change.Y, change.Before, change.After));
            }
        }

        changes.Sort(static (left, right) =>
        {
            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        });
        return new CampaignTileEditCommand(_layer, description, changes);
    }

    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        _completed = true;
        foreach (var change in _changes.Values)
        {
            _layer.SetTileType(change.X, change.Y, change.Before);
        }
    }

    private static long GetKey(int x, int y) => ((long)y << 32) | (uint)x;

    private sealed class MutableChange(
        int x,
        int y,
        CampaignTileType before,
        CampaignTileType after)
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public CampaignTileType Before { get; } = before;

        public CampaignTileType After { get; set; } = after;
    }
}
