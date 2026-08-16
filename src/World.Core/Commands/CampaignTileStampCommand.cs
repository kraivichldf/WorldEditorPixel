using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Core.Commands;

public sealed class CampaignTileStampCommand : IWorldCommand
{
    private readonly CampaignTileMap _tiles;
    private readonly CampaignTileStampChange[] _changes;

    public CampaignTileStampCommand(
        CampaignTileMap tiles,
        string description,
        IEnumerable<CampaignTileStampChange> changes)
    {
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(changes);

        Description = description;
        _changes = changes.Where(static change => change.Before != change.After).ToArray();
    }

    public string Description { get; }

    public IReadOnlyList<CampaignTileStampChange> Changes => _changes;

    public bool IsEmpty => _changes.Length == 0;

    public void Execute()
    {
        _tiles.SetTiles(_changes.Select(static change =>
            new CampaignTileEntry(change.X, change.Y, change.After)));
    }

    public void Undo()
    {
        _tiles.SetTiles(_changes.Reverse().Select(static change =>
            new CampaignTileEntry(change.X, change.Y, change.Before)));
    }
}
