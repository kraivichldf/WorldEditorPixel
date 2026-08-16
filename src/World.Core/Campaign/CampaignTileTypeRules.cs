namespace Kingdom.World.Core.Campaign;

public static class CampaignTileTypeRules
{
    public static bool IsWater(this CampaignTileType type) =>
        type is CampaignTileType.Water or CampaignTileType.Sea or CampaignTileType.Lake;

    public static bool IsRiver(this CampaignTileType type) =>
        type is CampaignTileType.River or CampaignTileType.LargeRiver or CampaignTileType.RiverJunction;

    public static bool IsRiverJunction(this CampaignTileType type) =>
        type == CampaignTileType.RiverJunction;

    public static int MaximumRiverExitCount(this CampaignTileType type) =>
        type.IsRiverJunction() ? 3 : type.IsRiver() ? 2 : 0;
}
