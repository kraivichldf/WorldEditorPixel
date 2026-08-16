using Kingdom.World.Core.Models;
using Kingdom.World.Core.Terrain;

namespace Kingdom.World.Core.Brushes;

internal static class TerrainBrushMath
{
    public static TerrainRegion GetAffectedRegion(
        WorldTerrain terrain,
        TerrainCoordinate center,
        double radius)
    {
        var minX = Math.Max(0, (int)Math.Floor(center.X - radius));
        var minY = Math.Max(0, (int)Math.Floor(center.Y - radius));
        var maxX = Math.Min(terrain.Definition.HeightSamplesX - 1, (int)Math.Ceiling(center.X + radius));
        var maxY = Math.Min(terrain.Definition.HeightSamplesY - 1, (int)Math.Ceiling(center.Y + radius));
        return new TerrainRegion(minX, minY, maxX, maxY);
    }

    public static double Weight(int x, int y, TerrainCoordinate center, BrushSettings settings)
    {
        var dx = x - center.X;
        var dy = y - center.Y;
        var normalizedDistance = Math.Sqrt((double)dx * dx + (double)dy * dy) / settings.RadiusSamples;
        if (normalizedDistance > 1)
        {
            return 0;
        }

        // Smoothstep produces a soft radial edge at every falloff value. Falloff controls
        // how quickly strength concentrates toward the center, never a hard inclusion mask.
        var inverse = 1 - normalizedDistance;
        var smooth = inverse * inverse * (3 - 2 * inverse);
        return Math.Pow(smooth, 1 + settings.Falloff * 3);
    }

    public static short ClampAndRound(WorldTerrain terrain, double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return (short)Math.Clamp(
            rounded,
            terrain.Definition.MinimumElevationMeters,
            terrain.Definition.MaximumElevationMeters);
    }

    public static void AddChange(
        WorldTerrain terrain,
        List<TerrainSampleChange> changes,
        int x,
        int y,
        short before,
        short after)
    {
        if (before == after)
        {
            return;
        }

        terrain.SetHeight(x, y, after);
        var stored = terrain.GetHeight(x, y);
        if (stored != before)
        {
            changes.Add(new TerrainSampleChange(x, y, before, stored));
        }
    }
}
