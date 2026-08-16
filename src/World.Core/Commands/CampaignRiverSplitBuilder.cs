using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Core.Commands;

public static class CampaignRiverSplitBuilder
{
    private readonly record struct SplitCell(int X, int Y, bool IsJunction);

    private static readonly (int X, int Y)[] CardinalOffsets =
    [
        (0, -1),
        (1, 0),
        (0, 1),
        (-1, 0),
    ];

    public static bool TryCreate(
        CampaignTileMap tiles,
        CampaignTileCoordinate root,
        int branchCount,
        RiverSplitDirection? requestedDirection,
        out CampaignTileStampCommand? command,
        out string? failureReason)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        command = null;
        if (!tiles.IsValidCoordinate(root.X, root.Y))
        {
            failureReason = $"Pinned tile ({root.X}, {root.Y}) is outside the world.";
            return false;
        }

        if (branchCount is < 2 or > 4)
        {
            failureReason = "River split branch count must be 2, 3, or 4.";
            return false;
        }

        var rootData = tiles.GetTile(root.X, root.Y);
        if (rootData.Type is not (CampaignTileType.River or CampaignTileType.LargeRiver))
        {
            failureReason = "Pin a normal River or Large River endpoint before creating a split.";
            return false;
        }

        var connections = tiles.GetRiverConnections(root.X, root.Y);
        var connectionCount = CountConnections(connections);
        if (connectionCount > 1)
        {
            failureReason = "The pinned river tile is not an endpoint. Pin a tile with zero or one river neighbour.";
            return false;
        }

        if (!TryResolveDirection(connections, requestedDirection, out var direction, out failureReason))
        {
            return false;
        }

        var template = GetTemplate(branchCount);
        var proposed = template
            .Select(cell =>
            {
                var rotated = Rotate(cell.X, cell.Y, direction);
                return new SplitCell(root.X + rotated.X, root.Y + rotated.Y, cell.IsJunction);
            })
            .ToArray();
        var proposedCoordinates = proposed
            .Select(static cell => new CampaignTileCoordinate(cell.X, cell.Y))
            .ToHashSet();

        foreach (var cell in proposed)
        {
            if (!tiles.IsValidCoordinate(cell.X, cell.Y))
            {
                failureReason =
                    $"The {branchCount}-branch split does not fit inside the world toward {direction}.";
                return false;
            }

            var current = tiles.GetTile(cell.X, cell.Y);
            if (current.Type.IsRiver())
            {
                failureReason =
                    $"The split would intersect an existing river at ({cell.X}, {cell.Y}). Choose another endpoint or direction.";
                return false;
            }

            if (current.Type is CampaignTileType.Water or CampaignTileType.Sea or CampaignTileType.Lake)
            {
                failureReason =
                    $"The split would replace water at ({cell.X}, {cell.Y}). Choose another endpoint or direction.";
                return false;
            }

            foreach (var offset in CardinalOffsets)
            {
                var neighbor = new CampaignTileCoordinate(cell.X + offset.X, cell.Y + offset.Y);
                if (!tiles.IsValidCoordinate(neighbor.X, neighbor.Y) ||
                    neighbor == root ||
                    proposedCoordinates.Contains(neighbor))
                {
                    continue;
                }

                if (tiles.GetTile(neighbor.X, neighbor.Y).Type.IsRiver())
                {
                    failureReason =
                        $"The split would touch another river beside ({cell.X}, {cell.Y}) and create an unintended merge or crossing.";
                    return false;
                }
            }
        }

        var changes = proposed
            .Select(cell =>
            {
                var before = tiles.GetTile(cell.X, cell.Y);
                var after = new CampaignTileData(
                    cell.IsJunction ? CampaignTileType.RiverJunction : rootData.Type,
                    rootData.HeightMeters);
                return new CampaignTileStampChange(cell.X, cell.Y, before, after);
            })
            .OrderBy(static change => change.Y)
            .ThenBy(static change => change.X)
            .ToArray();

        try
        {
            tiles.SetTiles(changes.Select(static change =>
                new CampaignTileEntry(change.X, change.Y, change.After)));
        }
        catch (CampaignTileTopologyException exception)
        {
            failureReason = exception.Message;
            return false;
        }

        command = new CampaignTileStampCommand(
            tiles,
            $"Split {rootData.Type} into {branchCount} branches toward {direction}",
            changes);
        failureReason = null;
        return true;
    }

    private static SplitCell[] GetTemplate(int branchCount) => branchCount switch
    {
        2 =>
        [
            new(0, -1, true),
            new(-1, -1, false),
            new(1, -1, false),
        ],
        3 =>
        [
            new(0, -1, true),
            new(1, -1, true),
            new(-1, -1, false),
            new(1, -2, false),
            new(2, -1, false),
        ],
        4 =>
        [
            new(0, -1, true),
            new(-1, -1, true),
            new(1, -1, true),
            new(-1, -2, false),
            new(-2, -1, false),
            new(1, -2, false),
            new(2, -1, false),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(branchCount)),
    };

    private static bool TryResolveDirection(
        RiverConnections connections,
        RiverSplitDirection? requested,
        out RiverSplitDirection direction,
        out string? failureReason)
    {
        if (connections == RiverConnections.None)
        {
            if (requested is null)
            {
                direction = default;
                failureReason = "This isolated river has no incoming side. Choose North, East, South, or West.";
                return false;
            }

            direction = requested.Value;
            failureReason = null;
            return true;
        }

        var suggested = connections switch
        {
            RiverConnections.North => RiverSplitDirection.South,
            RiverConnections.East => RiverSplitDirection.West,
            RiverConnections.South => RiverSplitDirection.North,
            RiverConnections.West => RiverSplitDirection.East,
            _ => throw new InvalidOperationException("A river endpoint must have exactly one incoming side."),
        };
        if (requested is not null && requested.Value != suggested)
        {
            direction = default;
            failureReason =
                $"The existing river enters from {connections}; continue the split toward {suggested}, or choose Auto.";
            return false;
        }

        direction = suggested;
        failureReason = null;
        return true;
    }

    private static (int X, int Y) Rotate(int x, int y, RiverSplitDirection direction) => direction switch
    {
        RiverSplitDirection.North => (x, y),
        RiverSplitDirection.East => (-y, x),
        RiverSplitDirection.South => (-x, -y),
        RiverSplitDirection.West => (y, -x),
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    private static int CountConnections(RiverConnections connections)
    {
        var value = (byte)connections;
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }
}
