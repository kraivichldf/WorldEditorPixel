namespace Kingdom.World.Core.Campaign.Generation;

public sealed record CampaignMapGenerationResult(
    CampaignMapGenerationPreset Preset,
    int Seed,
    CampaignMapTerrainStyle TerrainStyle,
    CampaignMapMountainDensity MountainDensity,
    CampaignMapHydrology Hydrology,
    IReadOnlyList<CampaignTileEntry> Tiles,
    int LandTileCount,
    int SeaTileCount,
    int LakeTileCount,
    int RiverTileCount,
    int CliffTileCount)
{
    public int GeneratedTileCount => Tiles.Count;

    /// <summary>
    /// The subset of <see cref="RiverTileCount"/> classified as broad downstream Large River corridors.
    /// </summary>
    public int LargeRiverTileCount { get; init; }

    /// <summary>
    /// The subset of <see cref="RiverTileCount"/> represented as explicit three-exit confluences.
    /// </summary>
    public int RiverJunctionTileCount { get; init; }

    /// <summary>
    /// The requested custom inland mix, or <see langword="null"/> when the normal terrain heuristics were used.
    /// </summary>
    public CampaignMapLandMix? RequestedLandMix { get; init; }

    /// <summary>
    /// The requested campaign-scale tidal-inlet treatment after preset safeguards are applied.
    /// </summary>
    public CampaignMapTidalInlets TidalInlets { get; init; } = CampaignMapTidalInlets.None;

    /// <summary>
    /// The requested directional-coast character. Non-directional presets retain the value for reproducibility but ignore it.
    /// </summary>
    public CampaignMapCoastlineStyle CoastlineStyle { get; init; } = CampaignMapCoastlineStyle.Natural;

    /// <summary>
    /// The safe land-only custom terrain definitions available to this generated world.
    /// </summary>
    public IReadOnlyList<CampaignCustomTerrainDefinition> CustomTerrainDefinitions { get; init; } = [];

    /// <summary>
    /// The number of generated tiles carrying a custom terrain identity.
    /// </summary>
    public int CustomTerrainTileCount { get; init; }

    /// <summary>
    /// Number of deterministic tectonic provinces used to shape uplift, rifts, and shear belts.
    /// </summary>
    public int TectonicProvinceCount { get; init; }

    /// <summary>
    /// Number of thermal-relaxation and fluvial-erosion passes applied before final hydrology.
    /// </summary>
    public int ErosionPassCount { get; init; }
}
