namespace Kingdom.World.Core.Campaign.Generation;

/// <summary>
/// Target percentages for the default inland terrain categories after water, Rivers, and water-facing shore tiles are excluded.
/// When custom terrain receives a generated share, these values form the remaining part of the same inland mix.
/// Geographical suitability can cap Mountain, Desert, and Steppe output; any unmet share becomes Plains.
/// </summary>
public readonly record struct CampaignMapLandMix(
    int PlainsPercent,
    int ForestPercent,
    int DesertPercent,
    int HillsPercent,
    int MountainPercent,
    int SteppePercent = 0)
{
    public const int RequiredTotalPercent = 100;
    public const int MaximumMountainPercent = 12;

    public static CampaignMapLandMix Balanced => new(
        PlainsPercent: 40,
        ForestPercent: 25,
        DesertPercent: 8,
        HillsPercent: 13,
        MountainPercent: 2,
        SteppePercent: 12);

    public int TotalPercent =>
        PlainsPercent + ForestPercent + DesertPercent + HillsPercent + MountainPercent + SteppePercent;

    /// <summary>
    /// Ensures each default category is within its independently valid range.
    /// This is used when custom terrain types occupy part of the same 100% inland mix.
    /// </summary>
    public void EnsureValuesValid()
    {
        EnsurePercent(nameof(PlainsPercent), PlainsPercent, 100);
        EnsurePercent(nameof(ForestPercent), ForestPercent, 100);
        EnsurePercent(nameof(DesertPercent), DesertPercent, 100);
        EnsurePercent(nameof(HillsPercent), HillsPercent, 100);
        EnsurePercent(nameof(MountainPercent), MountainPercent, MaximumMountainPercent);
        EnsurePercent(nameof(SteppePercent), SteppePercent, 100);
    }

    /// <summary>
    /// Ensures this standalone default-terrain mix fills the complete inland pool.
    /// </summary>
    public void EnsureValid()
    {
        EnsureValuesValid();

        if (TotalPercent != RequiredTotalPercent)
        {
            throw new ArgumentException(
                $"Custom inland tile ratios must total {RequiredTotalPercent}%; current total is {TotalPercent}%.",
                nameof(CampaignMapLandMix));
        }
    }

    private static void EnsurePercent(string name, int value, int maximum)
    {
        if (value < 0 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                $"{name.Replace("Percent", string.Empty)} must be between 0% and {maximum}%.");
        }
    }
}
