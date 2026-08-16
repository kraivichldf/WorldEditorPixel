namespace Kingdom.World.Core.Models;

public readonly record struct TerrainRegion(int MinX, int MinY, int MaxX, int MaxY)
{
    public int Width => MaxX - MinX + 1;

    public int Height => MaxY - MinY + 1;

    public bool IsEmpty => MaxX < MinX || MaxY < MinY;
}
