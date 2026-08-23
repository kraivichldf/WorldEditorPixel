using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Generation;

public static class CampaignMapGenerator
{
    public const int MinimumGeneratedTilesPerAxis = 8;
    public const long MaximumGeneratedTileCount = CampaignWorldDefinition.MaximumTileCount;

    private const int ArchipelagoOuterIslandCount = 7;
    private const int ContinentalMassCount = 5;
    private const int ContinentalIslandArcCount = 2;
    private const int ContinentalIslandsPerArc = 3;
    private const int CoastalLandmarkKindCount = 4;
    private const double CliffMinimumGrade = 0.06;
    private const double HillsMinimumGrade = 0.04;
    private const double MountainMinimumGrade = 0.12;

    private readonly record struct MountainCandidate(int Index, double Score);

    private readonly record struct ContinentalLobe(
        double CenterX,
        double CenterY,
        double RadiusX,
        double RadiusY,
        double Cosine,
        double Sine);

    private readonly record struct ContinentalMass(
        ContinentalLobe[] Lobes,
        ContinentalLobe[] Bays);

    private sealed record ContinentalProfile(
        ContinentalMass[] Masses,
        ContinentalLobe[] Islands);

    private readonly record struct LandTileCandidate(int Index, double Score);

    private readonly record struct CustomTerrainCandidate(int Index, double Score);

    private readonly record struct TidalInletMouth(
        int Index,
        int InwardX,
        int InwardY,
        double Score);

    private readonly record struct TidalInletProfile(
        int MaximumCount,
        int MinimumReach,
        int MaximumReach,
        int MouthWidenSteps,
        double MinimumMouthScore,
        double OpportunityChance,
        double MinimumRouteSuitability,
        double MaximumWideningElevationFactor);

    private readonly record struct CoastlineGenerationProfile(
        double BendAmplitude,
        double DetailAmplitude,
        double NearshoreNoiseAmplitude,
        double BaysPer700Kilometers,
        double PeninsulasPer700Kilometers,
        double IslandGroupsPer700Kilometers,
        double LandmarkSystemsPer700Kilometers,
        double FeatureScale);

    private enum CoastalLandmarkKind
    {
        MajorGulf,
        HookedCape,
        BarrierSound,
        OffshoreStrait,
    }

    private readonly record struct TerrainMixAssignments(
        CampaignTileType[] Types,
        string?[] CustomTerrainIds);

    private readonly record struct RiverGeneration(
        bool[] RiverTiles,
        bool[] LargeRiverTiles,
        bool[] JunctionTiles);

    private readonly record struct TerrainMixTargetCounts(
        int Plains,
        int Forest,
        int Desert,
        int Hills,
        int Mountain,
        int Steppe,
        IReadOnlyDictionary<string, int> CustomTerrainCounts);

    private readonly record struct MountainGenerationProfile(
        double TargetCoverage,
        int TargetSystemCount,
        double MinimumElevationFactor,
        double MinimumOrogenyStrength,
        double MinimumGrade);

    private readonly record struct ErosionProfile(
        int ThermalIterations,
        double TalusGrade,
        double ThermalTransfer,
        double FluvialStrength);

    private static readonly (int X, int Y)[] CardinalDirections =
    [
        (0, -1),
        (1, 0),
        (0, 1),
        (-1, 0),
    ];

    public static CampaignMapGenerationResult Generate(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options)
    {
        EnsureCanGenerate(definition, options);
        var customTerrainDefinitions = CampaignCustomTerrainDefinition.ValidateAll(options.CustomTerrainDefinitions);
        if (options.Preset == CampaignMapGenerationPreset.Blank)
        {
            return new CampaignMapGenerationResult(
                options.Preset,
                options.Seed,
                options.TerrainStyle,
                options.MountainDensity,
                CampaignMapHydrology.None,
                Array.Empty<CampaignTileEntry>(),
                0,
                0,
                0,
                0,
                0)
            {
                RequestedLandMix = options.LandMix,
                TidalInlets = CampaignMapTidalInlets.None,
                CoastlineStyle = options.CoastlineStyle,
                CustomTerrainDefinitions = customTerrainDefinitions,
            };
        }

        var optionsWithCustomTerrain = options with
        {
            CustomTerrainDefinitions = customTerrainDefinitions,
        };
        var effectiveOptions = options.Preset == CampaignMapGenerationPreset.LandOnly
            ? optionsWithCustomTerrain with
            {
                Hydrology = CampaignMapHydrology.None,
                TidalInlets = CampaignMapTidalInlets.None,
            }
            : optionsWithCustomTerrain;
        var width = definition.TilesX;
        var height = definition.TilesY;
        var count = checked(width * height);
        var landScores = new double[count];
        var isLand = new bool[count];
        var forcedLand = new bool[count];
        var forcedWater = new bool[count];
        var tectonicField = CampaignTectonicModel.Build(definition, effectiveOptions.Seed);
        var continentalProfile = effectiveOptions.Preset == CampaignMapGenerationPreset.Continent
            ? BuildContinentalProfile(definition, effectiveOptions.Seed)
            : null;

        BuildForcedMasks(
            width,
            height,
            effectiveOptions,
            continentalProfile,
            forcedLand,
            forcedWater);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = GetIndex(x, y, width);
                var (normalizedX, normalizedY) = GetNormalizedPosition(x, y, width, height);
                var score = EvaluateLandScore(
                    normalizedX,
                    normalizedY,
                    definition,
                    effectiveOptions,
                    continentalProfile);
                landScores[index] = score;
                isLand[index] = score > 0;
            }
        }

        ApplyForcedMasks(isLand, forcedLand, forcedWater);
        SmoothLandMask(width, height, isLand, forcedLand, forcedWater, passes: 2);
        RemoveTinyLandComponents(width, height, isLand, forcedLand, effectiveOptions.Preset);
        ApplyForcedMasks(isLand, forcedLand, forcedWater);
        var isSea = ResolveOcean(width, height, isLand, forcedWater);
        if (effectiveOptions.TidalInlets != CampaignMapTidalInlets.None)
        {
            var preliminaryHeights = BuildRawHeights(
                definition,
                effectiveOptions,
                landScores,
                isLand,
                tectonicField,
                width,
                height);
            CarveTidalInlets(
                definition,
                effectiveOptions,
                isLand,
                isSea,
                forcedLand,
                preliminaryHeights);
            isSea = ResolveOcean(width, height, isLand, forcedWater);
        }

        var rawHeights = BuildRawHeights(
            definition,
            effectiveOptions,
            landScores,
            isLand,
            tectonicField,
            width,
            height);
        ApplyTerrainErosion(
            definition,
            effectiveOptions,
            isLand,
            isSea,
            rawHeights);
        var isLake = new bool[count];
        if (effectiveOptions.Hydrology != CampaignMapHydrology.None)
        {
            var firstDrainage = BuildDrainage(
                width,
                height,
                isSea,
                rawHeights,
                effectiveOptions.Seed);
            GenerateLakes(
                definition,
                effectiveOptions,
                isLand,
                isSea,
                forcedLand,
                rawHeights,
                firstDrainage,
                isLake);
        }

        var isWater = new bool[count];
        for (var index = 0; index < count; index++)
        {
            isWater[index] = isSea[index] || isLake[index];
        }

        var drainage = BuildDrainage(
            width,
            height,
            isWater,
            rawHeights,
            OffsetSeed(effectiveOptions.Seed, 30_011));
        var riverGeneration = effectiveOptions.Hydrology == CampaignMapHydrology.None
            ? new RiverGeneration(new bool[count], new bool[count], new bool[count])
            : GenerateRivers(
                definition,
                effectiveOptions,
                isLand,
                isWater,
                rawHeights,
                drainage);
        var isRiver = riverGeneration.RiverTiles;
        var isLargeRiver = riverGeneration.LargeRiverTiles;
        var isRiverJunction = riverGeneration.JunctionTiles;
        var distanceToWater = ComputeDistanceToWater(width, height, isWater);
        var isMountain = SelectMountainTiles(
            definition,
            effectiveOptions,
            rawHeights,
            isWater,
            isRiver,
            tectonicField);
        TerrainMixAssignments? terrainMixAssignments = effectiveOptions.LandMix is { } landMix
            ? BuildCustomLandTypes(
                definition,
                effectiveOptions,
                rawHeights,
                isWater,
                isRiver,
                isMountain,
                distanceToWater,
                tectonicField,
                landMix,
                customTerrainDefinitions)
            : null;

        var resolvedTypes = new CampaignTileType[count];
        var landCount = 0;
        var seaCount = 0;
        var lakeCount = 0;
        var riverCount = 0;
        var largeRiverCount = 0;
        var riverJunctionCount = 0;
        var cliffCount = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = GetIndex(x, y, width);
                CampaignTileType type;
                if (isSea[index])
                {
                    type = CampaignTileType.Sea;
                    seaCount++;
                }
                else if (isLake[index])
                {
                    type = CampaignTileType.Lake;
                    lakeCount++;
                }
                else if (isRiver[index])
                {
                    type = isRiverJunction[index]
                        ? CampaignTileType.RiverJunction
                        : isLargeRiver[index]
                        ? CampaignTileType.LargeRiver
                        : CampaignTileType.River;
                    riverCount++;
                    largeRiverCount += isLargeRiver[index] ? 1 : 0;
                    riverJunctionCount += isRiverJunction[index] ? 1 : 0;
                    landCount++;
                }
                else
                {
                    type = ClassifyLand(
                        definition,
                        effectiveOptions,
                        x,
                        y,
                        rawHeights,
                        isWater,
                        isMountain,
                        terrainMixAssignments?.Types,
                        distanceToWater,
                        tectonicField);
                    if (type == CampaignTileType.Cliff)
                    {
                        cliffCount++;
                    }

                    landCount++;
                }

                resolvedTypes[index] = type;
            }
        }

        var customTerrainIds = terrainMixAssignments?.CustomTerrainIds ?? new string?[count];
        var entries = new List<CampaignTileEntry>(count);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = GetIndex(x, y, width);
                entries.Add(new CampaignTileEntry(
                    x,
                    y,
                    new CampaignTileData(
                        resolvedTypes[index],
                        RoundHeight(definition, rawHeights[index]),
                        customTerrainIds[index])));
            }
        }

        var validationMap = new CampaignTileMap(definition, effectiveOptions.CustomTerrainDefinitions);
        validationMap.SetTiles(entries);

        return new CampaignMapGenerationResult(
            effectiveOptions.Preset,
            effectiveOptions.Seed,
            effectiveOptions.TerrainStyle,
            effectiveOptions.MountainDensity,
            effectiveOptions.Hydrology,
            entries.AsReadOnly(),
            landCount,
            seaCount,
            lakeCount,
            riverCount,
            cliffCount)
        {
            RequestedLandMix = effectiveOptions.LandMix,
            TidalInlets = effectiveOptions.TidalInlets,
            CoastlineStyle = effectiveOptions.CoastlineStyle,
            CustomTerrainDefinitions = customTerrainDefinitions,
            CustomTerrainTileCount = customTerrainIds.Count(static id => id is not null),
            LargeRiverTileCount = largeRiverCount,
            RiverJunctionTileCount = riverJunctionCount,
            TectonicProvinceCount = tectonicField.ProvinceCount,
            ErosionPassCount = GetErosionPassCount(effectiveOptions.TerrainStyle),
        };
    }

    public static void EnsureCanGenerate(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CampaignWorldDefinition.EnsureValid(definition);
        if (!Enum.IsDefined(options.Preset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Preset,
                "Unknown campaign map generation preset.");
        }

        if (!Enum.IsDefined(options.TerrainStyle))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.TerrainStyle,
                "Unknown campaign map terrain style.");
        }

        if (!Enum.IsDefined(options.Hydrology))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.Hydrology,
                "Unknown campaign map hydrology amount.");
        }

        if (!Enum.IsDefined(options.MountainDensity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MountainDensity,
                "Unknown campaign map mountain density.");
        }

        if (!Enum.IsDefined(options.TidalInlets))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.TidalInlets,
                "Unknown campaign map tidal-inlet setting.");
        }

        if (!Enum.IsDefined(options.CoastlineStyle))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.CoastlineStyle,
                "Unknown directional coastline style.");
        }

        var customTerrainDefinitions = CampaignCustomTerrainDefinition.ValidateAll(options.CustomTerrainDefinitions);
        if (options.LandMix is { } landMix)
        {
            landMix.EnsureValuesValid();
            EnsureInlandTerrainMixTotal(landMix, customTerrainDefinitions);
        }
        else if (options.Preset != CampaignMapGenerationPreset.Blank &&
                 customTerrainDefinitions.Any(static definition => definition.GenerationSharePercent > 0))
        {
            throw new ArgumentException(
                "Generated custom terrain shares require an inland terrain mix so default and custom shares can total 100%.",
                nameof(options));
        }

        if (options.Preset == CampaignMapGenerationPreset.Blank)
        {
            return;
        }

        if (definition.TilesX < MinimumGeneratedTilesPerAxis ||
            definition.TilesY < MinimumGeneratedTilesPerAxis)
        {
            throw new ArgumentException(
                $"Generated maps need at least {MinimumGeneratedTilesPerAxis} × " +
                $"{MinimumGeneratedTilesPerAxis} campaign tiles. Choose Blank or increase the world size.",
                nameof(definition));
        }

        if (definition.TileCount > MaximumGeneratedTileCount)
        {
            throw new ArgumentException(
                $"Generated maps support up to {MaximumGeneratedTileCount:N0} campaign tiles; " +
                $"this world has {definition.TileCount:N0}. Choose Blank or increase the campaign tile size.",
                nameof(definition));
        }
    }

    private static void EnsureInlandTerrainMixTotal(
        CampaignMapLandMix landMix,
        IReadOnlyList<CampaignCustomTerrainDefinition> customTerrainDefinitions)
    {
        var customShare = customTerrainDefinitions.Sum(static definition => definition.GenerationSharePercent);
        var total = landMix.TotalPercent + customShare;
        if (total != CampaignMapLandMix.RequiredTotalPercent)
        {
            throw new ArgumentException(
                $"Default inland tile ratios plus custom terrain shares must total " +
                $"{CampaignMapLandMix.RequiredTotalPercent}%; current total is {total}%.",
                nameof(landMix));
        }
    }

    private static void BuildForcedMasks(
        int width,
        int height,
        CampaignMapGenerationOptions options,
        ContinentalProfile? continentalProfile,
        bool[] forcedLand,
        bool[] forcedWater)
    {
        switch (options.Preset)
        {
            case CampaignMapGenerationPreset.Continent:
                ForceContinentalOceanAnchors(width, height, options.Seed, forcedWater);
                foreach (var mass in continentalProfile!.Masses)
                {
                    var core = mass.Lobes[0];
                    ForceNormalizedLand(core.CenterX, core.CenterY, width, height, forcedLand);
                }

                break;
            case CampaignMapGenerationPreset.Island:
                ForceWaterBoundary(width, height, forcedWater);
                ForceNormalizedLand(0, 0, width, height, forcedLand);
                break;
            case CampaignMapGenerationPreset.Archipelago:
                ForceWaterBoundary(width, height, forcedWater);
                ForceArchipelagoCenters(width, height, options.Seed, forcedLand);
                break;
            case CampaignMapGenerationPreset.EastCoast:
                for (var y = 0; y < height; y++)
                {
                    forcedWater[GetIndex(width - 1, y, width)] = true;
                }

                break;
            case CampaignMapGenerationPreset.WestCoast:
                for (var y = 0; y < height; y++)
                {
                    forcedWater[GetIndex(0, y, width)] = true;
                }

                break;
            case CampaignMapGenerationPreset.NorthCoast:
                for (var x = 0; x < width; x++)
                {
                    forcedWater[GetIndex(x, 0, width)] = true;
                }

                break;
            case CampaignMapGenerationPreset.SouthCoast:
                for (var x = 0; x < width; x++)
                {
                    forcedWater[GetIndex(x, height - 1, width)] = true;
                }

                break;
            case CampaignMapGenerationPreset.InlandSea:
                ForceLandBoundary(width, height, forcedLand);
                ForceNormalizedWater(0, 0, width, height, forcedWater);
                break;
            case CampaignMapGenerationPreset.LandOnly:
                Array.Fill(forcedLand, true);
                break;
        }
    }

    private static double EvaluateLandScore(
        double x,
        double y,
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        ContinentalProfile? continentalProfile)
    {
        var warpX = FractalNoise(x, y, OffsetSeed(options.Seed, 1_019), 1.7, 3) * 0.10;
        var warpY = FractalNoise(x, y, OffsetSeed(options.Seed, 2_039), 1.7, 3) * 0.10;
        var warpedX = x + warpX;
        var warpedY = y + warpY;
        var coastNoise = FractalNoise(x, y, options.Seed, 2.4, 5);
        var coastDetail = FractalNoise(x, y, OffsetSeed(options.Seed, 7_919), 7.8, 3);

        return options.Preset switch
        {
            CampaignMapGenerationPreset.Continent =>
                EvaluateContinentalWorld(
                    x,
                    y,
                    definition,
                    options.Seed,
                    continentalProfile!),
            CampaignMapGenerationPreset.Island =>
                EvaluateSingleLandmass(
                    warpedX,
                    warpedY,
                    options.Seed,
                    radius: 0.61,
                    radiusX: 0.92,
                    radiusY: 0.84,
                    coastNoise,
                    coastDetail),
            CampaignMapGenerationPreset.Archipelago =>
                EvaluateArchipelago(warpedX, warpedY, options.Seed, coastNoise, coastDetail),
            CampaignMapGenerationPreset.EastCoast =>
                EvaluateDirectionalCoast(
                    warpedX,
                    warpedY,
                    definition,
                    options.Seed,
                    CampaignMapGenerationPreset.EastCoast,
                    options.CoastlineStyle),
            CampaignMapGenerationPreset.WestCoast =>
                EvaluateDirectionalCoast(
                    warpedX,
                    warpedY,
                    definition,
                    options.Seed,
                    CampaignMapGenerationPreset.WestCoast,
                    options.CoastlineStyle),
            CampaignMapGenerationPreset.NorthCoast =>
                EvaluateDirectionalCoast(
                    warpedX,
                    warpedY,
                    definition,
                    options.Seed,
                    CampaignMapGenerationPreset.NorthCoast,
                    options.CoastlineStyle),
            CampaignMapGenerationPreset.SouthCoast =>
                EvaluateDirectionalCoast(
                    warpedX,
                    warpedY,
                    definition,
                    options.Seed,
                    CampaignMapGenerationPreset.SouthCoast,
                    options.CoastlineStyle),
            CampaignMapGenerationPreset.InlandSea =>
                EvaluateInlandSea(warpedX, warpedY, coastNoise, coastDetail),
            CampaignMapGenerationPreset.LandOnly =>
                0.32 + (0.38 * (1 - Math.Max(Math.Abs(x), Math.Abs(y)))) + (coastNoise * 0.10),
            _ => double.NegativeInfinity,
        };
    }

    private static double EvaluateSingleLandmass(
        double x,
        double y,
        int seed,
        double radius,
        double radiusX,
        double radiusY,
        double coastNoise,
        double coastDetail)
    {
        var centerX = (HashUnit(seed, 17, 31) - 0.5) * 0.12;
        var centerY = (HashUnit(seed, 47, 73) - 0.5) * 0.10;
        var distance = EllipseDistance(x, y, centerX, centerY, radiusX, radiusY);
        return radius - distance + (coastNoise * 0.22) + (coastDetail * 0.05);
    }

    private static ContinentalProfile BuildContinentalProfile(
        CampaignWorldDefinition definition,
        int seed)
    {
        var aspectRatio = definition.WorldWidthMeters / (double)definition.WorldHeightMeters;
        var horizontalRadiusScale = Math.Clamp(1.20 / aspectRatio, 0.76, 0.92);
        var layoutVariant = Math.Min(
            2,
            (int)Math.Floor(HashUnit(seed, 40_009, 601) * 3));
        var mirrorX = HashUnit(seed, 40_031, 607) < 0.5 ? -1.0 : 1.0;
        var mirrorY = HashUnit(seed, 40_061, 613) < 0.5 ? -1.0 : 1.0;
        var globalOffsetX = (HashUnit(seed, 40_087, 617) - 0.5) * 0.06;
        var globalOffsetY = (HashUnit(seed, 40_109, 619) - 0.5) * 0.05;
        var roleOffset = Math.Min(
            ContinentalMassCount - 1,
            (int)Math.Floor(HashUnit(seed, 40_127, 623) * ContinentalMassCount));
        double[] roleScales = [1.50, 1.28, 1.08, 0.84, 0.66];
        var masses = new ContinentalMass[ContinentalMassCount];
        for (var index = 0; index < masses.Length; index++)
        {
            var (anchorX, anchorY) = GetContinentalAnchor(layoutVariant, index);
            var centerX = Math.Clamp(
                (anchorX * mirrorX) + globalOffsetX +
                ((HashUnit(seed, 40_151 + index, 631) - 0.5) * 0.10),
                -0.76,
                0.76);
            var centerY = Math.Clamp(
                (anchorY * mirrorY) + globalOffsetY +
                ((HashUnit(seed, 40_211 + index, 641) - 0.5) * 0.10),
                -0.68,
                0.68);
            var roleScale = roleScales[(index + roleOffset) % roleScales.Length];
            var radiusX = (0.250 + (0.055 * HashUnit(seed, 40_271 + index, 647))) *
                horizontalRadiusScale * roleScale;
            var radiusY = (0.220 + (0.055 * HashUnit(seed, 40_309 + index, 653))) * roleScale;
            var angle = (HashUnit(seed, 40_349 + index, 659) - 0.5) * 1.50;
            var axisX = Math.Cos(angle);
            var axisY = Math.Sin(angle);
            var sideX = -axisY;
            var sideY = axisX;
            var peninsulaSide = HashUnit(seed, 40_399 + index, 661) < 0.5 ? -1.0 : 1.0;
            var branchAngle = angle + (peninsulaSide * (0.82 +
                (0.34 * HashUnit(seed, 40_421 + index, 663))));
            var branchX = Math.Cos(branchAngle);
            var branchY = Math.Sin(branchAngle);
            var oppositeBranchAngle = angle - (peninsulaSide * (1.05 +
                (0.28 * HashUnit(seed, 40_439 + index, 665))));
            var oppositeBranchX = Math.Cos(oppositeBranchAngle);
            var oppositeBranchY = Math.Sin(oppositeBranchAngle);

            var lobes = new[]
            {
                CreateContinentalLobe(centerX, centerY, radiusX, radiusY, angle),
                CreateContinentalLobe(
                    centerX + (axisX * radiusX * 0.46) + (sideX * radiusY * 0.12),
                    centerY + (axisY * radiusY * 0.46) + (sideY * radiusX * 0.12),
                    radiusX * 0.78,
                    radiusY * 0.72,
                    angle + 0.18),
                CreateContinentalLobe(
                    centerX - (axisX * radiusX * 0.40) - (sideX * radiusY * 0.16),
                    centerY - (axisY * radiusY * 0.40) - (sideY * radiusX * 0.16),
                    radiusX * 0.70,
                    radiusY * 0.74,
                    angle - 0.22),
                CreateContinentalLobe(
                    centerX + (branchX * radiusX * 0.68),
                    centerY + (branchY * radiusY * 0.68),
                    radiusX * 0.58,
                    radiusY * 0.48,
                    branchAngle),
                CreateContinentalLobe(
                    centerX + (oppositeBranchX * radiusX * 0.62),
                    centerY + (oppositeBranchY * radiusY * 0.62),
                    radiusX * 0.48,
                    radiusY * 0.43,
                    oppositeBranchAngle),
                CreateContinentalLobe(
                    centerX + (axisX * radiusX * 1.05) +
                    (sideX * radiusY * 0.74 * peninsulaSide),
                    centerY + (axisY * radiusY * 1.05) +
                    (sideY * radiusX * 0.74 * peninsulaSide),
                    radiusX * 0.43,
                    radiusY * 0.25,
                    angle + (peninsulaSide * 0.58)),
            };

            var bayDirection = angle - (peninsulaSide * 1.08);
            var secondBayDirection = angle + (peninsulaSide * 2.12);
            var bays = new[]
            {
                CreateContinentalLobe(
                    centerX + (Math.Cos(bayDirection) * radiusX * 0.84),
                    centerY + (Math.Sin(bayDirection) * radiusY * 0.84),
                    radiusX * 0.42,
                    radiusY * 0.35,
                    bayDirection + (peninsulaSide * 0.28)),
                CreateContinentalLobe(
                    centerX + (Math.Cos(secondBayDirection) * radiusX * 0.91),
                    centerY + (Math.Sin(secondBayDirection) * radiusY * 0.91),
                    radiusX * 0.30,
                    radiusY * 0.27,
                    secondBayDirection - (peninsulaSide * 0.20)),
            };
            masses[index] = new ContinentalMass(lobes, bays);
        }

        return new ContinentalProfile(
            masses,
            BuildContinentalIslandArcs(masses, horizontalRadiusScale, seed));
    }

    private static (double X, double Y) GetContinentalAnchor(int layoutVariant, int index) =>
        (layoutVariant, index) switch
        {
            (0, 0) => (-0.68, -0.40),
            (0, 1) => (-0.58, 0.44),
            (0, 2) => (-0.05, -0.46),
            (0, 3) => (0.43, -0.08),
            (0, 4) => (0.62, 0.50),
            (1, 0) => (-0.72, -0.08),
            (1, 1) => (-0.38, 0.52),
            (1, 2) => (-0.03, -0.50),
            (1, 3) => (0.47, -0.34),
            (1, 4) => (0.64, 0.40),
            (2, 0) => (-0.66, -0.50),
            (2, 1) => (-0.54, 0.28),
            (2, 2) => (0.02, 0.50),
            (2, 3) => (0.38, -0.32),
            (2, 4) => (0.68, 0.28),
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Unknown continental anchor."),
        };

    private static ContinentalLobe[] BuildContinentalIslandArcs(
        IReadOnlyList<ContinentalMass> masses,
        double horizontalRadiusScale,
        int seed)
    {
        var islands = new List<ContinentalLobe>(
            ContinentalIslandArcCount * ContinentalIslandsPerArc);
        for (var arcIndex = 0; arcIndex < ContinentalIslandArcCount; arcIndex++)
        {
            var bestX = 0.0;
            var bestY = 0.0;
            var bestClearance = double.NegativeInfinity;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var salt = (arcIndex * 101) + attempt;
                var candidateX = Lerp(-0.72, 0.72, HashUnit(seed, 41_003 + salt, 673));
                var candidateY = Lerp(-0.62, 0.62, HashUnit(seed, 41_039 + salt, 677));
                var clearance = masses.Min(mass =>
                {
                    var core = mass.Lobes[0];
                    var deltaX = candidateX - core.CenterX;
                    var deltaY = candidateY - core.CenterY;
                    return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                });
                if (islands.Count > 0)
                {
                    clearance = Math.Min(
                        clearance,
                        islands.Min(island =>
                        {
                            var deltaX = candidateX - island.CenterX;
                            var deltaY = candidateY - island.CenterY;
                            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                        }));
                }

                if (clearance > bestClearance)
                {
                    bestX = candidateX;
                    bestY = candidateY;
                    bestClearance = clearance;
                }
            }

            var angle = HashUnit(seed, 41_201 + arcIndex, 683) * Math.PI;
            var tangentX = Math.Cos(angle);
            var tangentY = Math.Sin(angle);
            var normalX = -tangentY;
            var normalY = tangentX;
            for (var islandIndex = 0; islandIndex < ContinentalIslandsPerArc; islandIndex++)
            {
                var local = islandIndex - 1.0;
                var curve = 1 - Math.Abs(local);
                var radius = 0.038 +
                    (0.018 * HashUnit(seed, 41_251 + (arcIndex * 7) + islandIndex, 691));
                islands.Add(CreateContinentalLobe(
                    bestX + (tangentX * local * 0.085) + (normalX * curve * 0.035),
                    bestY + (tangentY * local * 0.085) + (normalY * curve * 0.035),
                    radius * horizontalRadiusScale,
                    radius * (0.78 + (0.30 * HashUnit(seed, 41_309 + islandIndex, 701))),
                    angle + ((HashUnit(seed, 41_353 + islandIndex, 709) - 0.5) * 0.50)));
            }
        }

        return islands.ToArray();
    }

    private static double EvaluateContinentalWorld(
        double x,
        double y,
        CampaignWorldDefinition definition,
        int seed,
        ContinentalProfile profile)
    {
        var worldWidthKilometers = definition.WorldWidthMeters / 1_000.0;
        var worldHeightKilometers = definition.WorldHeightMeters / 1_000.0;
        var shorterDimensionKilometers = Math.Min(worldWidthKilometers, worldHeightKilometers);
        var xKilometers = (x + 1) * worldWidthKilometers * 0.5;
        var yKilometers = (y + 1) * worldHeightKilometers * 0.5;
        var warpWavelength = Math.Max(40, shorterDimensionKilometers * 0.62);
        var warpedX = x + (CampaignTerrainNoise.Fractal(
            xKilometers,
            yKilometers,
            OffsetSeed(seed, 42_017),
            warpWavelength,
            3) * 0.065);
        var warpedY = y + (CampaignTerrainNoise.Fractal(
            xKilometers,
            yKilometers,
            OffsetSeed(seed, 42_043),
            warpWavelength,
            3) * 0.065);

        var bestScore = double.NegativeInfinity;
        foreach (var mass in profile.Masses)
        {
            var massScore = double.NegativeInfinity;
            foreach (var lobe in mass.Lobes)
            {
                massScore = Math.Max(
                    massScore,
                    1 - OrientedEllipseDistance(warpedX, warpedY, lobe));
            }

            foreach (var bay in mass.Bays)
            {
                var bayDistance = OrientedEllipseDistance(warpedX, warpedY, bay);
                massScore -= 0.60 * (1 - SmoothStep(0.12, 1, bayDistance));
            }

            bestScore = Math.Max(bestScore, massScore);
        }

        foreach (var island in profile.Islands)
        {
            bestScore = Math.Max(
                bestScore,
                0.88 - OrientedEllipseDistance(warpedX, warpedY, island));
        }

        var regionalNoise = CampaignTerrainNoise.Fractal(
            xKilometers,
            yKilometers,
            OffsetSeed(seed, 42_089),
            Math.Max(28, shorterDimensionKilometers * 0.34),
            3) * 0.055;
        var coastNoise = CampaignTerrainNoise.Fractal(
            xKilometers,
            yKilometers,
            OffsetSeed(seed, 42_127),
            Math.Max(16, shorterDimensionKilometers * 0.15),
            4) * 0.120;
        var coastDetail = CampaignTerrainNoise.Fractal(
            xKilometers,
            yKilometers,
            OffsetSeed(seed, 42_163),
            Math.Max(8, shorterDimensionKilometers * 0.055),
            3) * 0.030;
        return bestScore + regionalNoise + coastNoise + coastDetail - 0.025;
    }

    private static ContinentalLobe CreateContinentalLobe(
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        double angleRadians) =>
        new(
            centerX,
            centerY,
            radiusX,
            radiusY,
            Math.Cos(angleRadians),
            Math.Sin(angleRadians));

    private static double OrientedEllipseDistance(
        double x,
        double y,
        ContinentalLobe ellipse)
    {
        var deltaX = x - ellipse.CenterX;
        var deltaY = y - ellipse.CenterY;
        var localX = (deltaX * ellipse.Cosine) + (deltaY * ellipse.Sine);
        var localY = (-deltaX * ellipse.Sine) + (deltaY * ellipse.Cosine);
        var normalizedX = localX / ellipse.RadiusX;
        var normalizedY = localY / ellipse.RadiusY;
        return Math.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
    }

    private static double EvaluateArchipelago(
        double x,
        double y,
        int seed,
        double coastNoise,
        double coastDetail)
    {
        GetArchipelagoIsland(seed, -1, out var centerX, out var centerY, out var radiusX, out var radiusY);
        var bestScore = 1 - EllipseDistance(x, y, centerX, centerY, radiusX, radiusY);
        for (var index = 0; index < ArchipelagoOuterIslandCount; index++)
        {
            GetArchipelagoIsland(seed, index, out centerX, out centerY, out radiusX, out radiusY);
            bestScore = Math.Max(
                bestScore,
                1 - EllipseDistance(x, y, centerX, centerY, radiusX, radiusY));
        }

        return bestScore + (coastNoise * 0.16) + (coastDetail * 0.045) - 0.12;
    }

    private static double EvaluateDirectionalCoast(
        double x,
        double y,
        CampaignWorldDefinition definition,
        int seed,
        CampaignMapGenerationPreset preset,
        CampaignMapCoastlineStyle coastlineStyle)
    {
        var (acrossCoast, alongCoast) = preset switch
        {
            CampaignMapGenerationPreset.EastCoast => (x, y),
            CampaignMapGenerationPreset.WestCoast => (-x, y),
            CampaignMapGenerationPreset.NorthCoast => (-y, x),
            CampaignMapGenerationPreset.SouthCoast => (y, x),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Expected a directional coast preset."),
        };
        var isVerticalCoast = preset is CampaignMapGenerationPreset.EastCoast or CampaignMapGenerationPreset.WestCoast;
        var acrossLengthKilometers = (isVerticalCoast
            ? definition.WorldWidthMeters
            : definition.WorldHeightMeters) / 1_000.0;
        var alongLengthKilometers = (isVerticalCoast
            ? definition.WorldHeightMeters
            : definition.WorldWidthMeters) / 1_000.0;
        if (coastlineStyle == CampaignMapCoastlineStyle.FlowingCapes)
        {
            return EvaluateFlowingCapeCoast(
                acrossCoast,
                alongCoast,
                definition,
                seed,
                acrossLengthKilometers,
                alongLengthKilometers);
        }

        var profile = GetCoastlineProfile(coastlineStyle);
        var coastPosition = GetDirectionalCoastPosition(
            alongCoast,
            alongLengthKilometers,
            seed,
            profile);
        var score = coastPosition - acrossCoast;
        var nearshore = 1 - SmoothStep(0.04, 0.34, Math.Abs(score));
        var physicalAcross = acrossCoast * Math.Max(0.35, acrossLengthKilometers / 700.0);
        var physicalAlong = alongCoast * Math.Max(0.35, alongLengthKilometers / 700.0);
        var largeWorldDetailScale = Lerp(
            1,
            1.55,
            SmoothStep(1_400, 4_200, alongLengthKilometers));
        score += FractalNoise(
            physicalAcross,
            physicalAlong,
            OffsetSeed(seed, 8_843),
            2.2,
            3) * profile.NearshoreNoiseAmplitude * largeWorldDetailScale * nearshore;

        score = ApplyCoastalLandmarkSystems(
            score,
            acrossCoast,
            alongCoast,
            definition,
            seed,
            profile,
            acrossLengthKilometers,
            alongLengthKilometers);
        score = ApplyCoastalBays(
            score,
            acrossCoast,
            alongCoast,
            definition,
            seed,
            profile,
            acrossLengthKilometers,
            alongLengthKilometers);
        score = ApplyCoastalPeninsulas(
            score,
            acrossCoast,
            alongCoast,
            definition,
            seed,
            profile,
            acrossLengthKilometers,
            alongLengthKilometers);
        score = ApplyRegionalCoastalSkeleton(
            score,
            acrossCoast,
            alongCoast,
            definition,
            seed,
            coastlineStyle,
            profile,
            acrossLengthKilometers,
            alongLengthKilometers);
        return ApplyCoastalIslandGroups(
            score,
            acrossCoast,
            alongCoast,
            definition,
            seed,
            profile,
            acrossLengthKilometers,
            alongLengthKilometers);
    }

    private static double EvaluateFlowingCapeCoast(
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var mirror = HashUnit(seed, 16_019, 359) < 0.5 ? -1.0 : 1.0;
        var shift = HashSigned(seed, 16_037, 367) * 0.08;
        var canonicalAlong = (alongCoast - shift) * mirror;
        var coastPosition = GetFlowingCoastPosition(
            canonicalAlong,
            alongLengthKilometers,
            seed);
        var score = coastPosition - acrossCoast;
        var nearshore = 1 - SmoothStep(0.03, 0.24, Math.Abs(score));
        score += FractalNoise(
            acrossCoast * Math.Max(0.35, acrossLengthKilometers / 700.0),
            canonicalAlong * Math.Max(0.35, alongLengthKilometers / 700.0),
            OffsetSeed(seed, 16_061),
            2.0,
            2) * 0.008 * nearshore;

        return ApplyRegionalCoastalSkeleton(
            score,
            acrossCoast,
            canonicalAlong,
            definition,
            seed,
            CampaignMapCoastlineStyle.FlowingCapes,
            profile: null,
            acrossLengthKilometers,
            alongLengthKilometers);
    }

    private static double ApplyRegionalCoastalSkeleton(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        CampaignMapCoastlineStyle coastlineStyle,
        CoastlineGenerationProfile? profile,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var shorterLengthKilometers = Math.Min(
            acrossLengthKilometers,
            alongLengthKilometers);
        if (shorterLengthKilometers < 90)
        {
            return score;
        }

        var regionalVisibility = coastlineStyle == CampaignMapCoastlineStyle.FlowingCapes
            ? 1
            : 1 - SmoothStep(1_400, 4_200, shorterLengthKilometers);
        if (regionalVisibility <= 0)
        {
            return score;
        }

        var systemCount = GetRegionalCoastSystemCount(alongLengthKilometers);
        var largeWorldScale = GetRegionalCoastSystemScale(shorterLengthKilometers);
        var largeWorldBlend = SmoothStep(1_400, 4_200, shorterLengthKilometers);
        for (var systemIndex = 0; systemIndex < systemCount; systemIndex++)
        {
            var featureSeed = systemCount == 1
                ? seed
                : OffsetSeed(seed, 43_009 + (systemIndex * 1_013));
            var compactWorldRootAlong = systemCount == 1
                ? coastlineStyle == CampaignMapCoastlineStyle.FlowingCapes
                    ? 0.38
                    : HashSigned(seed, 16_189, 401) * 0.10
                : GetDistributedCoastFeaturePosition(
                    seed,
                    systemIndex,
                    systemCount,
                    43_051) * 0.92;
            var continentalRootAlong = GetDistributedCoastFeaturePosition(
                seed,
                systemIndex,
                systemCount,
                43_051) * 0.82;
            var rootAlong = Lerp(
                compactWorldRootAlong,
                continentalRootAlong,
                largeWorldBlend);
            var systemScale = largeWorldScale * (systemCount == 1
                ? 1
                : 0.86 + (0.28 * HashUnit(featureSeed, 43_087, 733)));
            score = ApplyRegionalCoastalSkeletonAt(
                score,
                acrossCoast,
                alongCoast,
                definition,
                seed,
                featureSeed,
                rootAlong,
                systemScale,
                regionalVisibility,
                coastlineStyle,
                profile,
                acrossLengthKilometers,
                alongLengthKilometers);
        }

        return score;
    }

    private static double ApplyRegionalCoastalSkeletonAt(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int coastSeed,
        int featureSeed,
        double rootAlong,
        double regionalScale,
        double regionalVisibility,
        CampaignMapCoastlineStyle coastlineStyle,
        CoastlineGenerationProfile? profile,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var sourceScore = score;
        var isFlowing = coastlineStyle == CampaignMapCoastlineStyle.FlowingCapes;
        var styleScale = coastlineStyle switch
        {
            CampaignMapCoastlineStyle.Smooth => 0.76,
            CampaignMapCoastlineStyle.FlowingCapes => 1.00,
            CampaignMapCoastlineStyle.Natural => 1.04,
            CampaignMapCoastlineStyle.Rugged => 1.18,
            _ => throw new ArgumentOutOfRangeException(
                nameof(coastlineStyle),
                coastlineStyle,
                "Unknown directional coastline style."),
        } * regionalScale;
        var sizeJitter = 0.88 + (0.24 * HashUnit(featureSeed, 16_151, 389));
        var curveDirection = HashUnit(featureSeed, 16_177, 397) < 0.5 ? -1.0 : 1.0;
        var reachKilometers = ClampCoastFeatureKilometers(
            definition,
            (isFlowing ? 225 : 238) * styleScale * sizeJitter,
            acrossLengthKilometers,
            maximumFraction: 0.42);
        var sweepKilometers = ClampCoastFeatureKilometers(
            definition,
            (isFlowing ? 145 : 118) * styleScale * sizeJitter,
            alongLengthKilometers,
            maximumFraction: 0.28);
        var rootRadiusKilometers = ClampCoastFeatureKilometers(
            definition,
            (isFlowing ? 58 : 66) * styleScale * sizeJitter,
            Math.Min(acrossLengthKilometers, alongLengthKilometers),
            maximumFraction: 0.12);
        var neckRadiusKilometers = Math.Max(
            definition.CampaignTileSizeMeters * 0.004,
            (isFlowing ? 27 : 31) * styleScale * sizeJitter);
        var bodyRadiusKilometers = Math.Max(
            neckRadiusKilometers,
            (isFlowing ? 37 : 47) * styleScale * sizeJitter);
        var tipRadiusKilometers = Math.Max(
            definition.CampaignTileSizeMeters * 0.003,
            (isFlowing ? 13 : 20) * styleScale * sizeJitter);
        var bayDepthKilometers = ClampCoastFeatureKilometers(
            definition,
            105 * styleScale * sizeJitter,
            acrossLengthKilometers,
            maximumFraction: 0.22);
        var baySpanKilometers = ClampCoastFeatureKilometers(
            definition,
            118 * styleScale * sizeJitter,
            alongLengthKilometers,
            maximumFraction: 0.24);
        var largeWorldBlend = SmoothStep(
            1_400,
            4_200,
            Math.Min(acrossLengthKilometers, alongLengthKilometers));
        var primaryBaySide = HashUnit(featureSeed, 43_109, 739) < 0.5 ? -1 : 1;
        var effectiveCurveDirection = Lerp(
            curveDirection,
            -primaryBaySide,
            largeWorldBlend);
        var effectiveSweepKilometers = sweepKilometers * Lerp(1, 0.62, largeWorldBlend);
        var flankOffsetKilometers = (rootRadiusKilometers * 0.78) +
            (baySpanKilometers * 0.56);
        if (alongLengthKilometers > 1_400)
        {
            var alongDistanceKilometers = Math.Abs(alongCoast - rootAlong) *
                alongLengthKilometers * 0.5;
            var maximumAlongInfluence = flankOffsetKilometers +
                (baySpanKilometers * 1.18) +
                Math.Abs(sweepKilometers) +
                (rootRadiusKilometers * 1.50) +
                (definition.CampaignTileSizeMeters * 0.004);
            if (alongDistanceKilometers > maximumAlongInfluence)
            {
                return score;
            }
        }

        var coastAnchorAcross = GetRegionalCoastPosition(
            rootAlong,
            alongLengthKilometers,
            coastSeed,
            coastlineStyle,
            profile);
        var connectionDepthKilometers = Math.Max(
            rootRadiusKilometers * 0.72,
            (bayDepthKilometers * 1.50) - (rootRadiusKilometers * 0.75) +
            (definition.CampaignTileSizeMeters * 0.005));
        var rootAcross = coastAnchorAcross - KilometersToNormalized(
            connectionDepthKilometers,
            acrossLengthKilometers);
        for (var side = -1; side <= 1; side += 2)
        {
            var isPrimaryBay = side == primaryBaySide;
            var bayDepthScale = isPrimaryBay
                ? 1
                : Lerp(1, 0.18, largeWorldBlend);
            var baySpanScale = isPrimaryBay
                ? Lerp(1, 1.30, largeWorldBlend)
                : Lerp(1, 0.42, largeWorldBlend);
            var bayOffsetScale = isPrimaryBay
                ? 1
                : Lerp(1, 0.72, largeWorldBlend);
            var bayAlong = rootAlong + KilometersToNormalized(
                side * flankOffsetKilometers * bayOffsetScale,
                alongLengthKilometers);
            var localCoast = GetRegionalCoastPosition(
                bayAlong,
                alongLengthKilometers,
                coastSeed,
                coastlineStyle,
                profile);
            var bayAcross = localCoast - KilometersToNormalized(
                bayDepthKilometers * (side < 0 ? 0.30 : 0.18),
                acrossLengthKilometers);
            score = CarveRegionalWaterEllipse(
                score,
                acrossCoast,
                alongCoast,
                bayAcross,
                bayAlong,
                bayDepthKilometers * (side < 0 ? 1.08 : 0.92) * bayDepthScale,
                baySpanKilometers * (side < 0 ? 1.12 : 0.88) * baySpanScale,
                side * 0.10 * effectiveCurveDirection,
                acrossLengthKilometers,
                alongLengthKilometers,
                OffsetSeed(featureSeed, 43_151 + side),
                largeWorldBlend);
        }

        var reach = KilometersToNormalized(reachKilometers, acrossLengthKilometers);
        var sweep = KilometersToNormalized(effectiveSweepKilometers, alongLengthKilometers);
        var p0 = (Across: rootAcross, Along: rootAlong);
        var p1 = (
            Across: coastAnchorAcross + (reach * 0.14),
            Along: rootAlong - (effectiveCurveDirection * sweep * Lerp(0.10, 0.06, largeWorldBlend)));
        var p2 = (
            Across: coastAnchorAcross + (reach * 0.68),
            Along: rootAlong + (effectiveCurveDirection * sweep * Lerp(0.40, 0.32, largeWorldBlend)));
        var p3 = (
            Across: coastAnchorAcross + reach,
            Along: rootAlong + (effectiveCurveDirection * sweep));
        var regionalScore = AddCurvedRegionalPeninsula(
            score,
            acrossCoast,
            alongCoast,
            p0,
            p1,
            p2,
            p3,
            rootRadiusKilometers,
            neckRadiusKilometers * Lerp(1, 1.20, largeWorldBlend),
            bodyRadiusKilometers * Lerp(1, 1.15, largeWorldBlend),
            tipRadiusKilometers * Lerp(1, 1.25, largeWorldBlend),
            acrossLengthKilometers,
            alongLengthKilometers,
            OffsetSeed(featureSeed, 16_223));
        return Lerp(sourceScore, regionalScore, regionalVisibility);
    }

    private static int GetRegionalCoastSystemCount(double alongLengthKilometers) => 1;

    private static double GetRegionalCoastSystemScale(double shorterLengthKilometers)
    {
        var largeWorldBlend = SmoothStep(1_400, 4_200, shorterLengthKilometers);
        var targetScale = Math.Clamp(
            Math.Sqrt(shorterLengthKilometers / 700),
            1,
            4);
        return Lerp(1, targetScale, largeWorldBlend);
    }

    private static double GetRegionalCoastPosition(
        double alongCoast,
        double alongLengthKilometers,
        int seed,
        CampaignMapCoastlineStyle coastlineStyle,
        CoastlineGenerationProfile? profile) =>
        coastlineStyle == CampaignMapCoastlineStyle.FlowingCapes
            ? GetFlowingCoastPosition(alongCoast, alongLengthKilometers, seed)
            : GetDirectionalCoastPosition(
                alongCoast,
                alongLengthKilometers,
                seed,
                profile ?? throw new InvalidOperationException(
                    "A standard directional coast requires a generation profile."));

    private static double CarveRegionalWaterEllipse(
        double score,
        double acrossCoast,
        double alongCoast,
        double centerAcross,
        double centerAlong,
        double radiusAcrossKilometers,
        double radiusAlongKilometers,
        double angleRadians,
        double acrossLengthKilometers,
        double alongLengthKilometers,
        int noiseSeed,
        double boundaryRoughness)
    {
        var distance = CoastalEllipseDistance(
            acrossCoast,
            alongCoast,
            centerAcross,
            centerAlong,
            radiusAcrossKilometers,
            radiusAlongKilometers,
            angleRadians,
            acrossLengthKilometers,
            alongLengthKilometers);
        var boundaryNoise = FractalNoise(
            (acrossCoast - centerAcross) * acrossLengthKilometers /
                Math.Max(1, radiusAcrossKilometers * 2),
            (alongCoast - centerAlong) * alongLengthKilometers /
                Math.Max(1, radiusAlongKilometers * 2),
            noiseSeed,
            1.65,
            3) * 0.32 * boundaryRoughness;
        var signedWaterBoundary = (distance - 1 + boundaryNoise) * Math.Min(
            KilometersToNormalized(radiusAcrossKilometers, acrossLengthKilometers),
            KilometersToNormalized(radiusAlongKilometers, alongLengthKilometers));
        return Math.Min(score, signedWaterBoundary);
    }

    private static double AddCurvedRegionalPeninsula(
        double score,
        double acrossCoast,
        double alongCoast,
        (double Across, double Along) p0,
        (double Across, double Along) p1,
        (double Across, double Along) p2,
        (double Across, double Along) p3,
        double rootRadiusKilometers,
        double neckRadiusKilometers,
        double bodyRadiusKilometers,
        double tipRadiusKilometers,
        double acrossLengthKilometers,
        double alongLengthKilometers,
        int noiseSeed)
    {
        var bestSignedDistance = double.NegativeInfinity;
        var previous = p0;
        var compactWorldDetail = CampaignTerrainNoise.Fractal(
            acrossCoast * acrossLengthKilometers * 0.5,
            alongCoast * alongLengthKilometers * 0.5,
            noiseSeed,
            Math.Max(22, bodyRadiusKilometers * 1.3),
            2) * Math.Min(6, bodyRadiusKilometers * 0.10);
        var largeWorldBlend = SmoothStep(
            1_400,
            4_200,
            Math.Min(acrossLengthKilometers, alongLengthKilometers));
        var continentalDetail = CampaignTerrainNoise.Fractal(
            acrossCoast * acrossLengthKilometers * 0.5,
            alongCoast * alongLengthKilometers * 0.5,
            OffsetSeed(noiseSeed, 43_193),
            Math.Max(32, bodyRadiusKilometers * 0.62),
            3) * Math.Min(42, bodyRadiusKilometers * 0.20);
        var detail = Lerp(compactWorldDetail, continentalDetail, largeWorldBlend);
        const int segmentCount = 28;
        for (var segment = 1; segment <= segmentCount; segment++)
        {
            var endT = segment / (double)segmentCount;
            var current = CubicBezier(p0, p1, p2, p3, endT);
            var startT = (segment - 1) / (double)segmentCount;
            var localT = ClosestPointOnPhysicalSegment(
                acrossCoast,
                alongCoast,
                previous,
                current,
                acrossLengthKilometers,
                alongLengthKilometers,
                out var distanceKilometers);
            var pathT = Lerp(startT, endT, localT);
            var radiusKilometers = GetRegionalPeninsulaRadius(
                pathT,
                rootRadiusKilometers,
                neckRadiusKilometers,
                bodyRadiusKilometers,
                tipRadiusKilometers);
            var signedDistance = (radiusKilometers + detail - distanceKilometers) /
                Math.Max(1, acrossLengthKilometers * 0.5);
            bestSignedDistance = Math.Max(bestSignedDistance, signedDistance);
            previous = current;
        }

        return Math.Max(score, bestSignedDistance);
    }

    private static double GetRegionalPeninsulaRadius(
        double pathT,
        double rootRadiusKilometers,
        double neckRadiusKilometers,
        double bodyRadiusKilometers,
        double tipRadiusKilometers)
    {
        if (pathT <= 0.24)
        {
            return Lerp(
                rootRadiusKilometers,
                neckRadiusKilometers,
                SmoothStep(0, 0.24, pathT));
        }

        if (pathT <= 0.64)
        {
            return Lerp(
                neckRadiusKilometers,
                bodyRadiusKilometers,
                SmoothStep(0.24, 0.64, pathT));
        }

        return Lerp(
            bodyRadiusKilometers,
            tipRadiusKilometers,
            SmoothStep(0.64, 1, pathT));
    }

    private static double GetFlowingCoastPosition(
        double canonicalAlong,
        double alongLengthKilometers,
        int seed)
    {
        var physicalScale = Math.Max(0.35, alongLengthKilometers / 700.0);
        var broadVariation = FractalNoise(
            0.41,
            canonicalAlong * physicalScale,
            OffsetSeed(seed, 16_093),
            1.10,
            3) * 0.035;
        var seededBalance = 0.90 + (0.20 * HashUnit(seed, 16_117, 373));
        var coastPosition = 0.27 +
            GetDirectionalCoastBalanceOffset(seed) +
            broadVariation +
            GetLargeWorldCoastMacroDisplacement(
                canonicalAlong,
                alongLengthKilometers,
                seed,
                amplitude: 0.16) +
            (0.24 * seededBalance * GaussianInfluence(canonicalAlong, -0.70, 0.21)) -
            (0.34 * seededBalance * GaussianInfluence(canonicalAlong, -0.18, 0.27)) +
            (0.13 * GaussianInfluence(canonicalAlong, 0.31, 0.15)) -
            (0.23 * GaussianInfluence(canonicalAlong, 0.67, 0.17));
        return coastPosition - GetDirectionalOpenBoundaryRetreat(
            canonicalAlong,
            alongLengthKilometers,
            seed);
    }

    private static (double Across, double Along) CubicBezier(
        (double Across, double Along) p0,
        (double Across, double Along) p1,
        (double Across, double Along) p2,
        (double Across, double Along) p3,
        double amount)
    {
        var inverse = 1 - amount;
        var p0Weight = inverse * inverse * inverse;
        var p1Weight = 3 * inverse * inverse * amount;
        var p2Weight = 3 * inverse * amount * amount;
        var p3Weight = amount * amount * amount;
        return (
            (p0.Across * p0Weight) + (p1.Across * p1Weight) +
            (p2.Across * p2Weight) + (p3.Across * p3Weight),
            (p0.Along * p0Weight) + (p1.Along * p1Weight) +
            (p2.Along * p2Weight) + (p3.Along * p3Weight));
    }

    private static double ClosestPointOnPhysicalSegment(
        double acrossCoast,
        double alongCoast,
        (double Across, double Along) start,
        (double Across, double Along) end,
        double acrossLengthKilometers,
        double alongLengthKilometers,
        out double distanceKilometers)
    {
        var pointAcross = acrossCoast * acrossLengthKilometers * 0.5;
        var pointAlong = alongCoast * alongLengthKilometers * 0.5;
        var startAcross = start.Across * acrossLengthKilometers * 0.5;
        var startAlong = start.Along * alongLengthKilometers * 0.5;
        var endAcross = end.Across * acrossLengthKilometers * 0.5;
        var endAlong = end.Along * alongLengthKilometers * 0.5;
        var segmentAcross = endAcross - startAcross;
        var segmentAlong = endAlong - startAlong;
        var segmentLengthSquared = (segmentAcross * segmentAcross) + (segmentAlong * segmentAlong);
        var amount = segmentLengthSquared <= double.Epsilon
            ? 0
            : Math.Clamp(
                (((pointAcross - startAcross) * segmentAcross) +
                 ((pointAlong - startAlong) * segmentAlong)) / segmentLengthSquared,
                0,
                1);
        var closestAcross = startAcross + (segmentAcross * amount);
        var closestAlong = startAlong + (segmentAlong * amount);
        var deltaAcross = pointAcross - closestAcross;
        var deltaAlong = pointAlong - closestAlong;
        distanceKilometers = Math.Sqrt((deltaAcross * deltaAcross) + (deltaAlong * deltaAlong));
        return amount;
    }

    private static double GaussianInfluence(double value, double center, double standardDeviation)
    {
        var normalized = (value - center) / standardDeviation;
        return Math.Exp(-0.5 * normalized * normalized);
    }

    private static double ApplyCoastalLandmarkSystems(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        CoastlineGenerationProfile profile,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var count = GetCoastFeatureCount(
            alongLengthKilometers,
            profile.LandmarkSystemsPer700Kilometers,
            maximum: GetLargeWorldCoastFeatureMaximum(
                alongLengthKilometers,
                standardMaximum: 6,
                largeWorldMaximum: 18));
        var kindOffset = (int)Math.Floor(
            HashUnit(seed, 15_011, 251) * CoastalLandmarkKindCount);
        for (var landmarkIndex = 0; landmarkIndex < count; landmarkIndex++)
        {
            var centerAlong = GetDistributedCoastFeaturePosition(
                seed,
                landmarkIndex,
                count,
                15_043) * 0.84;
            var sizeJitter = (0.82 + (0.42 * HashUnit(seed, 15_079 + landmarkIndex, 269))) *
                GetLargeWorldLandmarkScale(
                    alongLengthKilometers,
                    landmarkIndex,
                    count);
            if (alongLengthKilometers > 1_400 &&
                GetAlongCoastDistanceKilometers(
                    alongCoast,
                    centerAlong,
                    alongLengthKilometers) > 650 * profile.FeatureScale * sizeJitter)
            {
                continue;
            }

            var kind = (CoastalLandmarkKind)(
                (landmarkIndex + kindOffset) % CoastalLandmarkKindCount);
            score = kind switch
            {
                CoastalLandmarkKind.MajorGulf => ApplyMajorGulf(
                    score,
                    acrossCoast,
                    alongCoast,
                    definition,
                    seed,
                    profile,
                    landmarkIndex,
                    centerAlong,
                    sizeJitter,
                    acrossLengthKilometers,
                    alongLengthKilometers),
                CoastalLandmarkKind.HookedCape => ApplyHookedCape(
                    score,
                    acrossCoast,
                    alongCoast,
                    definition,
                    seed,
                    profile,
                    landmarkIndex,
                    centerAlong,
                    sizeJitter,
                    acrossLengthKilometers,
                    alongLengthKilometers),
                CoastalLandmarkKind.BarrierSound => ApplyBarrierSound(
                    score,
                    acrossCoast,
                    alongCoast,
                    definition,
                    seed,
                    profile,
                    landmarkIndex,
                    centerAlong,
                    sizeJitter,
                    acrossLengthKilometers,
                    alongLengthKilometers),
                CoastalLandmarkKind.OffshoreStrait => ApplyOffshoreStrait(
                    score,
                    acrossCoast,
                    alongCoast,
                    definition,
                    seed,
                    profile,
                    landmarkIndex,
                    centerAlong,
                    sizeJitter,
                    acrossLengthKilometers,
                    alongLengthKilometers),
                _ => score,
            };
        }

        return score;
    }

    private static double ApplyMajorGulf(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        CoastlineGenerationProfile profile,
        int landmarkIndex,
        double centerAlong,
        double sizeJitter,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var depthKilometers = ClampCoastFeatureKilometers(
            definition,
            118 * profile.FeatureScale * sizeJitter,
            acrossLengthKilometers,
            maximumFraction: 0.25);
        var spanKilometers = ClampCoastFeatureKilometers(
            definition,
            depthKilometers * (1.35 + (0.35 * HashUnit(seed, 15_127 + landmarkIndex, 277))),
            alongLengthKilometers,
            maximumFraction: 0.34);
        var coastPosition = GetDirectionalCoastPosition(
            centerAlong,
            alongLengthKilometers,
            seed,
            profile);
        var centerAcross = coastPosition - KilometersToNormalized(
            depthKilometers * 0.28,
            acrossLengthKilometers);
        var angle = HashSigned(seed, 15_163 + landmarkIndex, 293) * 0.18;
        score = SubtractCoastalWaterEllipse(
            score,
            acrossCoast,
            alongCoast,
            centerAcross,
            centerAlong,
            depthKilometers,
            spanKilometers,
            angle,
            acrossLengthKilometers,
            alongLengthKilometers,
            strength: 1.48,
            exponent: 1.12,
            OffsetSeed(seed, 15_191 + landmarkIndex));

        for (var side = -1; side <= 1; side += 2)
        {
            var jawAlong = centerAlong + KilometersToNormalized(
                side * spanKilometers * 0.72,
                alongLengthKilometers);
            var jawCoast = GetDirectionalCoastPosition(
                jawAlong,
                alongLengthKilometers,
                seed,
                profile);
            score = AddCoastalLandEllipse(
                score,
                acrossCoast,
                alongCoast,
                jawCoast + KilometersToNormalized(depthKilometers * 0.18, acrossLengthKilometers),
                jawAlong,
                depthKilometers * 0.44,
                spanKilometers * 0.30,
                angle * side,
                acrossLengthKilometers,
                alongLengthKilometers,
                strength: 1.16,
                OffsetSeed(seed, 15_223 + landmarkIndex + side));
        }

        return score;
    }

    private static double ApplyHookedCape(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        CoastlineGenerationProfile profile,
        int landmarkIndex,
        double centerAlong,
        double sizeJitter,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var reachKilometers = ClampCoastFeatureKilometers(
            definition,
            132 * profile.FeatureScale * sizeJitter,
            acrossLengthKilometers,
            maximumFraction: 0.24);
        var curveKilometers = ClampCoastFeatureKilometers(
            definition,
            84 * profile.FeatureScale * sizeJitter,
            alongLengthKilometers,
            maximumFraction: 0.18);
        var widthKilometers = ClampCoastFeatureKilometers(
            definition,
            32 * profile.FeatureScale * (alongLengthKilometers <= 1_400
                ? 1
                : 1.25 * Math.Sqrt(sizeJitter)),
            Math.Min(acrossLengthKilometers, alongLengthKilometers),
            maximumFraction: 0.07);
        var curveDirection = HashUnit(seed, 15_271 + landmarkIndex, 307) < 0.5 ? -1 : 1;
        for (var segment = 0; segment < 4; segment++)
        {
            var progress = (segment + 1) / 4.0;
            var segmentAlong = centerAlong + KilometersToNormalized(
                curveDirection * curveKilometers * progress * progress,
                alongLengthKilometers);
            var coastPosition = GetDirectionalCoastPosition(
                segmentAlong,
                alongLengthKilometers,
                seed,
                profile);
            var segmentAcross = coastPosition + KilometersToNormalized(
                reachKilometers * progress,
                acrossLengthKilometers);
            var taper = 1 - (0.18 * progress);
            score = AddCoastalLandEllipse(
                score,
                acrossCoast,
                alongCoast,
                segmentAcross,
                segmentAlong,
                widthKilometers * taper,
                widthKilometers * (1.18 - (0.12 * progress)),
                curveDirection * (0.12 + (0.52 * progress)),
                acrossLengthKilometers,
                alongLengthKilometers,
                strength: 1.28,
                OffsetSeed(seed, 15_307 + (landmarkIndex * 7) + segment));
        }

        return score;
    }

    private static double ApplyBarrierSound(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        CoastlineGenerationProfile profile,
        int landmarkIndex,
        double centerAlong,
        double sizeJitter,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var depthKilometers = ClampCoastFeatureKilometers(
            definition,
            54 * profile.FeatureScale * sizeJitter,
            acrossLengthKilometers,
            maximumFraction: 0.12);
        var spanKilometers = ClampCoastFeatureKilometers(
            definition,
            156 * profile.FeatureScale * sizeJitter,
            alongLengthKilometers,
            maximumFraction: 0.30);
        var coastPosition = GetDirectionalCoastPosition(
            centerAlong,
            alongLengthKilometers,
            seed,
            profile);
        var angle = HashSigned(seed, 15_359 + landmarkIndex, 331) * 0.12;
        score = SubtractCoastalWaterEllipse(
            score,
            acrossCoast,
            alongCoast,
            coastPosition - KilometersToNormalized(depthKilometers * 0.10, acrossLengthKilometers),
            centerAlong,
            depthKilometers,
            spanKilometers,
            angle,
            acrossLengthKilometers,
            alongLengthKilometers,
            strength: 1.30,
            exponent: 1.20,
            OffsetSeed(seed, 15_397 + landmarkIndex));

        var barrierDistanceKilometers = 42 * profile.FeatureScale * sizeJitter;
        for (var islandIndex = 0; islandIndex < 3; islandIndex++)
        {
            var offset = (islandIndex - 1) * spanKilometers * 0.58;
            var islandAlong = centerAlong + KilometersToNormalized(offset, alongLengthKilometers);
            var islandCoast = GetDirectionalCoastPosition(
                islandAlong,
                alongLengthKilometers,
                seed,
                profile);
            score = AddCoastalLandEllipse(
                score,
                acrossCoast,
                alongCoast,
                islandCoast + KilometersToNormalized(barrierDistanceKilometers, acrossLengthKilometers),
                islandAlong,
                16 * profile.FeatureScale * sizeJitter,
                spanKilometers * 0.22,
                angle,
                acrossLengthKilometers,
                alongLengthKilometers,
                strength: 1.18,
                OffsetSeed(seed, 15_431 + (landmarkIndex * 7) + islandIndex));
        }

        return score;
    }

    private static double ApplyOffshoreStrait(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        CoastlineGenerationProfile profile,
        int landmarkIndex,
        double centerAlong,
        double sizeJitter,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var islandAcrossRadius = ClampCoastFeatureKilometers(
            definition,
            42 * profile.FeatureScale * sizeJitter,
            acrossLengthKilometers,
            maximumFraction: 0.09);
        var islandAlongRadius = ClampCoastFeatureKilometers(
            definition,
            104 * profile.FeatureScale * sizeJitter,
            alongLengthKilometers,
            maximumFraction: 0.22);
        var channelWidthKilometers = ClampCoastFeatureKilometers(
            definition,
            34 * profile.FeatureScale,
            acrossLengthKilometers,
            maximumFraction: 0.07);
        var coastPosition = GetDirectionalCoastPosition(
            centerAlong,
            alongLengthKilometers,
            seed,
            profile);
        var offshoreDistanceKilometers = 68 * profile.FeatureScale * sizeJitter;
        var islandAcross = coastPosition + KilometersToNormalized(
            offshoreDistanceKilometers,
            acrossLengthKilometers);
        var angle = HashSigned(seed, 15_479 + landmarkIndex, 347) * 0.32;
        score = SubtractCoastalWaterEllipse(
            score,
            acrossCoast,
            alongCoast,
            coastPosition + KilometersToNormalized(channelWidthKilometers * 0.20, acrossLengthKilometers),
            centerAlong,
            channelWidthKilometers,
            islandAlongRadius * 1.08,
            angle * 0.45,
            acrossLengthKilometers,
            alongLengthKilometers,
            strength: 1.08,
            exponent: 1.16,
            OffsetSeed(seed, 15_503 + landmarkIndex));
        score = AddCoastalLandEllipse(
            score,
            acrossCoast,
            alongCoast,
            islandAcross,
            centerAlong,
            islandAcrossRadius,
            islandAlongRadius,
            angle,
            acrossLengthKilometers,
            alongLengthKilometers,
            strength: 1.30,
            OffsetSeed(seed, 15_527 + landmarkIndex));

        for (var satellite = -1; satellite <= 1; satellite += 2)
        {
            score = AddCoastalLandEllipse(
                score,
                acrossCoast,
                alongCoast,
                islandAcross + KilometersToNormalized(
                    islandAcrossRadius * (0.65 + (0.15 * satellite)),
                    acrossLengthKilometers),
                centerAlong + KilometersToNormalized(
                    satellite * islandAlongRadius * 0.90,
                    alongLengthKilometers),
                islandAcrossRadius * 0.42,
                islandAcrossRadius * 0.58,
                angle * satellite,
                acrossLengthKilometers,
                alongLengthKilometers,
                strength: 1.12,
                OffsetSeed(seed, 15_557 + landmarkIndex + satellite));
        }

        return score;
    }

    private static double SubtractCoastalWaterEllipse(
        double score,
        double acrossCoast,
        double alongCoast,
        double centerAcross,
        double centerAlong,
        double radiusAcrossKilometers,
        double radiusAlongKilometers,
        double angleRadians,
        double acrossLengthKilometers,
        double alongLengthKilometers,
        double strength,
        double exponent,
        int noiseSeed)
    {
        var distance = CoastalEllipseDistance(
            acrossCoast,
            alongCoast,
            centerAcross,
            centerAlong,
            radiusAcrossKilometers,
            radiusAlongKilometers,
            angleRadians,
            acrossLengthKilometers,
            alongLengthKilometers);
        var largeFeatureBlend = SmoothStep(
            160,
            420,
            Math.Max(radiusAcrossKilometers, radiusAlongKilometers)) *
            SmoothStep(
                1_400,
                4_200,
                Math.Min(acrossLengthKilometers, alongLengthKilometers));
        var compactBoundaryNoise = FractalNoise(
            (acrossCoast - centerAcross) * acrossLengthKilometers / Math.Max(1, radiusAcrossKilometers * 2),
            (alongCoast - centerAlong) * alongLengthKilometers / Math.Max(1, radiusAlongKilometers * 2),
            noiseSeed,
            1.8,
            2) * 0.07;
        var largeBoundaryNoise = FractalNoise(
            (acrossCoast - centerAcross) * acrossLengthKilometers / Math.Max(1, radiusAcrossKilometers * 2),
            (alongCoast - centerAlong) * alongLengthKilometers / Math.Max(1, radiusAlongKilometers * 2),
            noiseSeed,
            1.8,
            3) * 0.16;
        var boundaryNoise = Lerp(compactBoundaryNoise, largeBoundaryNoise, largeFeatureBlend);
        var influence = Math.Pow(
            1 - SmoothStep(0, 1, distance + boundaryNoise),
            exponent);
        return score - (KilometersToNormalized(radiusAcrossKilometers, acrossLengthKilometers) * strength * influence);
    }

    private static double AddCoastalLandEllipse(
        double score,
        double acrossCoast,
        double alongCoast,
        double centerAcross,
        double centerAlong,
        double radiusAcrossKilometers,
        double radiusAlongKilometers,
        double angleRadians,
        double acrossLengthKilometers,
        double alongLengthKilometers,
        double strength,
        int noiseSeed)
    {
        var distance = CoastalEllipseDistance(
            acrossCoast,
            alongCoast,
            centerAcross,
            centerAlong,
            radiusAcrossKilometers,
            radiusAlongKilometers,
            angleRadians,
            acrossLengthKilometers,
            alongLengthKilometers);
        var largeFeatureBlend = SmoothStep(
            160,
            420,
            Math.Max(radiusAcrossKilometers, radiusAlongKilometers)) *
            SmoothStep(
                1_400,
                4_200,
                Math.Min(acrossLengthKilometers, alongLengthKilometers));
        var compactBoundaryNoise = FractalNoise(
            (acrossCoast - centerAcross) * acrossLengthKilometers / Math.Max(1, radiusAcrossKilometers * 2),
            (alongCoast - centerAlong) * alongLengthKilometers / Math.Max(1, radiusAlongKilometers * 2),
            noiseSeed,
            1.9,
            2) * 0.09;
        var largeBoundaryNoise = FractalNoise(
            (acrossCoast - centerAcross) * acrossLengthKilometers / Math.Max(1, radiusAcrossKilometers * 2),
            (alongCoast - centerAlong) * alongLengthKilometers / Math.Max(1, radiusAlongKilometers * 2),
            noiseSeed,
            1.9,
            3) * 0.17;
        var boundaryNoise = Lerp(compactBoundaryNoise, largeBoundaryNoise, largeFeatureBlend);
        var signedLand = (1 - distance + boundaryNoise) *
            Math.Min(
                KilometersToNormalized(radiusAcrossKilometers, acrossLengthKilometers),
                KilometersToNormalized(radiusAlongKilometers, alongLengthKilometers)) *
            strength;
        return Math.Max(score, signedLand);
    }

    private static double ApplyCoastalBays(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        CoastlineGenerationProfile profile,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var count = GetCoastFeatureCount(
            alongLengthKilometers,
            profile.BaysPer700Kilometers,
            maximum: GetLargeWorldCoastFeatureMaximum(
                alongLengthKilometers,
                standardMaximum: 9,
                largeWorldMaximum: 26));
        for (var featureIndex = 0; featureIndex < count; featureIndex++)
        {
            var centerAlong = GetDistributedCoastFeaturePosition(seed, featureIndex, count, 11_927);
            var sizeJitter = 0.72 + (0.62 * HashUnit(seed, 11_993 + featureIndex, 41));
            var depthKilometers = ClampCoastFeatureKilometers(
                definition,
                58 * profile.FeatureScale * sizeJitter,
                acrossLengthKilometers,
                maximumFraction: 0.17);
            var spanKilometers = ClampCoastFeatureKilometers(
                definition,
                depthKilometers * (1.35 + (0.75 * HashUnit(seed, 12_017 + featureIndex, 59))),
                alongLengthKilometers,
                maximumFraction: 0.24);
            if (alongLengthKilometers > 1_400 &&
                GetAlongCoastDistanceKilometers(
                    alongCoast,
                    centerAlong,
                    alongLengthKilometers) > spanKilometers * 1.15)
            {
                continue;
            }

            var radiusAcross = KilometersToNormalized(depthKilometers, acrossLengthKilometers);
            var radiusAlong = KilometersToNormalized(spanKilometers, alongLengthKilometers);
            var coastPosition = GetDirectionalCoastPosition(
                centerAlong,
                alongLengthKilometers,
                seed,
                profile);
            var centerAcross = coastPosition - (radiusAcross * 0.18);
            var distance = EllipseDistance(
                acrossCoast,
                alongCoast,
                centerAcross,
                centerAlong,
                radiusAcross,
                radiusAlong);
            var influence = Math.Pow(1 - SmoothStep(0, 1, distance), 1.35);
            score -= radiusAcross * 1.18 * influence;
        }

        return score;
    }

    private static double ApplyCoastalPeninsulas(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        CoastlineGenerationProfile profile,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var count = GetCoastFeatureCount(
            alongLengthKilometers,
            profile.PeninsulasPer700Kilometers,
            maximum: GetLargeWorldCoastFeatureMaximum(
                alongLengthKilometers,
                standardMaximum: 8,
                largeWorldMaximum: 22));
        for (var featureIndex = 0; featureIndex < count; featureIndex++)
        {
            var centerAlong = GetDistributedCoastFeaturePosition(seed, featureIndex, count, 13_031);
            var sizeJitter = 0.72 + (0.58 * HashUnit(seed, 13_067 + featureIndex, 73));
            var reachKilometers = ClampCoastFeatureKilometers(
                definition,
                48 * profile.FeatureScale * sizeJitter,
                acrossLengthKilometers,
                maximumFraction: 0.14);
            var spanKilometers = ClampCoastFeatureKilometers(
                definition,
                reachKilometers * (1.15 + (0.65 * HashUnit(seed, 13_103 + featureIndex, 89))),
                alongLengthKilometers,
                maximumFraction: 0.18);
            if (alongLengthKilometers > 1_400 &&
                GetAlongCoastDistanceKilometers(
                    alongCoast,
                    centerAlong,
                    alongLengthKilometers) > spanKilometers * 1.20)
            {
                continue;
            }

            var radiusAcross = KilometersToNormalized(reachKilometers, acrossLengthKilometers);
            var radiusAlong = KilometersToNormalized(spanKilometers, alongLengthKilometers);
            var coastPosition = GetDirectionalCoastPosition(
                centerAlong,
                alongLengthKilometers,
                seed,
                profile);
            var centerAcross = coastPosition + (radiusAcross * 0.16);
            var distance = EllipseDistance(
                acrossCoast,
                alongCoast,
                centerAcross,
                centerAlong,
                radiusAcross,
                radiusAlong);
            var influence = Math.Pow(1 - SmoothStep(0, 1, distance), 1.25);
            score += radiusAcross * 1.14 * influence;
        }

        return score;
    }

    private static double ApplyCoastalIslandGroups(
        double score,
        double acrossCoast,
        double alongCoast,
        CampaignWorldDefinition definition,
        int seed,
        CoastlineGenerationProfile profile,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var groupCount = GetCoastFeatureCount(
            alongLengthKilometers,
            profile.IslandGroupsPer700Kilometers,
            maximum: GetLargeWorldCoastFeatureMaximum(
                alongLengthKilometers,
                standardMaximum: 5,
                largeWorldMaximum: 14));
        for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            var groupAlong = GetDistributedCoastFeaturePosition(seed, groupIndex, groupCount, 14_117);
            var groupScale = GetLargeWorldIslandGroupScale(
                alongLengthKilometers,
                groupIndex,
                groupCount);
            if (alongLengthKilometers > 1_400 &&
                GetAlongCoastDistanceKilometers(
                    alongCoast,
                    groupAlong,
                    alongLengthKilometers) > 360 * profile.FeatureScale * groupScale)
            {
                continue;
            }

            var coastPosition = GetDirectionalCoastPosition(
                groupAlong,
                alongLengthKilometers,
                seed,
                profile);
            var groupDistanceKilometers = ClampCoastFeatureKilometers(
                definition,
                (82 + (82 * HashUnit(seed, 14_149 + groupIndex, 101))) *
                    profile.FeatureScale * groupScale,
                acrossLengthKilometers,
                maximumFraction: 0.20);
            var groupAcross = coastPosition + KilometersToNormalized(
                groupDistanceKilometers,
                acrossLengthKilometers);
            var islandCount = 2 +
                (int)Math.Floor(HashUnit(seed, 14_173 + groupIndex, 127) * 3) +
                (int)Math.Round((groupScale - 1) * 1.5, MidpointRounding.AwayFromZero);
            for (var islandIndex = 0; islandIndex < islandCount; islandIndex++)
            {
                var islandSeedIndex = (groupIndex * 11) + islandIndex;
                var alongOffsetKilometers =
                    (HashUnit(seed, 14_209 + islandSeedIndex, 149) - 0.5) *
                    92 * profile.FeatureScale * groupScale;
                var acrossOffsetKilometers =
                    (HashUnit(seed, 14_251 + islandSeedIndex, 167) - 0.5) *
                    46 * profile.FeatureScale * Math.Sqrt(groupScale);
                var centerAlong = Math.Clamp(
                    groupAlong + KilometersToNormalized(alongOffsetKilometers, alongLengthKilometers),
                    -0.92,
                    0.92);
                var centerAcross = groupAcross + KilometersToNormalized(
                    acrossOffsetKilometers,
                    acrossLengthKilometers);
                var radiusAcrossKilometers = ClampCoastFeatureKilometers(
                    definition,
                    (14 + (19 * HashUnit(seed, 14_293 + islandSeedIndex, 181))) *
                        profile.FeatureScale * Math.Sqrt(groupScale),
                    acrossLengthKilometers,
                    maximumFraction: 0.055);
                var radiusAlongKilometers = ClampCoastFeatureKilometers(
                    definition,
                    radiusAcrossKilometers * (0.85 + (0.85 * HashUnit(seed, 14_329 + islandSeedIndex, 199))),
                    alongLengthKilometers,
                    maximumFraction: 0.075);
                var radiusAcross = KilometersToNormalized(radiusAcrossKilometers, acrossLengthKilometers);
                var radiusAlong = KilometersToNormalized(radiusAlongKilometers, alongLengthKilometers);
                var distance = EllipseDistance(
                    acrossCoast,
                    alongCoast,
                    centerAcross,
                    centerAlong,
                    radiusAcross,
                    radiusAlong);
                var islandNoise = FractalNoise(
                    (acrossCoast - centerAcross) / Math.Max(0.01, radiusAcross),
                    (alongCoast - centerAlong) / Math.Max(0.01, radiusAlong),
                    OffsetSeed(seed, 14_357 + islandSeedIndex),
                    1.7,
                    2) * 0.10;
                var islandScore = (1 - distance + islandNoise) * Math.Min(radiusAcross, radiusAlong);
                score = Math.Max(score, islandScore);
            }
        }

        return score;
    }

    private static CoastlineGenerationProfile GetCoastlineProfile(
        CampaignMapCoastlineStyle coastlineStyle) => coastlineStyle switch
        {
            CampaignMapCoastlineStyle.Smooth => new(0.09, 0.016, 0.007, 1.2, 0.7, 0, 0, 0.72),
            CampaignMapCoastlineStyle.Natural => new(0.14, 0.032, 0.015, 1.6, 1.1, 0.7, 2.0, 1.00),
            CampaignMapCoastlineStyle.Rugged => new(0.18, 0.046, 0.026, 2.6, 2.0, 1.5, 3.2, 1.22),
            CampaignMapCoastlineStyle.FlowingCapes => throw new InvalidOperationException(
                "Flowing capes use their dedicated coastline profile."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(coastlineStyle),
                coastlineStyle,
                "Unknown directional coastline style."),
        };

    private static double GetDirectionalCoastPosition(
        double alongCoast,
        double alongLengthKilometers,
        int seed,
        CoastlineGenerationProfile profile)
    {
        var physicalScale = Math.Max(0.35, alongLengthKilometers / 700.0);
        var bend = FractalNoise(
            0.37,
            alongCoast * physicalScale,
            OffsetSeed(seed, 4_271),
            1.55,
            4) * profile.BendAmplitude;
        var detail = FractalNoise(
            1.83,
            alongCoast * physicalScale,
            OffsetSeed(seed, 6_137),
            4.2,
            3) * profile.DetailAmplitude;
        var coastPosition = 0.52 +
            GetDirectionalCoastBalanceOffset(seed) +
            bend +
            detail +
            GetLargeWorldCoastMacroDisplacement(
                alongCoast,
                alongLengthKilometers,
                seed,
                profile.BendAmplitude * 1.40);
        return coastPosition - GetDirectionalOpenBoundaryRetreat(
            alongCoast,
            alongLengthKilometers,
            seed);
    }

    private static double GetLargeWorldCoastMacroDisplacement(
        double alongCoast,
        double alongLengthKilometers,
        int seed,
        double amplitude)
    {
        var largeWorldBlend = SmoothStep(1_400, 4_200, alongLengthKilometers);
        if (largeWorldBlend <= 0)
        {
            return 0;
        }

        var physicalScale = Math.Max(0.35, alongLengthKilometers / 2_800);
        return FractalNoise(
            2.71,
            alongCoast * physicalScale,
            OffsetSeed(seed, 43_211),
            0.58,
            3) * amplitude * largeWorldBlend;
    }

    private static double GetDirectionalOpenBoundaryRetreat(
        double alongCoast,
        double alongLengthKilometers,
        int seed)
    {
        var openness = HashUnit(seed, 20_033, 487);
        if (openness < 0.30 || alongLengthKilometers < 90)
        {
            return 0;
        }

        var strength = SmoothStep(0.30, 1, openness);
        var side = HashUnit(seed, 20_057, 491) < 0.5 ? -1.0 : 1.0;
        var center = side * Lerp(0.98, 1.08, HashUnit(seed, 20_081, 499));
        var spanFraction = Lerp(0.12, 0.20, HashUnit(seed, 20_117, 503));
        var maximumNormalizedSpan = Math.Min(
            0.48,
            260 / Math.Max(1, alongLengthKilometers));
        var minimumNormalizedSpan = Math.Min(0.18, maximumNormalizedSpan);
        var normalizedSpan = Math.Clamp(
            spanFraction * 2,
            minimumNormalizedSpan,
            maximumNormalizedSpan);
        var normalizedRetreat = Lerp(1.24, 1.84, strength);
        return normalizedRetreat * GaussianInfluence(
            alongCoast,
            center,
            normalizedSpan);
    }

    private static double GetDirectionalCoastBalanceOffset(int seed) =>
        Lerp(-0.45, 0.15, HashUnit(seed, 19_901, 475));

    private static int GetCoastFeatureCount(
        double alongLengthKilometers,
        double featuresPer700Kilometers,
        int maximum)
    {
        if (featuresPer700Kilometers <= 0 || alongLengthKilometers < 80)
        {
            return 0;
        }

        return Math.Clamp(
            (int)Math.Round(
                (alongLengthKilometers / 700.0) * featuresPer700Kilometers,
                MidpointRounding.AwayFromZero),
            1,
            maximum);
    }

    private static int GetLargeWorldCoastFeatureMaximum(
        double alongLengthKilometers,
        int standardMaximum,
        int largeWorldMaximum)
    {
        if (alongLengthKilometers <= 1_400)
        {
            return standardMaximum;
        }

        return Math.Clamp(
            (int)Math.Ceiling(alongLengthKilometers / 520),
            standardMaximum,
            largeWorldMaximum);
    }

    private static double GetLargeWorldLandmarkScale(
        double alongLengthKilometers,
        int landmarkIndex,
        int landmarkCount)
    {
        if (alongLengthKilometers <= 1_400 || landmarkCount <= 0)
        {
            return 1;
        }

        var macroLandmarkCount = Math.Clamp(
            (int)Math.Round(alongLengthKilometers / 2_500, MidpointRounding.AwayFromZero),
            1,
            4);
        var isMacroLandmark = false;
        for (var macroIndex = 0; macroIndex < macroLandmarkCount; macroIndex++)
        {
            var selectedIndex = Math.Clamp(
                (int)Math.Floor(((macroIndex + 0.5) / macroLandmarkCount) * landmarkCount),
                0,
                landmarkCount - 1);
            isMacroLandmark |= landmarkIndex == selectedIndex;
        }

        if (!isMacroLandmark)
        {
            return 1;
        }

        var targetScale = Math.Clamp(
            Math.Sqrt(alongLengthKilometers / 700),
            1,
            4.00);
        return Lerp(
            1,
            targetScale,
            SmoothStep(1_400, 4_200, alongLengthKilometers));
    }

    private static double GetLargeWorldIslandGroupScale(
        double alongLengthKilometers,
        int groupIndex,
        int groupCount)
    {
        if (alongLengthKilometers <= 1_400 || groupCount <= 0)
        {
            return 1;
        }

        var macroGroupCount = Math.Clamp(
            (int)Math.Round(alongLengthKilometers / 3_500, MidpointRounding.AwayFromZero),
            1,
            3);
        var isMacroGroup = false;
        for (var macroIndex = 0; macroIndex < macroGroupCount; macroIndex++)
        {
            var selectedIndex = Math.Clamp(
                (int)Math.Floor(((macroIndex + 0.5) / macroGroupCount) * groupCount),
                0,
                groupCount - 1);
            isMacroGroup |= groupIndex == selectedIndex;
        }

        if (!isMacroGroup)
        {
            return 1;
        }

        var targetScale = Math.Clamp(
            Math.Sqrt(alongLengthKilometers / 700) * 0.75,
            1,
            3.00);
        return Lerp(
            1,
            targetScale,
            SmoothStep(1_400, 4_200, alongLengthKilometers));
    }

    private static double GetAlongCoastDistanceKilometers(
        double alongCoast,
        double featureAlong,
        double alongLengthKilometers) =>
        Math.Abs(alongCoast - featureAlong) * alongLengthKilometers * 0.5;

    private static double GetDistributedCoastFeaturePosition(
        int seed,
        int featureIndex,
        int featureCount,
        int salt)
    {
        var slot = (featureIndex + 0.5) / featureCount;
        var jitter = (HashUnit(seed, salt + featureIndex, 223) - 0.5) * (0.70 / featureCount);
        return Lerp(-0.84, 0.84, Math.Clamp(slot + jitter, 0.04, 0.96));
    }

    private static double ClampCoastFeatureKilometers(
        CampaignWorldDefinition definition,
        double requestedKilometers,
        double dimensionKilometers,
        double maximumFraction) =>
        Math.Clamp(
            requestedKilometers,
            Math.Max(8, definition.CampaignTileSizeMeters * 0.003),
            Math.Max(8, dimensionKilometers * maximumFraction));

    private static double KilometersToNormalized(double kilometers, double dimensionKilometers) =>
        (kilometers * 2) / Math.Max(1, dimensionKilometers);

    private static double CoastalEllipseDistance(
        double acrossCoast,
        double alongCoast,
        double centerAcross,
        double centerAlong,
        double radiusAcrossKilometers,
        double radiusAlongKilometers,
        double angleRadians,
        double acrossLengthKilometers,
        double alongLengthKilometers)
    {
        var deltaAcrossKilometers = (acrossCoast - centerAcross) * acrossLengthKilometers * 0.5;
        var deltaAlongKilometers = (alongCoast - centerAlong) * alongLengthKilometers * 0.5;
        var cosine = Math.Cos(angleRadians);
        var sine = Math.Sin(angleRadians);
        var localAcross = (deltaAcrossKilometers * cosine) + (deltaAlongKilometers * sine);
        var localAlong = (-deltaAcrossKilometers * sine) + (deltaAlongKilometers * cosine);
        var normalizedAcross = localAcross / Math.Max(1, radiusAcrossKilometers);
        var normalizedAlong = localAlong / Math.Max(1, radiusAlongKilometers);
        return Math.Sqrt((normalizedAcross * normalizedAcross) + (normalizedAlong * normalizedAlong));
    }

    private static double EvaluateInlandSea(
        double x,
        double y,
        double coastNoise,
        double coastDetail)
    {
        var distance = EllipseDistance(x, y, 0, 0, 0.48, 0.38);
        return distance - 1 + (coastNoise * 0.16) + (coastDetail * 0.04);
    }

    private static void SmoothLandMask(
        int width,
        int height,
        bool[] isLand,
        IReadOnlyList<bool> forcedLand,
        IReadOnlyList<bool> forcedWater,
        int passes)
    {
        for (var pass = 0; pass < passes; pass++)
        {
            var next = (bool[])isLand.Clone();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = GetIndex(x, y, width);
                    if (forcedLand[index] || forcedWater[index])
                    {
                        continue;
                    }

                    var landNeighbors = 0;
                    var considered = 0;
                    for (var offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (var offsetX = -1; offsetX <= 1; offsetX++)
                        {
                            var neighborX = x + offsetX;
                            var neighborY = y + offsetY;
                            if (!IsInside(neighborX, neighborY, width, height))
                            {
                                continue;
                            }

                            considered++;
                            if (isLand[GetIndex(neighborX, neighborY, width)])
                            {
                                landNeighbors++;
                            }
                        }
                    }

                    if (landNeighbors * 2 >= considered + 3)
                    {
                        next[index] = true;
                    }
                    else if (landNeighbors * 2 <= considered - 3)
                    {
                        next[index] = false;
                    }
                }
            }

            Array.Copy(next, isLand, isLand.Length);
            ApplyForcedMasks(isLand, forcedLand, forcedWater);
        }
    }

    private static void RemoveTinyLandComponents(
        int width,
        int height,
        bool[] isLand,
        IReadOnlyList<bool> forcedLand,
        CampaignMapGenerationPreset preset)
    {
        var minimumArea = preset == CampaignMapGenerationPreset.Archipelago
            ? 2
            : Math.Max(3, isLand.Length / 20_000);
        var visited = new bool[isLand.Length];
        var queue = new Queue<int>();
        for (var start = 0; start < isLand.Length; start++)
        {
            if (!isLand[start] || visited[start])
            {
                continue;
            }

            var component = new List<int>();
            var containsForcedLand = false;
            visited[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                component.Add(index);
                containsForcedLand |= forcedLand[index];
                var x = index % width;
                var y = index / width;
                foreach (var direction in CardinalDirections)
                {
                    var neighborX = x + direction.X;
                    var neighborY = y + direction.Y;
                    if (!IsInside(neighborX, neighborY, width, height))
                    {
                        continue;
                    }

                    var neighbor = GetIndex(neighborX, neighborY, width);
                    if (isLand[neighbor] && !visited[neighbor])
                    {
                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (containsForcedLand || component.Count >= minimumArea)
            {
                continue;
            }

            foreach (var index in component)
            {
                isLand[index] = false;
            }
        }
    }

    private static bool[] ResolveOcean(
        int width,
        int height,
        bool[] isLand,
        IReadOnlyList<bool> forcedWater)
    {
        var isSea = new bool[isLand.Length];
        var queue = new Queue<int>();
        for (var index = 0; index < forcedWater.Count; index++)
        {
            if (forcedWater[index] && !isLand[index])
            {
                isSea[index] = true;
                queue.Enqueue(index);
            }
        }

        if (queue.Count == 0)
        {
            for (var x = 0; x < width; x++)
            {
                EnqueueOceanSeed(GetIndex(x, 0, width), isLand, isSea, queue);
                EnqueueOceanSeed(GetIndex(x, height - 1, width), isLand, isSea, queue);
            }

            for (var y = 1; y + 1 < height; y++)
            {
                EnqueueOceanSeed(GetIndex(0, y, width), isLand, isSea, queue);
                EnqueueOceanSeed(GetIndex(width - 1, y, width), isLand, isSea, queue);
            }
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % width;
            var y = index / width;
            foreach (var direction in CardinalDirections)
            {
                var neighborX = x + direction.X;
                var neighborY = y + direction.Y;
                if (!IsInside(neighborX, neighborY, width, height))
                {
                    continue;
                }

                var neighbor = GetIndex(neighborX, neighborY, width);
                if (!isLand[neighbor] && !isSea[neighbor])
                {
                    isSea[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }

        for (var index = 0; index < isLand.Length; index++)
        {
            if (!isLand[index] && !isSea[index])
            {
                isLand[index] = true;
            }
        }

        return isSea;
    }

    private static void CarveTidalInlets(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        bool[] isLand,
        IReadOnlyList<bool> isSea,
        IReadOnlyList<bool> forcedLand,
        IReadOnlyList<double> preliminaryHeights)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var profile = GetTidalInletProfile(options.TidalInlets, width, height);
        if (profile.MaximumCount == 0 || !isSea.Any(static sea => sea))
        {
            return;
        }

        var distanceToSea = ComputeDistanceToWater(width, height, isSea);
        var mouths = GetTidalInletMouths(
            definition,
            options,
            isLand,
            isSea,
            forcedLand,
            preliminaryHeights);
        var carved = new bool[isLand.Length];
        var acceptedMouths = new bool[isLand.Length];
        var consideredRegions = new bool[isLand.Length];
        var mouthSeparation = Math.Max(4, profile.MaximumReach / 2);
        var opportunitySeparation = Math.Max(6, (profile.MaximumReach * 3) / 2);
        var consideredCount = 0;
        var acceptedCount = 0;
        foreach (var mouth in mouths)
        {
            if (acceptedCount >= profile.MaximumCount)
            {
                break;
            }

            if (IsNearMarkedCell(
                    mouth.Index,
                    width,
                    height,
                    consideredRegions,
                    opportunitySeparation) ||
                IsNearMarkedCell(mouth.Index, width, height, acceptedMouths, mouthSeparation) ||
                IsNearMarkedCell(mouth.Index, width, height, carved, 2))
            {
                continue;
            }

            if (consideredCount >= profile.MaximumCount)
            {
                break;
            }

            consideredRegions[mouth.Index] = true;
            consideredCount++;

            var opportunityStrength = SmoothStep(
                profile.MinimumMouthScore,
                0.92,
                mouth.Score);
            var opportunityChance = profile.OpportunityChance *
                Lerp(0.45, 1, opportunityStrength);
            var opportunityRoll = HashUnit(
                options.Seed,
                mouth.Index + 31_337,
                521);
            if (mouth.Score < profile.MinimumMouthScore ||
                opportunityRoll > opportunityChance)
            {
                continue;
            }

            var target = FindTidalInletTarget(
                definition,
                options,
                isLand,
                forcedLand,
                carved,
                distanceToSea,
                preliminaryHeights,
                mouth,
                profile);
            if (target < 0 || !TryFindTidalInletRoute(
                    definition,
                    options.Seed,
                    isLand,
                    forcedLand,
                    carved,
                    distanceToSea,
                    preliminaryHeights,
                    mouth,
                    target,
                    profile,
                    out var route) ||
                route.Count - 1 < profile.MinimumReach)
            {
                continue;
            }


            if (GetTidalInletRouteSuitability(
                    definition,
                    preliminaryHeights,
                    route,
                    mouth) < profile.MinimumRouteSuitability)
            {
                continue;
            }

            CarveTidalInletRoute(
                route,
                definition,
                options,
                isLand,
                forcedLand,
                carved,
                preliminaryHeights,
                profile);
            acceptedMouths[mouth.Index] = true;
            acceptedCount++;
        }
    }

    private static IReadOnlyList<TidalInletMouth> GetTidalInletMouths(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        IReadOnlyList<bool> isLand,
        IReadOnlyList<bool> isSea,
        IReadOnlyList<bool> forcedLand,
        IReadOnlyList<double> preliminaryHeights)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var mouths = new List<TidalInletMouth>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = GetIndex(x, y, width);
                if (!isLand[index] || forcedLand[index])
                {
                    continue;
                }

                var seaNeighborCount = 0;
                var inwardX = 0;
                var inwardY = 0;
                foreach (var direction in CardinalDirections)
                {
                    var neighborX = x + direction.X;
                    var neighborY = y + direction.Y;
                    if (!IsInside(neighborX, neighborY, width, height) ||
                        !isSea[GetIndex(neighborX, neighborY, width)])
                    {
                        continue;
                    }

                    seaNeighborCount++;
                    inwardX = -direction.X;
                    inwardY = -direction.Y;
                }

                if (seaNeighborCount != 1)
                {
                    continue;
                }

                var (normalizedX, normalizedY) = GetNormalizedPosition(x, y, width, height);
                var shallowLowland = 1 - GetElevationFactor(definition, preliminaryHeights[index]);
                var inwardXPosition = x + inwardX;
                var inwardYPosition = y + inwardY;
                var inwardGrade = IsInside(inwardXPosition, inwardYPosition, width, height)
                    ? Math.Max(
                        0,
                        preliminaryHeights[GetIndex(inwardXPosition, inwardYPosition, width)] -
                        preliminaryHeights[index]) /
                        Math.Max(1, definition.CampaignTileSizeMeters)
                    : 1;
                var valleyOpening = 1 - Math.Clamp(inwardGrade / 0.05, 0, 1);
                var estuaryNoise = (FractalNoise(
                    normalizedX,
                    normalizedY,
                    OffsetSeed(options.Seed, 27_541),
                    1.25,
                    3) + 1) * 0.5;
                var score = (0.52 * shallowLowland) + (0.28 * estuaryNoise) +
                    (0.20 * valleyOpening);
                mouths.Add(new TidalInletMouth(index, inwardX, inwardY, score));
            }
        }

        return mouths
            .OrderByDescending(static mouth => mouth.Score)
            .ThenBy(static mouth => mouth.Index)
            .ToArray();
    }

    private static int FindTidalInletTarget(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        IReadOnlyList<bool> isLand,
        IReadOnlyList<bool> forcedLand,
        IReadOnlyList<bool> carved,
        IReadOnlyList<int> distanceToSea,
        IReadOnlyList<double> preliminaryHeights,
        TidalInletMouth mouth,
        TidalInletProfile profile)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var mouthX = mouth.Index % width;
        var mouthY = mouth.Index / width;
        var maximumLateralOffset = Math.Max(3, profile.MaximumReach / 3);
        var desiredReach = Lerp(
            profile.MinimumReach,
            profile.MaximumReach,
            0.18 + (0.64 * HashUnit(options.Seed, mouth.Index + 32_711, 547)));
        var reachRange = Math.Max(1, profile.MaximumReach - profile.MinimumReach);
        var target = -1;
        var bestScore = double.NegativeInfinity;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = GetIndex(x, y, width);
                if (!isLand[index] || forcedLand[index] ||
                    IsNearMarkedCell(index, width, height, carved, 2))
                {
                    continue;
                }

                var deltaX = x - mouthX;
                var deltaY = y - mouthY;
                var progress = (deltaX * mouth.InwardX) + (deltaY * mouth.InwardY);
                var lateralOffset = Math.Abs((deltaX * mouth.InwardY) - (deltaY * mouth.InwardX));
                if (progress < profile.MinimumReach || progress > profile.MaximumReach ||
                    lateralOffset > maximumLateralOffset ||
                    distanceToSea[index] < profile.MinimumReach)
                {
                    continue;
                }

                var (normalizedX, normalizedY) = GetNormalizedPosition(x, y, width, height);
                var targetNoise = (FractalNoise(
                    normalizedX,
                    normalizedY,
                    OffsetSeed(options.Seed, 29_173),
                    1.6,
                    3) + 1) * 0.5;
                var shallowLowland = 1 - GetElevationFactor(definition, preliminaryHeights[index]);
                var reachFit = 1 - Math.Clamp(
                    Math.Abs(progress - desiredReach) / reachRange,
                    0,
                    1);
                var lateralPenalty = lateralOffset / (double)Math.Max(1, maximumLateralOffset);
                var score = (0.38 * reachFit) + (0.37 * shallowLowland) +
                    (0.15 * targetNoise) - (0.25 * lateralPenalty);
                if (score > bestScore || (score == bestScore && index < target))
                {
                    bestScore = score;
                    target = index;
                }
            }
        }

        return target;
    }

    private static bool TryFindTidalInletRoute(
        CampaignWorldDefinition definition,
        int seed,
        IReadOnlyList<bool> isLand,
        IReadOnlyList<bool> forcedLand,
        IReadOnlyList<bool> carved,
        IReadOnlyList<int> distanceToSea,
        IReadOnlyList<double> preliminaryHeights,
        TidalInletMouth mouth,
        int target,
        TidalInletProfile profile,
        out List<int> route)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var sourceX = mouth.Index % width;
        var sourceY = mouth.Index / width;
        var targetX = target % width;
        var targetY = target / width;
        var targetDeltaX = targetX - sourceX;
        var targetDeltaY = targetY - sourceY;
        var targetProgress = Math.Max(
            1,
            (targetDeltaX * mouth.InwardX) + (targetDeltaY * mouth.InwardY));
        var targetLateral = (targetDeltaX * mouth.InwardY) -
            (targetDeltaY * mouth.InwardX);
        var meanderDirection = HashUnit(seed, mouth.Index + 33_271, 557) < 0.5 ? -1.0 : 1.0;
        var meanderAmplitude = Math.Max(
            1.25,
            Math.Min(profile.MaximumReach * 0.18, targetProgress * 0.22));
        var costs = new double[isLand.Count];
        Array.Fill(costs, double.PositiveInfinity);
        var previous = new int[isLand.Count];
        Array.Fill(previous, -1);
        var frontier = new PriorityQueue<int, (double Cost, uint Tie)>();
        costs[mouth.Index] = 0;
        frontier.Enqueue(mouth.Index, (0, Hash(definition.TilesX, sourceX, sourceY)));

        while (frontier.TryDequeue(out var current, out _))
        {
            if (current == target)
            {
                route = ReconstructTidalInletRoute(previous, current);
                return true;
            }

            var currentX = current % width;
            var currentY = current / width;
            foreach (var direction in CardinalDirections)
            {
                var nextX = currentX + direction.X;
                var nextY = currentY + direction.Y;
                if (!IsInside(nextX, nextY, width, height))
                {
                    continue;
                }

                var next = GetIndex(nextX, nextY, width);
                if (!isLand[next] || forcedLand[next] ||
                    IsNearMarkedCell(next, width, height, carved, 1))
                {
                    continue;
                }

                var deltaX = nextX - sourceX;
                var deltaY = nextY - sourceY;
                var progress = (deltaX * mouth.InwardX) + (deltaY * mouth.InwardY);
                var signedLateralOffset = (deltaX * mouth.InwardY) - (deltaY * mouth.InwardX);
                var lateralOffset = Math.Abs(signedLateralOffset);
                var lateralLimit = Math.Max(3, (progress / 2) + 2);
                if (progress < 0 || progress > profile.MaximumReach || lateralOffset > lateralLimit ||
                    (current != mouth.Index && distanceToSea[next] <= 1))
                {
                    continue;
                }

                var normalizedHeight = GetElevationFactor(definition, preliminaryHeights[next]);
                var grade = Math.Abs(preliminaryHeights[next] - preliminaryHeights[current]) /
                    Math.Max(1, definition.CampaignTileSizeMeters);
                var forwardStep = (direction.X * mouth.InwardX) + (direction.Y * mouth.InwardY);
                var routeProgress = Math.Clamp(progress / (double)targetProgress, 0, 1);
                var desiredLateralOffset = (targetLateral * routeProgress) +
                    (meanderDirection * meanderAmplitude * Math.Sin(Math.PI * routeProgress));
                var corridorDeviation = Math.Abs(
                    signedLateralOffset - desiredLateralOffset) /
                    Math.Max(1, meanderAmplitude);
                var tileKilometers = definition.CampaignTileSizeMeters / 1_000.0;
                var valleyVariation = (CampaignTerrainNoise.Fractal(
                    (nextX + 0.5) * tileKilometers,
                    (nextY + 0.5) * tileKilometers,
                    OffsetSeed(seed, 33_319),
                    Math.Max(tileKilometers * 6, 34),
                    3) + 1) * 0.5;
                var tentativeCost = costs[current] + 1 +
                    (3.8 * normalizedHeight) +
                    (2.4 * Math.Clamp(grade / 0.06, 0, 1)) +
                    (forwardStep <= 0 ? 1.0 : 0) +
                    (0.50 * lateralOffset / Math.Max(1, profile.MaximumReach)) +
                    (0.70 * corridorDeviation) +
                    (0.40 * valleyVariation);
                if (tentativeCost >= costs[next])
                {
                    continue;
                }

                costs[next] = tentativeCost;
                previous[next] = current;
                var heuristic = 0.9 * (Math.Abs(targetX - nextX) + Math.Abs(targetY - nextY));
                frontier.Enqueue(next, (tentativeCost + heuristic, Hash(0, nextX, nextY)));
            }
        }

        route = [];
        return false;
    }

    private static List<int> ReconstructTidalInletRoute(IReadOnlyList<int> previous, int target)
    {
        var route = new List<int>();
        for (var current = target; current >= 0; current = previous[current])
        {
            route.Add(current);
        }

        route.Reverse();
        return route;
    }

    private static double GetTidalInletRouteSuitability(
        CampaignWorldDefinition definition,
        IReadOnlyList<double> preliminaryHeights,
        IReadOnlyList<int> route,
        TidalInletMouth mouth)
    {
        if (route.Count < 2)
        {
            return 0;
        }

        var elevationTotal = 0.0;
        var gradeTotal = 0.0;
        var forwardSteps = 0;
        for (var step = 0; step < route.Count; step++)
        {
            var index = route[step];
            elevationTotal += GetElevationFactor(definition, preliminaryHeights[index]);
            if (step == 0)
            {
                continue;
            }

            var previous = route[step - 1];
            gradeTotal += Math.Abs(preliminaryHeights[index] - preliminaryHeights[previous]) /
                Math.Max(1, definition.CampaignTileSizeMeters);
            var deltaX = (index % definition.TilesX) - (previous % definition.TilesX);
            var deltaY = (index / definition.TilesX) - (previous / definition.TilesX);
            if ((deltaX * mouth.InwardX) + (deltaY * mouth.InwardY) > 0)
            {
                forwardSteps++;
            }
        }

        var averageElevation = elevationTotal / route.Count;
        var averageGrade = gradeTotal / (route.Count - 1);
        var forwardFraction = forwardSteps / (double)(route.Count - 1);
        return (0.50 * (1 - averageElevation)) +
            (0.30 * (1 - Math.Clamp(averageGrade / 0.05, 0, 1))) +
            (0.20 * forwardFraction);
    }

    private static void CarveTidalInletRoute(
        IReadOnlyList<int> route,
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        bool[] isLand,
        IReadOnlyList<bool> forcedLand,
        bool[] carved,
        IReadOnlyList<double> preliminaryHeights,
        TidalInletProfile profile)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        foreach (var index in route)
        {
            CarveTidalWaterCell(index, isLand, forcedLand, carved);
        }

        var widenedSteps = Math.Min(profile.MouthWidenSteps, route.Count - 1);
        for (var step = 0; step < widenedSteps; step++)
        {
            var current = route[step];
            var next = route[step + 1];
            var currentX = current % width;
            var currentY = current / width;
            var directionX = Math.Sign((next % width) - currentX);
            var directionY = Math.Sign((next / width) - currentY);
            var lateralX = -directionY;
            var lateralY = directionX;
            if ((Hash(options.Seed, currentX, currentY) & 1u) != 0)
            {
                lateralX = -lateralX;
                lateralY = -lateralY;
            }

            var widenedX = currentX + lateralX;
            var widenedY = currentY + lateralY;
            if (IsInside(widenedX, widenedY, width, height))
            {
                var widenedIndex = GetIndex(widenedX, widenedY, width);
                var wideningElevation = GetElevationFactor(
                    definition,
                    preliminaryHeights[widenedIndex]);
                var wideningGrade = Math.Abs(
                    preliminaryHeights[widenedIndex] - preliminaryHeights[current]) /
                    Math.Max(1, definition.CampaignTileSizeMeters);
                if (wideningElevation > profile.MaximumWideningElevationFactor ||
                    wideningGrade > 0.045)
                {
                    continue;
                }

                CarveTidalWaterCell(
                    widenedIndex,
                    isLand,
                    forcedLand,
                    carved);
            }
        }
    }

    private static void CarveTidalWaterCell(
        int index,
        bool[] isLand,
        IReadOnlyList<bool> forcedLand,
        bool[] carved)
    {
        if (!isLand[index] || forcedLand[index])
        {
            return;
        }

        isLand[index] = false;
        carved[index] = true;
    }

    private static TidalInletProfile GetTidalInletProfile(
        CampaignMapTidalInlets tidalInlets,
        int width,
        int height)
    {
        var shortestAxis = Math.Min(width, height);
        var densityScale = Math.Clamp(shortestAxis / 48, 1, 4);
        var baseReach = Math.Clamp(shortestAxis / 8, 5, 28);
        return tidalInlets switch
        {
            CampaignMapTidalInlets.None => default,
            CampaignMapTidalInlets.Few => new(
                Math.Max(1, (densityScale + 1) / 2),
                Math.Max(3, baseReach / 3),
                Math.Max(5, baseReach / 2),
                0,
                0.66,
                0.34,
                0.70,
                0.24),
            CampaignMapTidalInlets.Balanced => new(
                Math.Min(3, densityScale + 1),
                Math.Max(4, baseReach / 3),
                Math.Max(6, (baseReach * 3) / 4),
                1,
                0.60,
                0.50,
                0.63,
                0.30),
            CampaignMapTidalInlets.Drowned => new(
                Math.Min(5, densityScale + 1),
                Math.Max(5, baseReach / 3),
                Math.Min(shortestAxis / 5, baseReach + 4),
                2,
                0.53,
                0.68,
                0.56,
                0.38),
            _ => throw new ArgumentOutOfRangeException(
                nameof(tidalInlets),
                tidalInlets,
                "Unknown campaign map tidal-inlet setting."),
        };
    }

    private static double[] BuildRawHeights(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        IReadOnlyList<double> landScores,
        IReadOnlyList<bool> isLand,
        CampaignTectonicField tectonicField,
        int width,
        int height)
    {
        var heights = new double[landScores.Count];
        var terrainStrength = options.TerrainStyle switch
        {
            CampaignMapTerrainStyle.Gentle => 0.35,
            CampaignMapTerrainStyle.Balanced => 0.65,
            CampaignMapTerrainStyle.Rugged => 0.95,
            _ => 0.65,
        };
        var availableHeight = Math.Max(0, definition.MaximumHeightMeters - definition.SeaLevelMeters);
        var availableDepth = Math.Max(0, definition.SeaLevelMeters - definition.MinimumHeightMeters);
        var tileKilometers = definition.CampaignTileSizeMeters / 1_000.0;
        var shorterWorldDimension = Math.Min(
            definition.WorldWidthMeters,
            definition.WorldHeightMeters) / 1_000.0;
        var macroWavelength = Math.Max(
            tileKilometers * 12,
            Math.Min(320, shorterWorldDimension * 0.32));
        var detailWavelength = Math.Max(
            tileKilometers * 3,
            Math.Min(55, shorterWorldDimension * 0.055));
        var seabedWavelength = Math.Max(
            tileKilometers * 8,
            Math.Min(220, shorterWorldDimension * 0.24));
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = GetIndex(x, y, width);
                var (normalizedX, normalizedY) = GetNormalizedPosition(x, y, width, height);
                var xKilometers = (x + 0.5) * tileKilometers;
                var yKilometers = (y + 0.5) * tileKilometers;
                if (!isLand[index])
                {
                    var oceanStrength = SmoothStep(
                        0,
                        1,
                        Math.Clamp((-landScores[index] / 0.85) + 0.08, 0, 1));
                    var seabed = (CampaignTerrainNoise.Fractal(
                        xKilometers,
                        yKilometers,
                        OffsetSeed(options.Seed, 19_337),
                        seabedWavelength,
                        4) + 1) * 0.5;
                    var depthFactor = Math.Clamp(0.05 + (0.58 * oceanStrength) + (0.10 * seabed), 0.04, 0.82);
                    heights[index] = definition.SeaLevelMeters - (availableDepth * depthFactor);
                    continue;
                }

                var inland = SmoothStep(0, 0.78, Math.Max(0, landScores[index]));
                var macro = (CampaignTerrainNoise.Fractal(
                    xKilometers,
                    yKilometers,
                    OffsetSeed(options.Seed, 12_289),
                    macroWavelength,
                    4) + 1) * 0.5;
                var ridge = GetRidgeStrength(
                    tectonicField,
                    index);
                var detail = (CampaignTerrainNoise.Fractal(
                    xKilometers,
                    yKilometers,
                    OffsetSeed(options.Seed, 16_027),
                    detailWavelength,
                    3) + 1) * 0.5;
                var orogenyStrength = GetOrogenyStrength(
                    definition,
                    x,
                    y,
                    options.Seed,
                    tectonicField,
                    index,
                    ridge);
                var continentalUplift = Math.Pow(inland, 1.20) *
                    (0.055 + (0.11 * macro) + (terrainStrength * (0.025 * detail)) +
                    (tectonicField.ProvinceElevationBias[index] * 0.035));
                var rangeCore = Math.Pow(SmoothStep(0.20, 0.72, orogenyStrength), 1.20);
                var rangeUplift = inland * rangeCore *
                    (0.12 + (terrainStrength * (0.26 + (0.24 * ridge))) +
                    (0.28 * tectonicField.ConvergentUplift[index]));
                var riftSubsidence = inland * tectonicField.RiftStrength[index] *
                    (0.025 + (terrainStrength * 0.040));
                var coastalRidgeNoise = FractalNoise(
                    normalizedX,
                    normalizedY,
                    OffsetSeed(options.Seed, 17_653),
                    3.1,
                    4);
                var coastalEscarpment = Math.Pow(
                    Math.Clamp((coastalRidgeNoise - 0.10) / 0.90, 0, 1),
                    1.35) * terrainStrength * (1 - inland) * 0.16;
                var elevationFactor = Math.Clamp(
                    0.015 + continentalUplift + rangeUplift +
                    coastalEscarpment - riftSubsidence,
                    0.01,
                    0.96);
                heights[index] = definition.SeaLevelMeters + (availableHeight * elevationFactor);
            }
        }

        return heights;
    }

    internal static void ApplyTerrainErosion(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        IReadOnlyList<bool> isLand,
        IReadOnlyList<bool> isSea,
        double[] heights)
    {
        var profile = GetErosionProfile(options.TerrainStyle);
        ApplyThermalRelaxation(
            definition,
            isLand,
            heights,
            Math.Max(1, profile.ThermalIterations - 1),
            profile.TalusGrade,
            profile.ThermalTransfer);

        var drainage = BuildDrainage(
            definition.TilesX,
            definition.TilesY,
            isSea,
            heights,
            OffsetSeed(options.Seed, 32_003));
        var accumulation = BuildFlowAccumulation(isLand, drainage);
        var maximumAccumulation = accumulation.Length == 0 ? 1 : Math.Max(1, accumulation.Max());
        var accumulationScale = Math.Log(1 + maximumAccumulation);
        var availableHeight = Math.Max(1, definition.MaximumHeightMeters - definition.SeaLevelMeters);
        var delta = new double[heights.Length];
        for (var index = 0; index < heights.Length; index++)
        {
            if (!isLand[index])
            {
                continue;
            }

            var receiver = drainage.Receiver[index];
            if (receiver < 0)
            {
                continue;
            }

            var rawGrade = Math.Max(
                0,
                (heights[index] - heights[receiver]) / definition.CampaignTileSizeMeters);
            var filledGrade = Math.Max(
                0,
                (drainage.FilledHeights[index] - drainage.FilledHeights[receiver]) /
                definition.CampaignTileSizeMeters);
            var grade = Math.Max(rawGrade, filledGrade);
            if (grade <= double.Epsilon)
            {
                continue;
            }

            var drainageArea = Math.Log(1 + accumulation[index]) / accumulationScale;
            var streamPower = Math.Pow(drainageArea, 0.52) *
                Math.Pow(Math.Clamp(grade / 0.12, 0, 1), 0.85);
            var erosionMeters = availableHeight * profile.FluvialStrength * streamPower;
            delta[index] -= erosionMeters;
            if (isLand[receiver])
            {
                var depositionFraction = 0.08 + (0.12 * (1 - drainageArea));
                delta[receiver] += erosionMeters * depositionFraction;
            }
        }

        for (var index = 0; index < heights.Length; index++)
        {
            if (!isLand[index])
            {
                continue;
            }

            heights[index] = Math.Clamp(
                heights[index] + delta[index],
                definition.SeaLevelMeters + 1.0,
                definition.MaximumHeightMeters);
        }

        ApplyThermalRelaxation(
            definition,
            isLand,
            heights,
            iterations: 1,
            profile.TalusGrade,
            profile.ThermalTransfer * 0.75);
    }

    private static void ApplyThermalRelaxation(
        CampaignWorldDefinition definition,
        IReadOnlyList<bool> isLand,
        double[] heights,
        int iterations,
        double talusGrade,
        double transferFraction)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var talusHeight = definition.CampaignTileSizeMeters * talusGrade;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var delta = new double[heights.Length];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = GetIndex(x, y, width);
                    if (!isLand[index])
                    {
                        continue;
                    }

                    if (x + 1 < width)
                    {
                        TransferThermalMaterial(
                            index,
                            GetIndex(x + 1, y, width),
                            isLand,
                            heights,
                            delta,
                            talusHeight,
                            transferFraction);
                    }

                    if (y + 1 < height)
                    {
                        TransferThermalMaterial(
                            index,
                            GetIndex(x, y + 1, width),
                            isLand,
                            heights,
                            delta,
                            talusHeight,
                            transferFraction);
                    }
                }
            }

            for (var index = 0; index < heights.Length; index++)
            {
                if (isLand[index])
                {
                    heights[index] = Math.Clamp(
                        heights[index] + delta[index],
                        definition.SeaLevelMeters + 1.0,
                        definition.MaximumHeightMeters);
                }
            }
        }
    }

    private static void TransferThermalMaterial(
        int left,
        int right,
        IReadOnlyList<bool> isLand,
        IReadOnlyList<double> heights,
        double[] delta,
        double talusHeight,
        double transferFraction)
    {
        if (!isLand[right])
        {
            return;
        }

        var difference = heights[left] - heights[right];
        var excess = Math.Abs(difference) - talusHeight;
        if (excess <= 0)
        {
            return;
        }

        var transfer = excess * transferFraction;
        var higher = difference > 0 ? left : right;
        var lower = difference > 0 ? right : left;
        delta[higher] -= transfer;
        delta[lower] += transfer;
    }

    private static DrainageModel BuildDrainage(
        int width,
        int height,
        IReadOnlyList<bool> isWater,
        IReadOnlyList<double> rawHeights,
        int seed)
    {
        var filled = new double[rawHeights.Count];
        var receiver = new int[rawHeights.Count];
        Array.Fill(receiver, -1);
        var visitOrder = new int[rawHeights.Count];
        Array.Fill(visitOrder, -1);
        var visited = new bool[rawHeights.Count];
        var queue = new PriorityQueue<int, (double Height, uint Tie)>();
        var sequence = 0;

        for (var index = 0; index < isWater.Count; index++)
        {
            if (isWater[index])
            {
                SeedDrainage(index, width, rawHeights, seed, filled, visitOrder, visited, queue, ref sequence);
            }
        }

        if (queue.Count == 0)
        {
            for (var x = 0; x < width; x++)
            {
                SeedDrainage(GetIndex(x, 0, width), width, rawHeights, seed, filled, visitOrder, visited, queue, ref sequence);
                SeedDrainage(GetIndex(x, height - 1, width), width, rawHeights, seed, filled, visitOrder, visited, queue, ref sequence);
            }

            for (var y = 1; y + 1 < height; y++)
            {
                SeedDrainage(GetIndex(0, y, width), width, rawHeights, seed, filled, visitOrder, visited, queue, ref sequence);
                SeedDrainage(GetIndex(width - 1, y, width), width, rawHeights, seed, filled, visitOrder, visited, queue, ref sequence);
            }
        }

        while (queue.TryDequeue(out var index, out _))
        {
            var x = index % width;
            var y = index / width;
            foreach (var direction in CardinalDirections)
            {
                var neighborX = x + direction.X;
                var neighborY = y + direction.Y;
                if (!IsInside(neighborX, neighborY, width, height))
                {
                    continue;
                }

                var neighbor = GetIndex(neighborX, neighborY, width);
                if (visited[neighbor])
                {
                    continue;
                }

                visited[neighbor] = true;
                receiver[neighbor] = index;
                filled[neighbor] = Math.Max(rawHeights[neighbor], filled[index]);
                visitOrder[neighbor] = sequence++;
                queue.Enqueue(neighbor, (filled[neighbor], Hash(seed, neighborX, neighborY)));
            }
        }

        return new DrainageModel(filled, receiver, visitOrder);
    }

    private static void GenerateLakes(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        bool[] isLand,
        IReadOnlyList<bool> isSea,
        IReadOnlyList<bool> forcedLand,
        double[] rawHeights,
        DrainageModel drainage,
        bool[] isLake)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var targetCount = GetTargetLakeCount(Math.Min(width, height), options.Hydrology);
        if (targetCount == 0)
        {
            return;
        }

        var distanceToSea = ComputeDistanceToWater(width, height, isSea);
        var availableHeight = Math.Max(1, definition.MaximumHeightMeters - definition.SeaLevelMeters);
        var minimumDepth = Math.Max(2, availableHeight * GetMinimumLakeDepthFactor(options.Hydrology));
        var maximumArea = GetMaximumLakeArea(isLand.Length, options.Hydrology);
        var candidate = new bool[isLand.Length];
        for (var index = 0; index < candidate.Length; index++)
        {
            candidate[index] = isLand[index] &&
                !forcedLand[index] &&
                distanceToSea[index] >= 3 &&
                drainage.FilledHeights[index] - rawHeights[index] >= minimumDepth;
        }

        var basins = CollectLakeBasins(
            width,
            height,
            candidate,
            distanceToSea,
            rawHeights,
            drainage.FilledHeights,
            maximumArea);
        var selectedCount = 0;
        foreach (var basin in basins)
        {
            if (selectedCount >= targetCount)
            {
                break;
            }

            if (basin.Cells.Any(index => IsNearMarkedCell(index, width, height, isLake, radius: 2)))
            {
                continue;
            }

            MarkLakeBasin(definition, basin, isLand, rawHeights, isLake);
            selectedCount++;
        }

        if (selectedCount >= targetCount)
        {
            return;
        }

        var fallbackBasins = CollectLocalMinimumBasins(
            width,
            height,
            isLand,
            isSea,
            forcedLand,
            distanceToSea,
            rawHeights,
            minimumDepth * 0.20);
        foreach (var basin in fallbackBasins)
        {
            if (selectedCount >= targetCount)
            {
                break;
            }

            if (IsNearMarkedCell(basin.Anchor, width, height, isLake, radius: 3))
            {
                continue;
            }

            MarkLakeBasin(definition, basin, isLand, rawHeights, isLake);
            selectedCount++;
        }
    }

    private static List<LakeBasin> CollectLakeBasins(
        int width,
        int height,
        IReadOnlyList<bool> candidate,
        IReadOnlyList<int> distanceToSea,
        IReadOnlyList<double> rawHeights,
        IReadOnlyList<double> filledHeights,
        int maximumArea)
    {
        var basins = new List<LakeBasin>();
        var visited = new bool[candidate.Count];
        var queue = new Queue<int>();
        for (var start = 0; start < candidate.Count; start++)
        {
            if (!candidate[start] || visited[start])
            {
                continue;
            }

            var cells = new List<int>();
            var maximumDepth = 0.0;
            var minimumDistance = int.MaxValue;
            visited[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                cells.Add(index);
                maximumDepth = Math.Max(maximumDepth, filledHeights[index] - rawHeights[index]);
                minimumDistance = Math.Min(minimumDistance, distanceToSea[index]);
                var x = index % width;
                var y = index / width;
                foreach (var direction in CardinalDirections)
                {
                    var neighborX = x + direction.X;
                    var neighborY = y + direction.Y;
                    if (!IsInside(neighborX, neighborY, width, height))
                    {
                        continue;
                    }

                    var neighbor = GetIndex(neighborX, neighborY, width);
                    if (candidate[neighbor] && !visited[neighbor])
                    {
                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (cells.Count > maximumArea)
            {
                continue;
            }

            var spillHeight = cells.Max(index => filledHeights[index]);
            var score = maximumDepth * Math.Sqrt(cells.Count) * (1 + (Math.Min(minimumDistance, 20) * 0.02));
            basins.Add(new LakeBasin(cells.Min(), cells, score, spillHeight));
        }

        basins.Sort((left, right) =>
        {
            var scoreComparison = right.Score.CompareTo(left.Score);
            return scoreComparison != 0 ? scoreComparison : left.Anchor.CompareTo(right.Anchor);
        });
        return basins;
    }

    private static List<LakeBasin> CollectLocalMinimumBasins(
        int width,
        int height,
        IReadOnlyList<bool> isLand,
        IReadOnlyList<bool> isSea,
        IReadOnlyList<bool> forcedLand,
        IReadOnlyList<int> distanceToSea,
        IReadOnlyList<double> rawHeights,
        double minimumDepth)
    {
        var basins = new List<LakeBasin>();
        for (var y = 1; y + 1 < height; y++)
        {
            for (var x = 1; x + 1 < width; x++)
            {
                var index = GetIndex(x, y, width);
                if (!isLand[index] || isSea[index] || forcedLand[index] || distanceToSea[index] < 4)
                {
                    continue;
                }

                var spillHeight = double.PositiveInfinity;
                var isMinimum = true;
                foreach (var direction in CardinalDirections)
                {
                    var neighbor = GetIndex(x + direction.X, y + direction.Y, width);
                    spillHeight = Math.Min(spillHeight, rawHeights[neighbor]);
                    if (rawHeights[neighbor] < rawHeights[index])
                    {
                        isMinimum = false;
                    }
                }

                var depth = spillHeight - rawHeights[index];
                if (isMinimum && depth >= minimumDepth)
                {
                    basins.Add(new LakeBasin(
                        index,
                        [index],
                        depth * (1 + (Math.Min(distanceToSea[index], 20) * 0.02)),
                        spillHeight));
                }
            }
        }

        basins.Sort((left, right) =>
        {
            var scoreComparison = right.Score.CompareTo(left.Score);
            return scoreComparison != 0 ? scoreComparison : left.Anchor.CompareTo(right.Anchor);
        });
        return basins;
    }

    private static void MarkLakeBasin(
        CampaignWorldDefinition definition,
        LakeBasin basin,
        bool[] isLand,
        double[] rawHeights,
        bool[] isLake)
    {
        var waterLevel = Math.Clamp(
            basin.SpillHeight,
            definition.MinimumHeightMeters,
            definition.MaximumHeightMeters);
        foreach (var index in basin.Cells)
        {
            isLake[index] = true;
            isLand[index] = false;
            rawHeights[index] = waterLevel;
        }
    }

    private static RiverGeneration GenerateRivers(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        IReadOnlyList<bool> isLand,
        IReadOnlyList<bool> isWater,
        double[] rawHeights,
        DrainageModel drainage)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var targetCount = GetTargetRiverCount(Math.Min(width, height), options.Hydrology);
        var rivers = new bool[isLand.Count];
        var largeRivers = new bool[isLand.Count];
        var junctions = new bool[isLand.Count];
        if (targetCount == 0 || !isWater.Any(value => value))
        {
            return new RiverGeneration(rivers, largeRivers, junctions);
        }

        var accumulation = BuildFlowAccumulation(isLand, drainage);
        var order = Enumerable.Range(0, isLand.Count)
            .Where(index => isLand[index])
            .ToArray();

        var landCount = order.Length;
        var baseThreshold = Math.Max(6.0, landCount / (targetCount * 18.0));
        var minimumLength = Math.Max(4, Math.Min(width, height) / 20);
        var candidates = new List<RiverCandidate>();
        var candidateSources = new HashSet<int>();
        foreach (var thresholdScale in new[] { 1.0, 0.68, 0.46, 0.30, 0.20 })
        {
            var threshold = baseThreshold * thresholdScale;
            foreach (var source in order)
            {
                if (accumulation[source] < threshold ||
                    GetMaximumUpstreamAccumulation(source, width, height, isLand, drainage.Receiver, accumulation) >= threshold ||
                    !candidateSources.Add(source))
                {
                    continue;
                }

                if (!TryTraceRiver(source, isLand, isWater, drainage.Receiver, out var path, out var mouth) ||
                    path.Count < minimumLength ||
                    !IsSimpleCardinalPath(path, width, height))
                {
                    continue;
                }

                var relief = Math.Max(10, rawHeights[source] - rawHeights[mouth]);
                var score = accumulation[source] * path.Count * relief;
                candidates.Add(new RiverCandidate(source, path, score));
            }
        }

        candidates.Sort((left, right) =>
        {
            var scoreComparison = right.Score.CompareTo(left.Score);
            return scoreComparison != 0 ? scoreComparison : left.Source.CompareTo(right.Source);
        });
        var remainingCandidates = new List<RiverCandidate>(candidates);
        var accepted = 0;
        while (accepted < targetCount && remainingCandidates.Count > 0)
        {
            var selectedCandidateIndex = -1;
            IReadOnlyList<int> selectedAddedPath = Array.Empty<int>();
            var selectedMergeTile = -1;
            for (var candidateIndex = 0; candidateIndex < remainingCandidates.Count; candidateIndex++)
            {
                var candidate = remainingCandidates[candidateIndex];
                if (!TryPrepareRiverAddition(
                        candidate.Path,
                        width,
                        height,
                        rivers,
                        junctions,
                        minimumLength,
                        out var addedPath,
                        out var mergeTile))
                {
                    continue;
                }

                if (selectedCandidateIndex < 0 || mergeTile >= 0)
                {
                    selectedCandidateIndex = candidateIndex;
                    selectedAddedPath = addedPath;
                    selectedMergeTile = mergeTile;
                }

                if (mergeTile >= 0)
                {
                    break;
                }
            }

            if (selectedCandidateIndex < 0)
            {
                break;
            }

            var selectedCandidate = remainingCandidates[selectedCandidateIndex];
            remainingCandidates.RemoveAt(selectedCandidateIndex);
            var largeRiverStart = FindLargeRiverStart(
                definition,
                selectedCandidate.Path,
                accumulation,
                baseThreshold);
            for (var pathIndex = 0; pathIndex < selectedAddedPath.Count; pathIndex++)
            {
                var index = selectedAddedPath[pathIndex];
                rivers[index] = true;
                largeRivers[index] = largeRiverStart >= 0 && pathIndex >= largeRiverStart;
                rawHeights[index] = drainage.FilledHeights[index];
            }

            var createsJunction = selectedMergeTile >= 0 &&
                CountMarkedCardinalNeighbors(selectedMergeTile, width, height, rivers) == 3;
            if (createsJunction)
            {
                junctions[selectedMergeTile] = true;
                largeRivers[selectedMergeTile] = false;
            }

            if (selectedMergeTile < 0 || createsJunction)
            {
                accepted++;
            }
        }

        return new RiverGeneration(rivers, largeRivers, junctions);
    }

    private static bool TryPrepareRiverAddition(
        IReadOnlyList<int> candidatePath,
        int width,
        int height,
        IReadOnlyList<bool> rivers,
        IReadOnlyList<bool> junctions,
        int minimumLength,
        out IReadOnlyList<int> addedPath,
        out int mergeTile)
    {
        mergeTile = -1;
        var mergePathIndex = -1;
        for (var pathIndex = 0; pathIndex < candidatePath.Count; pathIndex++)
        {
            if (rivers[candidatePath[pathIndex]])
            {
                mergePathIndex = pathIndex;
                mergeTile = candidatePath[pathIndex];
                break;
            }
        }

        if (mergePathIndex >= 0)
        {
            var minimumTributaryLength = Math.Max(3, minimumLength / 2);
            if (mergePathIndex < minimumTributaryLength)
            {
                addedPath = Array.Empty<int>();
                return false;
            }

            addedPath = candidatePath.Take(mergePathIndex).ToArray();
        }
        else
        {
            if (candidatePath.Any(index => IsNearMarkedCell(index, width, height, rivers, radius: 1)))
            {
                addedPath = Array.Empty<int>();
                return false;
            }

            addedPath = candidatePath.ToArray();
        }

        var addedCells = addedPath.ToHashSet();
        foreach (var index in addedPath)
        {
            var x = index % width;
            var y = index / width;
            var routeNeighborCount = 0;
            foreach (var direction in CardinalDirections)
            {
                var neighborX = x + direction.X;
                var neighborY = y + direction.Y;
                if (!IsInside(neighborX, neighborY, width, height))
                {
                    continue;
                }

                var neighbor = GetIndex(neighborX, neighborY, width);
                if (addedCells.Contains(neighbor))
                {
                    routeNeighborCount++;
                }
                else if (rivers[neighbor])
                {
                    if (neighbor != mergeTile)
                    {
                        return false;
                    }

                    routeNeighborCount++;
                }
            }

            if (routeNeighborCount > 2)
            {
                return false;
            }
        }

        if (mergeTile < 0)
        {
            return true;
        }

        var mergeNeighbors = CountMarkedCardinalNeighbors(mergeTile, width, height, rivers);
        var mergeX = mergeTile % width;
        var mergeY = mergeTile / width;
        mergeNeighbors += CardinalDirections.Count(direction =>
        {
            var neighborX = mergeX + direction.X;
            var neighborY = mergeY + direction.Y;
            return IsInside(neighborX, neighborY, width, height) &&
                addedCells.Contains(GetIndex(neighborX, neighborY, width));
        });
        return mergeNeighbors <= 3 && (!junctions[mergeTile] || mergeNeighbors <= 3);
    }

    private static int CountMarkedCardinalNeighbors(
        int index,
        int width,
        int height,
        IReadOnlyList<bool> marked)
    {
        var x = index % width;
        var y = index / width;
        var count = 0;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.X;
            var neighborY = y + direction.Y;
            if (IsInside(neighborX, neighborY, width, height) &&
                marked[GetIndex(neighborX, neighborY, width)])
            {
                count++;
            }
        }

        return count;
    }

    private static double[] BuildFlowAccumulation(
        IReadOnlyList<bool> isLand,
        DrainageModel drainage)
    {
        var accumulation = new double[isLand.Count];
        var order = Enumerable.Range(0, isLand.Count)
            .Where(index => isLand[index])
            .ToArray();
        foreach (var index in order)
        {
            accumulation[index] = 1;
        }

        Array.Sort(order, (left, right) =>
        {
            var heightComparison = drainage.FilledHeights[right].CompareTo(drainage.FilledHeights[left]);
            return heightComparison != 0
                ? heightComparison
                : drainage.VisitOrder[right].CompareTo(drainage.VisitOrder[left]);
        });
        foreach (var index in order)
        {
            var receiver = drainage.Receiver[index];
            if (receiver >= 0 && isLand[receiver])
            {
                accumulation[receiver] += accumulation[index];
            }
        }

        return accumulation;
    }

    private static int FindLargeRiverStart(
        CampaignWorldDefinition definition,
        IReadOnlyList<int> path,
        IReadOnlyList<double> accumulation,
        double baseThreshold)
    {
        var minimumRouteTiles = Math.Max(
            2,
            (int)Math.Ceiling(100_000.0 / definition.CampaignTileSizeMeters));
        if (path.Count < minimumRouteTiles)
        {
            return -1;
        }

        var minimumLargeReachTiles = Math.Max(
            2,
            (int)Math.Ceiling(30_000.0 / definition.CampaignTileSizeMeters));
        var maximumLargeReachTiles = Math.Max(
            minimumLargeReachTiles,
            (int)Math.Ceiling(80_000.0 / definition.CampaignTileSizeMeters));
        var sixtyPercentDownstream = (int)Math.Ceiling(path.Count * 0.60);
        var preferredStart = Math.Max(sixtyPercentDownstream, path.Count - maximumLargeReachTiles);
        var latestStart = path.Count - minimumLargeReachTiles;
        var searchStart = Math.Min(preferredStart, latestStart);
        var minimumAccumulation = baseThreshold * 1.10;

        for (var pathIndex = searchStart; pathIndex <= latestStart; pathIndex++)
        {
            if (accumulation[path[pathIndex]] >= minimumAccumulation)
            {
                return pathIndex;
            }
        }

        return -1;
    }

    private static bool TryTraceRiver(
        int source,
        IReadOnlyList<bool> isLand,
        IReadOnlyList<bool> isWater,
        IReadOnlyList<int> receiver,
        out List<int> path,
        out int mouth)
    {
        path = [];
        mouth = -1;
        var visited = new HashSet<int>();
        var current = source;
        while (current >= 0 && isLand[current])
        {
            if (!visited.Add(current))
            {
                return false;
            }

            path.Add(current);
            current = receiver[current];
        }

        if (current < 0 || !isWater[current])
        {
            return false;
        }

        mouth = current;
        return true;
    }

    private static bool IsSimpleCardinalPath(IReadOnlyList<int> path, int width, int height)
    {
        var cells = path.ToHashSet();
        for (var pathIndex = 0; pathIndex < path.Count; pathIndex++)
        {
            var index = path[pathIndex];
            var x = index % width;
            var y = index / width;
            var neighborCount = 0;
            foreach (var direction in CardinalDirections)
            {
                var neighborX = x + direction.X;
                var neighborY = y + direction.Y;
                if (IsInside(neighborX, neighborY, width, height) &&
                    cells.Contains(GetIndex(neighborX, neighborY, width)))
                {
                    neighborCount++;
                }
            }

            var expected = path.Count == 1 || pathIndex is 0 || pathIndex == path.Count - 1 ? 1 : 2;
            if (path.Count == 1)
            {
                expected = 0;
            }

            if (neighborCount != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static double GetMaximumUpstreamAccumulation(
        int index,
        int width,
        int height,
        IReadOnlyList<bool> isLand,
        IReadOnlyList<int> receiver,
        IReadOnlyList<double> accumulation)
    {
        var maximum = 0.0;
        var x = index % width;
        var y = index / width;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.X;
            var neighborY = y + direction.Y;
            if (!IsInside(neighborX, neighborY, width, height))
            {
                continue;
            }

            var neighbor = GetIndex(neighborX, neighborY, width);
            if (isLand[neighbor] && receiver[neighbor] == index)
            {
                maximum = Math.Max(maximum, accumulation[neighbor]);
            }
        }

        return maximum;
    }

    private static bool[] SelectMountainTiles(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        IReadOnlyList<double> heights,
        IReadOnlyList<bool> isWater,
        IReadOnlyList<bool> isRiver,
        CampaignTectonicField tectonicField)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var profile = GetMountainProfile(options.MountainDensity);
        var suitabilityProfile = GetMountainProfile(CampaignMapMountainDensity.Balanced);
        var candidateScores = new double[heights.Count];
        Array.Fill(candidateScores, double.NegativeInfinity);
        var candidates = new List<MountainCandidate>();
        var inlandTileCount = 0;
        var availableHeight = Math.Max(1, definition.MaximumHeightMeters - definition.SeaLevelMeters);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = GetIndex(x, y, width);
                if (isWater[index] || isRiver[index])
                {
                    continue;
                }

                var maximumGrade = 0.0;
                var touchesWater = false;
                foreach (var direction in CardinalDirections)
                {
                    var neighborX = x + direction.X;
                    var neighborY = y + direction.Y;
                    if (!IsInside(neighborX, neighborY, width, height))
                    {
                        continue;
                    }

                    var neighbor = GetIndex(neighborX, neighborY, width);
                    maximumGrade = Math.Max(
                        maximumGrade,
                        Math.Abs(heights[index] - heights[neighbor]) / definition.CampaignTileSizeMeters);
                    touchesWater |= isWater[neighbor];
                }

                if (touchesWater)
                {
                    continue;
                }

                inlandTileCount++;
                var elevationFactor = Math.Clamp(
                    (heights[index] - definition.SeaLevelMeters) / availableHeight,
                    0,
                    1);
                var ridgeStrength = GetRidgeStrength(
                    tectonicField,
                    index);
                var orogenyStrength = GetOrogenyStrength(
                    definition,
                    x,
                    y,
                    options.Seed,
                    tectonicField,
                    index,
                    ridgeStrength);
                var meetsElevation = elevationFactor >= suitabilityProfile.MinimumElevationFactor;
                var meetsGrade = maximumGrade >= suitabilityProfile.MinimumGrade;
                if ((!meetsElevation && !meetsGrade) ||
                    (!meetsGrade && orogenyStrength < suitabilityProfile.MinimumOrogenyStrength))
                {
                    continue;
                }

                var gradeStrength = Math.Clamp(maximumGrade / MountainMinimumGrade, 0, 1);
                var crestStrength = GetLocalCrestStrength(
                    definition,
                    x,
                    y,
                    heights,
                    isWater);
                var score =
                    (0.38 * orogenyStrength) +
                    (0.25 * ridgeStrength) +
                    (0.17 * elevationFactor) +
                    (0.14 * crestStrength) +
                    (0.06 * gradeStrength);
                candidateScores[index] = score;
                candidates.Add(new MountainCandidate(index, score));
            }
        }

        var selected = new bool[heights.Count];
        if (candidates.Count == 0 || inlandTileCount == 0)
        {
            return selected;
        }

        candidates.Sort(static (left, right) =>
        {
            var score = right.Score.CompareTo(left.Score);
            return score != 0 ? score : left.Index.CompareTo(right.Index);
        });
        var targetCount = Math.Min(
            candidates.Count,
            GetTargetMountainTileCount(inlandTileCount, options, profile));
        if (targetCount == 0)
        {
            return selected;
        }

        var seedCount = Math.Min(profile.TargetSystemCount, targetCount);
        var maximumSystemCount = GetMountainProfile(CampaignMapMountainDensity.Dense).TargetSystemCount;
        var minimumSeedDistance = Math.Max(4, Math.Min(width, height) / (maximumSystemCount + 1));
        var minimumSeedDistanceSquared = minimumSeedDistance * minimumSeedDistance;
        var seeds = new List<int>(seedCount);
        foreach (var candidate in candidates)
        {
            var candidateX = candidate.Index % width;
            var candidateY = candidate.Index / width;
            var tooNearExistingSeed = seeds.Any(seed =>
            {
                var seedX = seed % width;
                var seedY = seed / width;
                var deltaX = candidateX - seedX;
                var deltaY = candidateY - seedY;
                return (deltaX * deltaX) + (deltaY * deltaY) < minimumSeedDistanceSquared;
            });
            if (tooNearExistingSeed)
            {
                continue;
            }

            seeds.Add(candidate.Index);
            if (seeds.Count == seedCount)
            {
                break;
            }
        }

        if (seeds.Count == 0)
        {
            seeds.Add(candidates[0].Index);
        }

        var selectedCount = 0;
        for (var seedIndex = 0; seedIndex < seeds.Count && selectedCount < targetCount; seedIndex++)
        {
            var seed = seeds[seedIndex];
            var dynamicSeedSeparation = Math.Max(2, minimumSeedDistance / 4);
            if (selected[seed] ||
                (selectedCount > 0 && IsNearMarkedCell(
                    seed,
                    width,
                    height,
                    selected,
                    dynamicSeedSeparation)))
            {
                seed = candidates
                    .Select(static candidate => candidate.Index)
                    .FirstOrDefault(
                        candidateIndex =>
                            !selected[candidateIndex] &&
                            !IsNearMarkedCell(
                                candidateIndex,
                                width,
                                height,
                                selected,
                                dynamicSeedSeparation),
                        -1);
                if (seed < 0)
                {
                    break;
                }
            }

            var systemTargetCount = GetCumulativeMountainSystemTarget(
                inlandTileCount,
                options,
                profile,
                candidates.Count,
                targetCount,
                seedIndex,
                seeds.Count);
            var frontier = new PriorityQueue<int, double>();
            selected[seed] = true;
            selectedCount++;
            EnqueueMountainNeighbors(seed, width, height, candidateScores, selected, frontier);

            while (selectedCount < systemTargetCount &&
                frontier.TryDequeue(out var next, out var queuedPriority))
            {
                if (selected[next] || double.IsNegativeInfinity(candidateScores[next]))
                {
                    continue;
                }

                if (!CanExtendMountainRidge(next, width, height, selected))
                {
                    continue;
                }

                var currentPriority = GetMountainGrowthPriority(
                    next,
                    width,
                    height,
                    candidateScores,
                    selected);
                if (queuedPriority < currentPriority - 1e-9)
                {
                    frontier.Enqueue(next, currentPriority);
                    continue;
                }

                selected[next] = true;
                selectedCount++;
                EnqueueMountainNeighbors(next, width, height, candidateScores, selected, frontier);
            }
        }

        return selected;
    }

    private static int GetCumulativeMountainSystemTarget(
        int inlandTileCount,
        CampaignMapGenerationOptions options,
        MountainGenerationProfile profile,
        int candidateCount,
        int targetCount,
        int systemIndex,
        int systemCount)
    {
        if (options.LandMix is null &&
            options.MountainDensity == CampaignMapMountainDensity.Dense &&
            systemIndex < GetMountainProfile(CampaignMapMountainDensity.Balanced).TargetSystemCount)
        {
            var balancedProfile = GetMountainProfile(CampaignMapMountainDensity.Balanced);
            var balancedTarget = Math.Min(
                candidateCount,
                GetTargetMountainTileCount(
                    inlandTileCount,
                    options with { MountainDensity = CampaignMapMountainDensity.Balanced },
                    balancedProfile));
            return Math.Min(
                targetCount,
                (int)Math.Ceiling(
                    balancedTarget * (systemIndex + 1) /
                    (double)balancedProfile.TargetSystemCount));
        }

        return Math.Min(
            targetCount,
            (int)Math.Ceiling(targetCount * (systemIndex + 1) / (double)systemCount));
    }

    private static bool CanExtendMountainRidge(
        int index,
        int width,
        int height,
        IReadOnlyList<bool> selected)
    {
        var x = index % width;
        var y = index / width;
        var existingNeighbor = -1;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.X;
            var neighborY = y + direction.Y;
            if (!IsInside(neighborX, neighborY, width, height))
            {
                continue;
            }

            var neighbor = GetIndex(neighborX, neighborY, width);
            if (!selected[neighbor])
            {
                continue;
            }

            if (existingNeighbor >= 0)
            {
                return false;
            }

            existingNeighbor = neighbor;
        }

        return existingNeighbor >= 0 &&
            CountMarkedCardinalNeighbors(existingNeighbor, width, height, selected) < 2;
    }

    private static TerrainMixAssignments BuildCustomLandTypes(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        IReadOnlyList<double> heights,
        IReadOnlyList<bool> isWater,
        IReadOnlyList<bool> isRiver,
        IReadOnlyList<bool> isMountain,
        IReadOnlyList<int> distanceToWater,
        CampaignTectonicField tectonicField,
        CampaignMapLandMix landMix,
        IReadOnlyList<CampaignCustomTerrainDefinition> customTerrainDefinitions)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var types = new CampaignTileType[heights.Count];
        Array.Fill(types, CampaignTileType.Plains);
        var customTerrainIds = new string?[heights.Count];
        var assigned = new bool[heights.Count];
        var inlandTiles = new List<int>();
        var hillScores = new double[heights.Count];
        var aridityScores = new double[heights.Count];
        var moistureScores = new double[heights.Count];
        var desertEligible = new bool[heights.Count];
        var steppeEligible = new bool[heights.Count];
        var foothillEligible = new bool[heights.Count];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = GetIndex(x, y, width);
                if (isWater[index] || isRiver[index] ||
                    GetMaximumCardinalWaterGrade(definition, x, y, heights, isWater) >= CliffMinimumGrade)
                {
                    continue;
                }

                inlandTiles.Add(index);
                if (isMountain[index])
                {
                    types[index] = CampaignTileType.Mountain;
                    assigned[index] = true;
                }

                var maximumGrade = GetMaximumCardinalGrade(
                    definition,
                    x,
                    y,
                    heights);
                var elevationFactor = GetElevationFactor(definition, heights[index]);
                var (normalizedX, normalizedY) = GetNormalizedPosition(x, y, width, height);
                var ridgeStrength = GetRidgeStrength(
                    tectonicField,
                    index);
                var gradeStrength = Math.Clamp(maximumGrade / MountainMinimumGrade, 0, 1);
                var mountainProximity = GetMountainProximity(
                    index,
                    width,
                    height,
                    isMountain);
                foothillEligible[index] = mountainProximity > 0;
                hillScores[index] =
                    (0.55 * mountainProximity) +
                    (0.20 * gradeStrength) +
                    (0.15 * elevationFactor) +
                    (0.10 * ridgeStrength);
                aridityScores[index] = GetAridity(
                    normalizedX,
                    normalizedY,
                    options.Seed,
                    distanceToWater[index],
                    elevationFactor);
                moistureScores[index] = GetMoisture(
                    normalizedX,
                    normalizedY,
                    options.Seed,
                    distanceToWater[index],
                    elevationFactor);
                desertEligible[index] =
                    distanceToWater[index] >= 4 &&
                    maximumGrade < HillsMinimumGrade &&
                    elevationFactor < 0.24;
                steppeEligible[index] =
                    distanceToWater[index] >= 2 &&
                    maximumGrade < HillsMinimumGrade &&
                    elevationFactor < 0.34;
            }
        }

        if (inlandTiles.Count == 0)
        {
            return new TerrainMixAssignments(types, customTerrainIds);
        }

        var targets = GetTerrainMixTargetCounts(
            inlandTiles.Count,
            landMix,
            customTerrainDefinitions);
        AssignHighestScoringTiles(
            inlandTiles,
            assigned,
            types,
            targets.Hills,
            CampaignTileType.Hills,
            hillScores,
            foothillEligible);
        var reservedFoothillCount = types.Count(static type => type == CampaignTileType.Hills);
        AssignCustomTerrainTiles(
            definition,
            options,
            inlandTiles,
            assigned,
            types,
            customTerrainIds,
            customTerrainDefinitions,
            targets.CustomTerrainCounts);
        AssignHighestScoringTiles(
            inlandTiles,
            assigned,
            types,
            targets.Desert,
            CampaignTileType.Desert,
            aridityScores,
            desertEligible);
        AssignHighestScoringTiles(
            inlandTiles,
            assigned,
            types,
            targets.Steppe,
            CampaignTileType.Steppe,
            aridityScores,
            steppeEligible);
        AssignHighestScoringTiles(
            inlandTiles,
            assigned,
            types,
            Math.Max(0, targets.Hills - reservedFoothillCount),
            CampaignTileType.Hills,
            hillScores);
        AssignHighestScoringTiles(
            inlandTiles,
            assigned,
            types,
            targets.Forest,
            CampaignTileType.Forest,
            moistureScores);

        return new TerrainMixAssignments(types, customTerrainIds);
    }

    private static void AssignCustomTerrainTiles(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        IReadOnlyList<int> inlandTiles,
        bool[] assigned,
        CampaignTileType[] types,
        string?[] customTerrainIds,
        IReadOnlyList<CampaignCustomTerrainDefinition> customTerrainDefinitions,
        IReadOnlyDictionary<string, int> targetCounts)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        foreach (var definitionItem in customTerrainDefinitions
                     .Where(static item => item.GenerationSharePercent > 0)
                     .OrderBy(static item => item.Id, StringComparer.Ordinal))
        {
            if (!targetCounts.TryGetValue(definitionItem.Id, out var targetCount) || targetCount <= 0)
            {
                continue;
            }

            var terrainSeed = GetStableCustomTerrainSeed(definitionItem.Id);
            var candidates = new List<CustomTerrainCandidate>();
            foreach (var index in inlandTiles)
            {
                if (assigned[index])
                {
                    continue;
                }

                var x = index % width;
                var y = index / width;
                var (normalizedX, normalizedY) = GetNormalizedPosition(x, y, width, height);
                candidates.Add(new CustomTerrainCandidate(
                    index,
                    GetCustomTerrainScore(
                        normalizedX,
                        normalizedY,
                        options.Seed,
                        terrainSeed)));
            }

            candidates.Sort(static (left, right) =>
            {
                var score = right.Score.CompareTo(left.Score);
                return score != 0 ? score : left.Index.CompareTo(right.Index);
            });
            var selectedCount = Math.Min(targetCount, candidates.Count);
            for (var candidateIndex = 0; candidateIndex < selectedCount; candidateIndex++)
            {
                var index = candidates[candidateIndex].Index;
                assigned[index] = true;
                types[index] = definitionItem.BaseType;
                customTerrainIds[index] = definitionItem.Id;
            }
        }
    }

    private static double GetCustomTerrainScore(
        double normalizedX,
        double normalizedY,
        int worldSeed,
        int terrainSeed)
    {
        var macro = (FractalNoise(
            normalizedX,
            normalizedY,
            OffsetSeed(worldSeed, terrainSeed),
            1.05,
            3) + 1) * 0.5;
        var detail = (FractalNoise(
            normalizedX,
            normalizedY,
            OffsetSeed(worldSeed, unchecked((terrainSeed * 31) + 7_127)),
            2.8,
            2) + 1) * 0.5;
        return (0.78 * macro) + (0.22 * detail);
    }

    private static int GetStableCustomTerrainSeed(string id)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in id)
            {
                hash = (hash * 31) + character;
            }

            return hash;
        }
    }

    private static void AssignHighestScoringTiles(
        IReadOnlyList<int> inlandTiles,
        bool[] assigned,
        CampaignTileType[] types,
        int targetCount,
        CampaignTileType type,
        IReadOnlyList<double> scores,
        IReadOnlyList<bool>? eligibility = null)
    {
        if (targetCount <= 0)
        {
            return;
        }

        var candidates = new List<LandTileCandidate>();
        foreach (var index in inlandTiles)
        {
            if (!assigned[index] && (eligibility is null || eligibility[index]))
            {
                candidates.Add(new LandTileCandidate(index, scores[index]));
            }
        }

        candidates.Sort(static (left, right) =>
        {
            var score = right.Score.CompareTo(left.Score);
            return score != 0 ? score : left.Index.CompareTo(right.Index);
        });
        var selectedCount = Math.Min(targetCount, candidates.Count);
        for (var candidateIndex = 0; candidateIndex < selectedCount; candidateIndex++)
        {
            var index = candidates[candidateIndex].Index;
            assigned[index] = true;
            types[index] = type;
        }
    }

    private static TerrainMixTargetCounts GetTerrainMixTargetCounts(
        int inlandTileCount,
        CampaignMapLandMix landMix,
        IReadOnlyList<CampaignCustomTerrainDefinition>? customTerrainDefinitions)
    {
        var generatedCustomTerrain = customTerrainDefinitions
            ?.Where(static definition => definition.GenerationSharePercent > 0)
            .OrderBy(static definition => definition.Id, StringComparer.Ordinal)
            .ToArray()
            ?? [];
        var percentages = new int[6 + generatedCustomTerrain.Length];
        percentages[0] = landMix.PlainsPercent;
        percentages[1] = landMix.ForestPercent;
        percentages[2] = landMix.DesertPercent;
        percentages[3] = landMix.HillsPercent;
        percentages[4] = landMix.MountainPercent;
        percentages[5] = landMix.SteppePercent;
        for (var customIndex = 0; customIndex < generatedCustomTerrain.Length; customIndex++)
        {
            percentages[6 + customIndex] = generatedCustomTerrain[customIndex].GenerationSharePercent;
        }

        if (percentages.Sum() != CampaignMapLandMix.RequiredTotalPercent)
        {
            throw new InvalidOperationException(
                "The inland terrain mix must be validated before target counts are calculated.");
        }

        var counts = new int[percentages.Length];
        var remainders = new int[percentages.Length];
        var assignedCount = 0;
        for (var index = 0; index < percentages.Length; index++)
        {
            var scaledCount = inlandTileCount * percentages[index];
            counts[index] = scaledCount / CampaignMapLandMix.RequiredTotalPercent;
            remainders[index] = scaledCount % CampaignMapLandMix.RequiredTotalPercent;
            assignedCount += counts[index];
        }

        var allocationOrder = Enumerable.Range(0, percentages.Length).ToArray();
        Array.Sort(allocationOrder, (left, right) =>
        {
            var remainder = remainders[right].CompareTo(remainders[left]);
            return remainder != 0 ? remainder : left.CompareTo(right);
        });
        for (var remainderIndex = 0; remainderIndex < inlandTileCount - assignedCount; remainderIndex++)
        {
            counts[allocationOrder[remainderIndex]]++;
        }

        var customCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var customIndex = 0; customIndex < generatedCustomTerrain.Length; customIndex++)
        {
            customCounts.Add(generatedCustomTerrain[customIndex].Id, counts[6 + customIndex]);
        }

        return new TerrainMixTargetCounts(
            counts[0],
            counts[1],
            counts[2],
            counts[3],
            counts[4],
            counts[5],
            customCounts);
    }

    private static double GetMaximumCardinalGrade(
        CampaignWorldDefinition definition,
        int x,
        int y,
        IReadOnlyList<double> heights)
    {
        var maximumGrade = 0.0;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.X;
            var neighborY = y + direction.Y;
            if (!IsInside(neighborX, neighborY, definition.TilesX, definition.TilesY))
            {
                continue;
            }

            var index = GetIndex(x, y, definition.TilesX);
            var neighbor = GetIndex(neighborX, neighborY, definition.TilesX);
            maximumGrade = Math.Max(
                maximumGrade,
                Math.Abs(heights[index] - heights[neighbor]) / definition.CampaignTileSizeMeters);
        }

        return maximumGrade;
    }

    private static double GetMaximumCardinalWaterGrade(
        CampaignWorldDefinition definition,
        int x,
        int y,
        IReadOnlyList<double> heights,
        IReadOnlyList<bool> isWater)
    {
        var index = GetIndex(x, y, definition.TilesX);
        var maximumGrade = 0.0;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.X;
            var neighborY = y + direction.Y;
            if (!IsInside(neighborX, neighborY, definition.TilesX, definition.TilesY))
            {
                continue;
            }

            var neighbor = GetIndex(neighborX, neighborY, definition.TilesX);
            if (isWater[neighbor])
            {
                maximumGrade = Math.Max(
                    maximumGrade,
                    Math.Abs(heights[index] - heights[neighbor]) / definition.CampaignTileSizeMeters);
            }
        }

        return maximumGrade;
    }

    private static void EnqueueMountainNeighbors(
        int index,
        int width,
        int height,
        IReadOnlyList<double> candidateScores,
        IReadOnlyList<bool> selected,
        PriorityQueue<int, double> frontier)
    {
        var x = index % width;
        var y = index / width;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.X;
            var neighborY = y + direction.Y;
            if (!IsInside(neighborX, neighborY, width, height))
            {
                continue;
            }

            var neighbor = GetIndex(neighborX, neighborY, width);
            if (!selected[neighbor] && !double.IsNegativeInfinity(candidateScores[neighbor]))
            {
                frontier.Enqueue(
                    neighbor,
                    GetMountainGrowthPriority(
                        neighbor,
                        width,
                        height,
                        candidateScores,
                        selected));
            }
        }
    }

    private static double GetMountainGrowthPriority(
        int index,
        int width,
        int height,
        IReadOnlyList<double> candidateScores,
        IReadOnlyList<bool> selected)
    {
        var selectedNeighborCount = CountMarkedCardinalNeighbors(index, width, height, selected);
        var thickeningPenalty = 0.24 * Math.Max(0, selectedNeighborCount - 1);
        return -candidateScores[index] + thickeningPenalty;
    }

    private static double GetLocalCrestStrength(
        CampaignWorldDefinition definition,
        int x,
        int y,
        IReadOnlyList<double> heights,
        IReadOnlyList<bool> isWater)
    {
        var index = GetIndex(x, y, definition.TilesX);
        var neighborHeightTotal = 0.0;
        var neighborCount = 0;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.X;
            var neighborY = y + direction.Y;
            if (!IsInside(neighborX, neighborY, definition.TilesX, definition.TilesY))
            {
                continue;
            }

            var neighbor = GetIndex(neighborX, neighborY, definition.TilesX);
            if (isWater[neighbor])
            {
                continue;
            }

            neighborHeightTotal += heights[neighbor];
            neighborCount++;
        }

        if (neighborCount == 0)
        {
            return 0;
        }

        var localProminence = heights[index] - (neighborHeightTotal / neighborCount);
        return Math.Clamp(
            localProminence / Math.Max(1, definition.CampaignTileSizeMeters * 0.04),
            0,
            1);
    }

    private static double GetMountainProximity(
        int index,
        int width,
        int height,
        IReadOnlyList<bool> mountains)
    {
        if (mountains[index])
        {
            return 1;
        }

        var centerX = index % width;
        var centerY = index / width;
        for (var radius = 1; radius <= 2; radius++)
        {
            for (var offsetY = -radius; offsetY <= radius; offsetY++)
            {
                var offsetX = radius - Math.Abs(offsetY);
                var x = centerX + offsetX;
                var y = centerY + offsetY;
                if (IsInside(x, y, width, height) && mountains[GetIndex(x, y, width)])
                {
                    return radius == 1 ? 1 : 0.55;
                }

                if (offsetX == 0)
                {
                    continue;
                }

                x = centerX - offsetX;
                if (IsInside(x, y, width, height) && mountains[GetIndex(x, y, width)])
                {
                    return radius == 1 ? 1 : 0.55;
                }
            }
        }

        return 0;
    }

    private static CampaignTileType ClassifyLand(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions options,
        int x,
        int y,
        IReadOnlyList<double> heights,
        IReadOnlyList<bool> isWater,
        IReadOnlyList<bool> isMountain,
        IReadOnlyList<CampaignTileType>? customLandTypes,
        IReadOnlyList<int> distanceToWater,
        CampaignTectonicField tectonicField)
    {
        var width = definition.TilesX;
        var height = definition.TilesY;
        var index = GetIndex(x, y, width);
        var maximumLandGrade = 0.0;
        var maximumWaterGrade = 0.0;
        foreach (var direction in CardinalDirections)
        {
            var neighborX = x + direction.X;
            var neighborY = y + direction.Y;
            if (!IsInside(neighborX, neighborY, width, height))
            {
                continue;
            }

            var neighbor = GetIndex(neighborX, neighborY, width);
            var grade = Math.Abs(heights[index] - heights[neighbor]) / definition.CampaignTileSizeMeters;
            if (isWater[neighbor])
            {
                maximumWaterGrade = Math.Max(maximumWaterGrade, grade);
            }
            else
            {
                maximumLandGrade = Math.Max(maximumLandGrade, grade);
            }
        }

        if (maximumWaterGrade >= CliffMinimumGrade)
        {
            return CampaignTileType.Cliff;
        }

        if (customLandTypes is not null)
        {
            return customLandTypes[index];
        }

        if (isMountain[index])
        {
            return CampaignTileType.Mountain;
        }

        var elevationFactor = GetElevationFactor(definition, heights[index]);
        var (normalizedX, normalizedY) = GetNormalizedPosition(x, y, width, height);
        var mountainProximity = GetMountainProximity(index, width, height, isMountain);
        var ridgeStrength = GetRidgeStrength(
            tectonicField,
            index);
        var elevatedRollingTerrain = elevationFactor >= 0.24 &&
            (maximumLandGrade >= HillsMinimumGrade * 0.5 || ridgeStrength >= 0.52);

        if (mountainProximity >= 1 ||
            (mountainProximity > 0 &&
             (maximumLandGrade >= HillsMinimumGrade * 0.5 || elevationFactor >= 0.16)) ||
            maximumLandGrade >= HillsMinimumGrade ||
            elevatedRollingTerrain)
        {
            return CampaignTileType.Hills;
        }

        var aridity = GetAridity(
            normalizedX,
            normalizedY,
            options.Seed,
            distanceToWater[index],
            elevationFactor);
        if (distanceToWater[index] >= 4 && aridity >= 0.68)
        {
            return CampaignTileType.Desert;
        }

        var moisture = GetMoisture(
            normalizedX,
            normalizedY,
            options.Seed,
            distanceToWater[index],
            elevationFactor);
        if (distanceToWater[index] >= 2 && aridity >= 0.52 && moisture < 0.53)
        {
            return CampaignTileType.Steppe;
        }

        return moisture >= 0.53 ? CampaignTileType.Forest : CampaignTileType.Plains;
    }

    private static double GetElevationFactor(
        CampaignWorldDefinition definition,
        double height)
    {
        var availableHeight = Math.Max(1, definition.MaximumHeightMeters - definition.SeaLevelMeters);
        return Math.Clamp(
            (height - definition.SeaLevelMeters) / availableHeight,
            0,
            1);
    }

    private static double GetAridity(
        double normalizedX,
        double normalizedY,
        int seed,
        int distanceToWater,
        double elevationFactor)
    {
        var aridityNoise = (FractalNoise(
            normalizedX,
            normalizedY,
            OffsetSeed(seed, 18_173),
            1.35,
            3) + 1) * 0.5;
        var inlandAridity = distanceToWater == int.MaxValue
            ? 1
            : 1 - Math.Exp(-distanceToWater / 12.0);
        return (0.54 * aridityNoise) +
            (0.34 * inlandAridity) +
            (0.12 * (1 - elevationFactor));
    }

    private static double GetMoisture(
        double normalizedX,
        double normalizedY,
        int seed,
        int distanceToWater,
        double elevationFactor)
    {
        var moistureNoise = (FractalNoise(
            normalizedX,
            normalizedY,
            OffsetSeed(seed, 15_407),
            4.1,
            4) + 1) * 0.5;
        var waterInfluence = distanceToWater == int.MaxValue
            ? 0
            : Math.Exp(-distanceToWater / 10.0);
        var latitudeModeration = 1 - Math.Abs(normalizedY);
        return (0.50 * moistureNoise) +
            (0.30 * waterInfluence) +
            (0.20 * latitudeModeration) -
            (0.18 * elevationFactor);
    }

    private static int[] ComputeDistanceToWater(
        int width,
        int height,
        IReadOnlyList<bool> isWater)
    {
        var distance = new int[isWater.Count];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<int>();
        for (var index = 0; index < isWater.Count; index++)
        {
            if (isWater[index])
            {
                distance[index] = 0;
                queue.Enqueue(index);
            }
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % width;
            var y = index / width;
            foreach (var direction in CardinalDirections)
            {
                var neighborX = x + direction.X;
                var neighborY = y + direction.Y;
                if (!IsInside(neighborX, neighborY, width, height))
                {
                    continue;
                }

                var neighbor = GetIndex(neighborX, neighborY, width);
                if (distance[neighbor] > distance[index] + 1)
                {
                    distance[neighbor] = distance[index] + 1;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return distance;
    }

    private static bool IsNearMarkedCell(
        int index,
        int width,
        int height,
        IReadOnlyList<bool> marked,
        int radius)
    {
        var centerX = index % width;
        var centerY = index / width;
        for (var offsetY = -radius; offsetY <= radius; offsetY++)
        {
            for (var offsetX = -radius; offsetX <= radius; offsetX++)
            {
                if (Math.Abs(offsetX) + Math.Abs(offsetY) > radius)
                {
                    continue;
                }

                var x = centerX + offsetX;
                var y = centerY + offsetY;
                if (IsInside(x, y, width, height) && marked[GetIndex(x, y, width)])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double GetRidgeStrength(
        CampaignTectonicField tectonicField,
        int index) => tectonicField.TerrainRidgeStrength[index];

    private static double GetOrogenyStrength(
        CampaignWorldDefinition definition,
        int x,
        int y,
        int seed,
        CampaignTectonicField tectonicField,
        int index,
        double ridgeStrength)
    {
        var tileKilometers = definition.CampaignTileSizeMeters / 1_000.0;
        var shorterWorldDimension = Math.Min(
            definition.WorldWidthMeters,
            definition.WorldHeightMeters) / 1_000.0;
        var provinceWavelength = Math.Max(
            tileKilometers * 16,
            Math.Min(420, shorterWorldDimension * 0.52));
        var province = (CampaignTerrainNoise.Fractal(
            (x + 0.5) * tileKilometers,
            (y + 0.5) * tileKilometers,
            OffsetSeed(seed, 22_981),
            provinceWavelength,
            2) + 1) * 0.5;
        var regionalStructure = (0.68 * province) + (0.32 * ridgeStrength);
        var activeBoundary = Math.Clamp(
            tectonicField.ConvergentUplift[index] +
            (0.35 * tectonicField.ShearStrength[index]),
            0,
            1);
        var tectonicBoundaryStructure = Math.Clamp(
            0.06 +
            (0.82 * tectonicField.ConvergentUplift[index]) +
            (0.18 * tectonicField.ShearStrength[index]) +
            (0.10 * tectonicField.BoundaryStrength[index]) +
            (0.18 * tectonicField.BoundaryAlignedRidgeStrength[index] * activeBoundary) +
            (0.16 * Math.Max(0, tectonicField.ProvinceElevationBias[index])),
            0,
            1);
        return Math.Clamp(
            (0.18 * regionalStructure) + (0.82 * tectonicBoundaryStructure),
            0,
            1);
    }

    private static ErosionProfile GetErosionProfile(CampaignMapTerrainStyle style) => style switch
    {
        CampaignMapTerrainStyle.Gentle => new(2, 0.075, 0.16, 0.006),
        CampaignMapTerrainStyle.Balanced => new(3, 0.105, 0.14, 0.012),
        CampaignMapTerrainStyle.Rugged => new(3, 0.145, 0.11, 0.018),
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown campaign terrain style."),
    };

    private static int GetErosionPassCount(CampaignMapTerrainStyle style) =>
        GetErosionProfile(style).ThermalIterations + 1;

    private static MountainGenerationProfile GetMountainProfile(
        CampaignMapMountainDensity density) =>
        density switch
        {
            CampaignMapMountainDensity.Sparse => new(0.018, 1, 0.40, 0.60, 0.080),
            CampaignMapMountainDensity.Balanced => new(0.050, 2, 0.35, 0.52, 0.070),
            CampaignMapMountainDensity.Dense => new(0.090, 3, 0.30, 0.44, 0.060),
            _ => throw new ArgumentOutOfRangeException(nameof(density), density, "Unknown campaign map mountain density."),
        };

    private static int GetTargetMountainTileCount(
        int inlandTileCount,
        CampaignMapGenerationOptions options,
        MountainGenerationProfile profile)
    {
        if (options.LandMix is { } landMix)
        {
            return GetTerrainMixTargetCounts(
                inlandTileCount,
                landMix,
                options.CustomTerrainDefinitions).Mountain;
        }

        var terrainMultiplier = options.TerrainStyle switch
        {
            CampaignMapTerrainStyle.Gentle => 0.60,
            CampaignMapTerrainStyle.Balanced => 1.00,
            CampaignMapTerrainStyle.Rugged => 1.20,
            _ => 1.00,
        };
        var coverage = Math.Min(0.12, profile.TargetCoverage * terrainMultiplier);
        return Math.Max(1, (int)Math.Round(inlandTileCount * coverage));
    }

    private static int GetTargetLakeCount(int minimumAxis, CampaignMapHydrology hydrology) => hydrology switch
    {
        CampaignMapHydrology.Light => Math.Max(1, minimumAxis / 70),
        CampaignMapHydrology.Balanced => Math.Max(1, minimumAxis / 45),
        CampaignMapHydrology.Abundant => Math.Max(2, minimumAxis / 30),
        _ => 0,
    };

    private static int GetTargetRiverCount(int minimumAxis, CampaignMapHydrology hydrology) => hydrology switch
    {
        CampaignMapHydrology.Light => Math.Max(1, minimumAxis / 45),
        CampaignMapHydrology.Balanced => Math.Max(2, minimumAxis / 28),
        CampaignMapHydrology.Abundant => Math.Max(3, minimumAxis / 18),
        _ => 0,
    };

    private static double GetMinimumLakeDepthFactor(CampaignMapHydrology hydrology) => hydrology switch
    {
        CampaignMapHydrology.Light => 0.008,
        CampaignMapHydrology.Balanced => 0.005,
        CampaignMapHydrology.Abundant => 0.003,
        _ => 1,
    };

    private static int GetMaximumLakeArea(int tileCount, CampaignMapHydrology hydrology)
    {
        var fraction = hydrology switch
        {
            CampaignMapHydrology.Light => 0.0015,
            CampaignMapHydrology.Balanced => 0.0030,
            CampaignMapHydrology.Abundant => 0.0060,
            _ => 0,
        };
        return Math.Max(1, (int)Math.Round(tileCount * fraction));
    }

    private static void SeedDrainage(
        int index,
        int width,
        IReadOnlyList<double> rawHeights,
        int seed,
        double[] filled,
        int[] visitOrder,
        bool[] visited,
        PriorityQueue<int, (double Height, uint Tie)> queue,
        ref int sequence)
    {
        if (visited[index])
        {
            return;
        }

        visited[index] = true;
        filled[index] = rawHeights[index];
        visitOrder[index] = sequence++;
        queue.Enqueue(index, (filled[index], Hash(seed, index % width, index / width)));
    }

    private static void EnqueueOceanSeed(
        int index,
        IReadOnlyList<bool> isLand,
        bool[] isSea,
        Queue<int> queue)
    {
        if (!isLand[index] && !isSea[index])
        {
            isSea[index] = true;
            queue.Enqueue(index);
        }
    }

    private static void ForceWaterBoundary(int width, int height, bool[] forcedWater)
    {
        for (var x = 0; x < width; x++)
        {
            forcedWater[GetIndex(x, 0, width)] = true;
            forcedWater[GetIndex(x, height - 1, width)] = true;
        }

        for (var y = 1; y + 1 < height; y++)
        {
            forcedWater[GetIndex(0, y, width)] = true;
            forcedWater[GetIndex(width - 1, y, width)] = true;
        }
    }

    private static void ForceContinentalOceanAnchors(
        int width,
        int height,
        int seed,
        bool[] forcedWater)
    {
        forcedWater[GetIndex(0, 0, width)] = true;
        forcedWater[GetIndex(width - 1, 0, width)] = true;
        forcedWater[GetIndex(0, height - 1, width)] = true;
        forcedWater[GetIndex(width - 1, height - 1, width)] = true;
        ForceNormalizedWater(0, -1, width, height, forcedWater);
        ForceNormalizedWater(0, 1, width, height, forcedWater);
        var openSide = HashUnit(seed, 42_211, 719) < 0.5 ? -1.0 : 1.0;
        var openLatitude = Lerp(-0.45, 0.45, HashUnit(seed, 42_229, 727));
        ForceNormalizedWater(openSide, openLatitude, width, height, forcedWater);
    }

    private static void ForceLandBoundary(int width, int height, bool[] forcedLand)
    {
        for (var x = 0; x < width; x++)
        {
            forcedLand[GetIndex(x, 0, width)] = true;
            forcedLand[GetIndex(x, height - 1, width)] = true;
        }

        for (var y = 1; y + 1 < height; y++)
        {
            forcedLand[GetIndex(0, y, width)] = true;
            forcedLand[GetIndex(width - 1, y, width)] = true;
        }
    }

    private static void ForceArchipelagoCenters(int width, int height, int seed, bool[] forcedLand)
    {
        GetArchipelagoIsland(seed, -1, out var centerX, out var centerY, out _, out _);
        ForceNormalizedLand(centerX, centerY, width, height, forcedLand);
        for (var index = 0; index < ArchipelagoOuterIslandCount; index++)
        {
            GetArchipelagoIsland(seed, index, out centerX, out centerY, out _, out _);
            ForceNormalizedLand(centerX, centerY, width, height, forcedLand);
        }
    }

    private static void GetArchipelagoIsland(
        int seed,
        int index,
        out double centerX,
        out double centerY,
        out double radiusX,
        out double radiusY)
    {
        if (index < 0)
        {
            centerX = (HashUnit(seed, 307, 401) - 0.5) * 0.12;
            centerY = (HashUnit(seed, 503, 601) - 0.5) * 0.12;
            radiusX = 0.21;
            radiusY = 0.18;
            return;
        }

        var angleOffset = HashUnit(seed, 101, 211) * Math.PI * 2;
        var angleJitter = (HashUnit(seed, index, 701) - 0.5) * 0.16;
        var angle = angleOffset + (index * Math.PI * 2 / ArchipelagoOuterIslandCount) + angleJitter;
        var ringRadius = 0.52 + ((HashUnit(seed, index, 809) - 0.5) * 0.08);
        centerX = Math.Cos(angle) * ringRadius;
        centerY = Math.Sin(angle) * ringRadius * 0.84;
        radiusX = 0.15 + (HashUnit(seed, index, 907) * 0.05);
        radiusY = 0.14 + (HashUnit(seed, index, 1_009) * 0.05);
    }

    private static void ForceNormalizedLand(
        double normalizedX,
        double normalizedY,
        int width,
        int height,
        bool[] forcedLand)
    {
        var x = Math.Clamp((int)Math.Floor(((normalizedX + 1) * 0.5) * width), 0, width - 1);
        var y = Math.Clamp((int)Math.Floor(((normalizedY + 1) * 0.5) * height), 0, height - 1);
        forcedLand[GetIndex(x, y, width)] = true;
    }

    private static void ForceNormalizedWater(
        double normalizedX,
        double normalizedY,
        int width,
        int height,
        bool[] forcedWater)
    {
        var x = Math.Clamp((int)Math.Floor(((normalizedX + 1) * 0.5) * width), 0, width - 1);
        var y = Math.Clamp((int)Math.Floor(((normalizedY + 1) * 0.5) * height), 0, height - 1);
        forcedWater[GetIndex(x, y, width)] = true;
    }

    private static void ApplyForcedMasks(
        bool[] isLand,
        IReadOnlyList<bool> forcedLand,
        IReadOnlyList<bool> forcedWater)
    {
        for (var index = 0; index < isLand.Length; index++)
        {
            if (forcedLand[index])
            {
                isLand[index] = true;
            }
            else if (forcedWater[index])
            {
                isLand[index] = false;
            }
        }
    }

    private static (double X, double Y) GetNormalizedPosition(
        int x,
        int y,
        int width,
        int height) =>
        ((((x + 0.5) / width) * 2) - 1, (((y + 0.5) / height) * 2) - 1);

    private static double EllipseDistance(
        double x,
        double y,
        double centerX,
        double centerY,
        double radiusX,
        double radiusY)
    {
        var deltaX = (x - centerX) / radiusX;
        var deltaY = (y - centerY) / radiusY;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static short RoundHeight(CampaignWorldDefinition definition, double height) =>
        checked((short)Math.Clamp(
            (int)Math.Round(height, MidpointRounding.AwayFromZero),
            definition.MinimumHeightMeters,
            definition.MaximumHeightMeters));

    private static int GetIndex(int x, int y, int width) => (y * width) + x;

    private static bool IsInside(int x, int y, int width, int height) =>
        (uint)x < (uint)width && (uint)y < (uint)height;

    private static double FractalNoise(
        double x,
        double y,
        int seed,
        double baseFrequency,
        int octaves)
    {
        var total = 0.0;
        var totalAmplitude = 0.0;
        var amplitude = 1.0;
        var frequency = baseFrequency;
        for (var octave = 0; octave < octaves; octave++)
        {
            total += ValueNoise(x * frequency, y * frequency, OffsetSeed(seed, octave * 1_013)) * amplitude;
            totalAmplitude += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }

        return total / totalAmplitude;
    }

    private static double ValueNoise(double x, double y, int seed)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var fractionX = Fade(x - x0);
        var fractionY = Fade(y - y0);
        var top = Lerp(HashSigned(seed, x0, y0), HashSigned(seed, x0 + 1, y0), fractionX);
        var bottom = Lerp(HashSigned(seed, x0, y0 + 1), HashSigned(seed, x0 + 1, y0 + 1), fractionX);
        return Lerp(top, bottom, fractionY);
    }

    private static double HashUnit(int seed, int x, int y) => Hash(seed, x, y) / (double)uint.MaxValue;

    private static double HashSigned(int seed, int x, int y) => (HashUnit(seed, x, y) * 2) - 1;

    private static uint Hash(int seed, int x, int y)
    {
        unchecked
        {
            var hash = (uint)seed;
            hash ^= (uint)x * 0x9E3779B9u;
            hash = (hash << 16) | (hash >> 16);
            hash ^= (uint)y * 0x85EBCA6Bu;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }
    }

    private static int OffsetSeed(int seed, int offset) => unchecked(seed + offset);

    private static double Fade(double value) =>
        value * value * value * ((value * ((value * 6) - 15)) + 10);

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var amount = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return amount * amount * (3 - (2 * amount));
    }

    private static double Lerp(double left, double right, double amount) =>
        left + ((right - left) * amount);

    private sealed record DrainageModel(
        double[] FilledHeights,
        int[] Receiver,
        int[] VisitOrder);

    private sealed record LakeBasin(
        int Anchor,
        List<int> Cells,
        double Score,
        double SpillHeight);

    private sealed record RiverCandidate(
        int Source,
        List<int> Path,
        double Score);
}
