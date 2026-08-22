using System.Text.Json;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Serialization;

/// <summary>
/// Writes one self-contained, engine-facing JSON file without materializing a second world-sized DTO.
/// </summary>
public static class CampaignWorldJsonExporter
{
    public const string FileExtension = ".json";

    public const string SuggestedFileSuffix = ".world.json";

    public const string FormatIdentifier = "world-editor-pixel-runtime-json";

    public const int FormatVersion = 1;

    private const int ExportBufferSize = 64 * 1024;

    private const int TilesPerAsyncFlush = 2_048;

    private static readonly CampaignTileType[] ExportedTileTypes =
    [
        CampaignTileType.Unassigned,
        CampaignTileType.Plains,
        CampaignTileType.Forest,
        CampaignTileType.Hills,
        CampaignTileType.Mountain,
        CampaignTileType.Sea,
        CampaignTileType.Lake,
        CampaignTileType.River,
        CampaignTileType.Beach,
        CampaignTileType.Cliff,
        CampaignTileType.Desert,
        CampaignTileType.LargeRiver,
        CampaignTileType.RiverJunction,
        CampaignTileType.Steppe,
    ];

    public static async Task ExportAsync(
        CampaignWorld world,
        CampaignResourceMap resources,
        CampaignSeasonMap seasons,
        string jsonPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(seasons);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);
        CampaignWorldDefinition.EnsureValid(world.Definition);
        CampaignWorldDefinition.EnsureValid(resources.Definition);
        CampaignWorldDefinition.EnsureValid(seasons.Definition);
        if (world.Definition != resources.Definition)
        {
            throw new ArgumentException(
                "The campaign resource map must use a value-equal world definition.",
                nameof(resources));
        }

        if (world.Definition != seasons.Definition)
        {
            throw new ArgumentException(
                "The campaign season map must use a value-equal world definition.",
                nameof(seasons));
        }

        if (!string.Equals(Path.GetExtension(jsonPath), FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Runtime JSON exports must use the '{FileExtension}' extension.",
                nameof(jsonPath));
        }

        var worldRevision = world.Revision;
        var resourceRevision = resources.Revision;
        var seasonRevision = seasons.Revision;
        resources.EnsureValid();
        seasons.EnsureValid();
        EnsureRevisionsUnchanged(
            world,
            worldRevision,
            resources,
            resourceRevision,
            seasons,
            seasonRevision);
        cancellationToken.ThrowIfCancellationRequested();

        var customTerrain = world.Tiles.CustomTerrainDefinitions
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        var resourceDefinitions = resources.Catalog.Definitions
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        var seasonDefinitions = seasons.Catalog.Definitions
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        var expectedResourceOccurrenceCount = resources.OccurrenceCount;
        var expectedSeasonOccurrenceCount = seasons.OccurrenceCount;

        var fullJsonPath = Path.GetFullPath(jsonPath);
        var jsonDirectory = Path.GetDirectoryName(fullJsonPath)
            ?? throw new ArgumentException("Runtime JSON file has no containing directory.", nameof(jsonPath));
        Directory.CreateDirectory(jsonDirectory);
        var temporaryPath = fullJsonPath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                ExportBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var writer = new Utf8JsonWriter(
                    stream,
                    new JsonWriterOptions
                    {
                        Indented = true,
                    });
                WriteHeader(
                    writer,
                    world,
                    resources,
                    seasons,
                    customTerrain,
                    resourceDefinitions,
                    seasonDefinitions);

                writer.WritePropertyName("tiles");
                writer.WriteStartArray();
                long writtenTileCount = 0;
                long writtenResourceOccurrenceCount = 0;
                long writtenSeasonOccurrenceCount = 0;
                for (var y = 0; y < world.Definition.TilesY; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureRevisionsUnchanged(
                        world,
                        worldRevision,
                        resources,
                        resourceRevision,
                        seasons,
                        seasonRevision);
                    for (var x = 0; x < world.Definition.TilesX; x++)
                    {
                        WriteTile(
                            writer,
                            world,
                            resources,
                            seasons,
                            x,
                            y,
                            ref writtenResourceOccurrenceCount,
                            ref writtenSeasonOccurrenceCount);
                        writtenTileCount++;
                        if (writtenTileCount % TilesPerAsyncFlush == 0)
                        {
                            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                if (writtenResourceOccurrenceCount != expectedResourceOccurrenceCount)
                {
                    throw new InvalidOperationException(
                        "The resource occurrence count changed while runtime JSON was being exported.");
                }

                if (writtenSeasonOccurrenceCount != expectedSeasonOccurrenceCount)
                {
                    throw new InvalidOperationException(
                        "The season occurrence count changed while runtime JSON was being exported.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            EnsureRevisionsUnchanged(
                world,
                worldRevision,
                resources,
                resourceRevision,
                seasons,
                seasonRevision);
            File.Move(temporaryPath, fullJsonPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static void WriteHeader(
        Utf8JsonWriter writer,
        CampaignWorld world,
        CampaignResourceMap resources,
        CampaignSeasonMap seasons,
        IReadOnlyList<CampaignCustomTerrainDefinition> customTerrain,
        IReadOnlyList<CampaignResourceDefinition> resourceDefinitions,
        IReadOnlyList<CampaignSeasonDefinition> seasonDefinitions)
    {
        var definition = world.Definition;
        writer.WriteStartObject();
        writer.WriteString("format", FormatIdentifier);
        writer.WriteNumber("version", FormatVersion);

        writer.WritePropertyName("world");
        writer.WriteStartObject();
        writer.WriteNumber("widthMeters", definition.WorldWidthMeters);
        writer.WriteNumber("heightMeters", definition.WorldHeightMeters);
        writer.WriteNumber("campaignTileSizeMeters", definition.CampaignTileSizeMeters);
        writer.WriteNumber("seaLevelMeters", definition.SeaLevelMeters);
        writer.WriteNumber("minimumHeightMeters", definition.MinimumHeightMeters);
        writer.WriteNumber("maximumHeightMeters", definition.MaximumHeightMeters);
        writer.WriteNumber("defaultTileHeightMeters", definition.DefaultTileHeightMeters);
        writer.WriteEndObject();

        writer.WritePropertyName("grid");
        writer.WriteStartObject();
        writer.WriteNumber("tilesX", definition.TilesX);
        writer.WriteNumber("tilesY", definition.TilesY);
        writer.WriteNumber("tileCount", definition.TileCount);
        writer.WriteString("origin", "northWest");
        writer.WriteString("xAxis", "east");
        writer.WriteString("yAxis", "south");
        writer.WriteString("order", "rowMajorYThenX");
        writer.WriteEndObject();

        writer.WritePropertyName("tileTypes");
        writer.WriteStartArray();
        foreach (var type in ExportedTileTypes)
        {
            writer.WriteStartObject();
            writer.WriteNumber("value", (byte)type);
            writer.WriteString("id", GetSerializedEnumName(type));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("customTerrain");
        writer.WriteStartArray();
        foreach (var custom in customTerrain)
        {
            writer.WriteStartObject();
            writer.WriteString("id", custom.Id);
            writer.WriteString("name", custom.Name);
            writer.WriteString("baseTerrainType", GetSerializedEnumName(custom.BaseType));
            writer.WriteString("color", custom.ColorHex);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WritePropertyName("resources");
        writer.WriteStartObject();
        writer.WriteNumber("occurrenceCount", resources.OccurrenceCount);
        writer.WritePropertyName("catalog");
        writer.WriteStartArray();
        foreach (var resource in resourceDefinitions)
        {
            writer.WriteStartObject();
            writer.WriteString("id", resource.Id);
            writer.WriteString("name", resource.Name);
            writer.WriteString("category", GetSerializedEnumName(resource.Category));
            writer.WriteBoolean("builtIn", resources.Catalog.IsBuiltIn(resource.Id));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WritePropertyName("seasons");
        writer.WriteStartObject();
        writer.WriteNumber("occurrenceCount", seasons.OccurrenceCount);
        writer.WritePropertyName("catalog");
        writer.WriteStartArray();
        foreach (var season in seasonDefinitions)
        {
            writer.WriteStartObject();
            writer.WriteString("id", season.Id);
            writer.WriteString("name", season.Name);
            writer.WriteBoolean("builtIn", seasons.Catalog.IsBuiltIn(season.Id));
            writer.WriteString("fallback", GetSerializedEnumName(season.Fallback));
            writer.WriteString("color", season.ColorHex);
            writer.WriteNumber("tintStrengthPercent", season.TintStrengthPercent);
            writer.WriteNumber("effectIntensityPercent", season.EffectIntensityPercent);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteTile(
        Utf8JsonWriter writer,
        CampaignWorld world,
        CampaignResourceMap resources,
        CampaignSeasonMap seasons,
        int x,
        int y,
        ref long writtenResourceOccurrenceCount,
        ref long writtenSeasonOccurrenceCount)
    {
        var tile = world.Tiles.GetTile(x, y);
        writer.WriteStartObject();
        writer.WriteNumber("x", x);
        writer.WriteNumber("y", y);
        writer.WriteString("terrainType", GetSerializedEnumName(NormalizeLegacyType(tile.Type)));
        if (tile.CustomTerrainId is null)
        {
            writer.WriteNull("customTerrainId");
        }
        else
        {
            writer.WriteString("customTerrainId", tile.CustomTerrainId);
        }

        writer.WriteNumber("heightMeters", tile.HeightMeters);

        writer.WritePropertyName("resources");
        writer.WriteStartArray();
        foreach (var occurrence in resources.GetOccurrences(x, y))
        {
            writer.WriteStartObject();
            writer.WriteString("id", occurrence.ResourceId);
            writer.WriteNumber("potential", occurrence.Potential);
            writer.WriteEndObject();
            writtenResourceOccurrenceCount = checked(writtenResourceOccurrenceCount + 1);
        }

        writer.WriteEndArray();

        writer.WritePropertyName("seasons");
        writer.WriteStartArray();
        foreach (var occurrence in seasons.GetOccurrences(x, y))
        {
            writer.WriteStringValue(occurrence.SeasonId);
            writtenSeasonOccurrenceCount = checked(writtenSeasonOccurrenceCount + 1);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static CampaignTileType NormalizeLegacyType(CampaignTileType type) => type switch
    {
        CampaignTileType.Water => CampaignTileType.Sea,
        CampaignTileType.Coastal => CampaignTileType.Plains,
        _ => type,
    };

    private static string GetSerializedEnumName<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static void EnsureRevisionsUnchanged(
        CampaignWorld world,
        long expectedWorldRevision,
        CampaignResourceMap resources,
        long expectedResourceRevision,
        CampaignSeasonMap seasons,
        long expectedSeasonRevision)
    {
        if (world.Revision != expectedWorldRevision)
        {
            throw new InvalidOperationException(
                "The campaign world changed while runtime JSON was being exported.");
        }

        if (resources.Revision != expectedResourceRevision)
        {
            throw new InvalidOperationException(
                "The resource map changed while runtime JSON was being exported.");
        }

        if (seasons.Revision != expectedSeasonRevision)
        {
            throw new InvalidOperationException(
                "The season map changed while runtime JSON was being exported.");
        }
    }
}
