using Avalonia.Media;
using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Editor.ViewModels;

public sealed record CampaignResourceOption(
    string Id,
    string Name,
    CampaignResourceCategory Category,
    IBrush SwatchBrush,
    bool IsCustom)
{
    public string CategoryText => Category.ToString();

    public string IdText => $"ID: {Id}";

    public override string ToString() => $"{Name} ({Id})";
}
