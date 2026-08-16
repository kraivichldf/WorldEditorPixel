using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Resources;

public enum CampaignResourceLatticeRemapMode
{
    PreserveSameLattice,
    RemapAllOccurrences,
    RemapLocksAndRegenerateUnlocked,
}

public sealed record CampaignResourceLockedDrop(
    int SourceX,
    int SourceY,
    string ResourceId,
    byte Potential);

public sealed record CampaignResourceRemapResourceReport(
    string ResourceId,
    int SourceOccurrenceCount,
    int RemappedSourceOccurrenceCount,
    int RetainedOccurrenceCount,
    int UnchangedSourceOccurrenceCount,
    int MovedSourceOccurrenceCount,
    int MergedOccurrenceCount,
    int DroppedOccurrenceCount,
    int LockedSourceOccurrenceCount,
    int LockedRetainedOccurrenceCount,
    int LockedMergedOccurrenceCount,
    int LockedDroppedOccurrenceCount);

public sealed class CampaignResourceWorldRegenerationReport
{
    internal CampaignResourceWorldRegenerationReport(
        CampaignResourceLatticeRemapMode mode,
        int sourceOccurrenceCount,
        int remappedSourceOccurrenceCount,
        int retainedOccurrenceCount,
        int finalOccurrenceCount,
        int unchangedSourceOccurrenceCount,
        int movedSourceOccurrenceCount,
        int mergedOccurrenceCount,
        int droppedOccurrenceCount,
        int lockedSourceOccurrenceCount,
        int lockedRetainedOccurrenceCount,
        int lockedMergedOccurrenceCount,
        int lockedDroppedOccurrenceCount,
        int replacedUnlockedSourceOccurrenceCount,
        int regeneratedUnlockedOccurrenceCount,
        IEnumerable<CampaignResourceRemapResourceReport> resourceReports,
        IEnumerable<CampaignResourceLockedDrop> lockedDrops,
        IEnumerable<CampaignResourceGenerationReport> generationReports)
    {
        Mode = mode;
        SourceOccurrenceCount = sourceOccurrenceCount;
        RemappedSourceOccurrenceCount = remappedSourceOccurrenceCount;
        RetainedOccurrenceCount = retainedOccurrenceCount;
        FinalOccurrenceCount = finalOccurrenceCount;
        UnchangedSourceOccurrenceCount = unchangedSourceOccurrenceCount;
        MovedSourceOccurrenceCount = movedSourceOccurrenceCount;
        MergedOccurrenceCount = mergedOccurrenceCount;
        DroppedOccurrenceCount = droppedOccurrenceCount;
        LockedSourceOccurrenceCount = lockedSourceOccurrenceCount;
        LockedRetainedOccurrenceCount = lockedRetainedOccurrenceCount;
        LockedMergedOccurrenceCount = lockedMergedOccurrenceCount;
        LockedDroppedOccurrenceCount = lockedDroppedOccurrenceCount;
        ReplacedUnlockedSourceOccurrenceCount = replacedUnlockedSourceOccurrenceCount;
        RegeneratedUnlockedOccurrenceCount = regeneratedUnlockedOccurrenceCount;
        ResourceReports = Array.AsReadOnly(resourceReports
            .OrderBy(static report => report.ResourceId, StringComparer.Ordinal)
            .ToArray());
        LockedDrops = Array.AsReadOnly(lockedDrops
            .OrderBy(static drop => drop.SourceY)
            .ThenBy(static drop => drop.SourceX)
            .ThenBy(static drop => drop.ResourceId, StringComparer.Ordinal)
            .ToArray());
        GenerationReports = Array.AsReadOnly(generationReports
            .OrderBy(static report => report.ResourceId, StringComparer.Ordinal)
            .ToArray());
    }

    public CampaignResourceLatticeRemapMode Mode { get; }

    public bool SameLattice => Mode == CampaignResourceLatticeRemapMode.PreserveSameLattice;

    public bool UnlockedResourcesRegenerated =>
        Mode == CampaignResourceLatticeRemapMode.RemapLocksAndRegenerateUnlocked;

    public int SourceOccurrenceCount { get; }

    public int RemappedSourceOccurrenceCount { get; }

    public int RetainedOccurrenceCount { get; }

    public int FinalOccurrenceCount { get; }

    public int UnchangedSourceOccurrenceCount { get; }

    public int MovedSourceOccurrenceCount { get; }

    public int MergedOccurrenceCount { get; }

    public int DroppedOccurrenceCount { get; }

    public int LockedSourceOccurrenceCount { get; }

    public int LockedRetainedOccurrenceCount { get; }

    public int LockedMergedOccurrenceCount { get; }

    public int LockedDroppedOccurrenceCount { get; }

    public int ReplacedUnlockedSourceOccurrenceCount { get; }

    public int RegeneratedUnlockedOccurrenceCount { get; }

    public IReadOnlyList<CampaignResourceRemapResourceReport> ResourceReports { get; }

    public IReadOnlyList<CampaignResourceLockedDrop> LockedDrops { get; }

    public IReadOnlyList<CampaignResourceGenerationReport> GenerationReports { get; }
}

public sealed class CampaignResourceWorldRegenerationSource
{
    private CampaignResourceWorldRegenerationSource(
        CampaignWorldDefinition definition,
        long terrainRevision,
        CampaignResourceCatalog catalog,
        long resourceRevision,
        CampaignResourceGenerationSettings? settings,
        CampaignResourceEntry[] entries)
    {
        Definition = definition;
        TerrainRevision = terrainRevision;
        Catalog = catalog;
        ResourceRevision = resourceRevision;
        Settings = settings;
        Entries = Array.AsReadOnly(entries);
    }

    public CampaignWorldDefinition Definition { get; }

    public long TerrainRevision { get; }

    public CampaignResourceCatalog Catalog { get; }

    public long ResourceRevision { get; }

    public CampaignResourceGenerationSettings? Settings { get; }

    public IReadOnlyList<CampaignResourceEntry> Entries { get; }

    public static CampaignResourceWorldRegenerationSource Capture(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(resourceMap);
        if (world.Definition != resourceMap.Definition)
        {
            throw new ArgumentException(
                "The terrain world and resource map must use the same value-equal definition.",
                nameof(resourceMap));
        }

        resourceMap.EnsureValid();
        settings?.EnsureValid(resourceMap.Catalog);
        var terrainRevisionBefore = world.Revision;
        var resourceRevisionBefore = resourceMap.Revision;
        var entries = resourceMap.GetMaterializedOccurrences().ToArray();
        if (terrainRevisionBefore != world.Revision ||
            resourceRevisionBefore != resourceMap.Revision)
        {
            throw new InvalidOperationException(
                "Terrain or resources changed while the regeneration source was being captured.");
        }

        return new CampaignResourceWorldRegenerationSource(
            world.Definition with { },
            terrainRevisionBefore,
            resourceMap.Catalog,
            resourceRevisionBefore,
            settings,
            entries);
    }
}

public sealed class CampaignResourceWorldRegenerationResult
{
    internal CampaignResourceWorldRegenerationResult(
        CampaignResourceWorldRegenerationSource source,
        CampaignWorld candidateWorld,
        CampaignResourceMap candidateMap,
        CampaignResourceWorldRegenerationReport report)
    {
        SourceDefinition = source.Definition;
        SourceTerrainRevision = source.TerrainRevision;
        SourceResourceRevision = source.ResourceRevision;
        CandidateMap = candidateMap ?? throw new ArgumentNullException(nameof(candidateMap));
        Settings = source.Settings;
        Report = report ?? throw new ArgumentNullException(nameof(report));
        CandidateTerrainRevision = candidateWorld?.Revision ??
            throw new ArgumentNullException(nameof(candidateWorld));
        CandidateResourceRevision = candidateMap.Revision;
        if (candidateWorld.Definition != candidateMap.Definition)
        {
            throw new ArgumentException(
                "Candidate terrain and resources must use the same value-equal definition.",
                nameof(candidateMap));
        }

        if (!ReferenceEquals(source.Catalog, candidateMap.Catalog))
        {
            throw new ArgumentException(
                "Candidate resources must retain the exact captured resource catalog.",
                nameof(candidateMap));
        }

        candidateMap.EnsureValid();
        Settings?.EnsureValid(candidateMap.Catalog);
        if (report.FinalOccurrenceCount != candidateMap.OccurrenceCount)
        {
            throw new ArgumentException(
                "The resource impact report does not match the candidate resource map.",
                nameof(report));
        }
    }

    public CampaignWorldDefinition SourceDefinition { get; }

    public long SourceTerrainRevision { get; }

    public long SourceResourceRevision { get; }

    public CampaignResourceMap CandidateMap { get; }

    public CampaignResourceGenerationSettings? Settings { get; }

    public CampaignResourceWorldRegenerationReport Report { get; }

    public long CandidateTerrainRevision { get; }

    public long CandidateResourceRevision { get; }

    public bool IsCurrent(
        CampaignWorld currentWorld,
        CampaignResourceMap currentResources,
        CampaignWorld candidateWorld)
    {
        ArgumentNullException.ThrowIfNull(currentWorld);
        ArgumentNullException.ThrowIfNull(currentResources);
        ArgumentNullException.ThrowIfNull(candidateWorld);
        return currentWorld.Definition == SourceDefinition &&
            currentWorld.Revision == SourceTerrainRevision &&
            currentResources.Definition == SourceDefinition &&
            currentResources.Revision == SourceResourceRevision &&
            ReferenceEquals(currentResources.Catalog, CandidateMap.Catalog) &&
            candidateWorld.Definition == CandidateMap.Definition &&
            candidateWorld.Revision == CandidateTerrainRevision &&
            CandidateMap.Revision == CandidateResourceRevision;
    }
}

/// <summary>
/// Builds the resource half of a previewed full-world replacement. The caller must
/// capture <see cref="CampaignResourceWorldRegenerationSource"/> on the owner thread;
/// this deterministic method can then run against a private candidate world on a worker.
/// </summary>
public sealed class CampaignResourceWorldRegenerator
{
    public CampaignResourceWorldRegenerationResult Generate(
        CampaignResourceWorldRegenerationSource source,
        CampaignWorld candidateWorld,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidateWorld);
        CampaignWorldDefinition.EnsureValid(candidateWorld.Definition);
        source.Settings?.EnsureValid(source.Catalog);
        cancellationToken.ThrowIfCancellationRequested();

        var sameLattice = HasSameCampaignLattice(source.Definition, candidateWorld.Definition);
        var mode = sameLattice
            ? CampaignResourceLatticeRemapMode.PreserveSameLattice
            : source.Settings is null
                ? CampaignResourceLatticeRemapMode.RemapAllOccurrences
                : CampaignResourceLatticeRemapMode.RemapLocksAndRegenerateUnlocked;
        var remap = Remap(source, candidateWorld.Definition, mode, cancellationToken);
        var candidateMap = remap.Map;
        IReadOnlyList<CampaignResourceGenerationReport> generationReports = [];
        var regeneratedUnlockedCount = 0;

        if (mode == CampaignResourceLatticeRemapMode.RemapLocksAndRegenerateUnlocked)
        {
            var terrainQuery = new CampaignResourceTerrainQueryV2(candidateWorld);
            var generationSource = CampaignResourceGenerationSource.Capture(
                terrainQuery,
                candidateMap,
                cancellationToken);
            var generationResult = new CampaignResourceGenerator().Generate(
                generationSource,
                source.Catalog,
                source.Settings!,
                CampaignResourceGenerationScope.All,
                cancellationToken);
            candidateMap = generationResult.CandidateMap;
            generationReports = generationResult.Reports;
            regeneratedUnlockedCount = candidateMap
                .GetMaterializedOccurrences()
                .Count(static entry => !entry.Occurrence.Locked);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var report = remap.CreateReport(
            mode,
            candidateMap.OccurrenceCount,
            regeneratedUnlockedCount,
            generationReports);
        return new CampaignResourceWorldRegenerationResult(
            source,
            candidateWorld,
            candidateMap,
            report);
    }

    private static RemapWorkResult Remap(
        CampaignResourceWorldRegenerationSource source,
        CampaignWorldDefinition targetDefinition,
        CampaignResourceLatticeRemapMode mode,
        CancellationToken cancellationToken)
    {
        var includeUnlocked = mode !=
            CampaignResourceLatticeRemapMode.RemapLocksAndRegenerateUnlocked;
        var accumulators = new Dictionary<TargetIdentity, TargetAccumulator>();
        var reports = source.Catalog.Definitions.ToDictionary(
            static definition => definition.Id,
            static _ => new MutableResourceReport(),
            StringComparer.Ordinal);
        var lockedDrops = new List<CampaignResourceLockedDrop>();
        var sourceCount = 0;
        var remappedSourceCount = 0;
        var unchangedCount = 0;
        var movedCount = 0;
        var droppedCount = 0;
        var lockedSourceCount = 0;
        var lockedDroppedCount = 0;
        var replacedUnlockedCount = 0;

        foreach (var entry in source.Entries)
        {
            if ((sourceCount & 0x0FFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            sourceCount++;
            var resourceReport = reports[entry.Occurrence.ResourceId];
            resourceReport.SourceOccurrenceCount++;
            if (entry.Occurrence.Locked)
            {
                lockedSourceCount++;
                resourceReport.LockedSourceOccurrenceCount++;
            }
            else if (!includeUnlocked)
            {
                replacedUnlockedCount++;
                continue;
            }

            remappedSourceCount++;
            resourceReport.RemappedSourceOccurrenceCount++;
            if (!TryMapCoordinate(
                    entry,
                    source.Definition,
                    targetDefinition,
                    out var targetX,
                    out var targetY))
            {
                droppedCount++;
                resourceReport.DroppedOccurrenceCount++;
                if (entry.Occurrence.Locked)
                {
                    lockedDroppedCount++;
                    resourceReport.LockedDroppedOccurrenceCount++;
                    lockedDrops.Add(new CampaignResourceLockedDrop(
                        entry.X,
                        entry.Y,
                        entry.Occurrence.ResourceId,
                        entry.Occurrence.Potential));
                }

                continue;
            }

            if (targetX == entry.X && targetY == entry.Y)
            {
                unchangedCount++;
                resourceReport.UnchangedSourceOccurrenceCount++;
            }
            else
            {
                movedCount++;
                resourceReport.MovedSourceOccurrenceCount++;
            }

            var identity = new TargetIdentity(targetX, targetY, entry.Occurrence.ResourceId);
            if (!accumulators.TryGetValue(identity, out var accumulator))
            {
                accumulator = new TargetAccumulator();
                accumulators.Add(identity, accumulator);
            }

            accumulator.Add(entry.Occurrence);
        }

        var targetMap = new CampaignResourceMap(targetDefinition, source.Catalog);
        var mutations = new List<CampaignResourceMutation>(accumulators.Count);
        var mergedCount = 0;
        var lockedRetainedCount = 0;
        var lockedMergedCount = 0;
        foreach (var pair in accumulators
                     .OrderBy(static pair => pair.Key.Y)
                     .ThenBy(static pair => pair.Key.X)
                     .ThenBy(static pair => pair.Key.ResourceId, StringComparer.Ordinal))
        {
            var key = pair.Key;
            var accumulator = pair.Value;
            var occurrence = new CampaignResourceOccurrence(
                key.ResourceId,
                accumulator.MaximumPotential,
                accumulator.AnyLocked);
            mutations.Add(CampaignResourceMutation.Upsert(key.X, key.Y, occurrence));
            var resourceReport = reports[key.ResourceId];
            resourceReport.RetainedOccurrenceCount++;
            var mergedHere = accumulator.SourceCount - 1;
            mergedCount += mergedHere;
            resourceReport.MergedOccurrenceCount += mergedHere;
            if (accumulator.AnyLocked)
            {
                lockedRetainedCount++;
                resourceReport.LockedRetainedOccurrenceCount++;
                var lockedMergedHere = accumulator.LockedSourceCount - 1;
                lockedMergedCount += lockedMergedHere;
                resourceReport.LockedMergedOccurrenceCount += lockedMergedHere;
            }
        }

        if (mutations.Count > 0)
        {
            targetMap.Apply(mutations);
        }

        targetMap.EnsureValid();
        return new RemapWorkResult(
            targetMap,
            sourceCount,
            remappedSourceCount,
            unchangedCount,
            movedCount,
            mergedCount,
            droppedCount,
            lockedSourceCount,
            lockedRetainedCount,
            lockedMergedCount,
            lockedDroppedCount,
            replacedUnlockedCount,
            reports,
            lockedDrops);
    }

    private static bool TryMapCoordinate(
        CampaignResourceEntry entry,
        CampaignWorldDefinition sourceDefinition,
        CampaignWorldDefinition targetDefinition,
        out int targetX,
        out int targetY)
    {
        var centreX = ((decimal)entry.X + 0.5m) * sourceDefinition.CampaignTileSizeMeters;
        var centreY = ((decimal)entry.Y + 0.5m) * sourceDefinition.CampaignTileSizeMeters;
        if (centreX >= targetDefinition.WorldWidthMeters ||
            centreY >= targetDefinition.WorldHeightMeters)
        {
            targetX = -1;
            targetY = -1;
            return false;
        }

        targetX = decimal.ToInt32(decimal.Floor(
            centreX / targetDefinition.CampaignTileSizeMeters));
        targetY = decimal.ToInt32(decimal.Floor(
            centreY / targetDefinition.CampaignTileSizeMeters));
        return (uint)targetX < (uint)targetDefinition.TilesX &&
            (uint)targetY < (uint)targetDefinition.TilesY;
    }

    private static bool HasSameCampaignLattice(
        CampaignWorldDefinition left,
        CampaignWorldDefinition right) =>
        left.WorldWidthMeters == right.WorldWidthMeters &&
        left.WorldHeightMeters == right.WorldHeightMeters &&
        left.CampaignTileSizeMeters == right.CampaignTileSizeMeters;

    private readonly record struct TargetIdentity(int X, int Y, string ResourceId);

    private sealed class TargetAccumulator
    {
        public int SourceCount { get; private set; }

        public int LockedSourceCount { get; private set; }

        public byte MaximumPotential { get; private set; }

        public bool AnyLocked => LockedSourceCount > 0;

        public void Add(CampaignResourceOccurrence occurrence)
        {
            SourceCount++;
            if (occurrence.Locked)
            {
                LockedSourceCount++;
            }

            MaximumPotential = Math.Max(MaximumPotential, occurrence.Potential);
        }
    }

    private sealed class MutableResourceReport
    {
        public int SourceOccurrenceCount { get; set; }

        public int RemappedSourceOccurrenceCount { get; set; }

        public int RetainedOccurrenceCount { get; set; }

        public int UnchangedSourceOccurrenceCount { get; set; }

        public int MovedSourceOccurrenceCount { get; set; }

        public int MergedOccurrenceCount { get; set; }

        public int DroppedOccurrenceCount { get; set; }

        public int LockedSourceOccurrenceCount { get; set; }

        public int LockedRetainedOccurrenceCount { get; set; }

        public int LockedMergedOccurrenceCount { get; set; }

        public int LockedDroppedOccurrenceCount { get; set; }

        public CampaignResourceRemapResourceReport ToReport(string resourceId) => new(
            resourceId,
            SourceOccurrenceCount,
            RemappedSourceOccurrenceCount,
            RetainedOccurrenceCount,
            UnchangedSourceOccurrenceCount,
            MovedSourceOccurrenceCount,
            MergedOccurrenceCount,
            DroppedOccurrenceCount,
            LockedSourceOccurrenceCount,
            LockedRetainedOccurrenceCount,
            LockedMergedOccurrenceCount,
            LockedDroppedOccurrenceCount);
    }

    private sealed record RemapWorkResult(
        CampaignResourceMap Map,
        int SourceOccurrenceCount,
        int RemappedSourceOccurrenceCount,
        int UnchangedSourceOccurrenceCount,
        int MovedSourceOccurrenceCount,
        int MergedOccurrenceCount,
        int DroppedOccurrenceCount,
        int LockedSourceOccurrenceCount,
        int LockedRetainedOccurrenceCount,
        int LockedMergedOccurrenceCount,
        int LockedDroppedOccurrenceCount,
        int ReplacedUnlockedSourceOccurrenceCount,
        IReadOnlyDictionary<string, MutableResourceReport> ResourceReports,
        IReadOnlyList<CampaignResourceLockedDrop> LockedDrops)
    {
        public CampaignResourceWorldRegenerationReport CreateReport(
            CampaignResourceLatticeRemapMode mode,
            int finalOccurrenceCount,
            int regeneratedUnlockedOccurrenceCount,
            IReadOnlyList<CampaignResourceGenerationReport> generationReports) => new(
            mode,
            SourceOccurrenceCount,
            RemappedSourceOccurrenceCount,
            Map.OccurrenceCount,
            finalOccurrenceCount,
            UnchangedSourceOccurrenceCount,
            MovedSourceOccurrenceCount,
            MergedOccurrenceCount,
            DroppedOccurrenceCount,
            LockedSourceOccurrenceCount,
            LockedRetainedOccurrenceCount,
            LockedMergedOccurrenceCount,
            LockedDroppedOccurrenceCount,
            ReplacedUnlockedSourceOccurrenceCount,
            regeneratedUnlockedOccurrenceCount,
            ResourceReports
                .Where(static pair => pair.Value.SourceOccurrenceCount > 0)
                .Select(static pair => pair.Value.ToReport(pair.Key)),
            LockedDrops,
            generationReports);
    }
}
