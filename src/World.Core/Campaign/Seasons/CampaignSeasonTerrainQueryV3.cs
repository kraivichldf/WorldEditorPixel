using Kingdom.World.Core.Campaign.V3;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonTerrainQueryV3 : ICampaignSeasonTerrainQuery
{
    private readonly CampaignWorldV3 _world;

    public CampaignSeasonTerrainQueryV3(CampaignWorldV3 world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public CampaignWorldDefinition Definition => _world.Definition;

    public long Revision => _world.Revision;

    public CampaignSeasonTerrainSample GetSample(int x, int y)
    {
        var tile = _world.Tiles.GetTile(x, y);
        var terrainType = NormalizeTerrainType(tile.Surface);
        var waterFeatures = terrainType switch
        {
            CampaignTileType.Sea => CampaignSeasonWaterFeatures.Sea,
            CampaignTileType.Lake => CampaignSeasonWaterFeatures.Lake,
            _ => CampaignSeasonWaterFeatures.None,
        };

        if (_world.Rivers.TryGetRiver(x, y, out var river))
        {
            terrainType = river.Junction == RiverJunctionKind.Confluence
                ? CampaignTileType.RiverJunction
                : river.Size == RiverSize.Large
                    ? CampaignTileType.LargeRiver
                    : CampaignTileType.River;
            waterFeatures = CampaignSeasonWaterFeatures.River;
        }

        var sample = new CampaignSeasonTerrainSample(
            terrainType,
            CustomTerrainId: null,
            tile.HeightMeters,
            waterFeatures);
        sample.EnsureValid();
        return sample;
    }

    private static CampaignTileType NormalizeTerrainType(CampaignSurfaceType surface) => surface switch
    {
        CampaignSurfaceType.Unassigned => CampaignTileType.Unassigned,
        CampaignSurfaceType.Grassland => CampaignTileType.Plains,
        CampaignSurfaceType.Forest => CampaignTileType.Forest,
        CampaignSurfaceType.Desert => CampaignTileType.Desert,
        CampaignSurfaceType.Wetland => CampaignTileType.Plains,
        CampaignSurfaceType.Tundra => CampaignTileType.Steppe,
        CampaignSurfaceType.BarrenRock => CampaignTileType.Mountain,
        CampaignSurfaceType.Sea => CampaignTileType.Sea,
        CampaignSurfaceType.Lake => CampaignTileType.Lake,
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown version-3 surface."),
    };
}
