namespace Kingdom.World.Core.Campaign;

public readonly record struct CampaignTileAssignment(
    int X,
    int Y,
    CampaignTileType Type);
