namespace Kingdom.World.Core.Campaign.Generation;

/// <summary>
/// Deterministic gradient-noise fields sampled in physical world units.
/// </summary>
internal static class CampaignTerrainNoise
{
    private const double SimplexSkew = 0.36602540378443864676372317075294;
    private const double SimplexUnskew = 0.21132486540518711774542560974902;

    private static readonly (double X, double Y)[] Gradients =
    [
        (1, 1),
        (-1, 1),
        (1, -1),
        (-1, -1),
        (1, 0),
        (-1, 0),
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1),
        (0, 1),
        (0, -1),
    ];

    public static double Sample(double x, double y, int seed)
    {
        var skew = (x + y) * SimplexSkew;
        var latticeX = FastFloor(x + skew);
        var latticeY = FastFloor(y + skew);
        var unskew = (latticeX + latticeY) * SimplexUnskew;
        var originX = latticeX - unskew;
        var originY = latticeY - unskew;
        var localX = x - originX;
        var localY = y - originY;
        var stepX = localX > localY ? 1 : 0;
        var stepY = localX > localY ? 0 : 1;

        var firstX = localX - stepX + SimplexUnskew;
        var firstY = localY - stepY + SimplexUnskew;
        var secondX = localX - 1 + (2 * SimplexUnskew);
        var secondY = localY - 1 + (2 * SimplexUnskew);

        var value = Corner(seed, latticeX, latticeY, localX, localY) +
            Corner(seed, latticeX + stepX, latticeY + stepY, firstX, firstY) +
            Corner(seed, latticeX + 1, latticeY + 1, secondX, secondY);
        return Math.Clamp(value * 70, -1, 1);
    }

    public static double Fractal(
        double xKilometers,
        double yKilometers,
        int seed,
        double wavelengthKilometers,
        int octaves,
        double persistence = 0.5,
        double lacunarity = 2)
    {
        ValidateFractalArguments(wavelengthKilometers, octaves, persistence, lacunarity);

        var total = 0.0;
        var totalAmplitude = 0.0;
        var amplitude = 1.0;
        var frequency = 1.0;
        for (var octave = 0; octave < octaves; octave++)
        {
            var octaveSeed = OffsetSeed(seed, octave * 1_013);
            var offsetX = HashSigned(octaveSeed, 47_021, 47_033) * 128;
            var offsetY = HashSigned(octaveSeed, 47_047, 47_051) * 128;
            var sampleX = (xKilometers / wavelengthKilometers * frequency) + offsetX;
            var sampleY = (yKilometers / wavelengthKilometers * frequency) + offsetY;
            total += Sample(sampleX, sampleY, octaveSeed) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return Math.Clamp(total / totalAmplitude, -1, 1);
    }

    public static double Ridged(
        double xKilometers,
        double yKilometers,
        int seed,
        double wavelengthKilometers,
        int octaves,
        double persistence = 0.5,
        double lacunarity = 2)
    {
        ValidateFractalArguments(wavelengthKilometers, octaves, persistence, lacunarity);

        var total = 0.0;
        var totalAmplitude = 0.0;
        var amplitude = 1.0;
        var frequency = 1.0;
        for (var octave = 0; octave < octaves; octave++)
        {
            var octaveSeed = OffsetSeed(seed, octave * 1_013);
            var offsetX = HashSigned(octaveSeed, 47_107, 47_117) * 128;
            var offsetY = HashSigned(octaveSeed, 47_129, 47_137) * 128;
            var sampleX = (xKilometers / wavelengthKilometers * frequency) + offsetX;
            var sampleY = (yKilometers / wavelengthKilometers * frequency) + offsetY;
            var ridge = 1 - Math.Abs(Sample(sampleX, sampleY, octaveSeed));
            total += ridge * ridge * amplitude;
            totalAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return Math.Clamp(total / totalAmplitude, 0, 1);
    }

    private static double Corner(
        int seed,
        int latticeX,
        int latticeY,
        double offsetX,
        double offsetY)
    {
        var attenuation = 0.5 - (offsetX * offsetX) - (offsetY * offsetY);
        if (attenuation <= 0)
        {
            return 0;
        }

        var gradient = Gradients[Hash(seed, latticeX, latticeY) % Gradients.Length];
        attenuation *= attenuation;
        return attenuation * attenuation *
            ((gradient.X * offsetX) + (gradient.Y * offsetY));
    }

    private static void ValidateFractalArguments(
        double wavelengthKilometers,
        int octaves,
        double persistence,
        double lacunarity)
    {
        if (!double.IsFinite(wavelengthKilometers) || wavelengthKilometers <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wavelengthKilometers),
                wavelengthKilometers,
                "Wavelength must be a finite positive distance in kilometres.");
        }

        if (octaves <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(octaves), octaves, "At least one octave is required.");
        }

        if (!double.IsFinite(persistence) || persistence <= 0 || persistence > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(persistence),
                persistence,
                "Persistence must be greater than zero and no greater than one.");
        }

        if (!double.IsFinite(lacunarity) || lacunarity <= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lacunarity),
                lacunarity,
                "Lacunarity must be finite and greater than one.");
        }
    }

    private static int FastFloor(double value)
    {
        var truncated = (int)value;
        return value < truncated ? truncated - 1 : truncated;
    }

    private static double HashSigned(int seed, int x, int y) =>
        ((Hash(seed, x, y) / (double)uint.MaxValue) * 2) - 1;

    private static uint Hash(int seed, int x, int y)
    {
        unchecked
        {
            var hash = (uint)seed;
            hash ^= (uint)x * 0x9E3779B9u;
            hash = (hash << 16) | (hash >> 16);
            hash ^= (uint)y * 0x85EBCA6Bu;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }
    }

    private static int OffsetSeed(int seed, int offset) => unchecked(seed + offset);
}
