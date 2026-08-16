using Kingdom.World.Core.Models;
using Kingdom.World.Core.Terrain;

namespace Kingdom.World.Core.Brushes;

public sealed class SmoothTerrainBrush : ITerrainBrush
{
    public TerrainBrushKind Kind => TerrainBrushKind.Smooth;

    public IReadOnlyList<TerrainSampleChange> Apply(
        WorldTerrain terrain,
        TerrainCoordinate center,
        BrushSettings settings)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid();

        var affected = TerrainBrushMath.GetAffectedRegion(terrain, center, settings.RadiusSamples);
        var source = new TerrainRegion(
            Math.Max(0, affected.MinX - 1),
            Math.Max(0, affected.MinY - 1),
            Math.Min(terrain.Definition.HeightSamplesX - 1, affected.MaxX + 1),
            Math.Min(terrain.Definition.HeightSamplesY - 1, affected.MaxY + 1));
        var snapshot = new short[checked(source.Width * source.Height)];

        for (var y = source.MinY; y <= source.MaxY; y++)
        {
            for (var x = source.MinX; x <= source.MaxX; x++)
            {
                snapshot[(y - source.MinY) * source.Width + x - source.MinX] = terrain.GetHeight(x, y);
            }
        }

        short ReadSnapshot(int x, int y) =>
            snapshot[(y - source.MinY) * source.Width + x - source.MinX];

        var changes = new List<TerrainSampleChange>();
        for (var y = affected.MinY; y <= affected.MaxY; y++)
        {
            for (var x = affected.MinX; x <= affected.MaxX; x++)
            {
                var weight = TerrainBrushMath.Weight(x, y, center, settings);
                var maxStep = settings.StrengthMeters * weight;
                if (maxStep < 0.5)
                {
                    continue;
                }

                long total = 0;
                var count = 0;
                var neighborMinY = Math.Max(0, y - 1);
                var neighborMaxY = Math.Min(terrain.Definition.HeightSamplesY - 1, y + 1);
                var neighborMinX = Math.Max(0, x - 1);
                var neighborMaxX = Math.Min(terrain.Definition.HeightSamplesX - 1, x + 1);
                for (var neighborY = neighborMinY; neighborY <= neighborMaxY; neighborY++)
                {
                    for (var neighborX = neighborMinX; neighborX <= neighborMaxX; neighborX++)
                    {
                        total += ReadSnapshot(neighborX, neighborY);
                        count++;
                    }
                }

                var before = ReadSnapshot(x, y);
                var average = (double)total / count;
                var step = Math.Clamp(average - before, -maxStep, maxStep);
                var after = TerrainBrushMath.ClampAndRound(terrain, before + step);
                TerrainBrushMath.AddChange(terrain, changes, x, y, before, after);
            }
        }

        return changes;
    }
}
