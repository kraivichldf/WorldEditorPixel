using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceGenerationSettingsTests
{
    [Fact]
    public void Settings_UseDefinitionDefaultsAndApplyExplicitManualOnlyOverride()
    {
        var catalog = new CampaignResourceCatalog();
        var settings = new CampaignResourceGenerationSettings(
            resourceSeed: 17_029,
            overrides:
            [
                new CampaignResourceGenerationOverride(
                    "iron-ore",
                    enabled: true,
                    coveragePercent: 0,
                    CampaignResourceRichness.Rich,
                    richnessBias: 12,
                    CampaignResourceConcentration.FewLarge,
                    mapPriority: 90),
            ]);

        settings.EnsureValid(catalog);
        var iron = settings.GetEffective(catalog.Get("iron-ore"));
        var timber = settings.GetEffective(catalog.Get("timber"));

        Assert.True(iron.Enabled);
        Assert.Equal(0, iron.CoveragePercent);
        Assert.Equal(12, iron.RichnessBias);
        Assert.Equal(65, timber.CoveragePercent);
        Assert.Equal(CampaignResourceRichness.Balanced, timber.Richness);
    }

    [Fact]
    public void Settings_RejectDuplicateAndUnknownOverrides()
    {
        var first = CreateOverride("gold");
        Assert.Throws<ArgumentException>(() => new CampaignResourceGenerationSettings(
            resourceSeed: 1,
            overrides: [first, first]));

        var settings = new CampaignResourceGenerationSettings(
            resourceSeed: 1,
            overrides: [CreateOverride("missing-resource")]);
        var exception = Assert.Throws<ArgumentException>(() =>
            settings.EnsureValid(new CampaignResourceCatalog()));
        Assert.Contains("unknown resource", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Settings_ExposeANonMutableOverrideView()
    {
        var settings = new CampaignResourceGenerationSettings(
            resourceSeed: 1,
            overrides: [CreateOverride("gold")]);
        var overrides = Assert.IsAssignableFrom<IList<CampaignResourceGenerationOverride>>(
            settings.Overrides);

        Assert.Throws<NotSupportedException>(() => overrides[0] = CreateOverride("silver"));
    }

    [Fact]
    public void Settings_RejectMoreThan256PositiveCoverageResources()
    {
        var customs = Enumerable.Range(0, 241)
            .Select(index => CampaignResourceCatalogTests.CreateCustom($"custom-{index:D3}", coverage: 1))
            .ToArray();
        var catalog = new CampaignResourceCatalog(customs);
        var settings = new CampaignResourceGenerationSettings(resourceSeed: 4);

        var exception = Assert.Throws<ArgumentException>(() => settings.EnsureValid(catalog));
        Assert.Contains("at most 256", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_AcceptLargeManualOnlyCatalog()
    {
        var customs = Enumerable.Range(0, 300)
            .Select(index => CampaignResourceCatalogTests.CreateCustom($"manual-{index:D3}", coverage: 0))
            .ToArray();
        var catalog = new CampaignResourceCatalog(customs);
        var settings = new CampaignResourceGenerationSettings(resourceSeed: 4);

        settings.EnsureValid(catalog);
        Assert.Equal(316, catalog.Definitions.Count);
    }

    [Fact]
    public void Override_RejectsInvalidBiasAndUnknownEnums()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignResourceGenerationOverride(
            "gold",
            enabled: true,
            coveragePercent: 2,
            CampaignResourceRichness.Balanced,
            richnessBias: 31,
            CampaignResourceConcentration.ManySmall,
            mapPriority: 50));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignResourceGenerationSettings(
            resourceSeed: 1,
            climate: (CampaignResourceClimateProfile)99));
    }

    private static CampaignResourceGenerationOverride CreateOverride(string id) =>
        new(
            id,
            enabled: true,
            coveragePercent: 2,
            CampaignResourceRichness.Balanced,
            richnessBias: 0,
            CampaignResourceConcentration.ManySmall,
            mapPriority: 50);
}
