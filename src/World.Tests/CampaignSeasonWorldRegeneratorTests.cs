using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonWorldRegeneratorTests
{
    [Fact]
    public void SameLatticePreservesEverySeasonAndLockExactly()
    {
        var definition = CreateDefinition(4, 3, 1_000);
        var sourceWorld = new CampaignWorld(definition);
        var sourceMap = new CampaignSeasonMap(definition with { });
        sourceMap.Paint(1, 1, CampaignSeasonCatalog.WinterId, locked: true);
        sourceMap.Paint(2, 2, CampaignSeasonCatalog.AutumnId, locked: false);
        var settings = CreateSettings(37);
        var saved = CreateSaved(settings);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.PriorityIds,
            saved);
        var candidateWorld = new CampaignWorld(definition with { MaximumHeightMeters = 5_500 });

        var result = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings);

        Assert.Equal(CampaignSeasonLatticeRemapMode.PreserveSameLattice, result.Report.Mode);
        Assert.True(result.Report.CanAccept);
        Assert.Same(saved, result.SavedGeneration);
        Assert.Null(result.Settings);
        Assert.Equal(sourceMap.GetAllTiles(), result.CandidateMap.GetAllTiles());
        Assert.Equal(1, result.CandidateMap.LockedTileCount);
        Assert.Empty(result.Report.Conflicts);
        Assert.Empty(result.Report.LockedDrops);
        Assert.True(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));
    }

    [Fact]
    public void ChangedLatticeMapsEachLockByGreatestPhysicalOverlapAndGeneratesUnlockedTiles()
    {
        var sourceDefinition = CreateDefinition(2, 2, 2_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(sourceDefinition);
        sourceMap.Paint(0, 0, CampaignSeasonCatalog.WinterId, locked: true);
        sourceMap.Paint(1, 1, CampaignSeasonCatalog.AutumnId, locked: false);
        var settings = CreateSettings(41, CampaignSeasonCatalog.SummerId);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.PriorityIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(4, 4, 1_000));

        var result = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings);

        Assert.Equal(
            CampaignSeasonLatticeRemapMode.RemapLocksAndRegenerateUnlocked,
            result.Report.Mode);
        Assert.True(result.Report.CanAccept);
        Assert.Equal(new CampaignSeasonTile(CampaignSeasonCatalog.WinterId, true),
            result.CandidateMap.GetTile(1, 1));
        Assert.Equal(new CampaignSeasonTile(CampaignSeasonCatalog.SummerId),
            result.CandidateMap.GetTile(3, 3));
        Assert.Equal(1, result.Report.SourceLockedTileCount);
        Assert.Equal(1, result.Report.FinalLockedTileCount);
        Assert.Equal(1, result.Report.MovedLockedTileCount);
        Assert.NotNull(result.SavedGeneration);
        Assert.Same(settings, result.SavedGeneration!.Settings);
        Assert.NotNull(result.SupportFields);
    }

    [Fact]
    public void SameIdEqualOverlapClaimsMergeIntoOneLockedTarget()
    {
        var sourceDefinition = CreateDefinition(2, 1, 1_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(sourceDefinition);
        sourceMap.Paint(0, 0, CampaignSeasonCatalog.WinterId, locked: true);
        sourceMap.Paint(1, 0, CampaignSeasonCatalog.WinterId, locked: true);
        var settings = CreateSettings(7, CampaignSeasonCatalog.SummerId);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.PriorityIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(1, 1, 2_000));

        var result = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings);

        Assert.True(result.Report.CanAccept);
        Assert.Equal(1, result.Report.FinalLockedTileCount);
        Assert.Equal(1, result.Report.MergedLockedTileCount);
        Assert.Empty(result.Report.Conflicts);
        Assert.Equal(new CampaignSeasonTile(CampaignSeasonCatalog.WinterId, true),
            result.CandidateMap.GetTile(0, 0));
    }

    [Fact]
    public void DifferentIdsWithStrictlyGreaterOverlapChooseTheLargerClaim()
    {
        var sourceDefinition = CreateDefinition(2, 2, 2_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(sourceDefinition);
        sourceMap.Paint(0, 0, CampaignSeasonCatalog.WinterId, locked: true);
        sourceMap.Paint(1, 0, CampaignSeasonCatalog.SpringId, locked: true);
        var settings = CreateSettings(9, CampaignSeasonCatalog.SummerId);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.PriorityIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(1, 1, 3_000));

        var result = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings,
            permitLockedDrops: true);

        Assert.True(result.Report.CanAccept);
        Assert.Empty(result.Report.Conflicts);
        Assert.Equal(1, result.Report.DisplacedLockedTileCount);
        Assert.Equal(new CampaignSeasonTile(CampaignSeasonCatalog.WinterId, true),
            result.CandidateMap.GetTile(0, 0));
    }

    [Fact]
    public void EqualOverlapDifferentIdsBlockUntilExplicitResolution()
    {
        var sourceDefinition = CreateDefinition(2, 1, 1_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(
            sourceDefinition,
            defaultSeasonId: CampaignSeasonCatalog.AutumnId);
        sourceMap.Paint(0, 0, CampaignSeasonCatalog.WinterId, locked: true);
        sourceMap.Paint(1, 0, CampaignSeasonCatalog.SpringId, locked: true);
        var settings = CreateSettings(13, CampaignSeasonCatalog.SummerId);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.PriorityIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(1, 1, 2_000));

        var unresolved = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings);

        Assert.False(unresolved.Report.CanAccept);
        var conflict = Assert.Single(unresolved.Report.Conflicts);
        Assert.False(conflict.IsResolved);
        Assert.Equal(2, conflict.Claims.Count);
        Assert.Equal(
            new CampaignSeasonTile(CampaignSeasonCatalog.AutumnId, Locked: true),
            unresolved.CandidateMap.GetTile(0, 0));

        var resolved = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings,
            [new CampaignSeasonLockResolution(0, 0, CampaignSeasonCatalog.WinterId)]);

        Assert.True(resolved.Report.CanAccept);
        Assert.Equal(CampaignSeasonCatalog.WinterId, Assert.Single(resolved.Report.Conflicts).ResolvedSeasonId);
        Assert.Equal(new CampaignSeasonTile(CampaignSeasonCatalog.WinterId, true),
            resolved.CandidateMap.GetTile(0, 0));
    }

    [Fact]
    public void LockedDropBlocksUntilExplicitlyPermitted()
    {
        var sourceDefinition = CreateDefinition(4, 4, 1_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(sourceDefinition);
        sourceMap.Paint(3, 3, CampaignSeasonCatalog.WinterId, locked: true);
        var settings = CreateSettings(17, CampaignSeasonCatalog.SummerId);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.PriorityIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(2, 2, 1_000));

        var blocked = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings);

        Assert.False(blocked.Report.CanAccept);
        Assert.True(blocked.Report.HasUnpermittedDrops);
        var drop = Assert.Single(blocked.Report.LockedDrops);
        Assert.Equal(new CampaignSeasonLockedDrop(
            3,
            3,
            CampaignSeasonCatalog.WinterId,
            100), drop);

        var permitted = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings,
            permitLockedDrops: true);

        Assert.True(permitted.Report.CanAccept);
        Assert.True(permitted.Report.DropsPermitted);
        Assert.Equal(0, permitted.CandidateMap.LockedTileCount);
    }

    [Fact]
    public void FreshnessRejectsSourceTerrainSourceSeasonOrCandidateMutation()
    {
        var sourceDefinition = CreateDefinition(2, 2, 1_000);
        var sourceWorld = new CampaignWorld(sourceDefinition);
        var sourceMap = new CampaignSeasonMap(sourceDefinition);
        var settings = CreateSettings(19, CampaignSeasonCatalog.SummerId);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.PriorityIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(4, 4, 500));
        var result = new CampaignSeasonWorldRegenerator().Generate(
            source,
            candidateWorld,
            settings);
        Assert.True(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));

        result.CandidateMap.Paint(0, 0, CampaignSeasonCatalog.WinterId);

        Assert.False(result.IsCurrent(sourceWorld, sourceMap, candidateWorld));
    }

    [Fact]
    public void NewWorldGenerationBuildsCompleteExactSeasonCandidateAndRecipe()
    {
        var world = new CampaignWorld(CreateDefinition(5, 4, 1_000));
        var catalog = new CampaignSeasonCatalog();
        var settings = CreateSettings(23, CampaignSeasonCatalog.SummerId);

        var result = new CampaignSeasonWorldRegenerator().GenerateNewWorld(
            world,
            catalog,
            CampaignSeasonCatalog.SpringId,
            settings);

        Assert.True(result.IsCurrent(world));
        Assert.Equal(20, result.CandidateMap.TileCount);
        Assert.Equal(20, result.CandidateMap.GetUsageCount(CampaignSeasonCatalog.SummerId));
        Assert.Equal(0, result.CandidateMap.LockedTileCount);
        Assert.Same(settings, result.SavedGeneration.Settings);
        Assert.Equal(64, result.SavedGeneration.SourceTerrainFingerprint.Length);
        Assert.Equal(64, result.SavedGeneration.InputFingerprint.Length);
    }

    [Fact]
    public void PreCancelledGenerationDoesNotMutateEitherAuthority()
    {
        var definition = CreateDefinition(2, 2, 1_000);
        var sourceWorld = new CampaignWorld(definition);
        var sourceMap = new CampaignSeasonMap(definition);
        sourceMap.Paint(0, 0, CampaignSeasonCatalog.WinterId, locked: true);
        var settings = CreateSettings(29, CampaignSeasonCatalog.SummerId);
        var source = CampaignSeasonWorldRegenerationSource.Capture(
            sourceWorld,
            sourceMap,
            settings.PriorityIds);
        var candidateWorld = new CampaignWorld(CreateDefinition(4, 4, 500));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            new CampaignSeasonWorldRegenerator().Generate(
                source,
                candidateWorld,
                settings,
                cancellationToken: cancellation.Token));
        Assert.Equal(new CampaignSeasonTile(CampaignSeasonCatalog.WinterId, true),
            sourceMap.GetTile(0, 0));
        Assert.Equal(0, candidateWorld.Revision);
    }

    private static CampaignSeasonGenerationSettings CreateSettings(
        int seed,
        params string[] priorityIds) => new(
        seed,
        seedDerivedFromTerrain: true,
        priorityIds: priorityIds.Length == 0
            ? CampaignSeasonGenerationSettings.DefaultPriority
            : priorityIds);

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
