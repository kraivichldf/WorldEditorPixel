using Kingdom.World.Core.Campaign.V3;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Resources;

internal static class CampaignResourceTerrainAnalysis
{
    public static (CampaignResourceTerrainForm Form, double MaximumCardinalGrade) Analyze(
        CampaignWorldDefinition definition,
        Func<int, int, short> getHeight,
        int x,
        int y,
        TerrainFormProfile profile)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(getHeight);
        ArgumentNullException.ThrowIfNull(profile);

        var centerHeight = getHeight(x, y);
        var maximumCardinalGrade = 0.0;
        foreach (var (offsetX, offsetY) in CardinalOffsets)
        {
            var neighborX = Math.Clamp(x + offsetX, 0, definition.TilesX - 1);
            var neighborY = Math.Clamp(y + offsetY, 0, definition.TilesY - 1);
            var grade = Math.Abs(centerHeight - getHeight(neighborX, neighborY)) /
                        (double)definition.CampaignTileSizeMeters;
            maximumCardinalGrade = Math.Max(maximumCardinalGrade, grade);
        }

        var minimumHeight = (int)centerHeight;
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var sampleX = Math.Clamp(x + offsetX, 0, definition.TilesX - 1);
                var sampleY = Math.Clamp(y + offsetY, 0, definition.TilesY - 1);
                minimumHeight = Math.Min(minimumHeight, getHeight(sampleX, sampleY));
            }
        }

        var localProminence = centerHeight - minimumHeight;
        var form = maximumCardinalGrade >= profile.CliffMinimumGrade
            ? CampaignResourceTerrainForm.Cliff
            : maximumCardinalGrade >= profile.MountainMinimumGrade ||
              localProminence >= profile.MountainMinimumProminenceMeters ||
              centerHeight >= definition.SeaLevelMeters + profile.MountainMinimumElevationAboveSeaMeters
                ? CampaignResourceTerrainForm.Mountain
                : maximumCardinalGrade >= profile.HillsMinimumGrade
                    ? CampaignResourceTerrainForm.Hills
                    : maximumCardinalGrade >= profile.RollingMinimumGrade
                        ? CampaignResourceTerrainForm.Rolling
                        : CampaignResourceTerrainForm.Flat;

        return (form, maximumCardinalGrade);
    }

    public static CampaignResourceTerrainForm Normalize(TerrainForm form) => form switch
    {
        TerrainForm.Flat => CampaignResourceTerrainForm.Flat,
        TerrainForm.Rolling => CampaignResourceTerrainForm.Rolling,
        TerrainForm.Hills => CampaignResourceTerrainForm.Hills,
        TerrainForm.Mountain => CampaignResourceTerrainForm.Mountain,
        TerrainForm.Cliff => CampaignResourceTerrainForm.Cliff,
        _ => throw new ArgumentOutOfRangeException(nameof(form), form, "Unknown version-3 terrain form."),
    };

    private static readonly (int X, int Y)[] CardinalOffsets =
    [
        (0, -1),
        (1, 0),
        (0, 1),
        (-1, 0),
    ];
}
