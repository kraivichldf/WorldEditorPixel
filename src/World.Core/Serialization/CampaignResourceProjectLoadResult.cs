using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Core.Serialization;

public sealed record CampaignResourceProjectLoadResult(
    CampaignResourceMap ResourceMap,
    CampaignResourceGenerationSettings? GenerationSettings,
    string SourceProjectDirectory);
