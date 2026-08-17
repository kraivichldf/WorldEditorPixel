using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

public enum CampaignSeasonLatticeRemapMode
{
    PreserveSameLattice,
    RemapLocksAndRegenerateUnlocked,
}

public enum CampaignSeasonLockedRemapOutcome
{
    Preserved,
    Merged,
    Displaced,
    Conflict,
    Dropped,
}

public sealed record CampaignSeasonLockResolution(
    int TargetX,
    int TargetY,
    string SeasonId);

public sealed record CampaignSeasonLockClaim(
    int SourceX,
    int SourceY,
    string SeasonId,
    int TargetX,
    int TargetY,
    double OverlapPercent,
    double OutOfBoundsPercent);

public sealed record CampaignSeasonLockConflict(
    int TargetX,
    int TargetY,
    IReadOnlyList<CampaignSeasonLockClaim> Claims,
    string? ResolvedSeasonId)
{
    public bool IsResolved => ResolvedSeasonId is not null;
}

public sealed record CampaignSeasonLockedDrop(
    int SourceX,
    int SourceY,
    string SeasonId,
    double OutOfBoundsPercent);

public sealed record CampaignSeasonLockedRemapEntry(
    int SourceX,
    int SourceY,
    string SeasonId,
    int? TargetX,
    int? TargetY,
    double OverlapPercent,
    double OutOfBoundsPercent,
    CampaignSeasonLockedRemapOutcome Outcome);

public sealed class CampaignSeasonWorldRegenerationReport
{
    internal CampaignSeasonWorldRegenerationReport(
        CampaignSeasonLatticeRemapMode mode,
        int sourceLockedTileCount,
        int finalLockedTileCount,
        int movedLockedTileCount,
        int mergedLockedTileCount,
        int displacedLockedTileCount,
        bool dropsPermitted,
        IEnumerable<CampaignSeasonLockedDrop> lockedDrops,
        IEnumerable<CampaignSeasonLockConflict> conflicts,
        IEnumerable<CampaignSeasonLockedRemapEntry> remapEntries,
        IEnumerable<CampaignSeasonGenerationReport> generationReports)
    {
        Mode = mode;
        SourceLockedTileCount = sourceLockedTileCount;
        FinalLockedTileCount = finalLockedTileCount;
        MovedLockedTileCount = movedLockedTileCount;
        MergedLockedTileCount = mergedLockedTileCount;
        DisplacedLockedTileCount = displacedLockedTileCount;
        DropsPermitted = dropsPermitted;
        LockedDrops = Array.AsReadOnly(lockedDrops
            .OrderBy(static value => value.SourceY)
            .ThenBy(static value => value.SourceX)
            .ThenBy(static value => value.SeasonId, StringComparer.Ordinal)
            .ToArray());
        Conflicts = Array.AsReadOnly(conflicts
            .OrderBy(static value => value.TargetY)
            .ThenBy(static value => value.TargetX)
            .ToArray());
        RemapEntries = Array.AsReadOnly(remapEntries
            .OrderBy(static value => value.SourceY)
            .ThenBy(static value => value.SourceX)
            .ThenBy(static value => value.SeasonId, StringComparer.Ordinal)
            .ToArray());
        GenerationReports = Array.AsReadOnly(generationReports
            .OrderBy(static value => value.SeasonId, StringComparer.Ordinal)
            .ToArray());
    }

    public CampaignSeasonLatticeRemapMode Mode { get; }

    public bool SameLattice => Mode == CampaignSeasonLatticeRemapMode.PreserveSameLattice;

    public int SourceLockedTileCount { get; }

    public int FinalLockedTileCount { get; }

    public int MovedLockedTileCount { get; }

    public int MergedLockedTileCount { get; }

    public int DisplacedLockedTileCount { get; }

    public bool DropsPermitted { get; }

    public IReadOnlyList<CampaignSeasonLockedDrop> LockedDrops { get; }

    public IReadOnlyList<CampaignSeasonLockConflict> Conflicts { get; }

    public IReadOnlyList<CampaignSeasonLockedRemapEntry> RemapEntries { get; }

    public IReadOnlyList<CampaignSeasonGenerationReport> GenerationReports { get; }

    public int UnresolvedConflictCount => Conflicts.Count(static value => !value.IsResolved);

    public bool HasUnpermittedDrops => LockedDrops.Count > 0 && !DropsPermitted;

    public bool CanAccept => UnresolvedConflictCount == 0 && !HasUnpermittedDrops;
}

public sealed class CampaignSeasonWorldRegenerationSource
{
    private CampaignSeasonWorldRegenerationSource(
        CampaignWorldDefinition definition,
        long terrainRevision,
        CampaignSeasonCatalog catalog,
        string defaultSeasonId,
        long seasonRevision,
        IReadOnlyList<string> priorityIds,
        CampaignSeasonSavedGeneration? savedGeneration,
        CampaignSeasonEntry[] entries)
    {
        Definition = definition;
        TerrainRevision = terrainRevision;
        Catalog = catalog;
        DefaultSeasonId = defaultSeasonId;
        SeasonRevision = seasonRevision;
        PriorityIds = Array.AsReadOnly(priorityIds.ToArray());
        SavedGeneration = savedGeneration;
        Entries = Array.AsReadOnly(entries);
    }

    public CampaignWorldDefinition Definition { get; }

    public long TerrainRevision { get; }

    public CampaignSeasonCatalog Catalog { get; }

    public string DefaultSeasonId { get; }

    public long SeasonRevision { get; }

    public IReadOnlyList<string> PriorityIds { get; }

    public CampaignSeasonSavedGeneration? SavedGeneration { get; }

    public IReadOnlyList<CampaignSeasonEntry> Entries { get; }

    public static CampaignSeasonWorldRegenerationSource Capture(
        CampaignWorld world,
        CampaignSeasonMap seasonMap,
        IEnumerable<string> priorityIds,
        CampaignSeasonSavedGeneration? savedGeneration = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(seasonMap);
        ArgumentNullException.ThrowIfNull(priorityIds);
        if (world.Definition != seasonMap.Definition)
        {
            throw new ArgumentException(
                "The terrain world and Season Layer must use the same value-equal definition.",
                nameof(seasonMap));
        }

        seasonMap.EnsureValid();
        var priority = ValidatePriority(seasonMap.Catalog, priorityIds);
        savedGeneration?.Settings.EnsureValid(seasonMap.Catalog, world.Definition);
        if (savedGeneration is not null &&
            !savedGeneration.Settings.PriorityIds.SequenceEqual(priority, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Saved Season settings and the active priority must match exactly.",
                nameof(savedGeneration));
        }

        var terrainRevisionBefore = world.Revision;
        var seasonRevisionBefore = seasonMap.Revision;
        var entries = seasonMap.GetAllTiles().ToArray();
        if (terrainRevisionBefore != world.Revision || seasonRevisionBefore != seasonMap.Revision)
        {
            throw new InvalidOperationException(
                "Terrain or seasons changed while the world-regeneration source was being captured.");
        }

        return new CampaignSeasonWorldRegenerationSource(
            world.Definition with { },
            terrainRevisionBefore,
            seasonMap.Catalog,
            seasonMap.DefaultSeasonId,
            seasonRevisionBefore,
            priority,
            savedGeneration,
            entries);
    }

    internal static IReadOnlyList<string> ValidatePriority(
        CampaignSeasonCatalog catalog,
        IEnumerable<string> priorityIds)
    {
        var priority = priorityIds.ToArray();
        if (priority.Length is 0 or > CampaignSeasonGenerationSettings.MaximumEnabledDefinitionCount)
        {
            throw new ArgumentException(
                $"Season priority must contain 1 through {CampaignSeasonGenerationSettings.MaximumEnabledDefinitionCount} definitions.",
                nameof(priorityIds));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seasonId in priority)
        {
            if (!catalog.Contains(seasonId) || !seen.Add(seasonId))
            {
                throw new ArgumentException(
                    $"Season priority contains unknown or duplicate ID '{seasonId}'.",
                    nameof(priorityIds));
            }
        }

        return Array.AsReadOnly(priority);
    }
}

public sealed class CampaignSeasonWorldRegenerationResult
{
    internal CampaignSeasonWorldRegenerationResult(
        CampaignSeasonWorldRegenerationSource source,
        CampaignWorld candidateWorld,
        CampaignSeasonMap candidateMap,
        CampaignSeasonGenerationSettings? settings,
        CampaignSeasonSavedGeneration? savedGeneration,
        CampaignSeasonSupportFields? supportFields,
        CampaignSeasonWorldRegenerationReport report)
    {
        SourceDefinition = source.Definition;
        SourceTerrainRevision = source.TerrainRevision;
        SourceSeasonRevision = source.SeasonRevision;
        SourcePriorityIds = Array.AsReadOnly(source.PriorityIds.ToArray());
        SourceSavedGeneration = source.SavedGeneration;
        CandidateMap = candidateMap ?? throw new ArgumentNullException(nameof(candidateMap));
        Settings = settings;
        SavedGeneration = savedGeneration;
        SupportFields = supportFields;
        Report = report ?? throw new ArgumentNullException(nameof(report));
        CandidateTerrainRevision = candidateWorld?.Revision ??
            throw new ArgumentNullException(nameof(candidateWorld));
        CandidateSeasonRevision = candidateMap.Revision;
        if (candidateWorld.Definition != candidateMap.Definition)
        {
            throw new ArgumentException(
                "Candidate terrain and seasons must use the same value-equal definition.",
                nameof(candidateMap));
        }

        if (!ReferenceEquals(source.Catalog, candidateMap.Catalog) ||
            !string.Equals(source.DefaultSeasonId, candidateMap.DefaultSeasonId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Candidate seasons must retain the captured catalog and default identity.",
                nameof(candidateMap));
        }

        candidateMap.EnsureValid();
        settings?.EnsureValid(candidateMap.Catalog, candidateMap.Definition);
        if (report.FinalLockedTileCount != candidateMap.LockedTileCount)
        {
            throw new ArgumentException(
                "The Season remap report does not match the candidate lock count.",
                nameof(report));
        }

        if (report.SameLattice)
        {
            if (settings is not null || supportFields is not null ||
                !ReferenceEquals(savedGeneration, source.SavedGeneration))
            {
                throw new ArgumentException(
                    "A same-lattice Season candidate must preserve saved authority without new generation support.",
                    nameof(report));
            }
        }
        else if (settings is null || savedGeneration is null || supportFields is null ||
                 !ReferenceEquals(savedGeneration.Settings, settings) ||
                 !ReferenceEquals(supportFields.Settings, settings))
        {
            throw new ArgumentException(
                "A changed-lattice Season candidate requires one exact settings, recipe, and support tuple.",
                nameof(report));
        }

        if (supportFields is not null && supportFields.Terrain.Definition != candidateMap.Definition)
        {
            throw new ArgumentException(
                "Candidate Season support fields must use the replacement world definition.",
                nameof(supportFields));
        }
    }

    public CampaignWorldDefinition SourceDefinition { get; }

    public long SourceTerrainRevision { get; }

    public long SourceSeasonRevision { get; }

    public IReadOnlyList<string> SourcePriorityIds { get; }

    public CampaignSeasonSavedGeneration? SourceSavedGeneration { get; }

    public CampaignSeasonMap CandidateMap { get; }

    public CampaignSeasonGenerationSettings? Settings { get; }

    public CampaignSeasonSavedGeneration? SavedGeneration { get; }

    public CampaignSeasonSupportFields? SupportFields { get; }

    public CampaignSeasonWorldRegenerationReport Report { get; }

    public long CandidateTerrainRevision { get; }

    public long CandidateSeasonRevision { get; }

    public bool IsCurrent(
        CampaignWorld currentWorld,
        CampaignSeasonMap currentSeasons,
        CampaignWorld candidateWorld)
    {
        ArgumentNullException.ThrowIfNull(currentWorld);
        ArgumentNullException.ThrowIfNull(currentSeasons);
        ArgumentNullException.ThrowIfNull(candidateWorld);
        return currentWorld.Definition == SourceDefinition &&
            currentWorld.Revision == SourceTerrainRevision &&
            currentSeasons.Definition == SourceDefinition &&
            currentSeasons.Revision == SourceSeasonRevision &&
            ReferenceEquals(currentSeasons.Catalog, CandidateMap.Catalog) &&
            candidateWorld.Definition == CandidateMap.Definition &&
            candidateWorld.Revision == CandidateTerrainRevision &&
            CandidateMap.Revision == CandidateSeasonRevision;
    }
}

public sealed class CampaignSeasonNewWorldGenerationResult
{
    internal CampaignSeasonNewWorldGenerationResult(
        CampaignWorld world,
        CampaignSeasonGenerationResult generationResult,
        CampaignSeasonSavedGeneration savedGeneration)
    {
        CandidateMap = generationResult.CandidateMap;
        GenerationResult = generationResult;
        SavedGeneration = savedGeneration;
        CandidateTerrainRevision = world.Revision;
        CandidateSeasonRevision = CandidateMap.Revision;
        if (!ReferenceEquals(savedGeneration.Settings, generationResult.Settings) ||
            !ReferenceEquals(generationResult.SupportFields.Settings, generationResult.Settings))
        {
            throw new ArgumentException(
                "A new-world Season candidate requires one exact settings, recipe, and support tuple.",
                nameof(generationResult));
        }
    }

    public CampaignSeasonMap CandidateMap { get; }

    public CampaignSeasonGenerationResult GenerationResult { get; }

    public CampaignSeasonSavedGeneration SavedGeneration { get; }

    public long CandidateTerrainRevision { get; }

    public long CandidateSeasonRevision { get; }

    public bool IsCurrent(CampaignWorld candidateWorld) =>
        candidateWorld.Definition == CandidateMap.Definition &&
        candidateWorld.Revision == CandidateTerrainRevision &&
        CandidateMap.Revision == CandidateSeasonRevision;
}

/// <summary>
/// Builds the Season half of a private terrain replacement candidate. Live source authority
/// is captured on the owner thread; this deterministic generator can run on a worker.
/// </summary>
public sealed class CampaignSeasonWorldRegenerator
{
    public CampaignSeasonNewWorldGenerationResult GenerateNewWorld(
        CampaignWorld candidateWorld,
        CampaignSeasonCatalog catalog,
        string defaultSeasonId,
        CampaignSeasonGenerationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateWorld);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid(catalog, candidateWorld.Definition);
        var initialized = new CampaignSeasonMap(candidateWorld.Definition, catalog, defaultSeasonId);
        var query = new CampaignSeasonTerrainQueryV2(candidateWorld);
        var source = CampaignSeasonGenerationSource.Capture(query, initialized, cancellationToken);
        var generated = CampaignSeasonGenerator.Generate(
            source,
            catalog,
            settings,
            CampaignSeasonGenerationScope.All,
            cancellationToken);
        var saved = CreateSavedGeneration(source, catalog, settings);
        cancellationToken.ThrowIfCancellationRequested();
        return new CampaignSeasonNewWorldGenerationResult(candidateWorld, generated, saved);
    }

    public CampaignSeasonWorldRegenerationResult Generate(
        CampaignSeasonWorldRegenerationSource source,
        CampaignWorld candidateWorld,
        CampaignSeasonGenerationSettings changedLatticeSettings,
        IEnumerable<CampaignSeasonLockResolution>? resolutions = null,
        bool permitLockedDrops = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidateWorld);
        ArgumentNullException.ThrowIfNull(changedLatticeSettings);
        cancellationToken.ThrowIfCancellationRequested();
        CampaignWorldDefinition.EnsureValid(candidateWorld.Definition);
        changedLatticeSettings.EnsureValid(source.Catalog, candidateWorld.Definition);
        if (!changedLatticeSettings.PriorityIds.SequenceEqual(
                source.PriorityIds,
                StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Changed-lattice Season settings must retain the captured active priority.",
                nameof(changedLatticeSettings));
        }

        if (HasSameCampaignLattice(source.Definition, candidateWorld.Definition))
        {
            var exact = CampaignSeasonMap.CreateSnapshot(
                candidateWorld.Definition,
                source.Catalog,
                source.DefaultSeasonId,
                source.Entries.Select(static value => value.Tile).ToArray());
            var preservedReport = new CampaignSeasonWorldRegenerationReport(
                CampaignSeasonLatticeRemapMode.PreserveSameLattice,
                source.Entries.Count(static value => value.Tile.Locked),
                exact.LockedTileCount,
                movedLockedTileCount: 0,
                mergedLockedTileCount: 0,
                displacedLockedTileCount: 0,
                dropsPermitted: true,
                lockedDrops: [],
                conflicts: [],
                remapEntries: source.Entries
                    .Where(static value => value.Tile.Locked)
                    .Select(static value => new CampaignSeasonLockedRemapEntry(
                        value.X,
                        value.Y,
                        value.Tile.SeasonId,
                        value.X,
                        value.Y,
                        100,
                        0,
                        CampaignSeasonLockedRemapOutcome.Preserved)),
                generationReports: []);
            return new CampaignSeasonWorldRegenerationResult(
                source,
                candidateWorld,
                exact,
                settings: null,
                source.SavedGeneration,
                supportFields: null,
                preservedReport);
        }

        var resolutionMap = ValidateResolutions(resolutions);
        var remap = RemapLocks(
            source,
            candidateWorld.Definition,
            resolutionMap,
            permitLockedDrops,
            cancellationToken);
        if (resolutionMap.Count != remap.ConsumedResolutionCount)
        {
            throw new ArgumentException(
                "A Season lock resolution does not match a current equal-overlap conflict.",
                nameof(resolutions));
        }

        var query = new CampaignSeasonTerrainQueryV2(candidateWorld);
        var generationSource = CampaignSeasonGenerationSource.Capture(
            query,
            remap.Map,
            cancellationToken);
        var generationResult = CampaignSeasonGenerator.Generate(
            generationSource,
            source.Catalog,
            changedLatticeSettings,
            CampaignSeasonGenerationScope.All,
            cancellationToken);
        var saved = CreateSavedGeneration(generationSource, source.Catalog, changedLatticeSettings);
        var generatedReport = remap.CreateReport(generationResult);
        cancellationToken.ThrowIfCancellationRequested();
        return new CampaignSeasonWorldRegenerationResult(
            source,
            candidateWorld,
            generationResult.CandidateMap,
            changedLatticeSettings,
            saved,
            generationResult.SupportFields,
            generatedReport);
    }

    private static CampaignSeasonSavedGeneration CreateSavedGeneration(
        CampaignSeasonGenerationSource source,
        CampaignSeasonCatalog catalog,
        CampaignSeasonGenerationSettings settings) => new(
        settings,
        CampaignSeasonGenerationFingerprint.GetSourceTerrainFingerprint(source.Terrain),
        CampaignSeasonGenerationFingerprint.GetInputFingerprint(catalog, settings));

    private static IReadOnlyDictionary<TargetCoordinate, string> ValidateResolutions(
        IEnumerable<CampaignSeasonLockResolution>? resolutions)
    {
        var result = new Dictionary<TargetCoordinate, string>();
        foreach (var resolution in resolutions ?? [])
        {
            if (!CampaignSeasonDefinition.IsValidIdentifier(resolution.SeasonId))
            {
                throw new ArgumentException(
                    "Season lock resolutions require valid stable IDs.",
                    nameof(resolutions));
            }

            var coordinate = new TargetCoordinate(resolution.TargetX, resolution.TargetY);
            if (!result.TryAdd(coordinate, resolution.SeasonId))
            {
                throw new ArgumentException(
                    $"Season lock target ({resolution.TargetX}, {resolution.TargetY}) is resolved more than once.",
                    nameof(resolutions));
            }
        }

        return result;
    }

    private static RemapWorkResult RemapLocks(
        CampaignSeasonWorldRegenerationSource source,
        CampaignWorldDefinition targetDefinition,
        IReadOnlyDictionary<TargetCoordinate, string> resolutions,
        bool permitLockedDrops,
        CancellationToken cancellationToken)
    {
        var targetClaims = new Dictionary<TargetCoordinate, List<MutableClaim>>();
        var drops = new List<CampaignSeasonLockedDrop>();
        var outcomes = new Dictionary<SourceCoordinate, CampaignSeasonLockedRemapEntry>();
        var lockedEntries = source.Entries.Where(static value => value.Tile.Locked).ToArray();
        for (var index = 0; index < lockedEntries.Length; index++)
        {
            if ((index & 0x03FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var entry = lockedEntries[index];
            if (!TryFindGreatestOverlap(
                    entry,
                    source.Definition,
                    targetDefinition,
                    out var targetX,
                    out var targetY,
                    out var overlapPercent,
                    out var outOfBoundsPercent,
                    out var overlapArea))
            {
                drops.Add(new CampaignSeasonLockedDrop(
                    entry.X,
                    entry.Y,
                    entry.Tile.SeasonId,
                    outOfBoundsPercent));
                outcomes.Add(
                    new SourceCoordinate(entry.X, entry.Y),
                    new CampaignSeasonLockedRemapEntry(
                        entry.X,
                        entry.Y,
                        entry.Tile.SeasonId,
                        TargetX: null,
                        TargetY: null,
                        OverlapPercent: 0,
                        outOfBoundsPercent,
                        CampaignSeasonLockedRemapOutcome.Dropped));
                continue;
            }

            var target = new TargetCoordinate(targetX, targetY);
            if (!targetClaims.TryGetValue(target, out var claims))
            {
                claims = [];
                targetClaims.Add(target, claims);
            }

            claims.Add(new MutableClaim(
                entry.X,
                entry.Y,
                entry.Tile.SeasonId,
                targetX,
                targetY,
                overlapArea,
                overlapPercent,
                outOfBoundsPercent));
        }

        var targetMap = new CampaignSeasonMap(
            targetDefinition,
            source.Catalog,
            source.DefaultSeasonId);
        var mutations = new List<CampaignSeasonMutation>(targetClaims.Count);
        var conflicts = new List<CampaignSeasonLockConflict>();
        var movedCount = 0;
        var mergedCount = 0;
        var displacedCount = 0;
        var consumedResolutions = 0;
        foreach (var pair in targetClaims
                     .OrderBy(static value => value.Key.Y)
                     .ThenBy(static value => value.Key.X))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = pair.Key;
            var claims = pair.Value;
            var maximumOverlap = claims.Max(static value => value.OverlapArea);
            var greatest = claims
                .Where(value => value.OverlapArea == maximumOverlap)
                .OrderBy(static value => value.SeasonId, StringComparer.Ordinal)
                .ThenBy(static value => value.SourceY)
                .ThenBy(static value => value.SourceX)
                .ToArray();
            var greatestIds = greatest
                .Select(static value => value.SeasonId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string winnerId;
            if (greatestIds.Length == 1)
            {
                winnerId = greatestIds[0];
            }
            else
            {
                resolutions.TryGetValue(target, out var resolvedId);
                if (resolvedId is not null && !greatestIds.Contains(resolvedId, StringComparer.Ordinal))
                {
                    throw new ArgumentException(
                        $"Resolution for Season target ({target.X}, {target.Y}) must select one of its equal-overlap IDs.",
                        nameof(resolutions));
                }

                if (resolvedId is not null)
                {
                    consumedResolutions++;
                }

                conflicts.Add(new CampaignSeasonLockConflict(
                    target.X,
                    target.Y,
                    Array.AsReadOnly(greatest.Select(static value => value.ToPublicClaim()).ToArray()),
                    resolvedId));
                if (resolvedId is null)
                {
                    // A dense Season map cannot contain an absent value. Reserve this target with
                    // the project default, then expose the coordinate as an unresolved conflict so
                    // preview clients can render a sentinel and omit it from observed distribution.
                    // This placeholder is never acceptable authority and does not choose a claimant.
                    mutations.Add(new CampaignSeasonMutation(
                        target.X,
                        target.Y,
                        new CampaignSeasonTile(source.DefaultSeasonId, Locked: true)));
                    foreach (var claim in claims)
                    {
                        var outcome = claim.OverlapArea == maximumOverlap
                            ? CampaignSeasonLockedRemapOutcome.Conflict
                            : CampaignSeasonLockedRemapOutcome.Displaced;
                        if (outcome == CampaignSeasonLockedRemapOutcome.Displaced)
                        {
                            displacedCount++;
                        }

                        outcomes.Add(
                            new SourceCoordinate(claim.SourceX, claim.SourceY),
                            claim.ToRemapEntry(outcome));
                    }

                    continue;
                }

                winnerId = resolvedId;
            }

            mutations.Add(new CampaignSeasonMutation(
                target.X,
                target.Y,
                new CampaignSeasonTile(winnerId, Locked: true)));
            var winningClaims = claims
                .Where(value => string.Equals(value.SeasonId, winnerId, StringComparison.Ordinal))
                .OrderByDescending(static value => value.OverlapArea)
                .ThenBy(static value => value.SourceY)
                .ThenBy(static value => value.SourceX)
                .ToArray();
            var primaryWinner = winningClaims[0];
            foreach (var claim in claims)
            {
                var sourceCoordinate = new SourceCoordinate(claim.SourceX, claim.SourceY);
                CampaignSeasonLockedRemapOutcome outcome;
                if (!string.Equals(claim.SeasonId, winnerId, StringComparison.Ordinal))
                {
                    outcome = CampaignSeasonLockedRemapOutcome.Displaced;
                    displacedCount++;
                }
                else if (!ReferenceEquals(claim, primaryWinner))
                {
                    outcome = CampaignSeasonLockedRemapOutcome.Merged;
                    mergedCount++;
                }
                else
                {
                    outcome = CampaignSeasonLockedRemapOutcome.Preserved;
                }

                if ((outcome is CampaignSeasonLockedRemapOutcome.Preserved or
                        CampaignSeasonLockedRemapOutcome.Merged) &&
                    (claim.SourceX != target.X || claim.SourceY != target.Y))
                {
                    movedCount++;
                }

                outcomes.Add(sourceCoordinate, claim.ToRemapEntry(outcome));
            }
        }

        if (mutations.Count > 0)
        {
            targetMap.Apply(mutations);
        }

        targetMap.EnsureValid();
        return new RemapWorkResult(
            targetMap,
            lockedEntries.Length,
            movedCount,
            mergedCount,
            displacedCount,
            permitLockedDrops,
            drops,
            conflicts,
            outcomes.Values,
            consumedResolutions);
    }

    private static bool TryFindGreatestOverlap(
        CampaignSeasonEntry entry,
        CampaignWorldDefinition sourceDefinition,
        CampaignWorldDefinition targetDefinition,
        out int targetX,
        out int targetY,
        out double overlapPercent,
        out double outOfBoundsPercent,
        out decimal overlapArea)
    {
        var sourceSize = (decimal)sourceDefinition.CampaignTileSizeMeters;
        var targetSize = (decimal)targetDefinition.CampaignTileSizeMeters;
        var sourceMinimumX = entry.X * sourceSize;
        var sourceMinimumY = entry.Y * sourceSize;
        var sourceMaximumX = sourceMinimumX + sourceSize;
        var sourceMaximumY = sourceMinimumY + sourceSize;
        var clippedMaximumX = Math.Min(sourceMaximumX, targetDefinition.WorldWidthMeters);
        var clippedMaximumY = Math.Min(sourceMaximumY, targetDefinition.WorldHeightMeters);
        var clippedMinimumX = Math.Max(0, sourceMinimumX);
        var clippedMinimumY = Math.Max(0, sourceMinimumY);
        var sourceArea = sourceSize * sourceSize;
        var inBoundsWidth = Math.Max(0, clippedMaximumX - clippedMinimumX);
        var inBoundsHeight = Math.Max(0, clippedMaximumY - clippedMinimumY);
        var inBoundsArea = inBoundsWidth * inBoundsHeight;
        outOfBoundsPercent = decimal.ToDouble((sourceArea - inBoundsArea) * 100 / sourceArea);
        if (inBoundsArea <= 0)
        {
            targetX = -1;
            targetY = -1;
            overlapPercent = 0;
            overlapArea = 0;
            return false;
        }

        var minimumTargetX = decimal.ToInt32(decimal.Floor(clippedMinimumX / targetSize));
        var minimumTargetY = decimal.ToInt32(decimal.Floor(clippedMinimumY / targetSize));
        var maximumTargetX = decimal.ToInt32(decimal.Ceiling(clippedMaximumX / targetSize)) - 1;
        var maximumTargetY = decimal.ToInt32(decimal.Ceiling(clippedMaximumY / targetSize)) - 1;
        minimumTargetX = Math.Clamp(minimumTargetX, 0, targetDefinition.TilesX - 1);
        minimumTargetY = Math.Clamp(minimumTargetY, 0, targetDefinition.TilesY - 1);
        maximumTargetX = Math.Clamp(maximumTargetX, 0, targetDefinition.TilesX - 1);
        maximumTargetY = Math.Clamp(maximumTargetY, 0, targetDefinition.TilesY - 1);
        var sourceCenterX = sourceMinimumX + (sourceSize / 2);
        var sourceCenterY = sourceMinimumY + (sourceSize / 2);
        var candidates = new List<(int X, int Y, decimal Area, bool ContainsCentre)>();
        overlapArea = 0;
        for (var y = minimumTargetY; y <= maximumTargetY; y++)
        {
            var targetMinimumY = y * targetSize;
            var targetMaximumY = targetMinimumY + targetSize;
            var overlapY = Math.Max(
                0,
                Math.Min(sourceMaximumY, targetMaximumY) - Math.Max(sourceMinimumY, targetMinimumY));
            for (var x = minimumTargetX; x <= maximumTargetX; x++)
            {
                var targetMinimumX = x * targetSize;
                var targetMaximumX = targetMinimumX + targetSize;
                var overlapX = Math.Max(
                    0,
                    Math.Min(sourceMaximumX, targetMaximumX) - Math.Max(sourceMinimumX, targetMinimumX));
                var area = overlapX * overlapY;
                if (area <= 0)
                {
                    continue;
                }

                var containsCentre =
                    sourceCenterX >= targetMinimumX && sourceCenterX < targetMaximumX &&
                    sourceCenterY >= targetMinimumY && sourceCenterY < targetMaximumY;
                candidates.Add((x, y, area, containsCentre));
                overlapArea = Math.Max(overlapArea, area);
            }
        }

        var greatestArea = overlapArea;
        var selected = candidates
            .Where(value => value.Area == greatestArea)
            .OrderByDescending(static value => value.ContainsCentre)
            .ThenBy(static value => value.Y)
            .ThenBy(static value => value.X)
            .First();
        targetX = selected.X;
        targetY = selected.Y;
        overlapPercent = decimal.ToDouble(overlapArea * 100 / sourceArea);
        return true;
    }

    private static bool HasSameCampaignLattice(
        CampaignWorldDefinition left,
        CampaignWorldDefinition right) =>
        left.WorldWidthMeters == right.WorldWidthMeters &&
        left.WorldHeightMeters == right.WorldHeightMeters &&
        left.CampaignTileSizeMeters == right.CampaignTileSizeMeters;

    private readonly record struct SourceCoordinate(int X, int Y);

    private readonly record struct TargetCoordinate(int X, int Y);

    private sealed class MutableClaim(
        int sourceX,
        int sourceY,
        string seasonId,
        int targetX,
        int targetY,
        decimal overlapArea,
        double overlapPercent,
        double outOfBoundsPercent)
    {
        public int SourceX { get; } = sourceX;

        public int SourceY { get; } = sourceY;

        public string SeasonId { get; } = seasonId;

        public int TargetX { get; } = targetX;

        public int TargetY { get; } = targetY;

        public decimal OverlapArea { get; } = overlapArea;

        public double OverlapPercent { get; } = overlapPercent;

        public double OutOfBoundsPercent { get; } = outOfBoundsPercent;

        public CampaignSeasonLockClaim ToPublicClaim() => new(
            SourceX,
            SourceY,
            SeasonId,
            TargetX,
            TargetY,
            OverlapPercent,
            OutOfBoundsPercent);

        public CampaignSeasonLockedRemapEntry ToRemapEntry(
            CampaignSeasonLockedRemapOutcome outcome) => new(
            SourceX,
            SourceY,
            SeasonId,
            TargetX,
            TargetY,
            OverlapPercent,
            OutOfBoundsPercent,
            outcome);
    }

    private sealed record RemapWorkResult(
        CampaignSeasonMap Map,
        int SourceLockedTileCount,
        int MovedLockedTileCount,
        int MergedLockedTileCount,
        int DisplacedLockedTileCount,
        bool DropsPermitted,
        IReadOnlyList<CampaignSeasonLockedDrop> LockedDrops,
        IReadOnlyList<CampaignSeasonLockConflict> Conflicts,
        IEnumerable<CampaignSeasonLockedRemapEntry> RemapEntries,
        int ConsumedResolutionCount)
    {
        public CampaignSeasonWorldRegenerationReport CreateReport(
            CampaignSeasonGenerationResult generationResult) => new(
            CampaignSeasonLatticeRemapMode.RemapLocksAndRegenerateUnlocked,
            SourceLockedTileCount,
            generationResult.CandidateMap.LockedTileCount,
            MovedLockedTileCount,
            MergedLockedTileCount,
            DisplacedLockedTileCount,
            DropsPermitted,
            LockedDrops,
            Conflicts,
            RemapEntries,
            generationResult.Reports);
    }
}
