namespace Kingdom.World.Core.Campaign.Seasons;

[Flags]
public enum CampaignSeasonWaterFeatures : byte
{
    None = 0,
    Sea = 1 << 0,
    Lake = 1 << 1,
    River = 1 << 2,
}

/// <summary>
/// Version-neutral terrain facts copied into an immutable season-generation snapshot.
/// </summary>
public readonly record struct CampaignSeasonTerrainSample(
    CampaignTileType TerrainType,
    string? CustomTerrainId,
    short ElevationMeters,
    CampaignSeasonWaterFeatures WaterFeatures)
{
    public bool IsSea => WaterFeatures.HasFlag(CampaignSeasonWaterFeatures.Sea);

    public bool IsLake => WaterFeatures.HasFlag(CampaignSeasonWaterFeatures.Lake);

    public bool HasRiver => WaterFeatures.HasFlag(CampaignSeasonWaterFeatures.River);

    public void EnsureValid()
    {
        if (!Enum.IsDefined(TerrainType) || TerrainType is CampaignTileType.Water or CampaignTileType.Coastal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TerrainType),
                TerrainType,
                "Season terrain samples require a canonical campaign tile type.");
        }

        const CampaignSeasonWaterFeatures allFeatures =
            CampaignSeasonWaterFeatures.Sea |
            CampaignSeasonWaterFeatures.Lake |
            CampaignSeasonWaterFeatures.River;
        if ((WaterFeatures & ~allFeatures) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(WaterFeatures),
                WaterFeatures,
                "Unknown season water feature.");
        }

        if (IsSea != (TerrainType == CampaignTileType.Sea) ||
            IsLake != (TerrainType == CampaignTileType.Lake))
        {
            throw new ArgumentException(
                "Sea and Lake water features must exactly match the canonical terrain type.",
                nameof(WaterFeatures));
        }

        var terrainCarriesRiver = TerrainType.IsRiver();
        if (HasRiver != terrainCarriesRiver)
        {
            throw new ArgumentException(
                "River water features must exactly match River, Large River, or River Junction terrain.",
                nameof(WaterFeatures));
        }

        if (TerrainType == CampaignTileType.Unassigned && WaterFeatures != CampaignSeasonWaterFeatures.None)
        {
            throw new ArgumentException(
                "Unassigned terrain cannot carry season water features.",
                nameof(WaterFeatures));
        }

        if (CustomTerrainId is null)
        {
            return;
        }

        if (!CampaignSeasonDefinition.IsValidPortableIdentifier(
                CustomTerrainId,
                CampaignCustomTerrainDefinition.MaximumIdentifierLength) ||
            !CampaignCustomTerrainDefinition.IsSupportedBaseType(TerrainType))
        {
            throw new ArgumentException(
                "A custom terrain ID must be portable and use a supported custom-terrain base type.",
                nameof(CustomTerrainId));
        }
    }
}
