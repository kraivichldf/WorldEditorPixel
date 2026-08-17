using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Editor.Dialogs;

public sealed record NewWorldDialogResult(
    CampaignWorld World,
    CampaignMapGenerationResult GenerationResult,
    CampaignSeasonMap SeasonMap,
    IReadOnlyList<string> SeasonPriorityIds,
    CampaignSeasonSavedGeneration? SeasonSavedGeneration,
    CampaignSeasonSupportFields? SeasonSupportFields,
    CampaignResourceWorldRegenerationResult? ResourceRegenerationResult = null,
    CampaignSeasonWorldRegenerationResult? SeasonRegenerationResult = null,
    CampaignSeasonNewWorldGenerationResult? NewSeasonGenerationResult = null);
