namespace Kingdom.World.Core.Campaign.Seasons;

public enum CampaignBuiltInSeason : byte
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3,
}

public enum CampaignSeasonCoverageMode : byte
{
    WholeGlobe = 0,
    Regional = 1,
}

public enum CampaignSeasonGenerationScopeKind : byte
{
    All = 0,
    Area = 1,
}
