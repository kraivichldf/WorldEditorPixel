using System.Text.Json.Serialization;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Models;

public sealed record CampaignWorldDefinition
{
    public const int CurrentVersion = 2;
    public const long MaximumTileCount = 250_000;

    [JsonRequired]
    public int Version { get; init; } = CurrentVersion;

    [JsonRequired]
    public long WorldWidthMeters { get; init; }

    [JsonRequired]
    public long WorldHeightMeters { get; init; }

    [JsonRequired]
    public int CampaignTileSizeMeters { get; init; }

    [JsonRequired]
    public short SeaLevelMeters { get; init; }

    [JsonRequired]
    public short MinimumHeightMeters { get; init; }

    [JsonRequired]
    public short MaximumHeightMeters { get; init; }

    [JsonRequired]
    public short DefaultTileHeightMeters { get; init; }

    [JsonIgnore]
    public int TilesX => checked((int)(WorldWidthMeters / CampaignTileSizeMeters));

    [JsonIgnore]
    public int TilesY => checked((int)(WorldHeightMeters / CampaignTileSizeMeters));

    [JsonIgnore]
    public long TileCount => (long)TilesX * TilesY;

    public static CampaignWorldDefinition Create(
        long worldWidthMeters,
        long worldHeightMeters,
        int campaignTileSizeMeters,
        short seaLevelMeters,
        short minimumHeightMeters,
        short maximumHeightMeters,
        short? defaultTileHeightMeters = null)
    {
        var definition = new CampaignWorldDefinition
        {
            WorldWidthMeters = worldWidthMeters,
            WorldHeightMeters = worldHeightMeters,
            CampaignTileSizeMeters = campaignTileSizeMeters,
            SeaLevelMeters = seaLevelMeters,
            MinimumHeightMeters = minimumHeightMeters,
            MaximumHeightMeters = maximumHeightMeters,
            DefaultTileHeightMeters = defaultTileHeightMeters ?? seaLevelMeters,
        };

        EnsureValid(definition);
        return definition;
    }

    public static void EnsureValid(CampaignWorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();

        if (definition.Version != CurrentVersion)
        {
            errors.Add($"World format version {definition.Version} is unsupported; expected {CurrentVersion}.");
        }

        if (definition.WorldWidthMeters <= 0 || definition.WorldHeightMeters <= 0)
        {
            errors.Add("World width and height must be greater than zero.");
        }

        if (definition.CampaignTileSizeMeters <= 0)
        {
            errors.Add("Campaign tile size must be greater than zero.");
        }
        else if (definition.WorldWidthMeters > 0 && definition.WorldHeightMeters > 0)
        {
            if (definition.WorldWidthMeters % definition.CampaignTileSizeMeters != 0 ||
                definition.WorldHeightMeters % definition.CampaignTileSizeMeters != 0)
            {
                errors.Add("World width and height must be exactly divisible by campaign tile size.");
            }
            else if (definition.WorldWidthMeters / definition.CampaignTileSizeMeters > int.MaxValue ||
                     definition.WorldHeightMeters / definition.CampaignTileSizeMeters > int.MaxValue)
            {
                errors.Add("Campaign grid dimensions exceed the supported 32-bit tile coordinate range.");
            }
            else
            {
                var tilesX = definition.WorldWidthMeters / definition.CampaignTileSizeMeters;
                var tilesY = definition.WorldHeightMeters / definition.CampaignTileSizeMeters;
                var tileCount = checked(tilesX * tilesY);
                if (tileCount > MaximumTileCount)
                {
                    errors.Add(
                        $"Campaign worlds support up to {MaximumTileCount:N0} editable tiles; " +
                        $"this definition has {tileCount:N0}. Increase the campaign tile size or reduce the world dimensions.");
                }
            }
        }

        if (definition.MinimumHeightMeters >= definition.MaximumHeightMeters)
        {
            errors.Add("Minimum height must be lower than maximum height.");
        }

        if (definition.SeaLevelMeters < definition.MinimumHeightMeters ||
            definition.SeaLevelMeters > definition.MaximumHeightMeters)
        {
            errors.Add("Sea level must be inside the allowed height range.");
        }

        if (definition.DefaultTileHeightMeters < definition.MinimumHeightMeters ||
            definition.DefaultTileHeightMeters > definition.MaximumHeightMeters)
        {
            errors.Add("Default tile height must be inside the allowed height range.");
        }

        if (errors.Count > 0)
        {
            throw new WorldValidationException(errors);
        }
    }
}
