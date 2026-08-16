using Kingdom.World.Core.Models;
using Kingdom.World.Core.Terrain;

namespace Kingdom.World.Core.Brushes;

public sealed class FlattenTerrainBrush : ITerrainBrush
{
    public TerrainBrushKind Kind => TerrainBrushKind.Flatten;

    public IReadOnlyList<TerrainSampleChange> Apply(
        WorldTerrain terrain,
        TerrainCoordinate center,
        BrushSettings settings)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid();

        var target = Math.Clamp(
            settings.TargetElevationMeters,
            terrain.Definition.MinimumElevationMeters,
            terrain.Definition.MaximumElevationMeters);
        var region = TerrainBrushMath.GetAffectedRegion(terrain, center, settings.RadiusSamples);
        var changes = new List<TerrainSampleChange>();
        for (var y = region.MinY; y <= region.MaxY; y++)
        {
            for (var x = region.MinX; x <= region.MaxX; x++)
            {
                var weight = TerrainBrushMath.Weight(x, y, center, settings);
                var maxStep = settings.StrengthMeters * weight;
                if (maxStep < 0.5)
                {
                    continue;
                }

                var before = terrain.GetHeight(x, y);
                var difference = target - before;
                var step = Math.Clamp((double)difference, -maxStep, maxStep);
                var after = TerrainBrushMath.ClampAndRound(terrain, before + step);
                TerrainBrushMath.AddChange(terrain, changes, x, y, before, after);
            }
        }

        return changes;
    }
}
