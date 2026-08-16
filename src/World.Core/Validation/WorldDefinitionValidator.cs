using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Validation;

public static class WorldDefinitionValidator
{
    public static IReadOnlyList<string> Validate(WorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<string>();
        if (definition.Version != WorldDefinition.CurrentVersion)
        {
            errors.Add($"Unsupported world format version {definition.Version}; expected {WorldDefinition.CurrentVersion}.");
        }

        if (definition.WorldWidthMeters <= 0 || definition.WorldHeightMeters <= 0)
        {
            errors.Add("World width and height must be greater than zero.");
        }

        if (definition.HeightSampleSpacingMeters <= 0)
        {
            errors.Add("Height sample spacing must be greater than zero.");
        }

        if (definition.HeightSamplesX < 2 || definition.HeightSamplesY < 2)
        {
            errors.Add("A world must contain at least two height samples on each axis.");
        }

        if (definition.HeightSampleSpacingMeters > 0 && definition.HeightSamplesX >= 2 && definition.HeightSamplesY >= 2)
        {
            var representedWidth = (long)(definition.HeightSamplesX - 1) * definition.HeightSampleSpacingMeters;
            var representedHeight = (long)(definition.HeightSamplesY - 1) * definition.HeightSampleSpacingMeters;
            if (representedWidth != definition.WorldWidthMeters || representedHeight != definition.WorldHeightMeters)
            {
                errors.Add("World dimensions, height sample counts, and sample spacing are inconsistent.");
            }
        }

        if (definition.CampaignTileSizeMeters <= 0)
        {
            errors.Add("Campaign tile size must be greater than zero.");
        }
        else if (definition.WorldWidthMeters > 0 && definition.WorldHeightMeters > 0 &&
                 ((definition.WorldWidthMeters - 1) / definition.CampaignTileSizeMeters > int.MaxValue ||
                  (definition.WorldHeightMeters - 1) / definition.CampaignTileSizeMeters > int.MaxValue))
        {
            errors.Add("Campaign tile coordinates exceed the supported 32-bit coordinate range.");
        }

        if (definition.MinimumElevationMeters >= definition.MaximumElevationMeters)
        {
            errors.Add("Minimum elevation must be lower than maximum elevation.");
        }

        if (definition.SeaLevelMeters < definition.MinimumElevationMeters ||
            definition.SeaLevelMeters > definition.MaximumElevationMeters)
        {
            errors.Add("Sea level must be within the allowed elevation range.");
        }

        if (definition.InitialElevationMeters < definition.MinimumElevationMeters ||
            definition.InitialElevationMeters > definition.MaximumElevationMeters)
        {
            errors.Add("Initial elevation must be within the allowed elevation range.");
        }

        if (definition.ChunkSize is < 1 or > 4096)
        {
            errors.Add("Chunk size must be between 1 and 4096 samples.");
        }

        return errors;
    }

    public static void EnsureValid(WorldDefinition definition)
    {
        var errors = Validate(definition);
        if (errors.Count > 0)
        {
            throw new WorldValidationException(errors);
        }
    }
}
