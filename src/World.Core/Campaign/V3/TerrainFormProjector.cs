namespace Kingdom.World.Core.Campaign.V3;

public static class TerrainFormProjector
{
    private static readonly CardinalDirection[] CardinalDirections =
    [
        CardinalDirection.North,
        CardinalDirection.East,
        CardinalDirection.South,
        CardinalDirection.West,
    ];

    public static TerrainForm Project(
        CampaignTileMapV3 tiles,
        int x,
        int y,
        TerrainFormProfile profile) =>
        Analyze(tiles, x, y, profile).Form;

    public static TerrainFormAnalysis Analyze(
        CampaignTileMapV3 tiles,
        int x,
        int y,
        TerrainFormProfile profile)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(profile);
        tiles.EnsureValidCoordinate(x, y);
        profile.EnsureValid();

        var centerHeight = tiles.GetTileUnchecked(x, y).HeightMeters;
        var maximumCardinalGrade = 0.0;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = Math.Clamp(x + direction.OffsetX(), 0, tiles.Definition.TilesX - 1);
            var neighborY = Math.Clamp(y + direction.OffsetY(), 0, tiles.Definition.TilesY - 1);
            var neighborHeight = tiles.GetTileUnchecked(neighborX, neighborY).HeightMeters;
            var grade = Math.Abs(centerHeight - neighborHeight) /
                        (double)tiles.Definition.CampaignTileSizeMeters;
            maximumCardinalGrade = Math.Max(maximumCardinalGrade, grade);
        }

        var minimumHeight = (int)centerHeight;
        var maximumHeight = (int)centerHeight;
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var sampleX = Math.Clamp(x + offsetX, 0, tiles.Definition.TilesX - 1);
                var sampleY = Math.Clamp(y + offsetY, 0, tiles.Definition.TilesY - 1);
                var sampleHeight = (int)tiles.GetTileUnchecked(sampleX, sampleY).HeightMeters;
                minimumHeight = Math.Min(minimumHeight, sampleHeight);
                maximumHeight = Math.Max(maximumHeight, sampleHeight);
            }
        }

        var localRelief = maximumHeight - minimumHeight;
        var localProminence = centerHeight - minimumHeight;
        var form = Classify(
            centerHeight,
            tiles.Definition.SeaLevelMeters,
            maximumCardinalGrade,
            localProminence,
            profile);

        return new TerrainFormAnalysis(
            form,
            maximumCardinalGrade,
            localRelief,
            localProminence);
    }

    private static TerrainForm Classify(
        short centerHeight,
        short seaLevel,
        double maximumCardinalGrade,
        int localProminence,
        TerrainFormProfile profile)
    {
        if (maximumCardinalGrade >= profile.CliffMinimumGrade)
        {
            return TerrainForm.Cliff;
        }

        if (maximumCardinalGrade >= profile.MountainMinimumGrade ||
            localProminence >= profile.MountainMinimumProminenceMeters ||
            centerHeight >= seaLevel + profile.MountainMinimumElevationAboveSeaMeters)
        {
            return TerrainForm.Mountain;
        }

        if (maximumCardinalGrade >= profile.HillsMinimumGrade)
        {
            return TerrainForm.Hills;
        }

        return maximumCardinalGrade >= profile.RollingMinimumGrade
            ? TerrainForm.Rolling
            : TerrainForm.Flat;
    }
}
