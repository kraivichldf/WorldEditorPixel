using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

public static class CampaignSeasonGenerator
{
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

        var support = CampaignSeasonSupportFields.Build(source.Terrain, settings, cancellationToken);
        var enabled = settings.GetPriorityDefinitions(catalog).ToArray();
        var enabledIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < enabled.Length; index++)
        {
            enabledIndexById.Add(enabled[index].Id, index);
        }

        var environmentalMatches = new int[enabled.Length];
        var priorityWins = new int[enabled.Length];
        var generatedCounts = new int[enabled.Length];
        var shadowedMatches = new int[enabled.Length];
        var lockedOverrides = new int[enabled.Length];
        var changedToSeason = new int[enabled.Length];
        var preservedLocksByCatalogIndex = new int[catalog.Definitions.Count];
        var currentCountsByCatalogIndex = new int[catalog.Definitions.Count];
        var candidateCountsByCatalogIndex = new int[catalog.Definitions.Count];
        var candidateTiles = source.CurrentTiles.ToArray();
        var definition = source.Definition;
        var width = definition.TilesX;
        var scopeTileCount = 0;
        var changedTileCount = 0;

        for (var y = 0; y < definition.TilesY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                if (!scope.Includes(x, y))
                {
                    continue;
                }

                scopeTileCount++;
                var flatIndex = (y * width) + x;
                var current = source.CurrentTiles[flatIndex];
                currentCountsByCatalogIndex[catalog.GetIndex(current.SeasonId)]++;
                var terrain = source.Terrain.GetSample(x, y);
                var supportSample = support.GetSample(x, y);
                var firstMatch = -1;
                for (var priorityIndex = 0; priorityIndex < enabled.Length; priorityIndex++)
                {
                    if ((priorityIndex & 31) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var matches = priorityIndex == enabled.Length - 1 ||
                        CampaignSeasonRuleEvaluator.MatchesValidated(
                            enabled[priorityIndex].Rule,
                            terrain,
                            supportSample);
                    if (!matches)
                    {
                        continue;
                    }

                    environmentalMatches[priorityIndex]++;
                    if (firstMatch < 0)
                    {
                        firstMatch = priorityIndex;
                    }
                    else
                    {
                        shadowedMatches[priorityIndex]++;
                    }
                }

                if (firstMatch < 0)
                {
                    throw new InvalidOperationException(
                        "The validated season priority did not produce its required final catch-all match.");
                }

                priorityWins[firstMatch]++;
                var winningId = enabled[firstMatch].Id;
                if (current.Locked)
                {
                    preservedLocksByCatalogIndex[catalog.GetIndex(current.SeasonId)]++;
                    if (!string.Equals(current.SeasonId, winningId, StringComparison.Ordinal))
                    {
                        lockedOverrides[firstMatch]++;
                    }

                    continue;
                }

                generatedCounts[firstMatch]++;
                if (!string.Equals(current.SeasonId, winningId, StringComparison.Ordinal))
                {
                    candidateTiles[flatIndex] = new CampaignSeasonTile(winningId, Locked: false);
                    changedToSeason[firstMatch]++;
                    changedTileCount++;
                }
            }
        }

        for (var y = 0; y < definition.TilesY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                if (!scope.Includes(x, y))
                {
                    continue;
                }

                var tile = candidateTiles[(y * width) + x];
                candidateCountsByCatalogIndex[catalog.GetIndex(tile.SeasonId)]++;
            }
        }

        var reports = new CampaignSeasonGenerationReport[catalog.Definitions.Count];
        for (var catalogIndex = 0; catalogIndex < catalog.Definitions.Count; catalogIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var season = catalog.Definitions[catalogIndex];
            var warnings = new List<string>();
            var generationEnabled = enabledIndexById.TryGetValue(season.Id, out var priorityIndex);
            var environmental = generationEnabled ? environmentalMatches[priorityIndex] : 0;
            var wins = generationEnabled ? priorityWins[priorityIndex] : 0;
            var generated = generationEnabled ? generatedCounts[priorityIndex] : 0;
            var shadowed = generationEnabled ? shadowedMatches[priorityIndex] : 0;
            var lockedOverride = generationEnabled ? lockedOverrides[priorityIndex] : 0;
            var changedTo = generationEnabled ? changedToSeason[priorityIndex] : 0;
            if (lockedOverride > 0)
            {
                warnings.Add(
                    $"{lockedOverride:N0} tile(s) would select {season.Name}, but a different locked season was preserved.");
            }

            var candidateCount = candidateCountsByCatalogIndex[catalogIndex];
            reports[catalogIndex] = new CampaignSeasonGenerationReport(
                season.Id,
                generationEnabled,
                scopeTileCount,
                currentCountsByCatalogIndex[catalogIndex],
                candidateCount,
                environmental,
                wins,
                generated,
                shadowed,
                preservedLocksByCatalogIndex[catalogIndex],
                lockedOverride,
                changedTo,
                scopeTileCount == 0 ? 0 : candidateCount * 100d / scopeTileCount,
                GetZeroReason(
                    season,
                    generationEnabled,
                    candidateCount,
                    environmental,
                    wins,
                    generated,
                    shadowed,
                    lockedOverride),
                Array.AsReadOnly(warnings.ToArray()));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var candidate = CampaignSeasonMap.CreateSnapshot(
            definition,
            catalog,
            source.DefaultSeasonId,
            candidateTiles);
        return new CampaignSeasonGenerationResult(
            candidate,
            settings,
            scope,
            support,
            reports,
            changedTileCount,
            source.TerrainRevision,
            source.SeasonRevision);
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

        if (!catalog.Contains(source.DefaultSeasonId))
        {
            throw new ArgumentException(
                $"Season source default '{source.DefaultSeasonId}' is absent from its catalog.",
                nameof(source));
        }

        if (source.CurrentTiles.Count != source.Definition.TileCount)
        {
            throw new ArgumentException(
                "Season source does not contain one authoritative season value per tile.",
                nameof(source));
        }

        foreach (var tile in source.CurrentTiles)
        {
            tile.EnsureValid(catalog);
        }
    }

    private static string? GetZeroReason(
        CampaignSeasonDefinition season,
        bool generationEnabled,
        int candidateCount,
        int environmentalMatches,
        int priorityWins,
        int generatedCount,
        int shadowedMatches,
        int lockedOverrideCount)
    {
        if (!generationEnabled)
        {
            return "Manual-paint-only: this season is not in the enabled priority list.";
        }

        if (candidateCount > 0)
        {
            return null;
        }

        if (environmentalMatches == 0)
        {
            return $"No tile passed the environmental rule for {season.Name}.";
        }

        if (priorityWins == 0 && shadowedMatches > 0)
        {
            return $"Matching tiles were captured by higher-priority seasons before {season.Name}.";
        }

        if (generatedCount == 0 && lockedOverrideCount > 0)
        {
            return $"Every winning tile retained a different locked season instead of {season.Name}.";
        }

        return $"{season.Name} produced no tile in the selected scope.";
    }
}
