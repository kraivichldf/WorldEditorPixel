using Kingdom.World.Core.Validation;

using System.Text.Json.Serialization;

namespace Kingdom.World.Core.Models;

public sealed record WorldDefinition
{
    public const int CurrentVersion = 1;

    [JsonRequired]
    public int Version { get; init; } = CurrentVersion;

    public long WorldWidthMeters { get; init; }

    public long WorldHeightMeters { get; init; }

    public int HeightSamplesX { get; init; }

    public int HeightSamplesY { get; init; }

    public int HeightSampleSpacingMeters { get; init; }

    public int CampaignTileSizeMeters { get; init; }

    public short SeaLevelMeters { get; init; }

    public short MinimumElevationMeters { get; init; }

    public short MaximumElevationMeters { get; init; }

    public short InitialElevationMeters { get; init; }

    public int ChunkSize { get; init; } = 256;

    [JsonIgnore]
    public long CampaignTilesX => 1 + (WorldWidthMeters - 1) / CampaignTileSizeMeters;

    [JsonIgnore]
    public long CampaignTilesY => 1 + (WorldHeightMeters - 1) / CampaignTileSizeMeters;

    public static WorldDefinition Create(
        long worldWidthMeters,
        long worldHeightMeters,
        int heightSampleSpacingMeters,
        int campaignTileSizeMeters,
        short seaLevelMeters,
        short minimumElevationMeters,
        short maximumElevationMeters,
        int chunkSize = 256,
        short? initialElevationMeters = null)
    {
        if (heightSampleSpacingMeters <= 0)
        {
            throw new WorldValidationException(["Height sample spacing must be greater than zero."]);
        }

        if (worldWidthMeters <= 0 || worldHeightMeters <= 0)
        {
            throw new WorldValidationException(["World width and height must be greater than zero."]);
        }

        if (worldWidthMeters % heightSampleSpacingMeters != 0 ||
            worldHeightMeters % heightSampleSpacingMeters != 0)
        {
            throw new WorldValidationException(
                ["World dimensions must be exactly divisible by height sample spacing in format version 1."]);
        }

        var samplesX = checked(worldWidthMeters / heightSampleSpacingMeters + 1);
        var samplesY = checked(worldHeightMeters / heightSampleSpacingMeters + 1);
        if (samplesX > int.MaxValue || samplesY > int.MaxValue)
        {
            throw new WorldValidationException(["Height sample dimensions exceed the supported 32-bit coordinate range."]);
        }

        var definition = new WorldDefinition
        {
            WorldWidthMeters = worldWidthMeters,
            WorldHeightMeters = worldHeightMeters,
            HeightSamplesX = (int)samplesX,
            HeightSamplesY = (int)samplesY,
            HeightSampleSpacingMeters = heightSampleSpacingMeters,
            CampaignTileSizeMeters = campaignTileSizeMeters,
            SeaLevelMeters = seaLevelMeters,
            MinimumElevationMeters = minimumElevationMeters,
            MaximumElevationMeters = maximumElevationMeters,
            InitialElevationMeters = initialElevationMeters ?? seaLevelMeters,
            ChunkSize = chunkSize,
        };

        WorldDefinitionValidator.EnsureValid(definition);
        return definition;
    }

    public (int X, int Y) GetCampaignTile(TerrainCoordinate coordinate)
    {
        var worldX = (long)coordinate.X * HeightSampleSpacingMeters;
        var worldY = (long)coordinate.Y * HeightSampleSpacingMeters;
        var maximumTileX = Math.Max(0, (int)((WorldWidthMeters - 1) / CampaignTileSizeMeters));
        var maximumTileY = Math.Max(0, (int)((WorldHeightMeters - 1) / CampaignTileSizeMeters));
        return (
            Math.Min((int)(worldX / CampaignTileSizeMeters), maximumTileX),
            Math.Min((int)(worldY / CampaignTileSizeMeters), maximumTileY));
    }
}
