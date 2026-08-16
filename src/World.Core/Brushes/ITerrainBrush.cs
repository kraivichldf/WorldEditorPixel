using Kingdom.World.Core.Models;
using Kingdom.World.Core.Terrain;

namespace Kingdom.World.Core.Brushes;

public interface ITerrainBrush
{
    TerrainBrushKind Kind { get; }

    IReadOnlyList<TerrainSampleChange> Apply(
        WorldTerrain terrain,
        TerrainCoordinate center,
        BrushSettings settings);
}
