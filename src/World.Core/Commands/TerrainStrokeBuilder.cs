using Kingdom.World.Core.Brushes;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Terrain;

namespace Kingdom.World.Core.Commands;

public sealed class TerrainStrokeBuilder
{
    private readonly WorldTerrain _terrain;
    private readonly Dictionary<long, MutableChange> _changes = [];
    private bool _completed;

    public TerrainStrokeBuilder(WorldTerrain terrain)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
    }

    public int ChangedSampleCount => _changes.Count;

    public void ApplyStamp(ITerrainBrush brush, TerrainCoordinate center, BrushSettings settings)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        ArgumentNullException.ThrowIfNull(brush);

        var stampChanges = brush.Apply(_terrain, center, settings);
        for (var index = 0; index < stampChanges.Count; index++)
        {
            var change = stampChanges[index];
            var key = ((long)change.Y << 32) | (uint)change.X;
            if (_changes.TryGetValue(key, out var existing))
            {
                existing.After = change.After;
            }
            else
            {
                _changes.Add(key, new MutableChange(change.X, change.Y, change.Before, change.After));
            }
        }
    }

    public TerrainEditCommand Complete(string description)
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        _completed = true;

        var changes = new List<TerrainSampleChange>(_changes.Count);
        foreach (var change in _changes.Values)
        {
            if (change.Before != change.After)
            {
                changes.Add(new TerrainSampleChange(change.X, change.Y, change.Before, change.After));
            }
        }

        changes.Sort(static (left, right) =>
        {
            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        });
        return new TerrainEditCommand(_terrain, description, changes);
    }

    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(_completed, this);
        _completed = true;
        foreach (var change in _changes.Values)
        {
            _terrain.SetHeight(change.X, change.Y, change.Before);
        }
    }

    private sealed class MutableChange(int x, int y, short before, short after)
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public short Before { get; } = before;

        public short After { get; set; } = after;
    }
}
