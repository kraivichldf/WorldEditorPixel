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
            .ThenBy(static change => change.SeasonId, StringComparer.Ordinal)
            .ToArray();
        _readOnlyChanges = Array.AsReadOnly(_changes);
    }

    public string Description { get; }

    public IReadOnlyList<CampaignSeasonChange> Changes => _readOnlyChanges;

    public bool IsEmpty => _changes.Length == 0;

    public void Execute() => Apply(static change => change.After);

    public void Undo() => Apply(static change => change.Before);

    private void Apply(Func<CampaignSeasonChange, CampaignSeasonOccurrence?> selectValue) =>
        _seasons.Apply(_changes.Select(change =>
            ToMutation(change, selectValue(change))));

    private static CampaignSeasonMutation ToMutation(
        CampaignSeasonChange change,
        CampaignSeasonOccurrence? value) =>
        value is { } occurrence
            ? CampaignSeasonMutation.Upsert(change.X, change.Y, occurrence)
            : CampaignSeasonMutation.Remove(change.X, change.Y, change.SeasonId);

    private static void ValidateChanges(
        CampaignSeasonMap seasons,
        IReadOnlyList<CampaignSeasonChange> changes)
    {
        var seen = new HashSet<(int X, int Y, string SeasonId)>();
        foreach (var change in changes)
        {
            if (!seasons.IsValidCoordinate(change.X, change.Y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(changes),
                    $"Campaign season coordinate ({change.X}, {change.Y}) is outside the campaign grid.");
            }

            if (!CampaignSeasonDefinition.IsValidIdentifier(change.SeasonId) ||
                !seasons.Catalog.Contains(change.SeasonId))
            {
                throw new ArgumentException(
                    $"Campaign season change references unknown season '{change.SeasonId}'.",
                    nameof(changes));
            }

            ValidateValue(change, change.Before, "Before", changes);
            ValidateValue(change, change.After, "After", changes);
            if (!seen.Add((change.X, change.Y, change.SeasonId)))
            {
                throw new ArgumentException(
                    $"Season '{change.SeasonId}' at ({change.X}, {change.Y}) appears more than once in one command.",
                    nameof(changes));
            }
        }
    }

    private static void ValidateValue(
        CampaignSeasonChange change,
        CampaignSeasonOccurrence? value,
        string valueName,
        IReadOnlyList<CampaignSeasonChange> changes)
    {
        if (value is not { } occurrence)
        {
            return;
        }

        occurrence.EnsureValid();
        if (!string.Equals(occurrence.SeasonId, change.SeasonId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Campaign season change {valueName} value does not match season identity '{change.SeasonId}'.",
                nameof(changes));
        }
    }
}
