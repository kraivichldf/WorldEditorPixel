namespace Kingdom.World.Core.Chunks;

public sealed class TerrainChunk
{
    private readonly short[] _samples;

    internal TerrainChunk(int chunkX, int chunkY, int width, int height, short initialElevation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(chunkX);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        ChunkX = chunkX;
        ChunkY = chunkY;
        Width = width;
        Height = height;
        _samples = new short[checked(width * height)];
        if (initialElevation != 0)
        {
            Array.Fill(_samples, initialElevation);
        }
    }

    private TerrainChunk(int chunkX, int chunkY, int width, int height, short[] samples)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Width = width;
        Height = height;
        _samples = samples;
    }

    public int ChunkX { get; }

    public int ChunkY { get; }

    public int Width { get; }

    public int Height { get; }

    internal ReadOnlySpan<short> Samples => _samples;

    internal static TerrainChunk FromSamples(int chunkX, int chunkY, int width, int height, short[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Length != checked(width * height))
        {
            throw new ArgumentException("Chunk sample count does not match its dimensions.", nameof(samples));
        }

        return new TerrainChunk(chunkX, chunkY, width, height, samples);
    }

    internal short GetHeight(int localX, int localY)
    {
        EnsureValid(localX, localY);
        return _samples[localY * Width + localX];
    }

    internal void SetHeight(int localX, int localY, short height)
    {
        EnsureValid(localX, localY);
        _samples[localY * Width + localX] = height;
    }

    private void EnsureValid(int localX, int localY)
    {
        if ((uint)localX >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(localX));
        }

        if ((uint)localY >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(localY));
        }
    }
}
