namespace Kingdom.World.Core.Campaign.Generation;

public readonly record struct CampaignMapGenerationOptions(
    CampaignMapGenerationPreset Preset,
    int Seed,
    CampaignMapTerrainStyle TerrainStyle = CampaignMapTerrainStyle.Balanced,
    CampaignMapHydrology Hydrology = CampaignMapHydrology.Balanced,
    CampaignMapMountainDensity MountainDensity = CampaignMapMountainDensity.Sparse,
    CampaignMapLandMix? LandMix = null,
    CampaignMapTidalInlets TidalInlets = CampaignMapTidalInlets.None,
    IReadOnlyList<CampaignCustomTerrainDefinition>? CustomTerrainDefinitions = null,
    CampaignMapCoastlineStyle CoastlineStyle = CampaignMapCoastlineStyle.Natural)
{
    public static CampaignMapGenerationOptions Blank => new(
        CampaignMapGenerationPreset.Blank,
        0,
        CampaignMapTerrainStyle.Balanced,
        CampaignMapHydrology.None,
        CampaignMapMountainDensity.Sparse,
        LandMix: null,
        TidalInlets: CampaignMapTidalInlets.None,
        CustomTerrainDefinitions: null,
        CoastlineStyle: CampaignMapCoastlineStyle.Natural);
}
