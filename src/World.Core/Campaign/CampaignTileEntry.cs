namespace Kingdom.World.Core.Campaign;

public readonly record struct CampaignTileEntry(
    int X,
    int Y,
    CampaignTileData Data);
