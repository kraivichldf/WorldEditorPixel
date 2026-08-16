using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceWorldRegeneratorTests
{
    [Fact]
    public void SameLatticePreservesEveryOccurrenceExactly()
    {
        var sourceDefinition = CreateDefinition(4, 4, 1_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignResourceMap(sourceDefinition with { });
        sourceMap.Apply(
        [
            CampaignResourceMutation.Upsert(
                0,
                1,
                new CampaignResourceOccurrence("iron-ore", 73, Locked: true)),
            CampaignResourceMutation.Upsert(
                3,
                2,
                new CampaignResourceOccurrence("fish", 41)),
        ]);
        var settings = CreateDisabledSettings(sourceMap.Catalog, seed: 17);
        var source = CampaignResourceWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings);
        var candidateWorld = new CampaignWorld(sourceDefinition with
        {
            MaximumHeightMeters = 5_000,
        });

        var result = new CampaignResourceWorldRegenerator().Generate(source, candidateWorld);

        Assert.Equal(CampaignResourceLatticeRemapMode.PreserveSameLattice, result.Report.Mode);
        Assert.Same(settings, result.Settings);
        Assert.Equal(sourceMap.GetMaterializedOccurrences(), result.CandidateMap.GetMaterializedOccurrences());
        Assert.Equal(2, result.Report.UnchangedSourceOccurrenceCount);
        Assert.Equal(0, result.Report.MovedSourceOccurrenceCount);
        Assert.Equal(0, result.Report.MergedOccurrenceCount);
        Assert.Equal(0, result.Report.DroppedOccurrenceCount);
        Assert.Empty(result.Report.GenerationReports);
        Assert.True(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));
    }

    [Fact]
    public void FinerGridUsesSourceTileCentresInPhysicalMeters()
    {
        var sourceDefinition = CreateDefinition(2, 1, 2_000);
        var sourceMap = new CampaignResourceMap(sourceDefinition);
        sourceMap.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("gold", 25)),
            CampaignResourceMutation.Upsert(1, 0, new CampaignResourceOccurrence("gold", 75)),
        ]);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var candidateWorld = new CampaignWorld(CreateDefinition(4, 2, 1_000));

        var result = GenerateWithoutSettings(sourceWorld, sourceMap, candidateWorld);

        AssertOccurrence(result.CandidateMap, 1, 1, "gold", 25, locked: false);
        AssertOccurrence(result.CandidateMap, 3, 1, "gold", 75, locked: false);
        Assert.Equal(2, result.Report.MovedSourceOccurrenceCount);
        Assert.Equal(0, result.Report.DroppedOccurrenceCount);
    }

    [Fact]
    public void CoarserGridMergesSameIdUsingHighestPotentialAndAnyLock()
    {
        var sourceDefinition = CreateDefinition(4, 2, 1_000);
        var sourceMap = new CampaignResourceMap(sourceDefinition);
        sourceMap.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("gold", 91)),
            CampaignResourceMutation.Upsert(1, 0, new CampaignResourceOccurrence("gold", 40, Locked: true)),
            CampaignResourceMutation.Upsert(1, 0, new CampaignResourceOccurrence("fish", 55)),
        ]);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var candidateWorld = new CampaignWorld(CreateDefinition(2, 1, 2_000));

        var result = GenerateWithoutSettings(sourceWorld, sourceMap, candidateWorld);

        Assert.Equal(2, result.CandidateMap.OccurrenceCount);
        AssertOccurrence(result.CandidateMap, 0, 0, "gold", 91, locked: true);
        AssertOccurrence(result.CandidateMap, 0, 0, "fish", 55, locked: false);
        Assert.Equal(1, result.Report.MergedOccurrenceCount);
        Assert.Equal(1, result.Report.LockedRetainedOccurrenceCount);
        Assert.Equal(0, result.Report.LockedMergedOccurrenceCount);
    }

    [Fact]
    public void ShrinkingWorldReportsExactLockedDropsBeforeAcceptance()
    {
        var sourceDefinition = CreateDefinition(4, 4, 1_000);
        var sourceMap = new CampaignResourceMap(sourceDefinition);
        sourceMap.Apply(
        [
            CampaignResourceMutation.Upsert(
                1,
                1,
                new CampaignResourceOccurrence("iron-ore", 61, Locked: true)),
            CampaignResourceMutation.Upsert(
                3,
                2,
                new CampaignResourceOccurrence("gold", 88, Locked: true)),
            CampaignResourceMutation.Upsert(
                0,
                3,
                new CampaignResourceOccurrence("fish", 22)),
        ]);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var candidateWorld = new CampaignWorld(CreateDefinition(2, 2, 1_000));

        var result = GenerateWithoutSettings(sourceWorld, sourceMap, candidateWorld);

        Assert.Equal(1, result.CandidateMap.OccurrenceCount);
        Assert.Equal(2, result.Report.DroppedOccurrenceCount);
        Assert.Equal(1, result.Report.LockedDroppedOccurrenceCount);
        var lockedDrop = Assert.Single(result.Report.LockedDrops);
        Assert.Equal(new CampaignResourceLockedDrop(3, 2, "gold", 88), lockedDrop);
        AssertOccurrence(result.CandidateMap, 1, 1, "iron-ore", 61, locked: true);
    }

    [Fact]
    public void SavedSettingsRemapLocksAndRegenerateUnlockedScope()
    {
        var sourceDefinition = CreateDefinition(4, 4, 1_000);
        var sourceMap = new CampaignResourceMap(sourceDefinition);
        sourceMap.Apply(
        [
            CampaignResourceMutation.Upsert(
                1,
                1,
                new CampaignResourceOccurrence("iron-ore", 67, Locked: true)),
            CampaignResourceMutation.Upsert(
                2,
                2,
                new CampaignResourceOccurrence("gold", 99)),
        ]);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var settings = CreateDisabledSettings(sourceMap.Catalog, seed: 501);
        var source = CampaignResourceWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings);
        var candidateWorld = new CampaignWorld(CreateDefinition(8, 8, 500));

        var result = new CampaignResourceWorldRegenerator().Generate(source, candidateWorld);

        Assert.Equal(
            CampaignResourceLatticeRemapMode.RemapLocksAndRegenerateUnlocked,
            result.Report.Mode);
        Assert.Equal(1, result.Report.ReplacedUnlockedSourceOccurrenceCount);
        Assert.Equal(0, result.Report.RegeneratedUnlockedOccurrenceCount);
        Assert.Equal(sourceMap.Catalog.Definitions.Count, result.Report.GenerationReports.Count);
        Assert.Equal(1, result.CandidateMap.OccurrenceCount);
        AssertOccurrence(result.CandidateMap, 3, 3, "iron-ore", 67, locked: true);
        Assert.False(result.CandidateMap.TryGetOccurrence(5, 5, "gold", out _));
    }

    [Fact]
    public void NoSavedSettingsRemapsUnlockedOccurrencesWithoutInventingGeneration()
    {
        var sourceDefinition = CreateDefinition(3, 2, 1_000);
        var sourceMap = new CampaignResourceMap(sourceDefinition);
        sourceMap.Upsert(2, 1, new CampaignResourceOccurrence("gold", 53));
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var candidateWorld = new CampaignWorld(CreateDefinition(6, 4, 500));

        var result = GenerateWithoutSettings(sourceWorld, sourceMap, candidateWorld);

        Assert.Equal(CampaignResourceLatticeRemapMode.RemapAllOccurrences, result.Report.Mode);
        Assert.Equal(0, result.Report.ReplacedUnlockedSourceOccurrenceCount);
        Assert.Empty(result.Report.GenerationReports);
        AssertOccurrence(result.CandidateMap, 5, 3, "gold", 53, locked: false);
    }

    [Fact]
    public void CandidateFreshnessRejectsSourceOrCandidateMutation()
    {
        var definition = CreateDefinition(4, 4, 1_000);
        var sourceWorld = new CampaignWorld(definition);
        var sourceMap = new CampaignResourceMap(definition);
        sourceMap.Upsert(0, 0, new CampaignResourceOccurrence("gold", 42));
        var candidateWorld = new CampaignWorld(CreateDefinition(8, 8, 500));
        var result = GenerateWithoutSettings(sourceWorld, sourceMap, candidateWorld);
        Assert.True(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));

        result.CandidateMap.Upsert(0, 0, new CampaignResourceOccurrence("fish", 12));

        Assert.False(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));
    }

    [Fact]
    public void CapturedSourceIsIndependentFromLaterLiveMapChanges()
    {
        var definition = CreateDefinition(4, 4, 1_000);
        var sourceWorld = new CampaignWorld(definition);
        var sourceMap = new CampaignResourceMap(definition);
        sourceMap.Upsert(1, 1, new CampaignResourceOccurrence("gold", 37));
        var source = CampaignResourceWorldRegenerationSource.Capture(sourceWorld, sourceMap);
        sourceMap.Upsert(2, 2, new CampaignResourceOccurrence("fish", 74));
        var candidateWorld = new CampaignWorld(CreateDefinition(8, 8, 500));

        var result = new CampaignResourceWorldRegenerator().Generate(source, candidateWorld);

        Assert.Equal(1, result.CandidateMap.OccurrenceCount);
        AssertOccurrence(result.CandidateMap, 3, 3, "gold", 37, locked: false);
        Assert.False(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));
    }

    [Fact]
    public void PreCancelledGenerationDoesNotBuildCandidate()
    {
        var definition = CreateDefinition(4, 4, 1_000);
        var sourceWorld = new CampaignWorld(definition);
        var sourceMap = new CampaignResourceMap(definition);
        var source = CampaignResourceWorldRegenerationSource.Capture(sourceWorld, sourceMap);
        var candidateWorld = new CampaignWorld(CreateDefinition(8, 8, 500));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new CampaignResourceWorldRegenerator().Generate(
                source,
                candidateWorld,
                cancellation.Token));
        Assert.Equal(0, sourceMap.OccurrenceCount);
    }

    private static CampaignResourceWorldRegenerationResult GenerateWithoutSettings(
        CampaignWorld sourceWorld,
        CampaignResourceMap sourceMap,
        CampaignWorld candidateWorld)
    {
        var source = CampaignResourceWorldRegenerationSource.Capture(sourceWorld, sourceMap);
        return new CampaignResourceWorldRegenerator().Generate(source, candidateWorld);
    }

    private static CampaignResourceGenerationSettings CreateDisabledSettings(
        CampaignResourceCatalog catalog,
        int seed) =>
        new(
            seed,
            overrides: catalog.Definitions.Select(definition =>
                new CampaignResourceGenerationOverride(
                    definition.Id,
                    enabled: false,
                    coveragePercent: 0,
                    CampaignResourceRichness.Balanced,
                    richnessBias: 0,
                    CampaignResourceConcentration.Balanced,
                    definition.MapPriority)));

    private static CampaignWorldDefinition CreateDefinition(
        int tilesX,
        int tilesY,
        int tileSizeMeters) =>
        CampaignWorldDefinition.Create(
            tilesX * (long)tileSizeMeters,
            tilesY * (long)tileSizeMeters,
            tileSizeMeters,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000,
            defaultTileHeightMeters: 0);

    private static void AssertOccurrence(
        CampaignResourceMap map,
        int x,
        int y,
        string resourceId,
        byte potential,
        bool locked)
    {
        Assert.True(map.TryGetOccurrence(x, y, resourceId, out var occurrence));
        Assert.Equal(potential, occurrence.Potential);
        Assert.Equal(locked, occurrence.Locked);
    }
}
