using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Resources;

/// <summary>
/// Synchronous deterministic generator over immutable campaign-resource inputs.
/// Callers may run this method on a worker after owner-thread source capture.
/// </summary>
public sealed class CampaignResourceGenerator
{
    public CampaignResourceGenerationResult Generate(
        CampaignResourceGenerationSource source,
        CampaignResourceCatalog catalog,
        CampaignResourceGenerationSettings settings,
        CampaignResourceGenerationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(scope);
        if (!ReferenceEquals(source.Catalog, catalog))
        {
            throw new ArgumentException(
                "Generation must use the exact catalog captured with the current resource map.",
                nameof(catalog));
        }

        settings.EnsureValid(catalog);
        scope.EnsureValid(catalog);
        ValidateSource(source, catalog);
        cancellationToken.ThrowIfCancellationRequested();

        var support = CampaignResourceSupportFields.Build(source.Terrain, settings, cancellationToken);
        var candidateEntries = new List<CampaignResourceEntry>(source.CurrentEntries.Count);
        var locksByResource = new Dictionary<string, List<CampaignResourceEntry>>(StringComparer.Ordinal);
        foreach (var entry in source.CurrentEntries)
        {
            var definition = catalog.Get(entry.Occurrence.ResourceId);
            if (!scope.Includes(definition))
            {
                AddCandidate(candidateEntries, entry);
                continue;
            }

            if (!entry.Occurrence.Locked)
            {
                continue;
            }

            AddCandidate(candidateEntries, entry);
            if (!locksByResource.TryGetValue(definition.Id, out var locks))
            {
                locks = [];
                locksByResource.Add(definition.Id, locks);
            }

            locks.Add(entry);
        }

        var reports = new List<CampaignResourceGenerationReport>();
        foreach (var definition in catalog.Definitions)
        {
            if (!scope.Includes(definition))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var evaluation = CampaignResourceSuitabilityEvaluator.Evaluate(
                definition,
                source.Terrain,
                support,
                cancellationToken);
            if (catalog.IsBuiltIn(definition.Id) && !evaluation.IsSupported)
            {
                throw new InvalidOperationException(
                    $"Built-in resource '{definition.Id}' references unsupported factors: " +
                    string.Join(", ", evaluation.UnsupportedFactorIds));
            }

            locksByResource.TryGetValue(definition.Id, out var preservedLocks);
            preservedLocks ??= [];
            var effective = settings.GetEffective(definition);
            var effectiveCoverageBasisPoints = effective.Enabled
                ? GetEffectiveCoverageBasisPoints(effective.CoveragePercent, settings.Abundance)
                : 0;
            var effectiveCoverage = effectiveCoverageBasisPoints / 100.0;
            var requestedCount = (int)(
                (long)evaluation.EligibleTileCount * effectiveCoverageBasisPoints / 10_000);
            var overTargetLocks = Math.Max(0, preservedLocks.Count - requestedCount);
            var newBudget = Math.Max(0, requestedCount - preservedLocks.Count);
            var warnings = new List<string>();
            if (overTargetLocks > 0)
            {
                warnings.Add(
                    $"{overTargetLocks:N0} preserved locks are above this resource's upper target.");
            }

            var admissionFloor = GetAdmissionFloor(definition.DistributionProfile);
            var outOfProfileLocks = evaluation.IsSupported
                ? preservedLocks.Count(entry =>
                {
                    var index = (entry.Y * source.Definition.TilesX) + entry.X;
                    return !evaluation.Eligible[index] || evaluation.Suitability[index] < admissionFloor;
                })
                : 0;
            if (outOfProfileLocks > 0)
            {
                warnings.Add(
                    $"{outOfProfileLocks:N0} preserved locks are outside the current hard rules or suitability profile.");
            }

            var unsupported = evaluation.UnsupportedFactorIds;
            if (unsupported.Count > 0)
            {
                warnings.Add(
                    "Unsupported suitability factors: " + string.Join(", ", unsupported) +
                    ". This run preserves locks only.");
                newBudget = 0;
            }

            var generated = new List<GeneratedOccurrence>(Math.Min(newBudget, 16_384));
            if (newBudget > 0)
            {
                generated.AddRange(GenerateResource(
                    definition,
                    effective,
                    settings,
                    source.Terrain,
                    support,
                    evaluation,
                    preservedLocks,
                    newBudget,
                    cancellationToken));
                foreach (var occurrence in generated)
                {
                    AddCandidate(
                        candidateEntries,
                        new CampaignResourceEntry(
                            occurrence.X,
                            occurrence.Y,
                            new CampaignResourceOccurrence(
                                definition.Id,
                                occurrence.Potential,
                                Locked: false)));
                }
            }

            var actualPotentials = preservedLocks
                .Select(static entry => entry.Occurrence.Potential)
                .Concat(generated.Select(static value => value.Potential))
                .ToArray();
            var actualCount = actualPotentials.Length;
            var shortfallReason = GetShortfallReason(
                effective,
                evaluation,
                requestedCount,
                preservedLocks.Count,
                generated.Count,
                unsupported);
            reports.Add(new CampaignResourceGenerationReport(
                definition.Id,
                evaluation.EligibleTileCount,
                requestedCount,
                actualCount,
                generated.Count,
                generated.Select(static value => value.RegionId).Distinct().Count(),
                actualCount == 0 ? 0 : actualPotentials.Average(static value => value),
                actualCount == 0 ? (byte)0 : actualPotentials.Max(),
                preservedLocks.Count,
                overTargetLocks,
                effectiveCoverage,
                evaluation.EligibleTileCount == 0
                    ? 0
                    : (actualCount * 100.0) / evaluation.EligibleTileCount,
                shortfallReason,
                Array.AsReadOnly(warnings.ToArray())));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var candidateMap = new CampaignResourceMap(source.Definition, catalog);
        foreach (var batch in candidateEntries
                     .OrderBy(static entry => entry.Y)
                     .ThenBy(static entry => entry.X)
                     .ThenBy(static entry => entry.Occurrence.ResourceId, StringComparer.Ordinal)
                     .Chunk(32_768))
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidateMap.Apply(batch.Select(static entry =>
                CampaignResourceMutation.Upsert(entry.X, entry.Y, entry.Occurrence)));
        }

        return new CampaignResourceGenerationResult(
            candidateMap,
            settings,
            scope,
            reports,
            source.TerrainRevision,
            source.ResourceRevision);
    }

    private static IReadOnlyList<GeneratedOccurrence> GenerateResource(
        CampaignResourceDefinition definition,
        CampaignResourceEffectiveGenerationSettings effective,
        CampaignResourceGenerationSettings settings,
        CampaignResourceTerrainSnapshot terrain,
        CampaignResourceSupportFields support,
        CampaignResourceSuitabilityEvaluation evaluation,
        IReadOnlyList<CampaignResourceEntry> preservedLocks,
        int budget,
        CancellationToken cancellationToken)
    {
        var width = terrain.Definition.TilesX;
        var height = terrain.Definition.TilesY;
        var count = checked((int)terrain.Definition.TileCount);
        var tileKilometers = terrain.Definition.CampaignTileSizeMeters / 1_000.0;
        var resourceSeed = CampaignResourceSeed.ForResource(settings.ResourceSeed, definition.Id);
        var admissionFloor = GetAdmissionFloor(definition.DistributionProfile);
        var radiusKilometers = GetRegionRadiusKilometers(definition, effective, tileKilometers);
        var profile = BuildSpatialProfile(
            definition.DistributionProfile,
            terrain,
            support,
            resourceSeed,
            radiusKilometers,
            cancellationToken);
        var shapeFloor = GetShapeFloor(definition.DistributionProfile);
        var ranking = new float[count];
        var qualified = new bool[count];
        var candidates = new List<int>();
        for (var index = 0; index < count; index++)
        {
            if ((index & 0x0FFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var suitable = evaluation.Suitability[index];
            if (!evaluation.Eligible[index] || suitable < admissionFloor || profile[index] < shapeFloor)
            {
                continue;
            }

            qualified[index] = true;
            ranking[index] = (0.72f * suitable) + (0.28f * profile[index]);
            candidates.Add(index);
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        var localMaxima = new List<int>();
        foreach (var index in candidates)
        {
            var x = index % width;
            var y = index / width;
            if (IsLocalMaximum(index, x, y, width, height, ranking, qualified, resourceSeed))
            {
                localMaxima.Add(index);
            }
        }

        localMaxima.Sort((left, right) => CompareRankedCells(left, right, ranking, width, resourceSeed));
        if (localMaxima.Count == 0)
        {
            candidates.Sort((left, right) => CompareRankedCells(left, right, ranking, width, resourceSeed));
            localMaxima.Add(candidates[0]);
        }

        var coreSpacingKilometers = radiusKilometers * GetCoreSpacingMultiplier(effective.Concentration);
        var cores = SelectSeparatedCores(
            localMaxima,
            width,
            tileKilometers,
            coreSpacingKilometers,
            cancellationToken);
        if (cores.Count == 0)
        {
            cores.Add(localMaxima[0]);
        }

        var lockedIndices = preservedLocks
            .Select(entry => (entry.Y * width) + entry.X)
            .ToHashSet();
        var visited = new bool[count];
        var result = new List<GeneratedOccurrence>(Math.Min(budget, candidates.Count));
        var queue = new PriorityQueue<GrowthNode, GrowthPriority>();
        for (var coreId = 0; coreId < cores.Count; coreId++)
        {
            var index = cores[coreId];
            queue.Enqueue(
                new GrowthNode(index, coreId),
                CreateGrowthPriority(index, coreId, cores, ranking, width, tileKilometers, radiusKilometers, resourceSeed));
        }

        while (queue.Count > 0 && result.Count < budget)
        {
            if ((result.Count & 0x03FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var node = queue.Dequeue();
            if (visited[node.Index])
            {
                continue;
            }

            var index = node.Index;
            var x = index % width;
            var y = index / width;
            var coreIndex = cores[node.CoreId];
            var coreX = coreIndex % width;
            var coreY = coreIndex / width;
            var distanceKilometers = DistanceKilometers(x, y, coreX, coreY, tileKilometers);
            if (distanceKilometers > radiusKilometers || !qualified[index])
            {
                continue;
            }

            visited[index] = true;
            if (!lockedIndices.Contains(index))
            {
                var potential = CalculatePotential(
                    evaluation.Suitability[index],
                    admissionFloor,
                    distanceKilometers,
                    radiusKilometers,
                    support.RegionalDetail[index],
                    settings.Abundance,
                    effective);
                result.Add(new GeneratedOccurrence(x, y, potential, node.CoreId));
                if (result.Count >= budget)
                {
                    break;
                }
            }

            EnqueueNeighbor(x - 1, y);
            EnqueueNeighbor(x + 1, y);
            EnqueueNeighbor(x, y - 1);
            EnqueueNeighbor(x, y + 1);

            void EnqueueNeighbor(int neighborX, int neighborY)
            {
                if ((uint)neighborX >= (uint)width || (uint)neighborY >= (uint)height)
                {
                    return;
                }

                var neighborIndex = (neighborY * width) + neighborX;
                if (visited[neighborIndex] || !qualified[neighborIndex])
                {
                    return;
                }

                queue.Enqueue(
                    new GrowthNode(neighborIndex, node.CoreId),
                    CreateGrowthPriority(
                        neighborIndex,
                        node.CoreId,
                        cores,
                        ranking,
                        width,
                        tileKilometers,
                        radiusKilometers,
                        resourceSeed));
            }
        }

        return result;
    }

    private static float[] BuildSpatialProfile(
        CampaignResourceDistributionProfile distributionProfile,
        CampaignResourceTerrainSnapshot terrain,
        CampaignResourceSupportFields support,
        int resourceSeed,
        double radiusKilometers,
        CancellationToken cancellationToken)
    {
        var width = terrain.Definition.TilesX;
        var height = terrain.Definition.TilesY;
        var count = checked((int)terrain.Definition.TileCount);
        var tileKilometers = terrain.Definition.CampaignTileSizeMeters / 1_000.0;
        var wavelength = Math.Max(tileKilometers * 3, radiusKilometers * 2.2);
        var profile = new float[count];
        _ = support.TryGetValues("aquatic", out var aquatic);
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                var centerXKm = (x + 0.5) * tileKilometers;
                var centerYKm = (y + 0.5) * tileKilometers;
                var regional = UnitNoise(centerXKm, centerYKm, resourceSeed, wavelength, 3);
                var value = distributionProfile switch
                {
                    CampaignResourceDistributionProfile.Field =>
                        (0.64 * regional) + (0.36 * support.RegionalDetail[index]),
                    CampaignResourceDistributionProfile.Vein =>
                        BuildVeinValue(index, centerXKm, centerYKm, regional),
                    CampaignResourceDistributionProfile.Basin =>
                        (0.68 * support.BasinProfile[index]) + (0.32 * (1 - regional)),
                    CampaignResourceDistributionProfile.SurfaceDeposit =>
                        (0.64 * support.SurfaceDepositProfile[index]) + (0.36 * regional),
                    CampaignResourceDistributionProfile.Aquatic =>
                        (0.74 * aquatic[index]) + (0.26 * regional),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(distributionProfile),
                        distributionProfile,
                        "Unknown distribution profile."),
                };
                profile[index] = (float)Math.Clamp(value, 0, 1);
            }
        }

        return profile;

        double BuildVeinValue(int index, double centerXKm, double centerYKm, double regional)
        {
            var tangentX = support.BoundaryTangentX[index];
            var tangentY = support.BoundaryTangentY[index];
            var along = (centerXKm * tangentX) + (centerYKm * tangentY);
            var across = (-centerXKm * tangentY) + (centerYKm * tangentX);
            var anisotropic = CampaignTerrainNoise.Ridged(
                along,
                across * 4.2,
                unchecked(resourceSeed + 6_127),
                wavelength,
                3,
                persistence: 0.54);
            return (0.52 * support.VeinProfile[index]) +
                (0.36 * anisotropic) +
                (0.12 * regional);
        }
    }

    private static List<int> SelectSeparatedCores(
        IReadOnlyList<int> localMaxima,
        int width,
        double tileKilometers,
        double spacingKilometers,
        CancellationToken cancellationToken)
    {
        var spacing = Math.Max(tileKilometers, spacingKilometers);
        var buckets = new Dictionary<(int X, int Y), List<int>>();
        var selected = new List<int>();
        foreach (var index in localMaxima)
        {
            if ((selected.Count & 0x00FF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var xKm = ((index % width) + 0.5) * tileKilometers;
            var yKm = ((index / width) + 0.5) * tileKilometers;
            var bucketX = (int)Math.Floor(xKm / spacing);
            var bucketY = (int)Math.Floor(yKm / spacing);
            var tooClose = false;
            for (var offsetY = -1; offsetY <= 1 && !tooClose; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1 && !tooClose; offsetX++)
                {
                    if (!buckets.TryGetValue((bucketX + offsetX, bucketY + offsetY), out var nearby))
                    {
                        continue;
                    }

                    foreach (var other in nearby)
                    {
                        var otherXKm = ((other % width) + 0.5) * tileKilometers;
                        var otherYKm = ((other / width) + 0.5) * tileKilometers;
                        var dx = xKm - otherXKm;
                        var dy = yKm - otherYKm;
                        if ((dx * dx) + (dy * dy) < spacing * spacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }
            }

            if (tooClose)
            {
                continue;
            }

            selected.Add(index);
            if (!buckets.TryGetValue((bucketX, bucketY), out var bucket))
            {
                bucket = [];
                buckets.Add((bucketX, bucketY), bucket);
            }

            bucket.Add(index);
        }

        return selected;
    }

    private static bool IsLocalMaximum(
        int index,
        int x,
        int y,
        int width,
        int height,
        IReadOnlyList<float> ranking,
        IReadOnlyList<bool> qualified,
        int seed)
    {
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var neighborX = x + offsetX;
                var neighborY = y + offsetY;
                if ((uint)neighborX >= (uint)width || (uint)neighborY >= (uint)height)
                {
                    continue;
                }

                var neighbor = (neighborY * width) + neighborX;
                if (!qualified[neighbor])
                {
                    continue;
                }

                var comparison = ranking[neighbor].CompareTo(ranking[index]);
                if (comparison > 0 ||
                    (comparison == 0 &&
                     CampaignResourceSeed.TieHash(seed, neighborX, neighborY) <
                     CampaignResourceSeed.TieHash(seed, x, y)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int CompareRankedCells(
        int left,
        int right,
        IReadOnlyList<float> ranking,
        int width,
        int seed)
    {
        var score = ranking[right].CompareTo(ranking[left]);
        if (score != 0)
        {
            return score;
        }

        var leftHash = CampaignResourceSeed.TieHash(seed, left % width, left / width);
        var rightHash = CampaignResourceSeed.TieHash(seed, right % width, right / width);
        var hash = leftHash.CompareTo(rightHash);
        return hash != 0 ? hash : left.CompareTo(right);
    }

    private static GrowthPriority CreateGrowthPriority(
        int index,
        int coreId,
        IReadOnlyList<int> cores,
        IReadOnlyList<float> ranking,
        int width,
        double tileKilometers,
        double radiusKilometers,
        int resourceSeed)
    {
        var x = index % width;
        var y = index / width;
        var core = cores[coreId];
        var distance = DistanceKilometers(x, y, core % width, core / width, tileKilometers);
        var coreResponse = Math.Exp(-Math.Pow(distance / radiusKilometers, 2));
        var score = (0.82 * ranking[index]) + (0.18 * coreResponse);
        return new GrowthPriority(
            -score,
            CampaignResourceSeed.TieHash(resourceSeed, x, y),
            coreId,
            index);
    }

    internal static byte CalculatePotential(
        double suitability,
        double admissionFloor,
        double distanceToCoreKilometers,
        double radiusKilometers,
        double detail,
        CampaignResourceAbundance abundance,
        CampaignResourceEffectiveGenerationSettings effective)
    {
        var normalized = Math.Clamp(
            (suitability - admissionFloor) / (1 - admissionFloor),
            0,
            1);
        var core = Math.Exp(-Math.Pow(distanceToCoreKilometers / radiusKilometers, 2));
        var raw = (0.70 * normalized) + (0.25 * core) + (0.05 * Math.Clamp(detail, 0, 1));
        var richnessShift = effective.Richness switch
        {
            CampaignResourceRichness.Poor => -15,
            CampaignResourceRichness.Balanced => 0,
            CampaignResourceRichness.Rich => 15,
            _ => throw new ArgumentOutOfRangeException(nameof(effective), "Unknown resource richness."),
        };
        richnessShift += effective.RichnessBias + GetAbundancePotentialShift(abundance);
        return (byte)Math.Clamp(
            (int)Math.Round(100 * raw, MidpointRounding.AwayFromZero) + richnessShift,
            CampaignResourceOccurrence.MinimumPotential,
            CampaignResourceOccurrence.MaximumPotential);
    }

    internal static double GetRegionRadiusKilometers(
        CampaignResourceDefinition definition,
        CampaignResourceEffectiveGenerationSettings effective,
        double tileKilometers)
    {
        var baseRadius = definition.Rules.RegionScaleKilometers is { } range
            ? (range.Minimum + range.Maximum) / 2
            : definition.DistributionProfile switch
            {
                CampaignResourceDistributionProfile.Field => 80,
                CampaignResourceDistributionProfile.Vein => 35,
                CampaignResourceDistributionProfile.Basin => 65,
                CampaignResourceDistributionProfile.SurfaceDeposit => 25,
                CampaignResourceDistributionProfile.Aquatic => 70,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    "Unknown distribution profile."),
            };
        var concentrationMultiplier = effective.Concentration switch
        {
            CampaignResourceConcentration.FewLarge => 1.60,
            CampaignResourceConcentration.Balanced => 1.00,
            CampaignResourceConcentration.ManySmall => 0.60,
            _ => throw new ArgumentOutOfRangeException(nameof(effective), "Unknown resource concentration."),
        };
        return Math.Max(tileKilometers, baseRadius * concentrationMultiplier);
    }

    internal static double GetAdmissionFloor(CampaignResourceDistributionProfile profile) => profile switch
    {
        CampaignResourceDistributionProfile.Field => 0.30,
        CampaignResourceDistributionProfile.Vein => 0.40,
        CampaignResourceDistributionProfile.Basin => 0.42,
        CampaignResourceDistributionProfile.SurfaceDeposit => 0.38,
        CampaignResourceDistributionProfile.Aquatic => 0.30,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown distribution profile."),
    };

    private static double GetShapeFloor(CampaignResourceDistributionProfile profile) => profile switch
    {
        CampaignResourceDistributionProfile.Field => 0.18,
        CampaignResourceDistributionProfile.Vein => 0.32,
        CampaignResourceDistributionProfile.Basin => 0.26,
        CampaignResourceDistributionProfile.SurfaceDeposit => 0.27,
        CampaignResourceDistributionProfile.Aquatic => 0.18,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown distribution profile."),
    };

    private static double GetCoreSpacingMultiplier(CampaignResourceConcentration concentration) => concentration switch
    {
        CampaignResourceConcentration.FewLarge => 0.92,
        CampaignResourceConcentration.Balanced => 0.82,
        CampaignResourceConcentration.ManySmall => 0.70,
        _ => throw new ArgumentOutOfRangeException(nameof(concentration), concentration, "Unknown concentration."),
    };

    private static int GetEffectiveCoverageBasisPoints(
        int coveragePercent,
        CampaignResourceAbundance abundance)
    {
        var multiplierPercent = abundance switch
        {
            CampaignResourceAbundance.Sparse => 60,
            CampaignResourceAbundance.Balanced => 100,
            CampaignResourceAbundance.Abundant => 150,
            CampaignResourceAbundance.Custom => 100,
            _ => throw new ArgumentOutOfRangeException(nameof(abundance), abundance, "Unknown abundance preset."),
        };
        return Math.Clamp(coveragePercent * multiplierPercent, 0, 10_000);
    }

    private static int GetAbundancePotentialShift(CampaignResourceAbundance abundance) => abundance switch
    {
        CampaignResourceAbundance.Sparse => -10,
        CampaignResourceAbundance.Balanced => 0,
        CampaignResourceAbundance.Abundant => 10,
        CampaignResourceAbundance.Custom => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(abundance), abundance, "Unknown abundance preset."),
    };

    private static string? GetShortfallReason(
        CampaignResourceEffectiveGenerationSettings effective,
        CampaignResourceSuitabilityEvaluation evaluation,
        int requestedCount,
        int preservedLockCount,
        int generatedCount,
        IReadOnlyList<string> unsupported)
    {
        if (unsupported.Count > 0)
        {
            return "Unsupported suitability factors make this resource manual-only for this run.";
        }

        if (!effective.Enabled)
        {
            return "Resource generation is disabled; only locked occurrences are preserved.";
        }

        if (effective.CoveragePercent == 0)
        {
            return "Coverage is 0%; only locked occurrences are preserved.";
        }

        if (evaluation.EligibleTileCount == 0)
        {
            return "No terrain cells satisfy the resource's hard medium and range rules.";
        }

        if (preservedLockCount >= requestedCount)
        {
            return preservedLockCount > requestedCount
                ? "Preserved locks exceed the upper target; no unlocked occurrences were added."
                : null;
        }

        var desiredGenerated = requestedCount - preservedLockCount;
        return generatedCount < desiredGenerated
            ? "Qualified coherent regions ended before the upper target; no unsuitable cells were forced."
            : null;
    }

    private static void ValidateSource(
        CampaignResourceGenerationSource source,
        CampaignResourceCatalog catalog)
    {
        CampaignWorldDefinition.EnsureValid(source.Definition);
        foreach (var entry in source.CurrentEntries)
        {
            if ((uint)entry.X >= (uint)source.Definition.TilesX ||
                (uint)entry.Y >= (uint)source.Definition.TilesY)
            {
                throw new ArgumentException(
                    $"Captured resource coordinate ({entry.X}, {entry.Y}) is outside the campaign grid.",
                    nameof(source));
            }

            entry.Occurrence.EnsureValid();
            if (!catalog.Contains(entry.Occurrence.ResourceId))
            {
                throw new ArgumentException(
                    $"Captured occurrence references unknown resource '{entry.Occurrence.ResourceId}'.",
                    nameof(source));
            }
        }
    }

    private static void AddCandidate(
        ICollection<CampaignResourceEntry> candidateEntries,
        CampaignResourceEntry entry)
    {
        EnsureCandidateLimit((long)candidateEntries.Count + 1);
        candidateEntries.Add(entry);
    }

    internal static void EnsureCandidateLimit(long prospectiveOccurrenceCount)
    {
        if (prospectiveOccurrenceCount > CampaignResourceGenerationResult.MaximumCandidateOccurrenceCount)
        {
            throw new CampaignResourceGenerationLimitException(
                prospectiveOccurrenceCount > int.MaxValue
                    ? int.MaxValue
                    : (int)prospectiveOccurrenceCount);
        }
    }

    private static double UnitNoise(
        double xKilometers,
        double yKilometers,
        int seed,
        double wavelengthKilometers,
        int octaves) =>
        Math.Clamp(
            0.5 +
            (0.5 * CampaignTerrainNoise.Fractal(
                xKilometers,
                yKilometers,
                seed,
                wavelengthKilometers,
                octaves,
                persistence: 0.52)),
            0,
            1);

    private static double DistanceKilometers(
        int x,
        int y,
        int otherX,
        int otherY,
        double tileKilometers)
    {
        var dx = (x - otherX) * tileKilometers;
        var dy = (y - otherY) * tileKilometers;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private readonly record struct GrowthNode(int Index, int CoreId);

    private readonly record struct GrowthPriority(
        double NegativeScore,
        uint TieHash,
        int CoreId,
        int Index) : IComparable<GrowthPriority>
    {
        public int CompareTo(GrowthPriority other)
        {
            var result = NegativeScore.CompareTo(other.NegativeScore);
            if (result != 0)
            {
                return result;
            }

            result = TieHash.CompareTo(other.TieHash);
            if (result != 0)
            {
                return result;
            }

            result = CoreId.CompareTo(other.CoreId);
            return result != 0 ? result : Index.CompareTo(other.Index);
        }
    }

    private readonly record struct GeneratedOccurrence(
        int X,
        int Y,
        byte Potential,
        int RegionId);
}
