namespace Kingdom.World.Core.Campaign.V3;

public sealed class RiverNetworkV3
{
    private static readonly CardinalDirection[] CardinalDirections =
    [
        CardinalDirection.North,
        CardinalDirection.East,
        CardinalDirection.South,
        CardinalDirection.West,
    ];

    private readonly CampaignTileMapV3 _tiles;
    private readonly Dictionary<long, RiverTileData> _rivers = [];

    internal RiverNetworkV3(CampaignTileMapV3 tiles)
    {
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
    }

    public long Revision { get; private set; }

    public int RiverTileCount => _rivers.Count;

    public bool HasRiver(int x, int y)
    {
        _tiles.EnsureValidCoordinate(x, y);
        return _rivers.ContainsKey(CampaignTileMapV3.GetKey(x, y));
    }

    public bool TryGetRiver(int x, int y, out RiverTileData data)
    {
        _tiles.EnsureValidCoordinate(x, y);
        return _rivers.TryGetValue(CampaignTileMapV3.GetKey(x, y), out data);
    }

    public RiverTileData GetRiver(int x, int y)
    {
        if (!TryGetRiver(x, y, out var data))
        {
            throw new InvalidOperationException($"Campaign tile ({x}, {y}) has no River overlay.");
        }

        return data;
    }

    public IEnumerable<RiverTileEntryV3> GetRivers()
    {
        foreach (var (key, data) in _rivers)
        {
            var (x, y) = CampaignTileMapV3.GetCoordinate(key);
            yield return new RiverTileEntryV3(x, y, data);
        }
    }

    public IReadOnlyList<string> Validate(bool requireResolvedOutflows = true)
    {
        var errors = new List<string>();
        var riverKeys = _rivers.Keys.ToHashSet();

        foreach (var (key, data) in _rivers)
        {
            var (x, y) = CampaignTileMapV3.GetCoordinate(key);
            var surface = _tiles.GetTileUnchecked(x, y).Surface;
            if (!surface.IsLand())
            {
                errors.Add(
                    $"River overlay at ({x}, {y}) requires a land surface; found {surface}.");
            }

            if (!Enum.IsDefined(data.Junction))
            {
                errors.Add($"River overlay at ({x}, {y}) has unknown junction kind {data.Junction}.");
            }

            if (!Enum.IsDefined(data.Size))
            {
                errors.Add($"River overlay at ({x}, {y}) has unknown size {data.Size}.");
            }

            if (!Enum.IsDefined(data.Outflow))
            {
                errors.Add($"River overlay at ({x}, {y}) has unknown outflow {data.Outflow}.");
                continue;
            }

            var neighbors = GetRiverNeighbors(x, y, riverKeys);
            if (neighbors.Count == 4)
            {
                errors.Add(
                    $"River overlay at ({x}, {y}) forms a forbidden four-way crossing.");
            }

            if (data.Junction == RiverJunctionKind.Segment && neighbors.Count > 2)
            {
                errors.Add(
                    $"River Segment at ({x}, {y}) has {neighbors.Count} River neighbors; the maximum is 2.");
            }
            else if (data.Junction == RiverJunctionKind.Confluence && neighbors.Count != 3)
            {
                errors.Add(
                    $"River Confluence at ({x}, {y}) must have exactly 3 River neighbors; " +
                    $"found {neighbors.Count}.");
            }

            if (data.Outflow == RiverOutflow.Unresolved)
            {
                if (requireResolvedOutflows)
                {
                    errors.Add($"River overlay at ({x}, {y}) has an unresolved outflow.");
                }

                continue;
            }

            ValidateResolvedOutflow(x, y, data.Outflow, riverKeys, errors);
        }

        ValidateRiverAdjacencyOrientations(riverKeys, requireResolvedOutflows, errors);
        ValidateConfluences(riverKeys, requireResolvedOutflows, errors);
        ValidateNoDirectedCycles(riverKeys, errors);
        return errors;
    }

    internal bool SetRiver(int x, int y, RiverTileData data)
    {
        _tiles.EnsureValidCoordinate(x, y);
        EnsureValidData(data);
        var surface = _tiles.GetTileUnchecked(x, y).Surface;
        if (!surface.IsLand())
        {
            throw new InvalidOperationException(
                $"River overlay at ({x}, {y}) requires a land surface; found {surface}.");
        }

        var key = CampaignTileMapV3.GetKey(x, y);
        if (_rivers.TryGetValue(key, out var previous) && previous == data)
        {
            return false;
        }

        _rivers[key] = data;
        Revision++;
        return true;
    }

    internal int SetRivers(IEnumerable<RiverTileEntryV3> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var updates = new Dictionary<long, RiverTileData>();
        foreach (var entry in entries)
        {
            _tiles.EnsureValidCoordinate(entry.X, entry.Y);
            EnsureValidData(entry.Data);
            var surface = _tiles.GetTileUnchecked(entry.X, entry.Y).Surface;
            if (!surface.IsLand())
            {
                throw new InvalidOperationException(
                    $"River overlay at ({entry.X}, {entry.Y}) requires a land surface; found {surface}.");
            }

            if (!updates.TryAdd(CampaignTileMapV3.GetKey(entry.X, entry.Y), entry.Data))
            {
                throw new ArgumentException(
                    $"River tile ({entry.X}, {entry.Y}) appears more than once in one update batch.",
                    nameof(entries));
            }
        }

        var changed = 0;
        foreach (var (key, data) in updates)
        {
            if (_rivers.TryGetValue(key, out var previous) && previous == data)
            {
                continue;
            }

            _rivers[key] = data;
            Revision++;
            changed++;
        }

        return changed;
    }

    internal bool RemoveRiver(int x, int y)
    {
        _tiles.EnsureValidCoordinate(x, y);
        if (!_rivers.Remove(CampaignTileMapV3.GetKey(x, y)))
        {
            return false;
        }

        Revision++;
        return true;
    }

    private void ValidateResolvedOutflow(
        int x,
        int y,
        RiverOutflow outflow,
        IReadOnlySet<long> riverKeys,
        ICollection<string> errors)
    {
        if (!outflow.TryGetDirection(out var direction))
        {
            return;
        }

        var targetX = x + direction.OffsetX();
        var targetY = y + direction.OffsetY();
        if (!_tiles.IsValidCoordinate(targetX, targetY))
        {
            errors.Add(
                $"River outflow from ({x}, {y}) points {direction} outside the world.");
            return;
        }

        var targetKey = CampaignTileMapV3.GetKey(targetX, targetY);
        if (riverKeys.Contains(targetKey))
        {
            var currentHeight = _tiles.GetTileUnchecked(x, y).HeightMeters;
            var targetHeight = _tiles.GetTileUnchecked(targetX, targetY).HeightMeters;
            if (targetHeight > currentHeight)
            {
                errors.Add(
                    $"River outflow from ({x}, {y}) at {currentHeight} m climbs uphill to " +
                    $"({targetX}, {targetY}) at {targetHeight} m.");
            }

            return;
        }

        var targetSurface = _tiles.GetTileUnchecked(targetX, targetY).Surface;
        if (!targetSurface.IsWater())
        {
            errors.Add(
                $"River outflow from ({x}, {y}) must enter an adjacent River, Sea, or Lake; " +
                $"found {targetSurface} at ({targetX}, {targetY}).");
        }
    }

    private void ValidateRiverAdjacencyOrientations(
        IReadOnlySet<long> riverKeys,
        bool requireResolvedOutflows,
        ICollection<string> errors)
    {
        foreach (var key in riverKeys)
        {
            var (x, y) = CampaignTileMapV3.GetCoordinate(key);
            ValidateOrientedPair(x, y, x + 1, y, riverKeys, requireResolvedOutflows, errors);
            ValidateOrientedPair(x, y, x, y + 1, riverKeys, requireResolvedOutflows, errors);
        }
    }

    private void ValidateOrientedPair(
        int firstX,
        int firstY,
        int secondX,
        int secondY,
        IReadOnlySet<long> riverKeys,
        bool requireResolvedOutflows,
        ICollection<string> errors)
    {
        if (!_tiles.IsValidCoordinate(secondX, secondY) ||
            !riverKeys.Contains(CampaignTileMapV3.GetKey(secondX, secondY)))
        {
            return;
        }

        var first = _rivers[CampaignTileMapV3.GetKey(firstX, firstY)];
        var second = _rivers[CampaignTileMapV3.GetKey(secondX, secondY)];
        if (first.Outflow == RiverOutflow.Unresolved || second.Outflow == RiverOutflow.Unresolved)
        {
            if (!requireResolvedOutflows)
            {
                return;
            }

            return;
        }

        var firstFlowsToSecond = FlowsTo(firstX, firstY, first.Outflow, secondX, secondY);
        var secondFlowsToFirst = FlowsTo(secondX, secondY, second.Outflow, firstX, firstY);
        if (firstFlowsToSecond == secondFlowsToFirst)
        {
            var reason = firstFlowsToSecond
                ? "both tiles flow toward each other"
                : "neither tile flows across their shared edge";
            errors.Add(
                $"Adjacent River tiles ({firstX}, {firstY}) and ({secondX}, {secondY}) " +
                $"are invalid because {reason}.");
        }
    }

    private void ValidateConfluences(
        IReadOnlySet<long> riverKeys,
        bool requireResolvedOutflows,
        ICollection<string> errors)
    {
        foreach (var (key, data) in _rivers)
        {
            if (data.Junction != RiverJunctionKind.Confluence)
            {
                continue;
            }

            var (x, y) = CampaignTileMapV3.GetCoordinate(key);
            var neighbors = GetRiverNeighbors(x, y, riverKeys);
            if (neighbors.Count != 3)
            {
                continue;
            }

            if (data.Outflow == RiverOutflow.Unresolved ||
                neighbors.Any(neighbor =>
                    _rivers[CampaignTileMapV3.GetKey(neighbor.X, neighbor.Y)].Outflow ==
                    RiverOutflow.Unresolved))
            {
                if (!requireResolvedOutflows)
                {
                    continue;
                }

                continue;
            }

            var incomingCount = neighbors.Count(neighbor =>
                FlowsTo(
                    neighbor.X,
                    neighbor.Y,
                    _rivers[CampaignTileMapV3.GetKey(neighbor.X, neighbor.Y)].Outflow,
                    x,
                    y));
            var outgoingToRiverCount = neighbors.Count(neighbor =>
                FlowsTo(x, y, data.Outflow, neighbor.X, neighbor.Y));
            if (incomingCount != 2 || outgoingToRiverCount != 1)
            {
                errors.Add(
                    $"River Confluence at ({x}, {y}) requires exactly 2 incoming River neighbors " +
                    $"and 1 River outflow; found {incomingCount} incoming and " +
                    $"{outgoingToRiverCount} River outflows.");
            }
        }
    }

    private void ValidateNoDirectedCycles(
        IReadOnlySet<long> riverKeys,
        ICollection<string> errors)
    {
        var completed = new HashSet<long>();
        foreach (var start in riverKeys)
        {
            if (completed.Contains(start))
            {
                continue;
            }

            var path = new List<long>();
            var pathIndexes = new Dictionary<long, int>();
            var current = start;
            while (!completed.Contains(current) && riverKeys.Contains(current))
            {
                if (pathIndexes.TryGetValue(current, out var cycleStartIndex))
                {
                    var (cycleX, cycleY) = CampaignTileMapV3.GetCoordinate(path[cycleStartIndex]);
                    errors.Add(
                        $"River network contains a directed cycle through ({cycleX}, {cycleY}).");
                    break;
                }

                pathIndexes[current] = path.Count;
                path.Add(current);
                var data = _rivers[current];
                if (!TryGetRiverTarget(current, data.Outflow, riverKeys, out var target))
                {
                    break;
                }

                current = target;
            }

            foreach (var key in path)
            {
                completed.Add(key);
            }
        }
    }

    private bool TryGetRiverTarget(
        long sourceKey,
        RiverOutflow outflow,
        IReadOnlySet<long> riverKeys,
        out long targetKey)
    {
        targetKey = default;
        if (!outflow.TryGetDirection(out var direction))
        {
            return false;
        }

        var (x, y) = CampaignTileMapV3.GetCoordinate(sourceKey);
        var targetX = x + direction.OffsetX();
        var targetY = y + direction.OffsetY();
        if (!_tiles.IsValidCoordinate(targetX, targetY))
        {
            return false;
        }

        targetKey = CampaignTileMapV3.GetKey(targetX, targetY);
        return riverKeys.Contains(targetKey);
    }

    private List<(int X, int Y)> GetRiverNeighbors(
        int x,
        int y,
        IReadOnlySet<long> riverKeys)
    {
        var neighbors = new List<(int X, int Y)>(4);
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.OffsetX();
            var neighborY = y + direction.OffsetY();
            if (_tiles.IsValidCoordinate(neighborX, neighborY) &&
                riverKeys.Contains(CampaignTileMapV3.GetKey(neighborX, neighborY)))
            {
                neighbors.Add((neighborX, neighborY));
            }
        }

        return neighbors;
    }

    private static bool FlowsTo(
        int sourceX,
        int sourceY,
        RiverOutflow outflow,
        int targetX,
        int targetY) =>
        outflow.TryGetDirection(out var direction) &&
        sourceX + direction.OffsetX() == targetX &&
        sourceY + direction.OffsetY() == targetY;

    private static void EnsureValidData(RiverTileData data)
    {
        if (!Enum.IsDefined(data.Outflow))
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                data.Outflow,
                "Unknown River outflow.");
        }

        if (!Enum.IsDefined(data.Junction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                data.Junction,
                "Unknown River junction kind.");
        }

        if (!Enum.IsDefined(data.Size))
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                data.Size,
                "Unknown River size.");
        }
    }
}
