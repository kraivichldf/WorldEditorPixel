using Avalonia.Media;
using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Editor.ViewModels;

public sealed record CampaignTileTypeOption(
    CampaignTileType Type,
    string Name,
    string Description,
    IBrush SwatchBrush,
    string? CustomTerrainId = null)
{
    public override string ToString() => Name;
}
