namespace Kingdom.World.Core.Campaign.Seasons;

public readonly record struct CampaignSeasonRange(double Minimum, double Maximum)
{
    public bool Contains(double value) =>
        double.IsFinite(value) && value >= Minimum && value <= Maximum;

    public void EnsureValid(
        string? name = null,
        double? allowedMinimum = null,
        double? allowedMaximum = null)
    {
        name ??= "Season range";
        if (!double.IsFinite(Minimum) || !double.IsFinite(Maximum))
        {
            throw new ArgumentException($"{name} values must be finite.", name);
        }

        if (Minimum > Maximum)
        {
            throw new ArgumentException($"{name} minimum cannot exceed its maximum.", name);
        }

        if (allowedMinimum is { } lowerBound && Minimum < lowerBound)
        {
            throw new ArgumentOutOfRangeException(
                name,
                Minimum,
                $"{name} cannot be lower than {lowerBound}.");
        }

        if (allowedMaximum is { } upperBound && Maximum > upperBound)
        {
            throw new ArgumentOutOfRangeException(
                name,
                Maximum,
                $"{name} cannot be greater than {upperBound}.");
        }
    }
}
