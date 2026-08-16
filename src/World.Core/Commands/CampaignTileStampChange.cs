using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Core.Commands;

public readonly record struct CampaignTileStampChange(
    int X,
    int Y,
    CampaignTileData Before,
    CampaignTileData After);
