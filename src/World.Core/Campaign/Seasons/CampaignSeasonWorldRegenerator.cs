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
    Dropped,
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
        int sourceLockedOccurrenceCount,
        int finalLockedOccurrenceCount,
        int movedLockedOccurrenceCount,
        int mergedLockedOccurrenceCount,
        bool dropsPermitted,
        IEnumerable<CampaignSeasonLockedDrop> lockedDrops,
        IEnumerable<CampaignSeasonLockedRemapEntry> remapEntries,
        IEnumerable<CampaignSeasonGenerationReport> generationReports)
    {
        Mode = mode;
        SourceLockedOccurrenceCount = sourceLockedOccurrenceCount;
        FinalLockedOccurrenceCount = finalLockedOccurrenceCount;
        MovedLockedOccurrenceCount = movedLockedOccurrenceCount;
        MergedLockedOccurrenceCount = mergedLockedOccurrenceCount;
        DropsPermitted = dropsPermitted;
        LockedDrops = Array.AsReadOnly((lockedDrops ?? throw new ArgumentNullException(nameof(lockedDrops)))
            .OrderBy(static value => value.SourceY)
            .ThenBy(static value => value.SourceX)
            .ThenBy(static value => value.SeasonId, StringComparer.Ordinal)
            .ToArray());
        RemapEntries = Array.AsReadOnly((remapEntries ?? throw new ArgumentNullException(nameof(remapEntries)))
            .OrderBy(static value => value.SourceY)
            .ThenBy(static value => value.SourceX)
            .ThenBy(static value => value.SeasonId, StringComparer.Ordinal)
            .ToArray());
        GenerationReports = Array.AsReadOnly((generationReports ?? throw new ArgumentNullException(nameof(generationReports)))
            .OrderBy(static value => value.SeasonId, StringComparer.Ordinal)
            .ToArray());
    }

    public CampaignSeasonLatticeRemapMode Mode { get; }

    public bool SameLattice => Mode == CampaignSeasonLatticeRemapMode.PreserveSameLattice;

    public int SourceLockedOccurrenceCount { get; }

    public int FinalLockedOccurrenceCount { get; }

    public int MovedLockedOccurrenceCount { get; }

    public int MergedLockedOccurrenceCount { get; }

    public bool DropsPermitted { get; }

    public IReadOnlyList<CampaignSeasonLockedDrop> LockedDrops { get; }

    public IReadOnlyList<CampaignSeasonLockedRemapEntry> RemapEntries { get; }

    public IReadOnlyList<CampaignSeasonGenerationReport> GenerationReports { get; }

    public bool HasUnpermittedDrops => LockedDrops.Count > 0 && !DropsPermitted;

    public bool CanAccept => !HasUnpermittedDrops;
}

public sealed class CampaignSeasonWorldRegenerationSource
{
    private CampaignSeasonWorldRegenerationSource(
        CampaignWorldDefinition definition,
        long terrainRevision,
        CampaignSeasonCatalog catalog,
        long seasonRevision,
        IReadOnlyList<string> enabledSeasonIds,
        CampaignSeasonSavedGeneration? savedGeneration,
        CampaignSeasonEntry[] entries)
    {
        Definition = definition;
        TerrainRevision = terrainRevision;
        Catalog = catalog;
        SeasonRevision = seasonRevision;
        EnabledSeasonIds = Array.AsReadOnly(enabledSeasonIds.ToArray());
        SavedGeneration = savedGeneration;
        Entries = Array.AsReadOnly(entries);
    }

    public CampaignWorldDefinition Definition { get; }

    public long TerrainRevision { get; }

    public CampaignSeasonCatalog Catalog { get; }

    public long SeasonRevision { get; }

    public IReadOnlyList<string> EnabledSeasonIds { get; }

    public CampaignSeasonSavedGeneration? SavedGeneration { get; }

    public IReadOnlyList<CampaignSeasonEntry> Entries { get; }

    public static CampaignSeasonWorldRegenerationSource Capture(
        CampaignWorld world,
        CampaignSeasonMap seasonMap,
        IEnumerable<string> enabledSeasonIds,
        CampaignSeasonSavedGeneration? savedGeneration = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(seasonMap);
        ArgumentNullException.ThrowIfNull(enabledSeasonIds);
        if (world.Definition != seasonMap.Definition)
        {
            throw new ArgumentException(
                "The terrain world and Season Layer must use the same value-equal definition.",
                nameof(seasonMap));
        }

        seasonMap.EnsureValid();
        var enabled = ValidateEnabledSelection(seasonMap.Catalog, enabledSeasonIds);
        savedGeneration?.Settings.EnsureValid(seasonMap.Catalog, world.Definition);
        if (savedGeneration is not null &&
            !savedGeneration.Settings.EnabledSeasonIds.SequenceEqual(enabled, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Saved Season settings and the active generation selection must match exactly.",
                nameof(savedGeneration));
        }

        var terrainRevisionBefore = world.Revision;
        var seasonRevisionBefore = seasonMap.Revision;
        var entries = seasonMap.GetMaterializedOccurrences().ToArray();
        if (terrainRevisionBefore != world.Revision || seasonRevisionBefore != seasonMap.Revision)
        {
            throw new InvalidOperationException(
                "Terrain or seasons changed while the world-regeneration source was being captured.");
        }

        return new CampaignSeasonWorldRegenerationSource(
            world.Definition with { },
            terrainRevisionBefore,
            seasonMap.Catalog,
            seasonRevisionBefore,
            enabled,
            savedGeneration,
            entries);
    }

    internal static IReadOnlyList<string> ValidateEnabledSelection(
        CampaignSeasonCatalog catalog,
        IEnumerable<string> enabledSeasonIds)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(enabledSeasonIds);
        var enabled = enabledSeasonIds.Order(StringComparer.Ordinal).ToArray();
        if (enabled.Length is 0 or > CampaignSeasonGenerationSettings.MaximumEnabledDefinitionCount)
        {
            throw new ArgumentException(
                $"Season generation selection must contain 1 through {CampaignSeasonGenerationSettings.MaximumEnabledDefinitionCount} definitions.",
                nameof(enabledSeasonIds));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seasonId in enabled)
        {
            if (!catalog.Contains(seasonId) || !seen.Add(seasonId))
            {
                throw new ArgumentException(
                    $"Season generation selection contains unknown or duplicate ID '{seasonId}'.",
                    nameof(enabledSeasonIds));
            }
        }

        return Array.AsReadOnly(enabled);
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
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidateWorld);
        CandidateMap = candidateMap ?? throw new ArgumentNullException(nameof(candidateMap));
        Report = report ?? throw new ArgumentNullException(nameof(report));
        SourceDefinition = source.Definition;
        SourceTerrainRevision = source.TerrainRevision;
        SourceSeasonRevision = source.SeasonRevision;
        SourceEnabledSeasonIds = Array.AsReadOnly(source.EnabledSeasonIds.ToArray());
        SourceSavedGeneration = source.SavedGeneration;
        Settings = settings;
        SavedGeneration = savedGeneration;
        SupportFields = supportFields;
        CandidateTerrainRevision = candidateWorld.Revision;
        CandidateSeasonRevision = candidateMap.Revision;
        if (candidateWorld.Definition != candidateMap.Definition)
        {
            throw new ArgumentException(
                "Candidate terrain and seasons must use the same value-equal definition.",
                nameof(candidateMap));
        }

        if (!ReferenceEquals(source.Catalog, candidateMap.Catalog))
        {
            throw new ArgumentException(
                "Candidate seasons must retain the captured catalog.",
                nameof(candidateMap));
        }

        candidateMap.EnsureValid();
        settings?.EnsureValid(candidateMap.Catalog, candidateMap.Definition);
        if (report.FinalLockedOccurrenceCount != candidateMap.LockedOccurrenceCount)
        {
            throw new ArgumentException(
                "The Season remap report does not match the candidate locked-occurrence count.",
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
    }

    public CampaignWorldDefinition SourceDefinition { get; }

    public long SourceTerrainRevision { get; }

    public long SourceSeasonRevision { get; }

    public IReadOnlyList<string> SourceEnabledSeasonIds { get; }

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
        ArgumentNullException.ThrowIfNull(world);
        GenerationResult = generationResult ?? throw new ArgumentNullException(nameof(generationResult));
        SavedGeneration = savedGeneration ?? throw new ArgumentNullException(nameof(savedGeneration));
        CandidateMap = generationResult.CandidateMap;
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
/// Builds the Season Occurrence half of a private terrain replacement candidate. Live source
/// authority is captured on the owner thread; deterministic generation can run on a worker.
/// </summary>
public sealed class CampaignSeasonWorldRegenerator
{
    public CampaignSeasonNewWorldGenerationResult GenerateNewWorld(
        CampaignWorld candidateWorld,
        CampaignSeasonCatalog catalog,
        CampaignSeasonGenerationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateWorld);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid(catalog, candidateWorld.Definition);
        var initialized = new CampaignSeasonMap(candidateWorld.Definition, catalog);
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
        bool permitLockedDrops = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidateWorld);
        ArgumentNullException.ThrowIfNull(changedLatticeSettings);
        cancellationToken.ThrowIfCancellationRequested();
        CampaignWorldDefinition.EnsureValid(candidateWorld.Definition);
        changedLatticeSettings.EnsureValid(source.Catalog, candidateWorld.Definition);
        if (!changedLatticeSettings.EnabledSeasonIds.SequenceEqual(source.EnabledSeasonIds, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Changed-lattice Season settings must retain the captured generation selection.",
                nameof(changedLatticeSettings));
        }

        if (HasSameCampaignLattice(source.Definition, candidateWorld.Definition))
        {
            var exact = CampaignSeasonMap.CreateSnapshot(candidateWorld.Definition, source.Catalog, source.Entries);
            var preservedReport = new CampaignSeasonWorldRegenerationReport(
                CampaignSeasonLatticeRemapMode.PreserveSameLattice,
                source.Entries.Count(static value => value.Occurrence.Locked),
                exact.LockedOccurrenceCount,
                movedLockedOccurrenceCount: 0,
                mergedLockedOccurrenceCount: 0,
                dropsPermitted: true,
                lockedDrops: [],
                remapEntries: source.Entries
                    .Where(static value => value.Occurrence.Locked)
                    .Select(static value => new CampaignSeasonLockedRemapEntry(
                        value.X,
                        value.Y,
                        value.Occurrence.SeasonId,
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

        var remap = RemapLockedOccurrences(source, candidateWorld.Definition, cancellationToken);
        var query = new CampaignSeasonTerrainQueryV2(candidateWorld);
        var generationSource = CampaignSeasonGenerationSource.Capture(query, remap.Map, cancellationToken);
        var generationResult = CampaignSeasonGenerator.Generate(
            generationSource,
            source.Catalog,
            changedLatticeSettings,
            CampaignSeasonGenerationScope.All,
            cancellationToken);
        var saved = CreateSavedGeneration(generationSource, source.Catalog, changedLatticeSettings);
        var report = new CampaignSeasonWorldRegenerationReport(
            CampaignSeasonLatticeRemapMode.RemapLocksAndRegenerateUnlocked,
            remap.SourceLockedOccurrenceCount,
            generationResult.CandidateMap.LockedOccurrenceCount,
            remap.MovedLockedOccurrenceCount,
            remap.MergedLockedOccurrenceCount,
            permitLockedDrops,
            remap.LockedDrops,
            remap.RemapEntries,
            generationResult.Reports);
        cancellationToken.ThrowIfCancellationRequested();
        return new CampaignSeasonWorldRegenerationResult(
            source,
            candidateWorld,
            generationResult.CandidateMap,
            changedLatticeSettings,
            saved,
            generationResult.SupportFields,
            report);
    }

    private static CampaignSeasonSavedGeneration CreateSavedGeneration(
        CampaignSeasonGenerationSource source,
        CampaignSeasonCatalog catalog,
        CampaignSeasonGenerationSettings settings) => new(
        settings,
        CampaignSeasonGenerationFingerprint.GetSourceTerrainFingerprint(source.Terrain),
        CampaignSeasonGenerationFingerprint.GetInputFingerprint(catalog, settings));

    private static RemapResult RemapLockedOccurrences(
        CampaignSeasonWorldRegenerationSource source,
        CampaignWorldDefinition targetDefinition,
        CancellationToken cancellationToken)
    {
        var locked = source.Entries.Where(static entry => entry.Occurrence.Locked).ToArray();
        var target = new CampaignSeasonMap(targetDefinition, source.Catalog);
        var mutations = new List<CampaignSeasonMutation>(locked.Length);
        var targetIdentities = new HashSet<(int X, int Y, string SeasonId)>();
        var drops = new List<CampaignSeasonLockedDrop>();
        var entries = new List<CampaignSeasonLockedRemapEntry>(locked.Length);
        var moved = 0;
        var merged = 0;
        var sourceTileSize = (decimal)source.Definition.CampaignTileSizeMeters;
        var targetTileSize = (decimal)targetDefinition.CampaignTileSizeMeters;

        for (var index = 0; index < locked.Length; index++)
        {
            if ((index & 0x03FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var entry = locked[index];
            var centerX = ((decimal)entry.X + 0.5m) * sourceTileSize;
            var centerY = ((decimal)entry.Y + 0.5m) * sourceTileSize;
            if (centerX < 0 || centerY < 0 ||
                centerX >= targetDefinition.WorldWidthMeters ||
                centerY >= targetDefinition.WorldHeightMeters)
            {
                drops.Add(new CampaignSeasonLockedDrop(entry.X, entry.Y, entry.Occurrence.SeasonId, 100));
                entries.Add(new CampaignSeasonLockedRemapEntry(
                    entry.X,
                    entry.Y,
                    entry.Occurrence.SeasonId,
                    TargetX: null,
                    TargetY: null,
                    OverlapPercent: 0,
                    OutOfBoundsPercent: 100,
                    CampaignSeasonLockedRemapOutcome.Dropped));
                continue;
            }

            var targetX = decimal.ToInt32(decimal.Floor(centerX / targetTileSize));
            var targetY = decimal.ToInt32(decimal.Floor(centerY / targetTileSize));
            var identity = (targetX, targetY, entry.Occurrence.SeasonId);
            var outcome = targetIdentities.Add(identity)
                ? CampaignSeasonLockedRemapOutcome.Preserved
                : CampaignSeasonLockedRemapOutcome.Merged;
            if (outcome == CampaignSeasonLockedRemapOutcome.Merged)
            {
                merged++;
            }
            else
            {
                mutations.Add(CampaignSeasonMutation.Upsert(targetX, targetY, entry.Occurrence));
            }

            if (targetX != entry.X || targetY != entry.Y)
            {
                moved++;
            }

            entries.Add(new CampaignSeasonLockedRemapEntry(
                entry.X,
                entry.Y,
                entry.Occurrence.SeasonId,
                targetX,
                targetY,
                100,
                0,
                outcome));
        }

        if (mutations.Count > 0)
        {
            target.Apply(mutations);
        }

        target.EnsureValid();
        return new RemapResult(target, locked.Length, moved, merged, drops, entries);
    }

    private static bool HasSameCampaignLattice(
        CampaignWorldDefinition left,
        CampaignWorldDefinition right) =>
        left.WorldWidthMeters == right.WorldWidthMeters &&
        left.WorldHeightMeters == right.WorldHeightMeters &&
        left.CampaignTileSizeMeters == right.CampaignTileSizeMeters;

    private sealed record RemapResult(
        CampaignSeasonMap Map,
        int SourceLockedOccurrenceCount,
        int MovedLockedOccurrenceCount,
        int MergedLockedOccurrenceCount,
        IReadOnlyList<CampaignSeasonLockedDrop> LockedDrops,
        IReadOnlyList<CampaignSeasonLockedRemapEntry> RemapEntries);
}
