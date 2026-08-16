using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign;

public sealed class CampaignTileMap
{
    public const double AutomaticCoastWaterBandFraction = 0.10;

    private static readonly (int X, int Y, RiverConnections Connection)[] CardinalNeighbors =
    [
        (0, -1, RiverConnections.North),
        (1, 0, RiverConnections.East),
        (0, 1, RiverConnections.South),
        (-1, 0, RiverConnections.West),
    ];

    private readonly Dictionary<long, CampaignTileData> _tiles = [];
    private readonly HashSet<long> _riverTiles = [];
    private readonly Dictionary<string, CampaignCustomTerrainDefinition> _customTerrainDefinitions =
        new(StringComparer.Ordinal);

    public CampaignTileMap(
        CampaignWorldDefinition definition,
        IEnumerable<CampaignCustomTerrainDefinition>? customTerrainDefinitions = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CampaignWorldDefinition.EnsureValid(definition);
        ReplaceCustomTerrainDefinitions(customTerrainDefinitions, incrementRevision: false);
    }

    public CampaignWorldDefinition Definition { get; }

    public long Revision { get; private set; }

    public int MaterializedTileCount => _tiles.Count;

    public int RiverTileCount => _riverTiles.Count;

    public IReadOnlyList<CampaignCustomTerrainDefinition> CustomTerrainDefinitions => _customTerrainDefinitions.Values
        .OrderBy(static definition => definition.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(static definition => definition.Id, StringComparer.Ordinal)
        .ToArray();

    public CampaignTileData DefaultTile => new(
        CampaignTileType.Unassigned,
        Definition.DefaultTileHeightMeters);

    public bool IsValidCoordinate(int x, int y) =>
        (uint)x < (uint)Definition.TilesX && (uint)y < (uint)Definition.TilesY;

    public bool TryGetCustomTerrainDefinition(
        string? id,
        out CampaignCustomTerrainDefinition definition)
    {
        if (id is not null && _customTerrainDefinitions.TryGetValue(id, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    public int GetCustomTerrainUsageCount(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _tiles.Values.Count(data => string.Equals(
            data.CustomTerrainId,
            id,
            StringComparison.Ordinal));
    }

    public bool SetCustomTerrainDefinitions(
        IEnumerable<CampaignCustomTerrainDefinition>? customTerrainDefinitions) =>
        ReplaceCustomTerrainDefinitions(customTerrainDefinitions, incrementRevision: true);

    public CampaignTileData GetTile(int x, int y)
    {
        EnsureValidCoordinate(x, y);
        return GetTileUnchecked(x, y);
    }

    public bool SetTile(int x, int y, CampaignTileData data)
    {
        var previous = GetTile(x, y);
        if (!TrySetTile(x, y, data, out var failureReason))
        {
            throw new CampaignTileTopologyException(failureReason!);
        }

        return previous != data;
    }

    public bool TrySetTile(int x, int y, CampaignTileData data, out string? failureReason)
    {
        EnsureValidCoordinate(x, y);
        EnsureValidData(data);
        var key = GetKey(x, y);
        if (!ValidateSingleTileRiverTopology(x, y, data, out failureReason))
        {
            return false;
        }

        ApplyValidatedUpdate(key, data);
        return true;
    }

    public bool CanSetTile(int x, int y, CampaignTileData data, out string? failureReason)
    {
        EnsureValidCoordinate(x, y);
        EnsureValidData(data);
        return ValidateSingleTileRiverTopology(x, y, data, out failureReason);
    }

    public int SetTiles(IEnumerable<CampaignTileEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var updates = new Dictionary<long, CampaignTileData>();
        foreach (var entry in entries)
        {
            EnsureValidCoordinate(entry.X, entry.Y);
            EnsureValidData(entry.Data);
            var key = GetKey(entry.X, entry.Y);
            if (!updates.TryAdd(key, entry.Data))
            {
                throw new ArgumentException(
                    $"Campaign tile ({entry.X}, {entry.Y}) appears more than once in one update batch.",
                    nameof(entries));
            }
        }

        if (!ValidateRiverTopology(updates, out var failureReason))
        {
            throw new CampaignTileTopologyException(failureReason!);
        }

        return ApplyValidatedUpdates(updates);
    }

    public RiverConnections GetRiverConnections(int x, int y)
    {
        EnsureValidCoordinate(x, y);
        if (!GetTileUnchecked(x, y).Type.IsRiver())
        {
            return RiverConnections.None;
        }

        var connections = RiverConnections.None;
        foreach (var neighbor in CardinalNeighbors)
        {
            var neighborX = x + neighbor.X;
            var neighborY = y + neighbor.Y;
            if (IsValidCoordinate(neighborX, neighborY) &&
                GetTileUnchecked(neighborX, neighborY).Type.IsRiver())
            {
                connections |= neighbor.Connection;
            }
        }

        return connections;
    }

    public AutomaticCoastSurfaceMaterial GetAutomaticCoastSurfaceMaterial(
        int x,
        int y,
        double localX,
        double localY)
    {
        EnsureValidCoordinate(x, y);
        if (!double.IsFinite(localX) || !double.IsFinite(localY) ||
            localX < 0 || localX > 1 || localY < 0 || localY > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(localX),
                "Automatic-coast tile-local coordinates must be finite values from 0 through 1.");
        }

        if (GetTileUnchecked(x, y).Type.IsWater())
        {
            return AutomaticCoastSurfaceMaterial.Original;
        }

        var closestDistance = double.PositiveInfinity;
        var closestWaterType = CampaignTileType.Unassigned;
        foreach (var neighbor in CardinalNeighbors)
        {
            var edgeDistance = neighbor.Connection switch
            {
                RiverConnections.North => localY,
                RiverConnections.East => 1 - localX,
                RiverConnections.South => 1 - localY,
                RiverConnections.West => localX,
                _ => double.PositiveInfinity,
            };
            if (edgeDistance >= closestDistance || edgeDistance >= AutomaticCoastWaterBandFraction)
            {
                continue;
            }

            var neighborX = x + neighbor.X;
            var neighborY = y + neighbor.Y;
            if (!IsValidCoordinate(neighborX, neighborY))
            {
                continue;
            }

            var waterType = NormalizeWaterType(GetTileUnchecked(neighborX, neighborY).Type);
            if (waterType is not (CampaignTileType.Sea or CampaignTileType.Lake))
            {
                continue;
            }

            closestDistance = edgeDistance;
            closestWaterType = waterType;
        }

        if (closestDistance < AutomaticCoastWaterBandFraction)
        {
            return closestWaterType == CampaignTileType.Lake
                ? AutomaticCoastSurfaceMaterial.Lake
                : AutomaticCoastSurfaceMaterial.Sea;
        }

        return AutomaticCoastSurfaceMaterial.Original;
    }

    public IEnumerable<CampaignTileEntry> GetMaterializedTiles()
    {
        foreach (var (key, data) in _tiles)
        {
            yield return new CampaignTileEntry((int)(uint)key, (int)(key >> 32), data);
        }
    }

    public IEnumerable<CampaignTileEntry> GetRiverTiles()
    {
        foreach (var key in _riverTiles)
        {
            yield return new CampaignTileEntry(
                (int)(uint)key,
                (int)(key >> 32),
                _tiles[key]);
        }
    }

    public double GetDerivedHeight(double tileSpaceX, double tileSpaceY)
    {
        if (!double.IsFinite(tileSpaceX) || !double.IsFinite(tileSpaceY) ||
            tileSpaceX < 0 || tileSpaceY < 0 ||
            tileSpaceX > Definition.TilesX || tileSpaceY > Definition.TilesY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tileSpaceX),
                "Tile-space position must lie inside the world bounds.");
        }

        var centeredX = tileSpaceX - 0.5;
        var centeredY = tileSpaceY - 0.5;
        var floorX = Math.Floor(centeredX);
        var floorY = Math.Floor(centeredY);
        var fractionX = centeredX - floorX;
        var fractionY = centeredY - floorY;
        var x0 = Math.Clamp((int)floorX, 0, Definition.TilesX - 1);
        var y0 = Math.Clamp((int)floorY, 0, Definition.TilesY - 1);
        var x1 = Math.Clamp((int)floorX + 1, 0, Definition.TilesX - 1);
        var y1 = Math.Clamp((int)floorY + 1, 0, Definition.TilesY - 1);

        var top = Lerp(GetTileUnchecked(x0, y0).HeightMeters, GetTileUnchecked(x1, y0).HeightMeters, fractionX);
        var bottom = Lerp(GetTileUnchecked(x0, y1).HeightMeters, GetTileUnchecked(x1, y1).HeightMeters, fractionX);
        return Lerp(top, bottom, fractionY);
    }

    private bool ValidateRiverTopology(
        IReadOnlyDictionary<long, CampaignTileData> updates,
        out string? failureReason)
    {
        var affected = new HashSet<long>();
        foreach (var key in updates.Keys)
        {
            var x = (int)(uint)key;
            var y = (int)(key >> 32);
            affected.Add(key);
            foreach (var neighbor in CardinalNeighbors)
            {
                var neighborX = x + neighbor.X;
                var neighborY = y + neighbor.Y;
                if (IsValidCoordinate(neighborX, neighborY))
                {
                    affected.Add(GetKey(neighborX, neighborY));
                }
            }
        }

        foreach (var key in affected)
        {
            var x = (int)(uint)key;
            var y = (int)(key >> 32);
            if (!GetEffectiveTile(x, y, updates).Type.IsRiver())
            {
                continue;
            }

            var connectionCount = 0;
            foreach (var neighbor in CardinalNeighbors)
            {
                var neighborX = x + neighbor.X;
                var neighborY = y + neighbor.Y;
                if (IsValidCoordinate(neighborX, neighborY) &&
                    GetEffectiveTile(neighborX, neighborY, updates).Type.IsRiver())
                {
                    connectionCount++;
                }
            }

            var type = GetEffectiveTile(x, y, updates).Type;
            if (connectionCount > type.MaximumRiverExitCount())
            {
                failureReason = CreateRiverTopologyFailure(x, y, type, connectionCount);
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    private bool ValidateSingleTileRiverTopology(
        int x,
        int y,
        CampaignTileData data,
        out string? failureReason)
    {
        var previousType = GetTileUnchecked(x, y).Type;
        if (!data.Type.IsRiver())
        {
            failureReason = null;
            return true;
        }

        Span<long> riverNeighbors = stackalloc long[CardinalNeighbors.Length];
        var riverNeighborCount = 0;
        foreach (var neighbor in CardinalNeighbors)
        {
            var neighborX = x + neighbor.X;
            var neighborY = y + neighbor.Y;
            if (!IsValidCoordinate(neighborX, neighborY) ||
                !GetTileUnchecked(neighborX, neighborY).Type.IsRiver())
            {
                continue;
            }

            riverNeighbors[riverNeighborCount++] = GetKey(neighborX, neighborY);
        }

        if (riverNeighborCount > data.Type.MaximumRiverExitCount())
        {
            failureReason = CreateRiverTopologyFailure(x, y, data.Type, riverNeighborCount);
            return false;
        }

        if (previousType.IsRiver())
        {
            failureReason = null;
            return true;
        }

        for (var index = 0; index < riverNeighborCount; index++)
        {
            var key = riverNeighbors[index];
            var neighborX = (int)(uint)key;
            var neighborY = (int)(key >> 32);
            var existingConnections = 0;
            foreach (var direction in CardinalNeighbors)
            {
                var adjacentX = neighborX + direction.X;
                var adjacentY = neighborY + direction.Y;
                if (IsValidCoordinate(adjacentX, adjacentY) &&
                    GetTileUnchecked(adjacentX, adjacentY).Type.IsRiver())
                {
                    existingConnections++;
                }
            }

            var neighborType = GetTileUnchecked(neighborX, neighborY).Type;
            if (existingConnections >= neighborType.MaximumRiverExitCount())
            {
                failureReason = CreateRiverTopologyFailure(
                    neighborX,
                    neighborY,
                    neighborType,
                    existingConnections + 1);
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    private int ApplyValidatedUpdates(IReadOnlyDictionary<long, CampaignTileData> updates)
    {
        var changed = 0;
        foreach (var (key, data) in updates)
        {
            changed += ApplyValidatedUpdate(key, data) ? 1 : 0;
        }

        return changed;
    }

    private bool ApplyValidatedUpdate(long key, CampaignTileData data)
    {
        var previous = _tiles.GetValueOrDefault(key, DefaultTile);
        if (previous == data)
        {
            return false;
        }

        if (previous.Type.IsRiver())
        {
            _riverTiles.Remove(key);
        }

        if (data == DefaultTile)
        {
            _tiles.Remove(key);
        }
        else
        {
            _tiles[key] = data;
            if (data.Type.IsRiver())
            {
                _riverTiles.Add(key);
            }
        }

        Revision++;
        return true;
    }

    private CampaignTileData GetEffectiveTile(
        int x,
        int y,
        IReadOnlyDictionary<long, CampaignTileData> updates)
    {
        var key = GetKey(x, y);
        return updates.TryGetValue(key, out var update)
            ? update
            : _tiles.GetValueOrDefault(key, DefaultTile);
    }

    private CampaignTileData GetTileUnchecked(int x, int y) =>
        _tiles.GetValueOrDefault(GetKey(x, y), DefaultTile);

    private static CampaignTileType NormalizeWaterType(CampaignTileType type) =>
        type == CampaignTileType.Water ? CampaignTileType.Sea : type;

    private static string CreateRiverTopologyFailure(
        int x,
        int y,
        CampaignTileType type,
        int connectionCount) =>
        $"River tile ({x}, {y}) would have {connectionCount} exits. " +
        $"{(type.IsRiverJunction() ? "A River Junction allows three" : "A River segment allows two")} " +
        "north/east/south/west connections, so this crossing was blocked.";

    private void EnsureValidData(CampaignTileData data)
    {
        if (!Enum.IsDefined(data.Type))
        {
            throw new ArgumentOutOfRangeException(nameof(data), data.Type, "Unknown campaign tile type.");
        }

        if (data.Type == CampaignTileType.Coastal)
        {
            throw new ArgumentException(
                "Coastal is a legacy read-only value. Paint the original land type; its 10% water-facing edge is derived automatically.",
                nameof(data));
        }

        if (data.HeightMeters < Definition.MinimumHeightMeters ||
            data.HeightMeters > Definition.MaximumHeightMeters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                data.HeightMeters,
                $"Tile height must be between {Definition.MinimumHeightMeters} and {Definition.MaximumHeightMeters} metres.");
        }

        if (data.CustomTerrainId is not { } customTerrainId)
        {
            return;
        }

        if (!_customTerrainDefinitions.TryGetValue(customTerrainId, out var definition))
        {
            throw new ArgumentException(
                $"Campaign tile references unknown custom terrain '{customTerrainId}'.",
                nameof(data));
        }

        if (definition.BaseType != data.Type)
        {
            throw new ArgumentException(
                $"Custom terrain '{definition.Name}' requires base type {definition.BaseType}, not {data.Type}.",
                nameof(data));
        }
    }

    private bool ReplaceCustomTerrainDefinitions(
        IEnumerable<CampaignCustomTerrainDefinition>? customTerrainDefinitions,
        bool incrementRevision)
    {
        var definitions = CampaignCustomTerrainDefinition.ValidateAll(customTerrainDefinitions);
        var next = definitions.ToDictionary(static definition => definition.Id, StringComparer.Ordinal);
        foreach (var data in _tiles.Values)
        {
            if (data.CustomTerrainId is not { } customTerrainId)
            {
                continue;
            }

            if (!next.TryGetValue(customTerrainId, out var definition))
            {
                throw new InvalidOperationException(
                    $"Custom terrain '{customTerrainId}' is still used by {GetCustomTerrainUsageCount(customTerrainId):N0} tile(s). Repaint those tiles before removing the definition.");
            }

            if (definition.BaseType != data.Type)
            {
                throw new InvalidOperationException(
                    $"Custom terrain '{definition.Name}' is still used on {data.Type} tiles and cannot change its base terrain.");
            }
        }

        if (_customTerrainDefinitions.Count == next.Count &&
            next.All(pair => _customTerrainDefinitions.TryGetValue(pair.Key, out var current) && current == pair.Value))
        {
            return false;
        }

        _customTerrainDefinitions.Clear();
        foreach (var (id, definition) in next)
        {
            _customTerrainDefinitions.Add(id, definition);
        }

        if (incrementRevision)
        {
            Revision++;
        }

        return true;
    }

    private void EnsureValidCoordinate(int x, int y)
    {
        if (!IsValidCoordinate(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Campaign tile ({x}, {y}) is outside 0..{Definition.TilesX - 1}, 0..{Definition.TilesY - 1}.");
        }
    }

    private static long GetKey(int x, int y) => ((long)y << 32) | (uint)x;

    private static double Lerp(double left, double right, double amount) =>
        left + (right - left) * amount;
}
