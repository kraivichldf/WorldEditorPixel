using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign;

/// <summary>
/// A bounded, rectangular selection of complete campaign tiles.
/// </summary>
public readonly struct CampaignTileArea
{
    public CampaignTileArea(int minimumX, int minimumY, int maximumX, int maximumY)
    {
        if (minimumX > maximumX || minimumY > maximumY)
        {
            throw new ArgumentException("Campaign tile area bounds must not be inverted.");
        }

        MinimumX = minimumX;
        MinimumY = minimumY;
        MaximumX = maximumX;
        MaximumY = maximumY;
    }

    public int MinimumX { get; }

    public int MinimumY { get; }

    public int MaximumX { get; }

    public int MaximumY { get; }

    public int Width => MaximumX - MinimumX + 1;

    public int Height => MaximumY - MinimumY + 1;

    /// <summary>
    /// Builds a square, whole-tile footprint around a valid tile coordinate.
    /// The footprint is clipped at the campaign-world boundary.
    /// </summary>
    public static CampaignTileArea Centered(
        CampaignWorldDefinition definition,
        CampaignTileCoordinate center,
        int radius)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Campaign tile area radius cannot be negative.");
        }

        if ((uint)center.X >= (uint)definition.TilesX ||
            (uint)center.Y >= (uint)definition.TilesY)
        {
            throw new ArgumentOutOfRangeException(nameof(center), "Campaign tile area centre must be inside the world bounds.");
        }

        var maximumX = definition.TilesX - 1;
        var maximumY = definition.TilesY - 1;
        return new CampaignTileArea(
            ClampToAxis((long)center.X - radius, maximumX),
            ClampToAxis((long)center.Y - radius, maximumY),
            ClampToAxis((long)center.X + radius, maximumX),
            ClampToAxis((long)center.Y + radius, maximumY));
    }

    public IEnumerable<CampaignTileCoordinate> EnumerateCoordinates()
    {
        for (var y = MinimumY; y <= MaximumY; y++)
        {
            for (var x = MinimumX; x <= MaximumX; x++)
            {
                yield return new CampaignTileCoordinate(x, y);
            }
        }
    }

    private static int ClampToAxis(long value, int maximum) =>
        (int)Math.Clamp(value, 0, maximum);
}
