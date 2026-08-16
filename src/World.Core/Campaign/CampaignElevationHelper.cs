namespace Kingdom.World.Core.Campaign;

public static class CampaignElevationHelper
{
    private static readonly (int X, int Y)[] CardinalDirections =
    [
        (0, -1),
        (1, 0),
        (0, 1),
        (-1, 0),
    ];

    public static CampaignElevationSuggestion SuggestNearby(
        CampaignTileMap tiles,
        CampaignTileCoordinate target,
        int stepMeters = 10)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        if (stepMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stepMeters),
                stepMeters,
                "The elevation suggestion step must be greater than zero metres.");
        }

        var targetTile = tiles.GetTile(target.X, target.Y);
        long heightTotal = 0;
        var neighborCount = 0;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = target.X + direction.X;
            var neighborY = target.Y + direction.Y;
            if (!tiles.IsValidCoordinate(neighborX, neighborY))
            {
                continue;
            }

            heightTotal += tiles.GetTile(neighborX, neighborY).HeightMeters;
            neighborCount++;
        }

        var sourceHeight = neighborCount == 0
            ? targetTile.HeightMeters
            : (double)heightTotal / neighborCount;
        var steppedHeight = Math.Round(
            sourceHeight / stepMeters,
            MidpointRounding.AwayFromZero) * stepMeters;
        var clampedHeight = Math.Clamp(
            steppedHeight,
            tiles.Definition.MinimumHeightMeters,
            tiles.Definition.MaximumHeightMeters);
        return new CampaignElevationSuggestion((short)clampedHeight, neighborCount);
    }
}

public readonly record struct CampaignElevationSuggestion(
    short HeightMeters,
    int SourceNeighborCount);
