using Kingdom.World.Core.Chunks;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Terrain;

public sealed class WorldTerrain
{
    private readonly Dictionary<(int X, int Y), TerrainChunk> _chunks = [];

    public WorldTerrain(WorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        WorldDefinitionValidator.EnsureValid(definition);
        Definition = definition;
        CampaignTiles = new CampaignTileLayer(definition);
    }

    public WorldDefinition Definition { get; }

    public CampaignTileLayer CampaignTiles { get; }

    public long Revision { get; private set; }

    public int AllocatedChunkCount => _chunks.Count;

    public bool IsValidCoordinate(int x, int y) =>
        (uint)x < (uint)Definition.HeightSamplesX && (uint)y < (uint)Definition.HeightSamplesY;

    public short GetHeight(int x, int y)
    {
        EnsureValidCoordinate(x, y);
        var address = TerrainChunkAddress.FromGlobal(x, y, Definition.ChunkSize);
        return _chunks.TryGetValue((address.ChunkX, address.ChunkY), out var chunk)
            ? chunk.GetHeight(address.LocalX, address.LocalY)
            : Definition.InitialElevationMeters;
    }

    public void SetHeight(int x, int y, short height)
    {
        EnsureValidCoordinate(x, y);

        var clamped = Math.Clamp(height, Definition.MinimumElevationMeters, Definition.MaximumElevationMeters);
        var address = TerrainChunkAddress.FromGlobal(x, y, Definition.ChunkSize);
        if (!_chunks.TryGetValue((address.ChunkX, address.ChunkY), out var chunk))
        {
            if (clamped == Definition.InitialElevationMeters)
            {
                return;
            }

            chunk = CreateChunk(address.ChunkX, address.ChunkY);
            _chunks.Add((address.ChunkX, address.ChunkY), chunk);
        }

        var previous = chunk.GetHeight(address.LocalX, address.LocalY);
        if (previous == clamped)
        {
            return;
        }

        chunk.SetHeight(address.LocalX, address.LocalY, clamped);
        Revision++;
    }

    public TerrainChunkAddress ResolveAddress(int x, int y)
    {
        EnsureValidCoordinate(x, y);
        return TerrainChunkAddress.FromGlobal(x, y, Definition.ChunkSize);
    }

    internal IEnumerable<TerrainChunk> GetAllocatedChunks() => _chunks.Values;

    internal (int Width, int Height) GetChunkDimensions(int chunkX, int chunkY)
    {
        var startX = checked(chunkX * Definition.ChunkSize);
        var startY = checked(chunkY * Definition.ChunkSize);
        if (startX < 0 || startY < 0 || startX >= Definition.HeightSamplesX || startY >= Definition.HeightSamplesY)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkX), "Chunk coordinate lies outside the world.");
        }

        return (
            Math.Min(Definition.ChunkSize, Definition.HeightSamplesX - startX),
            Math.Min(Definition.ChunkSize, Definition.HeightSamplesY - startY));
    }

    internal void LoadChunk(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        var expected = GetChunkDimensions(chunk.ChunkX, chunk.ChunkY);
        if (chunk.Width != expected.Width || chunk.Height != expected.Height)
        {
            throw new ArgumentException("Loaded chunk dimensions do not match world metadata.", nameof(chunk));
        }

        if (!_chunks.TryAdd((chunk.ChunkX, chunk.ChunkY), chunk))
        {
            throw new InvalidOperationException($"Chunk {chunk.ChunkX},{chunk.ChunkY} was loaded more than once.");
        }
    }

    private TerrainChunk CreateChunk(int chunkX, int chunkY)
    {
        var (width, height) = GetChunkDimensions(chunkX, chunkY);
        return new TerrainChunk(chunkX, chunkY, width, height, Definition.InitialElevationMeters);
    }

    private void EnsureValidCoordinate(int x, int y)
    {
        if (!IsValidCoordinate(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Sample coordinate ({x}, {y}) is outside 0..{Definition.HeightSamplesX - 1}, 0..{Definition.HeightSamplesY - 1}.");
        }
    }
}
