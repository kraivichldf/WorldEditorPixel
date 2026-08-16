namespace Kingdom.World.Core.Campaign.V3;

public readonly record struct CampaignTileDataV3(
    CampaignSurfaceType Surface,
    short HeightMeters);

public readonly record struct CampaignTileEntryV3(
    int X,
    int Y,
    CampaignTileDataV3 Data);
