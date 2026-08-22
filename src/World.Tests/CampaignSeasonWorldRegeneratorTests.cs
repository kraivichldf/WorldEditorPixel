using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonWorldRegeneratorTests
{
    [Fact]
    public void SameLattice_PreservesEveryOccurrenceAndLockExactly()
    {
        var definition = CreateDefinition(4, 3, 1_000);
        var sourceWorld = new CampaignWorld(definition);
        var sourceMap = new CampaignSeasonMap(definition with { });
        sourceMap.Upsert(1, 1, new("spring"));
        sourceMap.Upsert(1, 1, new("summer"));
        sourceMap.Upsert(1, 1, new("fall", Locked: true));
        sourceMap.Upsert(2, 2, new("winter", Locked: true));
        var settings = CreateSettings(37);
        var saved = CreateSaved(settings);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.EnabledSeasonIds,
            saved);
        var candidateWorld = new CampaignWorld(definition with { MaximumHeightMeters = 5_500 });

        var result = new CampaignSeasonWorldRegenerator().Generate(source, candidateWorld, settings);

        Assert.Equal(CampaignSeasonLatticeRemapMode.PreserveSameLattice, result.Report.Mode);
        Assert.True(result.Report.CanAccept);
        Assert.Same(saved, result.SavedGeneration);
        Assert.Null(result.Settings);
        Assert.Equal(sourceMap.GetMaterializedOccurrences(), result.CandidateMap.GetMaterializedOccurrences());
        Assert.Equal(2, result.CandidateMap.LockedOccurrenceCount);
        Assert.Empty(result.Report.LockedDrops);
        Assert.True(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));
    }

    [Fact]
    public void ChangedLattice_DifferentLockedSeasonIdsCoexistAtOneTargetTile()
    {
        var sourceDefinition = CreateDefinition(2, 1, 1_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(sourceDefinition);
        sourceMap.Upsert(0, 0, new("winter", Locked: true));
        sourceMap.Upsert(1, 0, new("spring", Locked: true));
        var settings = CreateSettings(9, "summer");
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.EnabledSeasonIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(1, 1, 2_000));

        var result = new CampaignSeasonWorldRegenerator().Generate(source, candidateWorld, settings);

        Assert.True(result.Report.CanAccept);
        Assert.Equal(2, result.Report.FinalLockedOccurrenceCount);
        Assert.True(result.CandidateMap.TryGetOccurrence(0, 0, "spring", out var spring));
        Assert.True(result.CandidateMap.TryGetOccurrence(0, 0, "winter", out var winter));
        Assert.True(spring.Locked);
        Assert.True(winter.Locked);
    }

    [Fact]
    public void ChangedLattice_SameSeasonIdentityMergesAndLockSurvives()
    {
        var sourceDefinition = CreateDefinition(2, 1, 1_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(sourceDefinition);
        sourceMap.Upsert(0, 0, new("winter", Locked: true));
        sourceMap.Upsert(1, 0, new("winter", Locked: true));
        var settings = CreateSettings(7, "summer");
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.EnabledSeasonIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(1, 1, 2_000));

        var result = new CampaignSeasonWorldRegenerator().Generate(source, candidateWorld, settings);

        Assert.True(result.Report.CanAccept);
        Assert.Equal(1, result.Report.FinalLockedOccurrenceCount);
        Assert.Equal(1, result.Report.MergedLockedOccurrenceCount);
        Assert.True(result.CandidateMap.TryGetOccurrence(0, 0, "winter", out var winter));
        Assert.True(winter.Locked);
    }

    [Fact]
    public void ChangedLattice_RegeneratesUnlockedOccurrencesIndependently()
    {
        var always = CampaignSeasonCatalogTests.CreateCustom(
            "always-season",
            rule: CampaignSeasonRule.Unrestricted);
        var catalog = new CampaignSeasonCatalog([always]);
        var sourceDefinition = CreateDefinition(2, 2, 2_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(sourceDefinition, catalog);
        sourceMap.Upsert(1, 1, new("fall"));
        var settings = CreateSettings(41, always.Id);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.EnabledSeasonIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(4, 4, 1_000));

        var result = new CampaignSeasonWorldRegenerator().Generate(source, candidateWorld, settings);

        Assert.Equal(CampaignSeasonLatticeRemapMode.RemapLocksAndRegenerateUnlocked, result.Report.Mode);
        Assert.Equal(16, result.CandidateMap.GetUsageCount(always.Id));
        Assert.Equal(0, result.CandidateMap.GetUsageCount("fall"));
        Assert.NotNull(result.SavedGeneration);
        Assert.Same(settings, result.SavedGeneration!.Settings);
        Assert.NotNull(result.SupportFields);
    }

    [Fact]
    public void LockedDrop_BlocksUntilExplicitlyPermitted()
    {
        var sourceDefinition = CreateDefinition(4, 4, 1_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(sourceDefinition);
        sourceMap.Upsert(3, 3, new("winter", Locked: true));
        var settings = CreateSettings(17, "summer");
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.EnabledSeasonIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(2, 2, 1_000));

        var blocked = new CampaignSeasonWorldRegenerator().Generate(source, candidateWorld, settings);

        Assert.False(blocked.Report.CanAccept);
        Assert.True(blocked.Report.HasUnpermittedDrops);
        Assert.Equal("winter", Assert.Single(blocked.Report.LockedDrops).SeasonId);

        var permitted = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings,
            permitLockedDrops: true);

        Assert.True(permitted.Report.CanAccept);
        Assert.True(permitted.Report.DropsPermitted);
        Assert.Equal(0, permitted.CandidateMap.LockedOccurrenceCount);
    }

    [Fact]
    public void FreshnessRejectsSourceTerrainSourceSeasonOrCandidateMutation()
    {
        var definition = CreateDefinition(2, 2, 1_000);
        var sourceWorld = new CampaignWorld(definition);
        var sourceMap = new CampaignSeasonMap(definition);
        var settings = CreateSettings(19, "summer");
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.EnabledSeasonIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(4, 4, 500));
        var result = new CampaignSeasonWorldRegenerator().Generate(source, candidateWorld, settings);
        Assert.True(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));

        result.CandidateMap.Upsert(0, 0, new("winter"));

        Assert.False(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));
    }

    [Fact]
    public void NewWorldGeneration_CanPlaceThreeIndependentSeasonsOnEveryTile()
    {
        var definitions = new[]
        {
            CampaignSeasonCatalogTests.CreateCustom("alpha-season", rule: CampaignSeasonRule.Unrestricted),
            CampaignSeasonCatalogTests.CreateCustom("beta-season", rule: CampaignSeasonRule.Unrestricted),
            CampaignSeasonCatalogTests.CreateCustom("gamma-season", rule: CampaignSeasonRule.Unrestricted),
        };
        var world = new CampaignWorld(CreateDefinition(5, 4, 1_000));
        var catalog = new CampaignSeasonCatalog(definitions);
        var settings = CreateSettings(23, definitions.Select(static value => value.Id).ToArray());

        var result = new CampaignSeasonWorldRegenerator().GenerateNewWorld(world, catalog, settings);

        Assert.True(result.IsCurrent(world));
        Assert.Equal(20, result.CandidateMap.TileCount);
        Assert.Equal(60, result.CandidateMap.OccurrenceCount);
        Assert.All(definitions, definition =>
            Assert.Equal(20, result.CandidateMap.GetUsageCount(definition.Id)));
        Assert.Equal(0, result.CandidateMap.LockedOccurrenceCount);
        Assert.Same(settings, result.SavedGeneration.Settings);
    }

    [Fact]
    public void PreCancelledGeneration_DoesNotMutateEitherAuthority()
    {
        var definition = CreateDefinition(2, 2, 1_000);
        var sourceWorld = new CampaignWorld(definition);
        var sourceMap = new CampaignSeasonMap(definition);
        sourceMap.Upsert(0, 0, new("winter", Locked: true));
        var settings = CreateSettings(29, "summer");
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.EnabledSeasonIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(4, 4, 500));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new CampaignSeasonWorldRegenerator().Generate(
                source,
                candidateWorld,
                settings,
                cancellationToken: cancellation.Token));
        Assert.True(sourceMap.TryGetOccurrence(0, 0, "winter", out var winter));
        Assert.True(winter.Locked);
        Assert.Equal(0, candidateWorld.Revision);
    }

    private static CampaignSeasonGenerationSettings CreateSettings(
        int seed,
        params string[] enabledSeasonIds) => new(
        seed,
        seedDerivedFromTerrain: true,
        enabledSeasonIds: enabledSeasonIds.Length == 0
            ? CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds
            : enabledSeasonIds);

    private static CampaignSeasonSavedGeneration CreateSaved(
        CampaignSeasonGenerationSettings settings) => new(
        settings,
        new string('a', 64),
        new string('b', 64));

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
}
