using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Core.Commands;

public readonly record struct CampaignResourceChange(
    int X,
    int Y,
    string ResourceId,
    CampaignResourceOccurrence? Before,
    CampaignResourceOccurrence? After);
