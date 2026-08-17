using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Editor.Dialogs;

namespace Kingdom.World.Tests;

public sealed class CustomSeasonsDialogTests
{
    [Fact]
    public void EditorItem_RoundTripsEveryDefinitionAndRuleField()
    {
        var definition = new CampaignSeasonDefinition(
            "monsoon",
            "Monsoon",
            CampaignBuiltInSeason.Summer,
            "#3F9D78",
            tintStrengthPercent: 62,
            effectIntensityPercent: 73,
            new CampaignSeasonRule(
                latitudeDegrees: new CampaignSeasonRange(-20, 30),
                elevationMeters: new CampaignSeasonRange(0, 2_500),
                temperatureCelsius: new CampaignSeasonRange(18, 42),
                moisture: new CampaignSeasonRange(0.55, 1),
                seasonalIntensity: new CampaignSeasonRange(-0.2, 1),
                seasonalTendency: new CampaignSeasonRange(0.05, 1),
                seaDistanceKilometers: new CampaignSeasonRange(0, 900),
                lakeDistanceKilometers: new CampaignSeasonRange(0, 250),
                riverDistanceKilometers: new CampaignSeasonRange(0, 80),
                terrainIncludes: [CampaignTileType.Plains, CampaignTileType.Forest],
                terrainExcludes: [CampaignTileType.Desert],
                customTerrainIncludes: ["rice-basin"],
                customTerrainExcludes: ["salt-flat"]));

        var item = SeasonDefinitionEditorItem.FromDefinition(
            definition,
            isBuiltIn: false,
            usageCount: 12,
            generationEnabled: true,
            isProjectDefault: true,
            canEditId: false);
        var actual = item.ToDefinition();

        Assert.Equal(definition.Id, actual.Id);
        Assert.Equal(definition.Name, actual.Name);
        Assert.Equal(definition.Fallback, actual.Fallback);
        Assert.Equal(definition.ColorHex, actual.ColorHex);
        Assert.Equal(definition.TintStrengthPercent, actual.TintStrengthPercent);
        Assert.Equal(definition.EffectIntensityPercent, actual.EffectIntensityPercent);
        Assert.Equal(definition.Rule.LatitudeDegrees, actual.Rule.LatitudeDegrees);
        Assert.Equal(definition.Rule.ElevationMeters, actual.Rule.ElevationMeters);
        Assert.Equal(definition.Rule.TemperatureCelsius, actual.Rule.TemperatureCelsius);
        Assert.Equal(definition.Rule.Moisture, actual.Rule.Moisture);
        Assert.Equal(definition.Rule.SeasonalIntensity, actual.Rule.SeasonalIntensity);
        Assert.Equal(definition.Rule.SeasonalTendency, actual.Rule.SeasonalTendency);
        Assert.Equal(definition.Rule.SeaDistanceKilometers, actual.Rule.SeaDistanceKilometers);
        Assert.Equal(definition.Rule.LakeDistanceKilometers, actual.Rule.LakeDistanceKilometers);
        Assert.Equal(definition.Rule.RiverDistanceKilometers, actual.Rule.RiverDistanceKilometers);
        Assert.Equal(definition.Rule.TerrainIncludes, actual.Rule.TerrainIncludes);
        Assert.Equal(definition.Rule.TerrainExcludes, actual.Rule.TerrainExcludes);
        Assert.Equal(definition.Rule.CustomTerrainIncludes, actual.Rule.CustomTerrainIncludes);
        Assert.Equal(definition.Rule.CustomTerrainExcludes, actual.Rule.CustomTerrainExcludes);
        Assert.Contains("project default", item.SourceAndUsageText, StringComparison.Ordinal);
        Assert.False(item.CanEditId);
    }

    [Fact]
    public void RangeParser_UsesInvariantMinMaxAndRejectsMalformedText()
    {
        Assert.Null(CustomSeasonsDialog.ParseRange("   ", "Temperature"));
        Assert.Equal(
            new CampaignSeasonRange(-5.25, 22.5),
            CustomSeasonsDialog.ParseRange(" -5.25 .. 22.5 ", "Temperature"));
        Assert.Equal(
            "-5.25..22.5",
            CustomSeasonsDialog.FormatRange(new CampaignSeasonRange(-5.25, 22.5)));
        Assert.Throws<FormatException>(() =>
            CustomSeasonsDialog.ParseRange("-5,22", "Temperature"));
        Assert.Throws<ArgumentException>(() =>
            CustomSeasonsDialog.ParseRange("22..-5", "Temperature"));
    }

    [Fact]
    public void ReplacementRequirement_CoversTilesDefaultAndGenerationPriority()
    {
        var definition = new CampaignSeasonDefinition(
            "monsoon",
            "Monsoon",
            CampaignBuiltInSeason.Summer,
            "#3F9D78",
            60,
            70);
        var manual = SeasonDefinitionEditorItem.FromDefinition(
            definition,
            isBuiltIn: false,
            usageCount: 0,
            generationEnabled: false);
        var used = SeasonDefinitionEditorItem.FromDefinition(
            definition,
            isBuiltIn: false,
            usageCount: 1,
            generationEnabled: false);
        var enabled = SeasonDefinitionEditorItem.FromDefinition(
            definition,
            isBuiltIn: false,
            usageCount: 0,
            generationEnabled: true);

        Assert.False(CustomSeasonsDialog.RequiresReplacement(manual, CampaignSeasonCatalog.SpringId));
        Assert.True(CustomSeasonsDialog.RequiresReplacement(used, CampaignSeasonCatalog.SpringId));
        Assert.True(CustomSeasonsDialog.RequiresReplacement(enabled, CampaignSeasonCatalog.SpringId));
        Assert.True(CustomSeasonsDialog.RequiresReplacement(manual, definition.Id));
    }
}
