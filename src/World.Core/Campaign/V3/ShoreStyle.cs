namespace Kingdom.World.Core.Campaign.V3;

public enum ShoreStyle
{
    Auto,
    Beach,
    Cliff,
}

public readonly record struct ShoreEdgeOverrideV3(
    int X,
    int Y,
    CardinalDirection Edge,
    ShoreStyle Style);
