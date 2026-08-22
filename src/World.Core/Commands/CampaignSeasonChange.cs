using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Core.Commands;

public readonly record struct CampaignSeasonChange(
    int X,
    int Y,
    string SeasonId,
    CampaignSeasonOccurrence? Before,
    CampaignSeasonOccurrence? After);
