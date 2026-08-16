namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceGenerationOverride
{
    public const int MinimumRichnessBias = -30;

    public const int MaximumRichnessBias = 30;

    public CampaignResourceGenerationOverride(
        string resourceId,
        bool enabled,
        int coveragePercent,
        CampaignResourceRichness richness,
        int richnessBias,
        CampaignResourceConcentration concentration,
        int mapPriority)
    {
        ResourceId = resourceId;
        Enabled = enabled;
        CoveragePercent = coveragePercent;
        Richness = richness;
        RichnessBias = richnessBias;
        Concentration = concentration;
        MapPriority = mapPriority;
        EnsureValid();
    }

    public string ResourceId { get; }

    public bool Enabled { get; }

    public int CoveragePercent { get; }

    public CampaignResourceRichness Richness { get; }

    public int RichnessBias { get; }

    public CampaignResourceConcentration Concentration { get; }

    public int MapPriority { get; }

    public void EnsureValid()
    {
        if (!CampaignResourceDefinition.IsValidIdentifier(ResourceId))
        {
            throw new ArgumentException("Resource generation override has an invalid resource ID.", nameof(ResourceId));
        }

        if (CoveragePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CoveragePercent),
                CoveragePercent,
                "Resource coverage must be from 0 through 100 percent.");
        }

        if (!Enum.IsDefined(Richness))
        {
            throw new ArgumentOutOfRangeException(nameof(Richness), Richness, "Unknown resource richness.");
        }

        if (RichnessBias is < MinimumRichnessBias or > MaximumRichnessBias)
        {
            throw new ArgumentOutOfRangeException(
                nameof(RichnessBias),
                RichnessBias,
                $"Resource richness bias must be from {MinimumRichnessBias} through {MaximumRichnessBias}.");
        }

        if (!Enum.IsDefined(Concentration))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Concentration),
                Concentration,
                "Unknown resource concentration.");
        }

        if (MapPriority is < CampaignResourceDefinition.MinimumMapPriority or
            > CampaignResourceDefinition.MaximumMapPriority)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MapPriority),
                MapPriority,
                $"Resource map priority must be from {CampaignResourceDefinition.MinimumMapPriority} " +
                $"through {CampaignResourceDefinition.MaximumMapPriority}.");
        }
    }
}

public readonly record struct CampaignResourceEffectiveGenerationSettings(
    bool Enabled,
    int CoveragePercent,
    CampaignResourceRichness Richness,
    int RichnessBias,
    CampaignResourceConcentration Concentration,
    int MapPriority);

public sealed class CampaignResourceGenerationSettings
{
    public const int CurrentSchemaVersion = 1;

    public const int MaximumActiveGeneratedResources = 256;

    private readonly Dictionary<string, CampaignResourceGenerationOverride> _byId;

    public CampaignResourceGenerationSettings(
        int resourceSeed,
        bool seedDerivedFromWorld = true,
        CampaignResourceAbundance abundance = CampaignResourceAbundance.Balanced,
        CampaignResourceClimateProfile climate = CampaignResourceClimateProfile.AutoMixed,
        CampaignResourceGeologyProfile geology = CampaignResourceGeologyProfile.AutoMixed,
        IEnumerable<CampaignResourceGenerationOverride>? overrides = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        SchemaVersion = schemaVersion;
        ResourceSeed = resourceSeed;
        SeedDerivedFromWorld = seedDerivedFromWorld;
        Abundance = abundance;
        Climate = climate;
        Geology = geology;
        _byId = new Dictionary<string, CampaignResourceGenerationOverride>(StringComparer.Ordinal);
        foreach (var value in overrides ?? [])
        {
            if (value is null)
            {
                throw new ArgumentException("Resource generation overrides cannot contain null values.", nameof(overrides));
            }

            value.EnsureValid();
            if (!_byId.TryAdd(value.ResourceId, value))
            {
                throw new ArgumentException(
                    $"Resource generation override '{value.ResourceId}' appears more than once.",
                    nameof(overrides));
            }
        }

        Overrides = Array.AsReadOnly(_byId.Values
            .OrderBy(static value => value.ResourceId, StringComparer.Ordinal)
            .ToArray());
        EnsureBasicSettingsValid();
    }

    public int SchemaVersion { get; }

    public int ResourceSeed { get; }

    public bool SeedDerivedFromWorld { get; }

    public CampaignResourceAbundance Abundance { get; }

    public CampaignResourceClimateProfile Climate { get; }

    public CampaignResourceGeologyProfile Geology { get; }

    public IReadOnlyList<CampaignResourceGenerationOverride> Overrides { get; }

    public void EnsureValid(CampaignResourceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        EnsureBasicSettingsValid();
        foreach (var value in Overrides)
        {
            if (!catalog.Contains(value.ResourceId))
            {
                throw new ArgumentException(
                    $"Resource generation override references unknown resource '{value.ResourceId}'.",
                    nameof(catalog));
            }
        }

        var activeCount = catalog.Definitions.Count(definition =>
        {
            var effective = GetEffective(definition);
            return effective.Enabled && effective.CoveragePercent > 0;
        });
        if (activeCount > MaximumActiveGeneratedResources)
        {
            throw new ArgumentException(
                $"A generation run can enable at most {MaximumActiveGeneratedResources} resources with positive coverage; " +
                $"this configuration enables {activeCount}.",
                nameof(catalog));
        }
    }

    public CampaignResourceEffectiveGenerationSettings GetEffective(
        CampaignResourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_byId.TryGetValue(definition.Id, out var value))
        {
            return new CampaignResourceEffectiveGenerationSettings(
                value.Enabled,
                value.CoveragePercent,
                value.Richness,
                value.RichnessBias,
                value.Concentration,
                value.MapPriority);
        }

        return new CampaignResourceEffectiveGenerationSettings(
            true,
            definition.CoveragePercent,
            definition.Richness,
            0,
            definition.Concentration,
            definition.MapPriority);
    }

    private void EnsureBasicSettingsValid()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SchemaVersion),
                SchemaVersion,
                $"Resource generation settings version must be {CurrentSchemaVersion}.");
        }

        if (!Enum.IsDefined(Abundance))
        {
            throw new ArgumentOutOfRangeException(nameof(Abundance), Abundance, "Unknown resource abundance.");
        }

        if (!Enum.IsDefined(Climate))
        {
            throw new ArgumentOutOfRangeException(nameof(Climate), Climate, "Unknown resource climate profile.");
        }

        if (!Enum.IsDefined(Geology))
        {
            throw new ArgumentOutOfRangeException(nameof(Geology), Geology, "Unknown resource geology profile.");
        }
    }
}
