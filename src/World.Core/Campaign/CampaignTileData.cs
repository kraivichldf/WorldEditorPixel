namespace Kingdom.World.Core.Campaign;

public readonly record struct CampaignTileData(
    CampaignTileType Type,
    short HeightMeters,
    string? CustomTerrainId = null);
