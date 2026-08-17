using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonGenerationSettingsTests
{
    [Fact]
    public void Settings_UseAcceptedPriorityAndLeaveCustomDefinitionsManualOnly()
    {
        var catalog = new CampaignSeasonCatalog(
            [CampaignSeasonCatalogTests.CreateCustom("monsoon")]);
        var settings = new CampaignSeasonGenerationSettings(seasonSeed: 17_029);

        settings.EnsureValid(catalog, CreateDefinition());

        Assert.Equal(
        [
            "winter",
            "spring",
            "autumn",
            "summer",
        ], settings.PriorityIds);
        Assert.Equal("summer", settings.CatchAllSeasonId);
        Assert.False(settings.IsGenerationEnabled("monsoon"));
        Assert.True(settings.IsGenerationEnabled("winter"));
        Assert.Equal(CampaignSeasonCoverageMode.WholeGlobe, settings.CoverageMode);
        Assert.Equal(CampaignSeasonGenerationSettings.EarthAxialTiltDegrees, settings.AxialTiltDegrees);
    }

    [Fact]
    public void Settings_AllowCustomCatchAllAndExposePriorityDefinitions()
    {
        var monsoon = CampaignSeasonCatalogTests.CreateCustom("monsoon");
        var catalog = new CampaignSeasonCatalog([monsoon]);
        var settings = new CampaignSeasonGenerationSettings(
            seasonSeed: 4,
            priorityIds: ["winter", "monsoon"]);

        settings.EnsureValid(catalog);

        Assert.Equal("monsoon", settings.CatchAllSeasonId);
        Assert.Same(monsoon, settings.GetPriorityDefinitions(catalog)[1]);
    }

    [Fact]
    public void Settings_RejectEmptyDuplicateUnknownAndMoreThan256PriorityEntries()
    {
        Assert.Throws<ArgumentException>(() => new CampaignSeasonGenerationSettings(
            seasonSeed: 1,
            priorityIds: []));
        Assert.Throws<ArgumentException>(() => new CampaignSeasonGenerationSettings(
            seasonSeed: 1,
            priorityIds: ["winter", "winter"]));

        var unknown = new CampaignSeasonGenerationSettings(
            seasonSeed: 1,
            priorityIds: ["missing-season"]);
        Assert.Throws<ArgumentException>(() => unknown.EnsureValid(new CampaignSeasonCatalog()));

        var tooMany = Enumerable.Range(0, 257)
            .Select(static index => $"season-{index:D3}")
            .ToArray();
        Assert.Throws<ArgumentException>(() => new CampaignSeasonGenerationSettings(
            seasonSeed: 1,
            priorityIds: tooMany));
    }

    [Fact]
    public void Settings_Accept256EnabledAndALargerManualOnlyCatalog()
    {
        var customs = Enumerable.Range(0, 300)
            .Select(static index => CampaignSeasonCatalogTests.CreateCustom($"season-{index:D3}"))
            .ToArray();
        var catalog = new CampaignSeasonCatalog(customs);
        var enabled = new[]
        {
            "winter",
            "spring",
            "autumn",
            "summer",
        }.Concat(customs.Take(252).Select(static value => value.Id));
        var settings = new CampaignSeasonGenerationSettings(
            seasonSeed: 4,
            priorityIds: enabled);

        settings.EnsureValid(catalog);

        Assert.Equal(256, settings.PriorityIds.Count);
        Assert.Equal(304, catalog.Definitions.Count);
        Assert.False(settings.IsGenerationEnabled(customs[^1].Id));
    }

    [Fact]
    public void RegionalCoverage_UsesPhysicalHeightAndRejectsPoleCrossing()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 10_000_000,
            worldHeightMeters: 10_000_000,
            campaignTileSizeMeters: 20_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var equatorial = new CampaignSeasonGenerationSettings(
            seasonSeed: 1,
            coverageMode: CampaignSeasonCoverageMode.Regional,
            regionalCenterLatitudeDegrees: 0);

        equatorial.EnsureValid(new CampaignSeasonCatalog(), definition);
        var span = equatorial.GetRegionalLatitudeSpan(definition);
        Assert.Equal(-44.966, span.MinimumLatitude, precision: 3);
        Assert.Equal(44.966, span.MaximumLatitude, precision: 3);

        var crossing = new CampaignSeasonGenerationSettings(
            seasonSeed: 1,
            coverageMode: CampaignSeasonCoverageMode.Regional,
            regionalCenterLatitudeDegrees: 60);
        var exception = Assert.Throws<ArgumentException>(() =>
            crossing.EnsureValid(new CampaignSeasonCatalog(), definition));
        Assert.Contains("crosses a pole", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Settings_RejectCoverageAndClimateContradictions()
    {
        Assert.Throws<ArgumentException>(() => new CampaignSeasonGenerationSettings(
            seasonSeed: 1,
            coverageMode: CampaignSeasonCoverageMode.WholeGlobe,
            regionalCenterLatitudeDegrees: 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonGenerationSettings(
            seasonSeed: 1,
            coverageMode: CampaignSeasonCoverageMode.Regional));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonGenerationSettings(
            seasonSeed: 1,
            axialTiltDegrees: 91));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonClimateSettings(
            lapseRateCelsiusPerKilometer: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonClimateSettings(
            seaMaritimeRadiusKilometers: 0));
    }

    [Fact]
    public void Scope_AllAndAreaUseCompleteTileBounds()
    {
        var definition = CreateDefinition();
        var all = CampaignSeasonGenerationScope.All;
        var area = CampaignSeasonGenerationScope.ForArea(new CampaignTileArea(1, 2, 3, 4));

        all.EnsureValid(definition);
        area.EnsureValid(definition);
        Assert.True(all.Includes(7, 7));
        Assert.True(area.Includes(1, 2));
        Assert.True(area.Includes(3, 4));
        Assert.False(area.Includes(0, 2));
        Assert.Equal(
            area,
            CampaignSeasonGenerationScope.ForArea(new CampaignTileArea(1, 2, 3, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CampaignSeasonGenerationScope.ForArea(new CampaignTileArea(0, 0, 8, 8))
                .EnsureValid(definition));
    }

    private static CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: 8_000,
            worldHeightMeters: 8_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
}
