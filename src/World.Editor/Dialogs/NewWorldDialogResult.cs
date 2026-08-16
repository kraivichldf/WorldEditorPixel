using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Editor.Dialogs;

public sealed record NewWorldDialogResult(
    CampaignWorld World,
    CampaignMapGenerationResult GenerationResult,
    CampaignResourceWorldRegenerationResult? ResourceRegenerationResult = null);
