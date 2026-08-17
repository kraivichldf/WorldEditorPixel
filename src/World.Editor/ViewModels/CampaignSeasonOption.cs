using Avalonia.Media;
using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Editor.ViewModels;

public sealed record CampaignSeasonOption(
    string Id,
    string Name,
    CampaignBuiltInSeason Fallback,
    IBrush SwatchBrush,
    bool IsCustom,
    bool IsGenerationEnabled)
{
    public string IdText => $"ID: {Id}";

    public string SourceText => IsCustom ? "Custom" : "Built-in";

    public string GenerationText => IsGenerationEnabled ? "Generated" : "Manual only";

    public override string ToString() => $"{Name} ({Id})";
}
