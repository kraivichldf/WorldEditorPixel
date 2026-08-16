namespace Kingdom.World.Core.Campaign.Generation;

/// <summary>
/// Controls the number and bounded coverage of coherent mountain systems during generation.
/// </summary>
public enum CampaignMapMountainDensity : byte
{
    /// <summary>One small, focused mountain system.</summary>
    Sparse = 0,

    /// <summary>A few regular mountain systems across suitable highland.</summary>
    Balanced = 1,

    /// <summary>Several broader mountain systems, still with bounded coverage.</summary>
    Dense = 2,
}
