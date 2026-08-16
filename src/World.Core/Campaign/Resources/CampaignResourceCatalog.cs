namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceCatalog
{
    public const int MaximumDefinitionCount = ushort.MaxValue;

    private static readonly IReadOnlyList<CampaignResourceDefinition> BuiltIns = CreateBuiltIns();
    private static readonly HashSet<string> BuiltInIds = BuiltIns
        .Select(static definition => definition.Id)
        .ToHashSet(StringComparer.Ordinal);

    private readonly Dictionary<string, CampaignResourceDefinition> _byId;

    public CampaignResourceCatalog(IEnumerable<CampaignResourceDefinition>? customDefinitions = null)
    {
        var custom = customDefinitions?.ToArray() ?? [];
        if (BuiltIns.Count + custom.Length > MaximumDefinitionCount)
        {
            throw new ArgumentException(
                $"A resource catalog can contain at most {MaximumDefinitionCount:N0} definitions.",
                nameof(customDefinitions));
        }

        _byId = new Dictionary<string, CampaignResourceDefinition>(StringComparer.Ordinal);
        foreach (var definition in BuiltIns.Concat(custom))
        {
            if (definition is null)
            {
                throw new ArgumentException("Resource definitions cannot contain null values.", nameof(customDefinitions));
            }

            definition.EnsureValid();
            if (!_byId.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException(
                    $"Resource ID '{definition.Id}' is defined more than once or conflicts with a built-in resource.",
                    nameof(customDefinitions));
            }
        }

        Definitions = Array.AsReadOnly(_byId.Values
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray());
        CustomDefinitions = Array.AsReadOnly(custom
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<CampaignResourceDefinition> Definitions { get; }

    public IReadOnlyList<CampaignResourceDefinition> CustomDefinitions { get; }

    public static IReadOnlyList<CampaignResourceDefinition> BuiltInDefinitions => BuiltIns;

    public bool Contains(string resourceId) =>
        resourceId is not null && _byId.ContainsKey(resourceId);

    public bool IsBuiltIn(string resourceId) =>
        resourceId is not null && BuiltInIds.Contains(resourceId);

    public CampaignResourceDefinition Get(string resourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        return _byId.TryGetValue(resourceId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown resource ID '{resourceId}'.");
    }

    public bool TryGet(string? resourceId, out CampaignResourceDefinition definition)
    {
        if (resourceId is not null && _byId.TryGetValue(resourceId, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    private static IReadOnlyList<CampaignResourceDefinition> CreateBuiltIns()
    {
        const int mapPriority = 50;
        CampaignResourceDefinition[] definitions =
        [
            BuiltIn("fertile-land", "Fertile Land", CampaignResourceCategory.Renewable,
                CampaignResourceDistributionProfile.Field, CampaignResourceMedium.Land,
                "grain", "#A8B85C", mapPriority, 45, CampaignResourceConcentration.FewLarge,
                ["freshwater", "lowland", "moist"], ["arid", "exposed-rock"],
                [CampaignResourceSurfaceType.Desert, CampaignResourceSurfaceType.BarrenRock,
                    CampaignResourceSurfaceType.Tundra]),
            BuiltIn("timber", "Timber", CampaignResourceCategory.Renewable,
                CampaignResourceDistributionProfile.Field, CampaignResourceMedium.Land,
                "tree", "#2F684F", mapPriority, 65, CampaignResourceConcentration.FewLarge,
                ["biomass", "forest", "moist"], ["arid", "open-land"],
                [CampaignResourceSurfaceType.Desert, CampaignResourceSurfaceType.BarrenRock,
                    CampaignResourceSurfaceType.Tundra]),
            BuiltIn("fresh-water", "Fresh Water", CampaignResourceCategory.Renewable,
                CampaignResourceDistributionProfile.Field, CampaignResourceMedium.Land,
                "water", "#3C92C3", mapPriority, 35, CampaignResourceConcentration.Balanced,
                ["groundwater", "lake", "river"], ["arid"]),
            BuiltIn("fish", "Fish", CampaignResourceCategory.Renewable,
                CampaignResourceDistributionProfile.Aquatic, CampaignResourceMedium.Water,
                "fish", "#2F7FB3", mapPriority, 55, CampaignResourceConcentration.FewLarge,
                ["aquatic", "coast", "lake"]),
            BuiltIn("grazing", "Grazing", CampaignResourceCategory.Renewable,
                CampaignResourceDistributionProfile.Field, CampaignResourceMedium.Land,
                "animal", "#8CAB58", mapPriority, 50, CampaignResourceConcentration.FewLarge,
                ["lowland", "open-land"], ["forest", "relief"]),
            BuiltIn("wild-game", "Wild Game", CampaignResourceCategory.Renewable,
                CampaignResourceDistributionProfile.Field, CampaignResourceMedium.Land,
                "animal", "#8A6A45", mapPriority, 35, CampaignResourceConcentration.ManySmall,
                ["biomass", "ecotone", "freshwater"], ["arid", "exposed-rock"]),
            BuiltIn("stone", "Stone", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.SurfaceDeposit, CampaignResourceMedium.Land,
                "stone", "#777777", mapPriority, 40, CampaignResourceConcentration.Balanced,
                ["erosion", "exposed-rock", "relief"]),
            BuiltIn("clay", "Clay", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.SurfaceDeposit, CampaignResourceMedium.Land,
                "clay", "#A66B4F", mapPriority, 25, CampaignResourceConcentration.Balanced,
                ["lake", "lowland", "river"], ["exposed-rock", "relief"]),
            BuiltIn("sand-gravel", "Sand and Gravel", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.SurfaceDeposit, CampaignResourceMedium.Land,
                "sand", "#C9B66E", mapPriority, 35, CampaignResourceConcentration.ManySmall,
                ["arid", "coast", "river"], ["forest"]),
            BuiltIn("salt", "Salt", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.Basin, CampaignResourceMedium.Land,
                "crystal", "#E3E0D8", mapPriority, 12, CampaignResourceConcentration.FewLarge,
                ["arid", "coast", "sedimentary"], ["freshwater", "moist"]),
            BuiltIn("iron-ore", "Iron Ore", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.Vein, CampaignResourceMedium.Land,
                "ore", "#7C5149", mapPriority, 10, CampaignResourceConcentration.Balanced,
                ["fold-belt", "mineralized", "old-crust"]),
            BuiltIn("copper-ore", "Copper Ore", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.Vein, CampaignResourceMedium.Land,
                "ore", "#B56C3B", mapPriority, 7, CampaignResourceConcentration.Balanced,
                ["hydrothermal", "rift", "volcanic"]),
            BuiltIn("tin-ore", "Tin Ore", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.Vein, CampaignResourceMedium.Land,
                "ore", "#8A9AA1", mapPriority, 4, CampaignResourceConcentration.FewLarge,
                ["granitic", "mineralized", "old-crust"]),
            BuiltIn("coal", "Coal", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.Basin, CampaignResourceMedium.Land,
                "coal", "#2F3032", mapPriority, 8, CampaignResourceConcentration.FewLarge,
                ["biomass", "burial", "sedimentary"]),
            BuiltIn("gold", "Gold", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.Vein, CampaignResourceMedium.Land,
                "ore", "#D1A62A", mapPriority, 2, CampaignResourceConcentration.ManySmall,
                ["hydrothermal", "mineralized", "shear"]),
            BuiltIn("silver", "Silver", CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.Vein, CampaignResourceMedium.Land,
                "ore", "#AEB8C2", mapPriority, 3, CampaignResourceConcentration.ManySmall,
                ["hydrothermal", "mineralized", "shear"]),
        ];

        return Array.AsReadOnly(definitions);
    }

    private static CampaignResourceDefinition BuiltIn(
        string id,
        string name,
        CampaignResourceCategory category,
        CampaignResourceDistributionProfile profile,
        CampaignResourceMedium medium,
        string symbol,
        string color,
        int mapPriority,
        int coverage,
        CampaignResourceConcentration concentration,
        IReadOnlyList<string> preferredTags,
        IReadOnlyList<string>? avoidedTags = null,
        IReadOnlyList<CampaignResourceSurfaceType>? excludedSurfaces = null) =>
        new(
            id,
            name,
            category,
            profile,
            medium,
            symbol,
            color,
            mapPriority,
            coverage,
            CampaignResourceRichness.Balanced,
            concentration,
            new CampaignResourceRuleSet(
                medium,
                preferredTerrainTags: preferredTags,
                avoidedTerrainTags: avoidedTags,
                excludedTerrainSurfaces: excludedSurfaces));
}
