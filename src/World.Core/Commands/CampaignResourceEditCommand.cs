using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Core.Commands;

public sealed class CampaignResourceEditCommand : IWorldCommand
{
    private readonly CampaignResourceMap _resources;
    private readonly CampaignResourceChange[] _changes;
    private readonly IReadOnlyList<CampaignResourceChange> _readOnlyChanges;

    public CampaignResourceEditCommand(
        CampaignResourceMap resources,
        string description,
        IEnumerable<CampaignResourceChange> changes)
    {
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(changes);

        var pending = changes.ToArray();
        ValidateChanges(resources, pending);

        Description = description;
        _changes = pending
            .Where(static change => change.Before != change.After)
            .OrderBy(static change => change.Y)
            .ThenBy(static change => change.X)
            .ThenBy(static change => change.ResourceId, StringComparer.Ordinal)
            .ToArray();
        _readOnlyChanges = Array.AsReadOnly(_changes);
    }

    public string Description { get; }

    public IReadOnlyList<CampaignResourceChange> Changes => _readOnlyChanges;

    public bool IsEmpty => _changes.Length == 0;

    public void Execute() => Apply(static change => change.After);

    public void Undo() => Apply(static change => change.Before);

    private void Apply(
        Func<CampaignResourceChange, CampaignResourceOccurrence?> selectValue)
    {
        _resources.Apply(_changes.Select(change =>
            ToMutation(change, selectValue(change))));
    }

    private static CampaignResourceMutation ToMutation(
        CampaignResourceChange change,
        CampaignResourceOccurrence? value) =>
        value is { } occurrence
            ? CampaignResourceMutation.Upsert(change.X, change.Y, occurrence)
            : CampaignResourceMutation.Remove(change.X, change.Y, change.ResourceId);

    private static void ValidateChanges(
        CampaignResourceMap resources,
        IReadOnlyList<CampaignResourceChange> changes)
    {
        var seen = new HashSet<ResourceIdentity>();
        foreach (var change in changes)
        {
            if (!resources.IsValidCoordinate(change.X, change.Y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(changes),
                    $"Campaign resource coordinate ({change.X}, {change.Y}) is outside the campaign grid.");
            }

            if (!CampaignResourceDefinition.IsValidIdentifier(change.ResourceId))
            {
                throw new ArgumentException(
                    $"Campaign resource change at ({change.X}, {change.Y}) has an invalid resource ID.",
                    nameof(changes));
            }

            if (!resources.Catalog.Contains(change.ResourceId))
            {
                throw new ArgumentException(
                    $"Campaign resource change references unknown resource '{change.ResourceId}'.",
                    nameof(changes));
            }

            ValidateValue(change, change.Before, "Before", changes);
            ValidateValue(change, change.After, "After", changes);

            if (!seen.Add(new ResourceIdentity(change.X, change.Y, change.ResourceId)))
            {
                throw new ArgumentException(
                    $"Resource '{change.ResourceId}' at ({change.X}, {change.Y}) appears more than once in one command.",
                    nameof(changes));
            }
        }
    }

    private static void ValidateValue(
        CampaignResourceChange change,
        CampaignResourceOccurrence? value,
        string valueName,
        IReadOnlyList<CampaignResourceChange> changes)
    {
        if (value is not { } occurrence)
        {
            return;
        }

        occurrence.EnsureValid();
        if (!string.Equals(occurrence.ResourceId, change.ResourceId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Campaign resource change {valueName} value does not match resource identity '{change.ResourceId}'.",
                nameof(changes));
        }
    }

    private readonly record struct ResourceIdentity(int X, int Y, string ResourceId);
}
