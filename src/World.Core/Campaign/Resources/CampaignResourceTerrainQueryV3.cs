using Kingdom.World.Core.Campaign.V3;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceTerrainQueryV3 : ICampaignResourceTerrainQuery
{
    private static readonly CardinalDirection[] CardinalDirections =
    [
        CardinalDirection.North,
        CardinalDirection.East,
        CardinalDirection.South,
        CardinalDirection.West,
    ];

    private readonly CampaignWorldV3 _world;
    private CampaignResourceDistanceField? _distanceField;
    private long _distanceFieldTileRevision = -1;
    private long _distanceFieldRiverRevision = -1;

    public CampaignResourceTerrainQueryV3(CampaignWorldV3 world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public CampaignWorldDefinition Definition => _world.Definition;

    public long Revision => _world.Revision;

    public CampaignResourceTerrainSample GetSample(int x, int y)
    {
        var tile = _world.Tiles.GetTile(x, y);
        var surface = NormalizeSurface(tile.Surface);
        var analysis = _world.AnalyzeTerrainForm(x, y);
        var distances = GetDistanceField().GetDistances(x, y);
        var sample = new CampaignResourceTerrainSample(
            GetKind(surface),
            surface,
            CampaignResourceTerrainAnalysis.Normalize(analysis.Form),
            CustomTerrainId: null,
            tile.HeightMeters,
            analysis.MaximumCardinalGrade,
            distances.Sea,
            distances.Lake,
            distances.River,
            GetRiverFeatures(x, y),
            GetCoastFlags(x, y, tile.Surface));
        sample.EnsureValid();
        return sample;
    }

    private CampaignResourceDistanceField GetDistanceField()
    {
        var tileRevision = _world.Tiles.Revision;
        var riverRevision = _world.Rivers.Revision;
        if (_distanceField is not null &&
            _distanceFieldTileRevision == tileRevision &&
            _distanceFieldRiverRevision == riverRevision)
        {
            return _distanceField;
        }

        _distanceField = new CampaignResourceDistanceField(
            Definition.TilesX,
            Definition.TilesY,
            Definition.CampaignTileSizeMeters,
            (x, y) => GetWaterSources(x, y));
        _distanceFieldTileRevision = tileRevision;
        _distanceFieldRiverRevision = riverRevision;
        return _distanceField;
    }

    private CampaignResourceWaterSources GetWaterSources(int x, int y)
    {
        var sources = _world.Tiles.GetTile(x, y).Surface switch
        {
            CampaignSurfaceType.Sea => CampaignResourceWaterSources.Sea,
            CampaignSurfaceType.Lake => CampaignResourceWaterSources.Lake,
            _ => CampaignResourceWaterSources.None,
        };
        if (_world.Rivers.HasRiver(x, y))
        {
            sources |= CampaignResourceWaterSources.River;
        }

        return sources;
    }

    private CampaignResourceRiverFeatures GetRiverFeatures(int x, int y)
    {
        if (!_world.Rivers.TryGetRiver(x, y, out var river))
        {
            return CampaignResourceRiverFeatures.None;
        }

        var features = CampaignResourceRiverFeatures.Present;
        if (river.Size == RiverSize.Large)
        {
            features |= CampaignResourceRiverFeatures.Large;
        }

        if (river.Junction == RiverJunctionKind.Confluence)
        {
            features |= CampaignResourceRiverFeatures.Junction;
        }

        return features;
    }

    private CampaignResourceCoastFlags GetCoastFlags(
        int x,
        int y,
        CampaignSurfaceType surface)
    {
        var flags = CampaignResourceCoastFlags.None;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.OffsetX();
            var neighborY = y + direction.OffsetY();
            if (!_world.Tiles.IsValidCoordinate(neighborX, neighborY))
            {
                continue;
            }

            var neighborSurface = _world.Tiles.GetTile(neighborX, neighborY).Surface;
            if (surface.IsLand() && neighborSurface.IsWater())
            {
                flags |= neighborSurface == CampaignSurfaceType.Sea
                    ? CampaignResourceCoastFlags.AdjacentSea
                    : CampaignResourceCoastFlags.AdjacentLake;
                flags |= _world.GetEffectiveShoreStyle(x, y, direction) switch
                {
                    ShoreStyle.Beach => CampaignResourceCoastFlags.BeachShore,
                    ShoreStyle.Cliff => CampaignResourceCoastFlags.CliffShore,
                    _ => CampaignResourceCoastFlags.None,
                };
            }
            else if (surface.IsWater() && neighborSurface.IsLand())
            {
                flags |= CampaignResourceCoastFlags.CoastalWater;
            }
        }

        return flags;
    }

    private static CampaignResourceSurfaceType NormalizeSurface(CampaignSurfaceType surface) => surface switch
    {
        CampaignSurfaceType.Unassigned => CampaignResourceSurfaceType.Unassigned,
        CampaignSurfaceType.Grassland => CampaignResourceSurfaceType.Grassland,
        CampaignSurfaceType.Forest => CampaignResourceSurfaceType.Forest,
        CampaignSurfaceType.Desert => CampaignResourceSurfaceType.Desert,
        CampaignSurfaceType.Wetland => CampaignResourceSurfaceType.Wetland,
        CampaignSurfaceType.Tundra => CampaignResourceSurfaceType.Tundra,
        CampaignSurfaceType.BarrenRock => CampaignResourceSurfaceType.BarrenRock,
        CampaignSurfaceType.Sea => CampaignResourceSurfaceType.Sea,
        CampaignSurfaceType.Lake => CampaignResourceSurfaceType.Lake,
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown version-3 surface."),
    };

    private static CampaignResourceTerrainKind GetKind(CampaignResourceSurfaceType surface) => surface switch
    {
        CampaignResourceSurfaceType.Unassigned => CampaignResourceTerrainKind.Unassigned,
        CampaignResourceSurfaceType.Sea or CampaignResourceSurfaceType.Lake =>
            CampaignResourceTerrainKind.Water,
        _ => CampaignResourceTerrainKind.Land,
    };
}
