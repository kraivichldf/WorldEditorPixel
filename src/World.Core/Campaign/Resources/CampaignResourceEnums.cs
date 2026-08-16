namespace Kingdom.World.Core.Campaign.Resources;

public enum CampaignResourceCategory
{
    Renewable = 0,
    Finite = 1,
}

public enum CampaignResourceMedium
{
    Land = 0,
    Water = 1,
    Either = 2,
}

public enum CampaignResourceDistributionProfile
{
    Field = 0,
    Vein = 1,
    Basin = 2,
    SurfaceDeposit = 3,
    Aquatic = 4,
}

public enum CampaignResourceRichness
{
    Poor = 0,
    Balanced = 1,
    Rich = 2,
}

public enum CampaignResourceConcentration
{
    FewLarge = 0,
    Balanced = 1,
    ManySmall = 2,
}

public enum CampaignResourceAbundance
{
    Sparse = 0,
    Balanced = 1,
    Abundant = 2,
    Custom = 3,
}

public enum CampaignResourceClimateProfile
{
    AutoMixed = 0,
    Tropical = 1,
    Temperate = 2,
    Continental = 3,
    Arid = 4,
    Cold = 5,
}

public enum CampaignResourceGeologyProfile
{
    AutoMixed = 0,
    AncientCraton = 1,
    VolcanicArc = 2,
    SedimentaryBasins = 3,
    FoldBelt = 4,
    YoungRift = 5,
}
