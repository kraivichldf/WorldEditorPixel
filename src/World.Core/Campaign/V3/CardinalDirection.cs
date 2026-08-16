namespace Kingdom.World.Core.Campaign.V3;

public enum CardinalDirection
{
    North,
    East,
    South,
    West,
}

public static class CardinalDirectionRules
{
    public static int OffsetX(this CardinalDirection direction) => direction switch
    {
        CardinalDirection.East => 1,
        CardinalDirection.West => -1,
        CardinalDirection.North or CardinalDirection.South => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction."),
    };

    public static int OffsetY(this CardinalDirection direction) => direction switch
    {
        CardinalDirection.North => -1,
        CardinalDirection.South => 1,
        CardinalDirection.East or CardinalDirection.West => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction."),
    };

    public static CardinalDirection Opposite(this CardinalDirection direction) => direction switch
    {
        CardinalDirection.North => CardinalDirection.South,
        CardinalDirection.East => CardinalDirection.West,
        CardinalDirection.South => CardinalDirection.North,
        CardinalDirection.West => CardinalDirection.East,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown cardinal direction."),
    };
}
