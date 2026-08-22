using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Editor.Controls;

internal static class CampaignWorldPreviewRenderer
{
    private const int MaximumRasterEdge = 512;

    public static unsafe WriteableBitmap Render(CampaignWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var tilesX = world.Definition.TilesX;
        var tilesY = world.Definition.TilesY;
        var scale = Math.Min(
            (double)MaximumRasterEdge / tilesX,
            (double)MaximumRasterEdge / tilesY);
        var rasterWidth = Math.Max(1, (int)Math.Round(tilesX * scale));
        var rasterHeight = Math.Max(1, (int)Math.Round(tilesY * scale));
        var bitmap = new WriteableBitmap(
            new PixelSize(rasterWidth, rasterHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var framebuffer = bitmap.Lock();
        var destination = (byte*)framebuffer.Address;
        for (var pixelY = 0; pixelY < rasterHeight; pixelY++)
        {
            var tileY = Math.Min(tilesY - 1, (int)((long)pixelY * tilesY / rasterHeight));
            var row = destination + pixelY * framebuffer.RowBytes;
            for (var pixelX = 0; pixelX < rasterWidth; pixelX++)
            {
                var tileSpaceX = (pixelX + 0.5) * tilesX / rasterWidth;
                var tileSpaceY = (pixelY + 0.5) * tilesY / rasterHeight;
                var tileX = Math.Min(tilesX - 1, (int)((long)pixelX * tilesX / rasterWidth));
                var tile = world.Tiles.GetTile(tileX, tileY);
                var color = GetTerrainColor(world, tile);
                color = ShadeForRelief(world, tile, tileSpaceX, tileSpaceY, color);

                var offset = pixelX * 4;
                row[offset] = color.Blue;
                row[offset + 1] = color.Green;
                row[offset + 2] = color.Red;
                row[offset + 3] = byte.MaxValue;
            }
        }

        return bitmap;
    }

    public static unsafe WriteableBitmap RenderSeasons(CampaignSeasonMap seasons)
    {
        ArgumentNullException.ThrowIfNull(seasons);
        seasons.EnsureValid();
        var tilesX = seasons.Definition.TilesX;
        var tilesY = seasons.Definition.TilesY;
        var scale = Math.Min(
            (double)MaximumRasterEdge / tilesX,
            (double)MaximumRasterEdge / tilesY);
        var rasterWidth = Math.Max(1, (int)Math.Round(tilesX * scale));
        var rasterHeight = Math.Max(1, (int)Math.Round(tilesY * scale));
        var bitmap = new WriteableBitmap(
            new PixelSize(rasterWidth, rasterHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var framebuffer = bitmap.Lock();
        var destination = (byte*)framebuffer.Address;
        for (var pixelY = 0; pixelY < rasterHeight; pixelY++)
        {
            var tileY = Math.Min(tilesY - 1, (int)((long)pixelY * tilesY / rasterHeight));
            var row = destination + pixelY * framebuffer.RowBytes;
            for (var pixelX = 0; pixelX < rasterWidth; pixelX++)
            {
                var tileX = Math.Min(tilesX - 1, (int)((long)pixelX * tilesX / rasterWidth));
                var occurrences = seasons.GetOccurrences(tileX, tileY);
                var color = BlendSeasonColors(seasons, occurrences);
                var offset = pixelX * 4;
                row[offset] = color.Blue;
                row[offset + 1] = color.Green;
                row[offset + 2] = color.Red;
                row[offset + 3] = byte.MaxValue;
            }
        }

        return bitmap;
    }

    private static RgbColor BlendSeasonColors(
        CampaignSeasonMap seasons,
        IReadOnlyList<CampaignSeasonOccurrence> occurrences)
    {
        if (occurrences.Count == 0)
        {
            return new RgbColor(58, 65, 69);
        }

        var red = 0;
        var green = 0;
        var blue = 0;
        foreach (var occurrence in occurrences)
        {
            var color = ParseColor(seasons.Catalog.Get(occurrence.SeasonId).ColorHex);
            red += color.Red;
            green += color.Green;
            blue += color.Blue;
        }

        return new RgbColor(
            (byte)(red / occurrences.Count),
            (byte)(green / occurrences.Count),
            (byte)(blue / occurrences.Count));
    }

    private static RgbColor GetTerrainColor(CampaignWorld world, CampaignTileData tile)
    {
        if (world.Tiles.TryGetCustomTerrainDefinition(tile.CustomTerrainId, out var customTerrain))
        {
            return ParseColor(customTerrain.ColorHex);
        }

        return tile.Type switch
        {
            CampaignTileType.Unassigned => new(89, 102, 106),
            CampaignTileType.Water or CampaignTileType.Sea => new(30, 106, 139),
            CampaignTileType.Plains or CampaignTileType.Coastal => new(115, 148, 93),
            CampaignTileType.Steppe => new(164, 154, 88),
            CampaignTileType.Desert => new(201, 145, 66),
            CampaignTileType.Forest => new(47, 104, 79),
            CampaignTileType.Hills => new(139, 138, 98),
            CampaignTileType.Mountain => new(133, 135, 132),
            CampaignTileType.Lake => new(45, 142, 163),
            CampaignTileType.River => new(59, 155, 193),
            CampaignTileType.LargeRiver or CampaignTileType.RiverJunction => new(35, 127, 166),
            CampaignTileType.Beach => new(195, 168, 109),
            CampaignTileType.Cliff => new(111, 102, 94),
            _ => new(89, 102, 106),
        };
    }

    private static RgbColor ShadeForRelief(
        CampaignWorld world,
        CampaignTileData tile,
        double tileSpaceX,
        double tileSpaceY,
        RgbColor color)
    {
        var definition = world.Definition;
        var range = Math.Max(1, definition.MaximumHeightMeters - definition.MinimumHeightMeters);
        var surfaceHeight = world.Tiles.GetDerivedHeight(tileSpaceX, tileSpaceY);
        var normalized = (surfaceHeight - definition.MinimumHeightMeters) / range;
        var adjustment = (int)Math.Round((normalized - 0.42) * 30);
        if (!tile.Type.IsWater())
        {
            const double sampleDistance = 0.65;
            var leftX = Math.Max(0, tileSpaceX - sampleDistance);
            var rightX = Math.Min(definition.TilesX, tileSpaceX + sampleDistance);
            var topY = Math.Max(0, tileSpaceY - sampleDistance);
            var bottomY = Math.Min(definition.TilesY, tileSpaceY + sampleDistance);
            var gradeX = (world.Tiles.GetDerivedHeight(rightX, tileSpaceY) -
                          world.Tiles.GetDerivedHeight(leftX, tileSpaceY)) /
                         Math.Max(1, (rightX - leftX) * definition.CampaignTileSizeMeters);
            var gradeY = (world.Tiles.GetDerivedHeight(tileSpaceX, bottomY) -
                          world.Tiles.GetDerivedHeight(tileSpaceX, topY)) /
                         Math.Max(1, (bottomY - topY) * definition.CampaignTileSizeMeters);
            adjustment += GetReliefLightingAdjustment(gradeX, gradeY);
        }
        else
        {
            adjustment = Math.Min(4, adjustment);
        }

        return new RgbColor(
            Adjust(color.Red, adjustment),
            Adjust(color.Green, adjustment),
            Adjust(color.Blue, adjustment));
    }

    private static int GetReliefLightingAdjustment(double gradeX, double gradeY)
    {
        const double verticalExaggeration = 5.0;
        const double lightX = -0.45;
        const double lightY = -0.55;
        const double lightZ = 0.70;
        var normalX = -gradeX * verticalExaggeration;
        var normalY = -gradeY * verticalExaggeration;
        var inverseLength = 1 / Math.Sqrt((normalX * normalX) + (normalY * normalY) + 1);
        var illumination =
            (normalX * inverseLength * lightX) +
            (normalY * inverseLength * lightY) +
            (inverseLength * lightZ);
        return (int)Math.Round(Math.Clamp((illumination - lightZ) * 55, -24, 22));
    }

    private static RgbColor ParseColor(string value) => new(
        byte.Parse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        byte.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        byte.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    private static byte Adjust(byte value, int adjustment) =>
        (byte)Math.Clamp(value + adjustment, byte.MinValue, byte.MaxValue);

    private readonly record struct RgbColor(byte Red, byte Green, byte Blue);
}
