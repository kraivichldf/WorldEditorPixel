using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Kingdom.World.Core.Campaign.Seasons;

public static class CampaignSeasonSeed
{
    private const uint TerrainSeedSalt = 0xA341316Cu;

    private const uint DefinitionSeedSalt = 0xC8013EA4u;

    public static int FromTerrainSeed(int terrainSeed) =>
        unchecked((int)Mix(unchecked((uint)terrainSeed) ^ TerrainSeedSalt));

    public static int FromCurrentWorld(CampaignSeasonGenerationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var definition = source.Definition;
        var hash = 0x811C9DC5u;
        hash = MixState(hash, unchecked((uint)definition.Version));
        hash = MixLong(hash, definition.WorldWidthMeters);
        hash = MixLong(hash, definition.WorldHeightMeters);
        hash = MixState(hash, unchecked((uint)definition.CampaignTileSizeMeters));
        hash = MixState(hash, unchecked((uint)(ushort)definition.SeaLevelMeters));
        hash = MixState(hash, unchecked((uint)(ushort)definition.MinimumHeightMeters));
        hash = MixState(hash, unchecked((uint)(ushort)definition.MaximumHeightMeters));
        hash = MixState(hash, unchecked((uint)(ushort)definition.DefaultTileHeightMeters));
        foreach (var sample in source.Terrain.Samples)
        {
            hash = MixState(hash, unchecked((uint)sample.TerrainType));
            hash = MixString(hash, sample.CustomTerrainId);
            hash = MixState(hash, unchecked((uint)(ushort)sample.ElevationMeters));
            hash = MixState(hash, unchecked((uint)sample.WaterFeatures));
        }

        return unchecked((int)Mix(hash));
    }

    public static int ForDefinition(int seasonSeed, string seasonId)
    {
        if (!CampaignSeasonDefinition.IsValidIdentifier(seasonId))
        {
            throw new ArgumentException("Season seed derivation requires a valid season ID.", nameof(seasonId));
        }

        var idHash = Fnv1A32(Encoding.UTF8.GetBytes(seasonId));
        return unchecked((int)Mix(unchecked((uint)seasonSeed) ^ idHash ^ DefinitionSeedSalt));
    }

    public static double ToPhase01(int seasonSeed) =>
        Mix(unchecked((uint)seasonSeed) ^ 0x9E3779B9u) / 4_294_967_296d;

    public static string GetCatalogIdFingerprint(CampaignSeasonCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        foreach (var definition in catalog.Definitions)
        {
            var idBytes = Encoding.UTF8.GetBytes(definition.Id);
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, idBytes.Length);
            hash.AppendData(lengthBytes);
            hash.AppendData(idBytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static uint Fnv1A32(ReadOnlySpan<byte> values)
    {
        var hash = 2_166_136_261u;
        foreach (var value in values)
        {
            hash ^= value;
            hash *= 16_777_619u;
        }

        return hash;
    }

    private static uint MixLong(uint hash, long value)
    {
        hash = MixState(hash, unchecked((uint)value));
        return MixState(hash, unchecked((uint)(value >> 32)));
    }

    private static uint MixString(uint hash, string? value)
    {
        if (value is null)
        {
            return MixState(hash, uint.MaxValue);
        }

        hash = MixState(hash, unchecked((uint)value.Length));
        foreach (var character in value)
        {
            hash = MixState(hash, character);
        }

        return hash;
    }

    private static uint MixState(uint hash, uint value)
    {
        unchecked
        {
            hash ^= value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash *= 0x85EBCA6Bu;
            return hash ^ (hash >> 13);
        }
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }
}
