using Kingdom.World.Core.Models;
using Kingdom.World.Core.Terrain;

namespace Kingdom.World.Tests;

internal static class TestWorldFactory
{
    public static WorldTerrain Create(
        int samplesX = 9,
        int samplesY = 9,
        int chunkSize = 4,
        short initialElevation = 0,
        short minimumElevation = -1000,
        short maximumElevation = 6000)
    {
        var spacing = 10;
        var definition = WorldDefinition.Create(
            (samplesX - 1L) * spacing,
            (samplesY - 1L) * spacing,
            spacing,
            campaignTileSizeMeters: 40,
            seaLevelMeters: 0,
            minimumElevationMeters: minimumElevation,
            maximumElevationMeters: maximumElevation,
            chunkSize: chunkSize,
            initialElevationMeters: initialElevation);
        return new WorldTerrain(definition);
    }
}
