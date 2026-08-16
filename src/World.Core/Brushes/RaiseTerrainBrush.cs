using Kingdom.World.Core.Models;
using Kingdom.World.Core.Terrain;

namespace Kingdom.World.Core.Brushes;

public sealed class RaiseTerrainBrush : ITerrainBrush
{
    public TerrainBrushKind Kind => TerrainBrushKind.Raise;

    public IReadOnlyList<TerrainSampleChange> Apply(
        WorldTerrain terrain,
        TerrainCoordinate center,
        BrushSettings settings)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid();

        var region = TerrainBrushMath.GetAffectedRegion(terrain, center, settings.RadiusSamples);
        var changes = new List<TerrainSampleChange>();
        for (var y = region.MinY; y <= region.MaxY; y++)
        {
            for (var x = region.MinX; x <= region.MaxX; x++)
            {
                var weight = TerrainBrushMath.Weight(x, y, center, settings);
                var delta = settings.StrengthMeters * weight;
                if (delta < 0.5)
                {
                    continue;
                }

                var before = terrain.GetHeight(x, y);
                var after = TerrainBrushMath.ClampAndRound(terrain, before + delta);
                TerrainBrushMath.AddChange(terrain, changes, x, y, before, after);
            }
        }

        return changes;
    }
}
