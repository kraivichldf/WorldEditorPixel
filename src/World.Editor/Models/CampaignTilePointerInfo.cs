using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Editor.Models;

public readonly record struct CampaignTilePointerInfo(
    CampaignTileCoordinate Coordinate,
    double TileSpaceX,
    double TileSpaceY);
