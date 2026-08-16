namespace Kingdom.World.Core.Campaign;

public readonly record struct CampaignTileChange(
    int X,
    int Y,
    CampaignTileType Before,
    CampaignTileType After);
