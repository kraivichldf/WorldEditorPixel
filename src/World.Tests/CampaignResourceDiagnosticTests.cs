using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceDiagnosticTests
{
    [Fact]
    public void EvaluatorReportsEveryImplementedMismatchCode()
    {
        AssertCodes(
            CreateDefinition(),
            Sample(CampaignResourceTerrainKind.Unassigned, CampaignResourceSurfaceType.Unassigned),
            CampaignResourceDiagnosticCode.TerrainUnassigned);
        AssertCodes(
            CreateDefinition(medium: CampaignResourceMedium.Land),
            Sample(CampaignResourceTerrainKind.Water, CampaignResourceSurfaceType.Sea),
            CampaignResourceDiagnosticCode.MediumRequiresLand);
        AssertCodes(
            CreateDefinition(medium: CampaignResourceMedium.Water),
            Sample(CampaignResourceTerrainKind.Land, CampaignResourceSurfaceType.Grassland),
            CampaignResourceDiagnosticCode.MediumRequiresWater);
        AssertCodes(
            CreateDefinition(elevation: new CampaignResourceRange(100, 200)),
            Sample(elevation: 99),
            CampaignResourceDiagnosticCode.ElevationBelowMinimum);
        AssertCodes(
            CreateDefinition(elevation: new CampaignResourceRange(100, 200)),
            Sample(elevation: 201),
            CampaignResourceDiagnosticCode.ElevationAboveMaximum);
        AssertCodes(
            CreateDefinition(grade: new CampaignResourceRange(0.1, 0.2)),
            Sample(grade: 0.09),
            CampaignResourceDiagnosticCode.GradeBelowMinimum);
        AssertCodes(
            CreateDefinition(grade: new CampaignResourceRange(0.1, 0.2)),
            Sample(grade: 0.21),
            CampaignResourceDiagnosticCode.GradeAboveMaximum);
        AssertCodes(
            CreateDefinition(waterDistance: new CampaignResourceRange(2, 4)),
            Sample(seaDistance: 1),
            CampaignResourceDiagnosticCode.WaterDistanceBelowMinimum);
        AssertCodes(
            CreateDefinition(waterDistance: new CampaignResourceRange(2, 4)),
            Sample(seaDistance: 5),
            CampaignResourceDiagnosticCode.WaterDistanceAboveMaximum);
        AssertCodes(
            CreateDefinition(customIncludes: ["orchard"]),
            Sample(customTerrainId: "marsh-farm"),
            CampaignResourceDiagnosticCode.CustomTerrainNotIncluded);
        AssertCodes(
            CreateDefinition(customExcludes: ["marsh-farm"]),
            Sample(customTerrainId: "marsh-farm"),
            CampaignResourceDiagnosticCode.CustomTerrainExcluded);
    }

    [Fact]
    public void CustomIncludeIsAWhitelistOnlyForCustomTerrainCells()
    {
        var definition = CreateDefinition(
            customIncludes: ["orchard"],
            customExcludes: ["marsh-farm"]);

        Assert.False(CampaignResourceDiagnosticEvaluator.Evaluate(definition, Sample()).HasWarnings);
        Assert.False(CampaignResourceDiagnosticEvaluator.Evaluate(
            definition,
            Sample(customTerrainId: "orchard")).HasWarnings);
        AssertCodes(
            definition,
            Sample(customTerrainId: "marsh-farm"),
            CampaignResourceDiagnosticCode.CustomTerrainNotIncluded,
            CampaignResourceDiagnosticCode.CustomTerrainExcluded);
    }

    [Fact]
    public void HardExcludedSurfaceIsReportedWithoutDeletingManualAuthority()
    {
        var definition = CreateDefinition(
            rules: new CampaignResourceRuleSet(
                CampaignResourceMedium.Land,
                excludedTerrainSurfaces: [CampaignResourceSurfaceType.Desert]));

        AssertCodes(
            definition,
            Sample(surface: CampaignResourceSurfaceType.Desert),
            CampaignResourceDiagnosticCode.TerrainSurfaceExcluded);
        Assert.False(CampaignResourceDiagnosticEvaluator.Evaluate(
            definition,
            Sample(surface: CampaignResourceSurfaceType.Grassland)).HasWarnings);
    }

    [Fact]
    public void EvaluatorReportsUnevaluatedFactorsWithoutTurningThemIntoWarnings()
    {
        var rules = new CampaignResourceRuleSet(
            CampaignResourceMedium.Land,
            regionScaleKilometers: new CampaignResourceRange(25, 100),
            preferredTerrainTags: ["volcanic"],
            fieldWeights: new Dictionary<string, double> { ["rainfall"] = 9 },
            associationWeights: new Dictionary<string, double> { ["gold"] = -8 },
            avoidedTerrainTags: ["arid"]);
        var definition = CreateDefinition(rules: rules);
        var result = CampaignResourceDiagnosticEvaluator.Evaluate(definition, Sample());

        Assert.False(result.HasWarnings);
        Assert.Equal(
        [
            CampaignResourceUnevaluatedFactor.ClimateProfile,
            CampaignResourceUnevaluatedFactor.GeologyProfile,
            CampaignResourceUnevaluatedFactor.PreferredTerrainTags,
            CampaignResourceUnevaluatedFactor.FieldWeights,
            CampaignResourceUnevaluatedFactor.AssociationWeights,
            CampaignResourceUnevaluatedFactor.DistributionShape,
            CampaignResourceUnevaluatedFactor.RegionScale,
            CampaignResourceUnevaluatedFactor.FinalGeneratorSuitability,
            CampaignResourceUnevaluatedFactor.AvoidedTerrainTags,
        ], result.UnevaluatedFactors);
    }

    [Fact]
    public void ImplementedRangesAreInclusiveAndEitherAcceptsKnownLandOrWater()
    {
        var definition = CreateDefinition(
            medium: CampaignResourceMedium.Either,
            elevation: new CampaignResourceRange(100, 200),
            grade: new CampaignResourceRange(0.1, 0.2),
            waterDistance: new CampaignResourceRange(2, 4));

        Assert.False(CampaignResourceDiagnosticEvaluator.Evaluate(
            definition,
            Sample(elevation: 100, grade: 0.1, seaDistance: 2)).HasWarnings);
        Assert.False(CampaignResourceDiagnosticEvaluator.Evaluate(
            definition,
            Sample(
                CampaignResourceTerrainKind.Water,
                CampaignResourceSurfaceType.Lake,
                elevation: 200,
                grade: 0.2,
                seaDistance: 4)).HasWarnings);
        AssertCodes(
            definition,
            Sample(CampaignResourceTerrainKind.Unassigned, CampaignResourceSurfaceType.Unassigned),
            CampaignResourceDiagnosticCode.TerrainUnassigned);
    }

    [Fact]
    public void DiagnosticIssuesAreImmutableAndDeterministicallyOrdered()
    {
        var definition = CreateDefinition(
            medium: CampaignResourceMedium.Water,
            elevation: new CampaignResourceRange(100, 200),
            grade: new CampaignResourceRange(0, 0.1),
            waterDistance: new CampaignResourceRange(0, 2),
            customIncludes: ["orchard"],
            customExcludes: ["marsh-farm"]);

        var result = CampaignResourceDiagnosticEvaluator.Evaluate(
            definition,
            Sample(
                elevation: 50,
                grade: 0.2,
                seaDistance: 3,
                customTerrainId: "marsh-farm"));

        Assert.Equal(
        [
            CampaignResourceDiagnosticCode.MediumRequiresWater,
            CampaignResourceDiagnosticCode.ElevationBelowMinimum,
            CampaignResourceDiagnosticCode.GradeAboveMaximum,
            CampaignResourceDiagnosticCode.WaterDistanceAboveMaximum,
            CampaignResourceDiagnosticCode.CustomTerrainNotIncluded,
            CampaignResourceDiagnosticCode.CustomTerrainExcluded,
        ], result.Issues.Select(static issue => issue.Code));
        var list = Assert.IsAssignableFrom<IList<CampaignResourceDiagnosticIssue>>(result.Issues);
        Assert.Throws<NotSupportedException>(() => list[0] = default);
        var unevaluatedList = Assert.IsAssignableFrom<IList<CampaignResourceUnevaluatedFactor>>(
            result.UnevaluatedFactors);
        Assert.Throws<NotSupportedException>(() =>
            unevaluatedList[0] = CampaignResourceUnevaluatedFactor.ClimateProfile);
    }

    [Fact]
    public void SampleValidationRejectsImpossibleLayerCombinations()
    {
        Assert.Throws<ArgumentException>(() => Sample(
            CampaignResourceTerrainKind.Water,
            CampaignResourceSurfaceType.Sea,
            riverFeatures: CampaignResourceRiverFeatures.Present).EnsureValid());
        Assert.Throws<ArgumentException>(() => Sample(
            riverFeatures: CampaignResourceRiverFeatures.Large).EnsureValid());
        Assert.Throws<ArgumentException>(() => Sample(
            coastFlags: CampaignResourceCoastFlags.CoastalWater).EnsureValid());
        Assert.Throws<ArgumentException>(() => Sample(
            CampaignResourceTerrainKind.Water,
            CampaignResourceSurfaceType.Sea,
            coastFlags: CampaignResourceCoastFlags.AdjacentSea).EnsureValid());
        Assert.Throws<ArgumentException>(() => Sample(
            coastFlags: CampaignResourceCoastFlags.BeachShore).EnsureValid());
    }

    [Fact]
    public void OccurrenceServiceCachesByBothRevisionsAndNeverMutatesResources()
    {
        var definition = CreateWorldDefinition(3, 2);
        var world = new CampaignWorld(definition);
        var resources = new CampaignResourceMap(definition);
        resources.Upsert(1, 0, new CampaignResourceOccurrence("fish", 70, Locked: true));
        var service = new CampaignResourceOccurrenceDiagnosticService(
            resources,
            new CampaignResourceTerrainQueryV2(world));

        var first = service.GetDiagnostics();
        var cached = service.GetDiagnostics();
        Assert.Same(first, cached);
        Assert.Equal(CampaignResourceDiagnosticCode.TerrainUnassigned, Assert.Single(first).Issues.Single().Code);

        var occurrenceBeforeTerrainEdit = resources.GetOccurrences(1, 0).Single();
        world.Tiles.SetTile(1, 0, new CampaignTileData(CampaignTileType.Sea, 0));
        var afterTerrainEdit = service.GetDiagnostics();

        Assert.NotSame(first, afterTerrainEdit);
        Assert.False(Assert.Single(afterTerrainEdit).HasWarnings);
        Assert.Equal(occurrenceBeforeTerrainEdit, resources.GetOccurrences(1, 0).Single());
        Assert.Equal(1, resources.OccurrenceCount);

        resources.Upsert(0, 1, new CampaignResourceOccurrence("iron-ore", 40));
        var afterResourceEdit = service.GetDiagnostics();
        Assert.NotSame(afterTerrainEdit, afterResourceEdit);
        Assert.Equal(2, afterResourceEdit.Count);
    }

    [Fact]
    public void OccurrenceDiagnosticsAreSortedByYThenXThenOrdinalResourceId()
    {
        var definition = CreateWorldDefinition(3, 3);
        var world = new CampaignWorld(definition);
        var resources = new CampaignResourceMap(definition);
        resources.Apply(
        [
            CampaignResourceMutation.Upsert(2, 2, new CampaignResourceOccurrence("silver", 10)),
            CampaignResourceMutation.Upsert(1, 0, new CampaignResourceOccurrence("timber", 20)),
            CampaignResourceMutation.Upsert(2, 2, new CampaignResourceOccurrence("gold", 30)),
            CampaignResourceMutation.Upsert(0, 2, new CampaignResourceOccurrence("stone", 40)),
        ]);
        var service = new CampaignResourceOccurrenceDiagnosticService(
            resources,
            new CampaignResourceTerrainQueryV2(world));

        Assert.Equal(
        [
            (1, 0, "timber"),
            (0, 2, "stone"),
            (2, 2, "gold"),
            (2, 2, "silver"),
        ], service.GetDiagnostics().Select(static value => (value.X, value.Y, value.ResourceId)));
        Assert.Equal(4, service.GetWarnings().Count);
    }

    [Fact]
    public void OccurrenceServiceRejectsDifferentWorldDefinitions()
    {
        var resources = new CampaignResourceMap(CreateWorldDefinition(2, 2));
        var world = new CampaignWorld(CreateWorldDefinition(3, 2));

        Assert.Throws<ArgumentException>(() => new CampaignResourceOccurrenceDiagnosticService(
            resources,
            new CampaignResourceTerrainQueryV2(world)));
    }

    private static void AssertCodes(
        CampaignResourceDefinition definition,
        CampaignResourceTerrainSample sample,
        params CampaignResourceDiagnosticCode[] expected) =>
        Assert.Equal(
            expected,
            CampaignResourceDiagnosticEvaluator.Evaluate(definition, sample)
                .Issues
                .Select(static issue => issue.Code));

    private static CampaignResourceDefinition CreateDefinition(
        CampaignResourceMedium medium = CampaignResourceMedium.Land,
        CampaignResourceRange? elevation = null,
        CampaignResourceRange? grade = null,
        CampaignResourceRange? waterDistance = null,
        IEnumerable<string>? customIncludes = null,
        IEnumerable<string>? customExcludes = null,
        CampaignResourceRuleSet? rules = null) =>
        new(
            "test-resource",
            "Test Resource",
            CampaignResourceCategory.Finite,
            CampaignResourceDistributionProfile.Vein,
            medium,
            "ore",
            "#735A91",
            mapPriority: 50,
            coveragePercent: 0,
            CampaignResourceRichness.Balanced,
            CampaignResourceConcentration.Balanced,
            rules ?? new CampaignResourceRuleSet(
                medium,
                elevation,
                grade,
                waterDistance,
                customTerrainIncludes: customIncludes,
                customTerrainExcludes: customExcludes));

    private static CampaignResourceTerrainSample Sample(
        CampaignResourceTerrainKind kind = CampaignResourceTerrainKind.Land,
        CampaignResourceSurfaceType surface = CampaignResourceSurfaceType.Grassland,
        short elevation = 0,
        double grade = 0,
        double seaDistance = 0,
        string? customTerrainId = null,
        CampaignResourceRiverFeatures riverFeatures = CampaignResourceRiverFeatures.None,
        CampaignResourceCoastFlags coastFlags = CampaignResourceCoastFlags.None) =>
        new(
            kind,
            surface,
            CampaignResourceTerrainForm.Flat,
            customTerrainId,
            elevation,
            grade,
            seaDistance,
            double.PositiveInfinity,
            double.PositiveInfinity,
            riverFeatures,
            coastFlags);

    private static CampaignWorldDefinition CreateWorldDefinition(int tilesX, int tilesY) =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: tilesX * 5_000L,
            worldHeightMeters: tilesY * 5_000L,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
}
