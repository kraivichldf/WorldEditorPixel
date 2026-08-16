namespace Kingdom.World.Core.Brushes;

public static class TerrainBrushFactory
{
    private static readonly ITerrainBrush Raise = new RaiseTerrainBrush();
    private static readonly ITerrainBrush Lower = new LowerTerrainBrush();
    private static readonly ITerrainBrush Smooth = new SmoothTerrainBrush();
    private static readonly ITerrainBrush Flatten = new FlattenTerrainBrush();

    public static ITerrainBrush Get(TerrainBrushKind kind) => kind switch
    {
        TerrainBrushKind.Raise => Raise,
        TerrainBrushKind.Lower => Lower,
        TerrainBrushKind.Smooth => Smooth,
        TerrainBrushKind.Flatten => Flatten,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown terrain brush kind."),
    };
}
