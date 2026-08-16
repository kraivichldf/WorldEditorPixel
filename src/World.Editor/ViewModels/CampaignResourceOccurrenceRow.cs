using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Editor.ViewModels;

public sealed record CampaignResourceOccurrenceRow(
    string ResourceId,
    string Name,
    CampaignResourceCategory Category,
    byte Potential,
    bool IsLocked,
    bool HasHardWarnings,
    string HardWarningText,
    string UnevaluatedFactorsText)
{
    public string CategoryText => Category.ToString();

    public string PotentialText => $"{Potential:N0} / 100";

    public string LockText => IsLocked ? "Locked" : "Unlocked";

    public override string ToString() => $"{Name} · {PotentialText} · {LockText}";
}
