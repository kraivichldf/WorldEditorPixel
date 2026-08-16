namespace Kingdom.World.Core.Campaign.Resources;

/// <summary>
/// Stable, runtime-independent 32-bit seed derivation for campaign resources.
/// </summary>
public static class CampaignResourceSeed
{
    public static int FromTerrainSeed(int terrainSeed) =>
        unchecked((int)Finalize(Mix(0xA341_316Cu, unchecked((uint)terrainSeed))));

    public static int FromCurrentWorld(CampaignResourceGenerationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var definition = source.Definition;
        var hash = 0x811C_9DC5u;
        hash = Mix(hash, unchecked((uint)definition.Version));
        hash = Mix(hash, unchecked((uint)definition.WorldWidthMeters));
        hash = Mix(hash, unchecked((uint)(definition.WorldWidthMeters >> 32)));
        hash = Mix(hash, unchecked((uint)definition.WorldHeightMeters));
        hash = Mix(hash, unchecked((uint)(definition.WorldHeightMeters >> 32)));
        hash = Mix(hash, unchecked((uint)definition.CampaignTileSizeMeters));
        hash = Mix(hash, unchecked((uint)(ushort)definition.SeaLevelMeters));
        hash = Mix(hash, unchecked((uint)(ushort)definition.MinimumHeightMeters));
        hash = Mix(hash, unchecked((uint)(ushort)definition.MaximumHeightMeters));
        hash = Mix(hash, unchecked((uint)(ushort)definition.DefaultTileHeightMeters));

        foreach (var sample in source.Terrain.Samples)
        {
            hash = Mix(hash, unchecked((uint)sample.Kind));
            hash = Mix(hash, unchecked((uint)sample.Surface));
            hash = Mix(hash, unchecked((uint)sample.Form));
            hash = MixString(hash, sample.CustomTerrainId);
            hash = Mix(hash, unchecked((uint)(ushort)sample.ElevationMeters));
            hash = MixDouble(hash, sample.MaximumCardinalGrade);
            hash = MixDouble(hash, sample.SeaDistanceKilometers);
            hash = MixDouble(hash, sample.LakeDistanceKilometers);
            hash = MixDouble(hash, sample.RiverDistanceKilometers);
            hash = Mix(hash, unchecked((uint)sample.RiverFeatures));
            hash = Mix(hash, unchecked((uint)sample.CoastFlags));
        }

        return unchecked((int)Finalize(hash));
    }

    internal static int ForResource(int seed, string resourceId)
    {
        var hash = Mix(0xC801_3EA4u, unchecked((uint)seed));
        return unchecked((int)Finalize(MixString(hash, resourceId)));
    }

    internal static uint TieHash(int seed, int x, int y)
    {
        var hash = Mix(0x9E37_79B9u, unchecked((uint)seed));
        hash = Mix(hash, unchecked((uint)x));
        return Finalize(Mix(hash, unchecked((uint)y)));
    }

    private static uint MixString(uint hash, string? value)
    {
        if (value is null)
        {
            return Mix(hash, uint.MaxValue);
        }

        hash = Mix(hash, unchecked((uint)value.Length));
        foreach (var character in value)
        {
            hash = Mix(hash, character);
        }

        return hash;
    }

    private static uint MixDouble(uint hash, double value)
    {
        var bits = unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
        hash = Mix(hash, unchecked((uint)bits));
        return Mix(hash, unchecked((uint)(bits >> 32)));
    }

    private static uint Mix(uint state, uint value)
    {
        unchecked
        {
            state ^= value + 0x9E37_79B9u + (state << 6) + (state >> 2);
            state *= 0x85EB_CA6Bu;
            return state ^ (state >> 13);
        }
    }

    private static uint Finalize(uint hash)
    {
        unchecked
        {
            hash ^= hash >> 16;
            hash *= 0x7FEB_352Du;
            hash ^= hash >> 15;
            hash *= 0x846C_A68Bu;
            hash ^= hash >> 16;
            return hash;
        }
    }
}
