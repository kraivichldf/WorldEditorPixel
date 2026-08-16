using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Core.Commands;

public sealed class CampaignTileStampBuilder
{
    private readonly CampaignTileMap _tiles;
    private readonly Dictionary<long, MutableChange> _changes = [];
    private bool _completed;

    public CampaignTileStampBuilder(CampaignTileMap tiles)
    {
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
    }

    public int ChangedTileCount => _changes.Count;

    public void ApplyTile(CampaignTileCoordinate coordinate, CampaignTileData data)
    {
        if (!TryApplyTile(coordinate, data, out var failureReason))
        {
            throw new CampaignTileTopologyException(failureReason!);
        }
    }

    public bool TryApplyTile(
        CampaignTileCoordinate coordinate,
        CampaignTileData data,
        out string? failureReason)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        var key = GetKey(coordinate.X, coordinate.Y);
        var beforeCurrentEdit = _tiles.GetTile(coordinate.X, coordinate.Y);
        if (!_tiles.TrySetTile(coordinate.X, coordinate.Y, data, out failureReason))
        {
            return false;
        }

        if (_changes.TryGetValue(key, out var existing))
        {
            existing.After = data;
            return true;
        }

        if (beforeCurrentEdit == data)
        {
            return true;
        }

        _changes.Add(key, new MutableChange(coordinate.X, coordinate.Y, beforeCurrentEdit, data));
        return true;
    }

    public CampaignTileStampCommand Complete(string description)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        _completed = true;

        var changes = _changes.Values
            .Where(static change => change.Before != change.After)
            .Select(static change => new CampaignTileStampChange(
                change.X,
                change.Y,
                change.Before,
                change.After))
            .OrderBy(static change => change.Y)
            .ThenBy(static change => change.X)
            .ToArray();
        return new CampaignTileStampCommand(_tiles, description, changes);
    }

    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        _completed = true;
        _tiles.SetTiles(_changes.Values.Select(static change =>
            new CampaignTileEntry(change.X, change.Y, change.Before)));
    }

    private static long GetKey(int x, int y) => ((long)y << 32) | (uint)x;

    private sealed class MutableChange(
        int x,
        int y,
        CampaignTileData before,
        CampaignTileData after)
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public CampaignTileData Before { get; } = before;

        public CampaignTileData After { get; set; } = after;
    }
}
