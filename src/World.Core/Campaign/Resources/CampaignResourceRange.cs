namespace Kingdom.World.Core.Campaign.Resources;

public readonly record struct CampaignResourceRange(double Minimum, double Maximum)
{
    public static CampaignResourceRange NonNegative(double minimum, double maximum)
    {
        var range = new CampaignResourceRange(minimum, maximum);
        range.EnsureValid(requireNonNegative: true);
        return range;
    }

    public void EnsureValid(bool requireNonNegative = false, string? name = null)
    {
        name ??= "Resource range";
        if (!double.IsFinite(Minimum) || !double.IsFinite(Maximum))
        {
            throw new ArgumentException($"{name} values must be finite.", name);
        }

        if (Minimum > Maximum)
        {
            throw new ArgumentException($"{name} minimum cannot exceed its maximum.", name);
        }

        if (requireNonNegative && Minimum < 0)
        {
            throw new ArgumentOutOfRangeException(name, Minimum, $"{name} cannot be negative.");
        }
    }
}
