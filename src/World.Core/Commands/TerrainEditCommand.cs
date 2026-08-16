using Kingdom.World.Core.Terrain;

namespace Kingdom.World.Core.Commands;

public sealed class TerrainEditCommand : IWorldCommand
{
    private readonly WorldTerrain _terrain;
    private readonly TerrainSampleChange[] _changes;

    public TerrainEditCommand(
        WorldTerrain terrain,
        string description,
        IEnumerable<TerrainSampleChange> changes)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(changes);

        _terrain = terrain;
        Description = description;
        _changes = changes.Where(static change => change.Before != change.After).ToArray();
    }

    public string Description { get; }

    public IReadOnlyList<TerrainSampleChange> Changes => _changes;

    public bool IsEmpty => _changes.Length == 0;

    public void Execute()
    {
        for (var index = 0; index < _changes.Length; index++)
        {
            var change = _changes[index];
            _terrain.SetHeight(change.X, change.Y, change.After);
        }
    }

    public void Undo()
    {
        for (var index = _changes.Length - 1; index >= 0; index--)
        {
            var change = _changes[index];
            _terrain.SetHeight(change.X, change.Y, change.Before);
        }
    }
}
