using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Core.Commands;

public sealed class CampaignSeasonEditCommand : IWorldCommand
{
    private readonly CampaignSeasonMap _seasons;
    private readonly CampaignSeasonChange[] _changes;
    private readonly IReadOnlyList<CampaignSeasonChange> _readOnlyChanges;

    public CampaignSeasonEditCommand(
        CampaignSeasonMap seasons,
        string description,
        IEnumerable<CampaignSeasonChange> changes)
    {
        _seasons = seasons ?? throw new ArgumentNullException(nameof(seasons));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(changes);
        var pending = changes.ToArray();
        ValidateChanges(seasons, pending);

        Description = description;
        _changes = pending
            .Where(static change => change.Before != change.After)
            .OrderBy(static change => change.Y)
            .ThenBy(static change => change.X)
            .ToArray();
        _readOnlyChanges = Array.AsReadOnly(_changes);
    }

    public string Description { get; }

    public IReadOnlyList<CampaignSeasonChange> Changes => _readOnlyChanges;

    public bool IsEmpty => _changes.Length == 0;

    public void Execute() => Apply(static change => change.After);

    public void Undo() => Apply(static change => change.Before);

    private void Apply(Func<CampaignSeasonChange, CampaignSeasonTile> selectValue) =>
        _seasons.Apply(_changes.Select(change =>
            new CampaignSeasonMutation(change.X, change.Y, selectValue(change))));

    private static void ValidateChanges(
        CampaignSeasonMap seasons,
        IReadOnlyList<CampaignSeasonChange> changes)
    {
        var seen = new HashSet<(int X, int Y)>();
        foreach (var change in changes)
        {
            if (!seasons.IsValidCoordinate(change.X, change.Y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(changes),
                    $"Campaign season coordinate ({change.X}, {change.Y}) is outside the campaign grid.");
            }

            change.Before.EnsureValid(seasons.Catalog);
            change.After.EnsureValid(seasons.Catalog);
            if (!seen.Add((change.X, change.Y)))
            {
                throw new ArgumentException(
                    $"Season tile ({change.X}, {change.Y}) appears more than once in one command.",
                    nameof(changes));
            }
        }
    }
}
