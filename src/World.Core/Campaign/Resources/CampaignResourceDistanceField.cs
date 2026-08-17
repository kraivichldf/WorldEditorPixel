namespace Kingdom.World.Core.Campaign.Resources;

[Flags]
internal enum CampaignResourceWaterSources : byte
{
    None = 0,
    Sea = 1 << 0,
    Lake = 1 << 1,
    River = 1 << 2,
}

internal sealed class CampaignResourceDistanceField
{
    private readonly int _width;
    private readonly double[] _seaDistances;
    private readonly double[] _lakeDistances;
    private readonly double[] _riverDistances;

    public CampaignResourceDistanceField(
        int width,
        int height,
        int tileSizeMeters,
        Func<int, int, CampaignResourceWaterSources> getSources,
        CancellationToken cancellationToken = default)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (tileSizeMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSizeMeters));
        }

        ArgumentNullException.ThrowIfNull(getSources);
        cancellationToken.ThrowIfCancellationRequested();
        _width = width;
        _seaDistances = Build(
            width,
            height,
            tileSizeMeters,
            getSources,
            CampaignResourceWaterSources.Sea,
            cancellationToken);
        _lakeDistances = Build(
            width,
            height,
            tileSizeMeters,
            getSources,
            CampaignResourceWaterSources.Lake,
            cancellationToken);
        _riverDistances = Build(
            width,
            height,
            tileSizeMeters,
            getSources,
            CampaignResourceWaterSources.River,
            cancellationToken);
    }

    public (double Sea, double Lake, double River) GetDistances(int x, int y)
    {
        var index = checked(y * _width + x);
        return (_seaDistances[index], _lakeDistances[index], _riverDistances[index]);
    }

    private static double[] Build(
        int width,
        int height,
        int tileSizeMeters,
        Func<int, int, CampaignResourceWaterSources> getSources,
        CampaignResourceWaterSources source,
        CancellationToken cancellationToken)
    {
        var length = checked(width * height);
        var maximumSquaredDistance =
            (double)(width - 1) * (width - 1) +
            (double)(height - 1) * (height - 1) + 1;
        var values = new double[length];
        var hasSource = false;
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var isSource = (getSources(x, y) & source) != 0;
                values[(y * width) + x] = isSource ? 0 : maximumSquaredDistance;
                hasSource |= isSource;
            }
        }

        if (!hasSource)
        {
            Array.Fill(values, double.PositiveInfinity);
            return values;
        }

        var workspaceLength = Math.Max(width, height);
        var input = new double[workspaceLength];
        var output = new double[workspaceLength];
        var sites = new int[workspaceLength];
        var boundaries = new double[workspaceLength + 1];

        for (var x = 0; x < width; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var y = 0; y < height; y++)
            {
                input[y] = values[(y * width) + x];
            }

            TransformLine(input, output, height, sites, boundaries);
            for (var y = 0; y < height; y++)
            {
                values[(y * width) + x] = output[y];
            }
        }

        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowStart = y * width;
            Array.Copy(values, rowStart, input, 0, width);
            TransformLine(input, output, width, sites, boundaries);
            Array.Copy(output, 0, values, rowStart, width);
        }

        var tileSizeKilometers = tileSizeMeters / 1_000.0;
        for (var index = 0; index < values.Length; index++)
        {
            if ((index & 16_383) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            values[index] = Math.Sqrt(values[index]) * tileSizeKilometers;
        }

        return values;
    }

    private static void TransformLine(
        double[] input,
        double[] output,
        int length,
        int[] sites,
        double[] boundaries)
    {
        var envelopeIndex = 0;
        sites[0] = 0;
        boundaries[0] = double.NegativeInfinity;
        boundaries[1] = double.PositiveInfinity;

        for (var query = 1; query < length; query++)
        {
            var intersection = GetIntersection(input, sites[envelopeIndex], query);
            while (intersection <= boundaries[envelopeIndex])
            {
                envelopeIndex--;
                intersection = GetIntersection(input, sites[envelopeIndex], query);
            }

            envelopeIndex++;
            sites[envelopeIndex] = query;
            boundaries[envelopeIndex] = intersection;
            boundaries[envelopeIndex + 1] = double.PositiveInfinity;
        }

        envelopeIndex = 0;
        for (var query = 0; query < length; query++)
        {
            while (boundaries[envelopeIndex + 1] < query)
            {
                envelopeIndex++;
            }

            var delta = query - sites[envelopeIndex];
            output[query] = (double)delta * delta + input[sites[envelopeIndex]];
        }
    }

    private static double GetIntersection(double[] values, int left, int right) =>
        ((values[right] + ((double)right * right)) -
         (values[left] + ((double)left * left))) /
        (2.0 * (right - left));
}
