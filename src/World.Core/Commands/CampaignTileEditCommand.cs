using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Core.Commands;

public sealed class CampaignTileEditCommand : IWorldCommand
{
    private readonly CampaignTileLayer _layer;
    private readonly CampaignTileChange[] _changes;

    public CampaignTileEditCommand(
        CampaignTileLayer layer,
        string description,
        IEnumerable<CampaignTileChange> changes)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(changes);

        Description = description;
        _changes = changes.Where(static change => change.Before != change.After).ToArray();
    }

    public string Description { get; }

    public IReadOnlyList<CampaignTileChange> Changes => _changes;

    public bool IsEmpty => _changes.Length == 0;

    public void Execute()
    {
        for (var index = 0; index < _changes.Length; index++)
        {
            var change = _changes[index];
            _layer.SetTileType(change.X, change.Y, change.After);
        }
    }

    public void Undo()
    {
        for (var index = _changes.Length - 1; index >= 0; index--)
        {
            var change = _changes[index];
            _layer.SetTileType(change.X, change.Y, change.Before);
        }
    }
}
