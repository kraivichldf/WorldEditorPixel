namespace Kingdom.World.Core.Campaign.Generation;

/// <summary>
/// The amount of campaign-scale sea-connected tidal inlets carved into an eligible generated coast.
/// </summary>
public enum CampaignMapTidalInlets : byte
{
    None = 0,
    Few = 1,
    Balanced = 2,
    Drowned = 3,
}
