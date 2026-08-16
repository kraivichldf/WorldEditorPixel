namespace Kingdom.World.Core.Chunks;

public readonly record struct TerrainChunkAddress(int ChunkX, int ChunkY, int LocalX, int LocalY)
{
    public static TerrainChunkAddress FromGlobal(int x, int y, int chunkSize)
    {
        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        return new TerrainChunkAddress(x / chunkSize, y / chunkSize, x % chunkSize, y % chunkSize);
    }
}
