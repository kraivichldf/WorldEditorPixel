using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonTerrainQueryV2 : ICampaignSeasonTerrainQuery
{
    private readonly CampaignWorld _world;

    public CampaignSeasonTerrainQueryV2(CampaignWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public CampaignWorldDefinition Definition => _world.Definition;

    public long Revision => _world.Revision;

    public CampaignSeasonTerrainSample GetSample(int x, int y)
    {
        var tile = _world.Tiles.GetTile(x, y);
        var terrainType = tile.Type == CampaignTileType.Water
            ? CampaignTileType.Sea
            : tile.Type;
        var sample = new CampaignSeasonTerrainSample(
            terrainType,
            tile.CustomTerrainId,
            tile.HeightMeters,
            GetWaterFeatures(terrainType));
        sample.EnsureValid();
        return sample;
    }

    private static CampaignSeasonWaterFeatures GetWaterFeatures(CampaignTileType type) => type switch
    {
        CampaignTileType.Sea => CampaignSeasonWaterFeatures.Sea,
        CampaignTileType.Lake => CampaignSeasonWaterFeatures.Lake,
        CampaignTileType.River or CampaignTileType.LargeRiver or CampaignTileType.RiverJunction =>
            CampaignSeasonWaterFeatures.River,
        _ => CampaignSeasonWaterFeatures.None,
    };
}
