using Kingdom.World.Core.Campaign.V3;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceTerrainQueryV2 : ICampaignResourceTerrainQuery
{
    private static readonly (int X, int Y)[] CardinalOffsets =
    [
        (0, -1),
        (1, 0),
        (0, 1),
        (-1, 0),
    ];

    private readonly CampaignWorld _world;
    private CampaignResourceDistanceField? _distanceField;
    private long _distanceFieldRevision = -1;

    public CampaignResourceTerrainQueryV2(CampaignWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public CampaignWorldDefinition Definition => _world.Definition;

    public long Revision => _world.Revision;

    public CampaignResourceTerrainSample GetSample(int x, int y)
    {
        var tile = _world.Tiles.GetTile(x, y);
        var surface = NormalizeSurface(tile.Type);
        var kind = GetKind(surface);
        var (form, maximumCardinalGrade) = CampaignResourceTerrainAnalysis.Analyze(
            Definition,
            (sampleX, sampleY) => _world.Tiles.GetTile(sampleX, sampleY).HeightMeters,
            x,
            y,
            TerrainFormProfile.Default);
        var distances = GetDistanceField().GetDistances(x, y);
        var sample = new CampaignResourceTerrainSample(
            kind,
            surface,
            form,
            tile.CustomTerrainId,
            tile.HeightMeters,
            maximumCardinalGrade,
            distances.Sea,
            distances.Lake,
            distances.River,
            NormalizeRiverFeatures(tile.Type),
            GetCoastFlags(x, y, tile.Type, kind));
        sample.EnsureValid();
        return sample;
    }

    private CampaignResourceDistanceField GetDistanceField()
    {
        var revision = Revision;
        if (_distanceField is not null && _distanceFieldRevision == revision)
        {
            return _distanceField;
        }

        _distanceField = new CampaignResourceDistanceField(
            Definition.TilesX,
            Definition.TilesY,
            Definition.CampaignTileSizeMeters,
            (x, y) => GetWaterSources(_world.Tiles.GetTile(x, y).Type));
        _distanceFieldRevision = revision;
        return _distanceField;
    }

    private CampaignResourceCoastFlags GetCoastFlags(
        int x,
        int y,
        CampaignTileType type,
        CampaignResourceTerrainKind kind)
    {
        var flags = CampaignResourceCoastFlags.None;
        var hasWaterFacingEdge = false;
        foreach (var (offsetX, offsetY) in CardinalOffsets)
        {
            var neighborX = x + offsetX;
            var neighborY = y + offsetY;
            if (!_world.Tiles.IsValidCoordinate(neighborX, neighborY))
            {
                continue;
            }

            var neighborSurface = NormalizeSurface(_world.Tiles.GetTile(neighborX, neighborY).Type);
            if (kind == CampaignResourceTerrainKind.Land)
            {
                if (neighborSurface == CampaignResourceSurfaceType.Sea)
                {
                    flags |= CampaignResourceCoastFlags.AdjacentSea;
                    hasWaterFacingEdge = true;
                }
                else if (neighborSurface == CampaignResourceSurfaceType.Lake)
                {
                    flags |= CampaignResourceCoastFlags.AdjacentLake;
                    hasWaterFacingEdge = true;
                }
            }
            else if (kind == CampaignResourceTerrainKind.Water &&
                     GetKind(neighborSurface) == CampaignResourceTerrainKind.Land)
            {
                flags |= CampaignResourceCoastFlags.CoastalWater;
            }
        }

        if (hasWaterFacingEdge && type == CampaignTileType.Beach)
        {
            flags |= CampaignResourceCoastFlags.BeachShore;
        }
        else if (hasWaterFacingEdge && type == CampaignTileType.Cliff)
        {
            flags |= CampaignResourceCoastFlags.CliffShore;
        }

        return flags;
    }

    private static CampaignResourceSurfaceType NormalizeSurface(CampaignTileType type) => type switch
    {
        CampaignTileType.Unassigned => CampaignResourceSurfaceType.Unassigned,
        CampaignTileType.Water or CampaignTileType.Sea => CampaignResourceSurfaceType.Sea,
        CampaignTileType.Lake => CampaignResourceSurfaceType.Lake,
        CampaignTileType.Forest => CampaignResourceSurfaceType.Forest,
        CampaignTileType.Desert => CampaignResourceSurfaceType.Desert,
        CampaignTileType.Mountain or CampaignTileType.Cliff => CampaignResourceSurfaceType.BarrenRock,
        CampaignTileType.Plains or
        CampaignTileType.Steppe or
        CampaignTileType.Hills or
        CampaignTileType.River or
        CampaignTileType.LargeRiver or
        CampaignTileType.RiverJunction or
        CampaignTileType.Beach or
        CampaignTileType.Coastal => CampaignResourceSurfaceType.Grassland,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown version-2 campaign tile type."),
    };

    private static CampaignResourceTerrainKind GetKind(CampaignResourceSurfaceType surface) => surface switch
    {
        CampaignResourceSurfaceType.Unassigned => CampaignResourceTerrainKind.Unassigned,
        CampaignResourceSurfaceType.Sea or CampaignResourceSurfaceType.Lake =>
            CampaignResourceTerrainKind.Water,
        _ => CampaignResourceTerrainKind.Land,
    };

    private static CampaignResourceRiverFeatures NormalizeRiverFeatures(CampaignTileType type) => type switch
    {
        CampaignTileType.River => CampaignResourceRiverFeatures.Present,
        CampaignTileType.LargeRiver =>
            CampaignResourceRiverFeatures.Present | CampaignResourceRiverFeatures.Large,
        CampaignTileType.RiverJunction =>
            CampaignResourceRiverFeatures.Present | CampaignResourceRiverFeatures.Junction,
        _ => CampaignResourceRiverFeatures.None,
    };

    private static CampaignResourceWaterSources GetWaterSources(CampaignTileType type) => type switch
    {
        CampaignTileType.Water or CampaignTileType.Sea => CampaignResourceWaterSources.Sea,
        CampaignTileType.Lake => CampaignResourceWaterSources.Lake,
        CampaignTileType.River or CampaignTileType.LargeRiver or CampaignTileType.RiverJunction =>
            CampaignResourceWaterSources.River,
        _ => CampaignResourceWaterSources.None,
    };
}
