namespace Kingdom.World.Core.Campaign.V3;

public enum CampaignSurfaceType
{
    Unassigned,
    Grassland,
    Forest,
    Desert,
    Wetland,
    Tundra,
    BarrenRock,
    Sea,
    Lake,
}

public static class CampaignSurfaceTypeRules
{
    public static bool IsWater(this CampaignSurfaceType surface) =>
        surface is CampaignSurfaceType.Sea or CampaignSurfaceType.Lake;

    public static bool IsLand(this CampaignSurfaceType surface) =>
        surface is CampaignSurfaceType.Grassland
            or CampaignSurfaceType.Forest
            or CampaignSurfaceType.Desert
            or CampaignSurfaceType.Wetland
            or CampaignSurfaceType.Tundra
            or CampaignSurfaceType.BarrenRock;
}
