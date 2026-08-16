using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Editor.Services;

public sealed record CampaignEditorProjectLoadResult(
    CampaignWorld World,
    CampaignResourceMap ResourceMap,
    CampaignResourceGenerationSettings? ResourceGenerationSettings,
    bool WasConvertedFromLegacy,
    string SourceProjectDirectory,
    int NormalizedLegacyCoastalTileCount = 0);
