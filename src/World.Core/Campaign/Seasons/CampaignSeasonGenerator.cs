using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

public static class CampaignSeasonGenerator
{
    public const int MaximumCandidateOccurrenceCount = 2_000_000;

    public static CampaignSeasonGenerationResult Generate(
        CampaignSeasonGenerationSource source,
        CampaignSeasonCatalog catalog,
        CampaignSeasonGenerationSettings settings,
        CampaignSeasonGenerationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSource(source, catalog);
        settings.EnsureValid(catalog, source.Definition);
        scope.EnsureValid(source.Definition);
        if (source.CurrentEntries.Count > MaximumCandidateOccurrenceCount)
        {
            throw new InvalidOperationException(
                $"The current Season layer contains {source.CurrentEntries.Count:N0} occurrences; " +
                $"generation supports at most {MaximumCandidateOccurrenceCount:N0}.");
        }

        var support = CampaignSeasonSupportFields.Build(source.Terrain, settings, cancellationToken);
        var enabled = settings.GetEnabledDefinitions(catalog).ToArray();
        var enabledIds = enabled.Select(static definition => definition.Id)
            .ToHashSet(StringComparer.Ordinal);
        var definition = source.Definition;
        var scopeTileCount = CountScopeTiles(definition, scope);
        var currentCounts = CountInScope(source.CurrentEntries, scope, catalog);
        var candidate = CampaignSeasonMap.CreateSnapshot(definition, catalog, source.CurrentEntries);
        var workingReports = catalog.Definitions.ToDictionary(
            static value => value.Id,
            value => new MutableReport(
                value.Id,
                enabledIds.Contains(value.Id),
                scopeTileCount,
                currentCounts[catalog.GetIndex(value.Id)]),
            StringComparer.Ordinal);
        var changedIdentityCount = 0;

        foreach (var season in enabled)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var report = workingReports[season.Id];
            for (var y = 0; y < definition.TilesY; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < definition.TilesX; x++)
                {
                    if (!scope.Includes(x, y))
                    {
                        continue;
                    }

                    var matches = CampaignSeasonRuleEvaluator.MatchesValidated(
                        season.Rule,
                        source.Terrain.GetSample(x, y),
                        support.GetSample(x, y));
                    if (matches)
                    {
                        report.EnvironmentalMatchCount++;
                    }

                    var hasCurrent = candidate.TryGetOccurrence(x, y, season.Id, out var occurrence);
                    if (hasCurrent && occurrence.Locked)
                    {
                        report.PreservedLockCount++;
                        if (!matches)
                        {
                            report.LockedOutsideRuleCount++;
                        }

                        continue;
                    }

                    if (matches)
                    {
                        if (hasCurrent)
                        {
                            report.RetainedUnlockedCount++;
                            continue;
                        }

                        if (candidate.OccurrenceCount >= MaximumCandidateOccurrenceCount)
                        {
                            throw new InvalidOperationException(
                                $"Season generation would exceed {MaximumCandidateOccurrenceCount:N0} occurrences. " +
                                "Regenerate fewer definitions or use a smaller spatial scope.");
                        }

                        candidate.Upsert(x, y, new CampaignSeasonOccurrence(season.Id));
                        report.AddedOccurrenceCount++;
                        changedIdentityCount++;
                    }
                    else if (hasCurrent)
                    {
                        candidate.Remove(x, y, season.Id);
                        report.RemovedOccurrenceCount++;
                        changedIdentityCount++;
                    }
                }
            }
        }

        var candidateCounts = CountInScope(candidate.GetMaterializedOccurrences(), scope, catalog);
        var reports = new CampaignSeasonGenerationReport[catalog.Definitions.Count];
        for (var catalogIndex = 0; catalogIndex < catalog.Definitions.Count; catalogIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var season = catalog.Definitions[catalogIndex];
            var working = workingReports[season.Id];
            var candidateCount = candidateCounts[catalogIndex];
            var warnings = new List<string>();
            if (working.LockedOutsideRuleCount > 0)
            {
                warnings.Add(
                    $"{working.LockedOutsideRuleCount:N0} locked occurrence(s) remain even though {season.Name}'s current rule does not match.");
            }

            reports[catalogIndex] = new CampaignSeasonGenerationReport(
                season.Id,
                working.Selected,
                scopeTileCount,
                working.CurrentOccurrenceCount,
                working.EnvironmentalMatchCount,
                working.AddedOccurrenceCount,
                working.RemovedOccurrenceCount,
                working.RetainedUnlockedCount,
                working.PreservedLockCount,
                candidateCount,
                scopeTileCount == 0 ? 0 : candidateCount * 100d / scopeTileCount,
                GetZeroReason(season, working, candidateCount),
                Array.AsReadOnly(warnings.ToArray()));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new CampaignSeasonGenerationResult(
            candidate,
            settings,
            scope,
            support,
            reports,
            changedIdentityCount,
            source.TerrainRevision,
            source.SeasonRevision);
    }

    private static int CountScopeTiles(
        CampaignWorldDefinition definition,
        CampaignSeasonGenerationScope scope) =>
        scope.Kind == CampaignSeasonGenerationScopeKind.All
            ? checked((int)definition.TileCount)
            : checked(scope.Area!.Value.Width * scope.Area.Value.Height);

    private static int[] CountInScope(
        IEnumerable<CampaignSeasonEntry> entries,
        CampaignSeasonGenerationScope scope,
        CampaignSeasonCatalog catalog)
    {
        var counts = new int[catalog.Definitions.Count];
        foreach (var entry in entries)
        {
            if (scope.Includes(entry.X, entry.Y))
            {
                counts[catalog.GetIndex(entry.Occurrence.SeasonId)]++;
            }
        }

        return counts;
    }

    private static void ValidateSource(
        CampaignSeasonGenerationSource source,
        CampaignSeasonCatalog catalog)
    {
        CampaignWorldDefinition.EnsureValid(source.Definition);
        if (!ReferenceEquals(source.Catalog, catalog))
        {
            throw new ArgumentException(
                "Season generation must use the exact immutable catalog captured with the source.",
                nameof(catalog));
        }

        var seen = new HashSet<(int X, int Y, string SeasonId)>();
        foreach (var entry in source.CurrentEntries)
        {
            if ((uint)entry.X >= (uint)source.Definition.TilesX ||
                (uint)entry.Y >= (uint)source.Definition.TilesY)
            {
                throw new ArgumentException(
                    $"Season source coordinate ({entry.X}, {entry.Y}) is outside the campaign grid.",
                    nameof(source));
            }

            entry.Occurrence.EnsureValid();
            if (!catalog.Contains(entry.Occurrence.SeasonId))
            {
                throw new ArgumentException(
                    $"Season source references unknown season '{entry.Occurrence.SeasonId}'.",
                    nameof(source));
            }

            if (!seen.Add((entry.X, entry.Y, entry.Occurrence.SeasonId)))
            {
                throw new ArgumentException(
                    $"Season source repeats '{entry.Occurrence.SeasonId}' at ({entry.X}, {entry.Y}).",
                    nameof(source));
            }
        }
    }

    private static string? GetZeroReason(
        CampaignSeasonDefinition season,
        MutableReport report,
        int candidateCount)
    {
        if (candidateCount > 0)
        {
            return null;
        }

        if (!report.Selected)
        {
            return "Excluded — existing occurrences were kept unchanged.";
        }

        if (report.EnvironmentalMatchCount == 0)
        {
            return $"No tile passed the environmental rule for {season.Name}.";
        }

        return $"{season.Name} produced no occurrence in the selected scope.";
    }

    private sealed class MutableReport(
        string seasonId,
        bool selected,
        int scopeTileCount,
        int currentOccurrenceCount)
    {
        public string SeasonId { get; } = seasonId;

        public bool Selected { get; } = selected;

        public int ScopeTileCount { get; } = scopeTileCount;

        public int CurrentOccurrenceCount { get; } = currentOccurrenceCount;

        public int EnvironmentalMatchCount { get; set; }

        public int AddedOccurrenceCount { get; set; }

        public int RemovedOccurrenceCount { get; set; }

        public int RetainedUnlockedCount { get; set; }

        public int PreservedLockCount { get; set; }

        public int LockedOutsideRuleCount { get; set; }
    }
}
