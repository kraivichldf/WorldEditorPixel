namespace Kingdom.World.Core.Campaign.V3;

public enum RiverOutflow
{
    Unresolved,
    North,
    East,
    South,
    West,
}

public enum RiverJunctionKind
{
    Segment,
    Confluence,
}

public enum RiverSize
{
    Regular,
    Large,
}

public readonly record struct RiverTileData(
    RiverOutflow Outflow,
    RiverJunctionKind Junction = RiverJunctionKind.Segment,
    RiverSize Size = RiverSize.Regular);

public readonly record struct RiverTileEntryV3(
    int X,
    int Y,
    RiverTileData Data);

internal static class RiverOutflowRules
{
    public static bool TryGetDirection(
        this RiverOutflow outflow,
        out CardinalDirection direction)
    {
        direction = outflow switch
        {
            RiverOutflow.North => CardinalDirection.North,
            RiverOutflow.East => CardinalDirection.East,
            RiverOutflow.South => CardinalDirection.South,
            RiverOutflow.West => CardinalDirection.West,
            _ => default,
        };
        return outflow != RiverOutflow.Unresolved && Enum.IsDefined(outflow);
    }
}
