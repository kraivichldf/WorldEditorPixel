using Kingdom.World.Core.Campaign.V3;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

internal static class CampaignV3TestWorldFactory
{
    public static CampaignWorldV3 Create(
        int tilesX,
        int tilesY,
        short defaultHeight = 0,
        TerrainFormProfile? profile = null) =>
        new(
            CampaignWorldDefinition.Create(
                worldWidthMeters: tilesX * 5_000L,
                worldHeightMeters: tilesY * 5_000L,
                campaignTileSizeMeters: 5_000,
                seaLevelMeters: 0,
                minimumHeightMeters: -1_000,
                maximumHeightMeters: 6_000,
                defaultTileHeightMeters: defaultHeight),
            profile);

    public static void SetLand(
        CampaignWorldV3 world,
        int x,
        int y,
        short heightMeters,
        CampaignSurfaceType surface = CampaignSurfaceType.Grassland) =>
        world.SetTile(x, y, new CampaignTileDataV3(surface, heightMeters));
}
