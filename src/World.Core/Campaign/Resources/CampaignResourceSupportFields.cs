using Kingdom.World.Core.Campaign.Generation;

namespace Kingdom.World.Core.Campaign.Resources;

public static class CampaignResourceSupportFieldIds
{
    private static readonly string[] Values =
    [
        "aquatic",
        "aquatic-productivity",
        "arid",
        "biomass",
        "burial",
        "coast",
        "coast-transport",
        "competence",
        "ecotone",
        "erosion",
        "evaporative",
        "evaporative-potential",
        "exposed-rock",
        "fold-belt",
        "forest",
        "forest-capability",
        "freshwater",
        "granitic",
        "groundwater",
        "hydrothermal",
        "lake",
        "lowland",
        "mineralized",
        "moist",
        "moisture",
        "old-crust",
        "open-land",
        "relief",
        "rift",
        "river",
        "sedimentary",
        "shear",
        "temperature",
        "temperature-comfort",
        "volcanic",
    ];

    private static readonly HashSet<string> Supported = Values.ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(Values);

    public static bool IsSupported(string? factorId) =>
        factorId is not null && Supported.Contains(factorId);
}

/// <summary>
/// Inspectable shared climate, geology, and surface fields for one immutable terrain snapshot.
/// Values are normalized to 0..1 and stored once as floats for reuse across resources.
/// </summary>
public sealed class CampaignResourceSupportFields
{
    private readonly IReadOnlyDictionary<string, float[]> _byId;

    private CampaignResourceSupportFields(
        CampaignResourceTerrainSnapshot terrain,
        CampaignResourceGenerationSettings settings,
        IReadOnlyDictionary<string, float[]> byId,
        float[] veinProfile,
        float[] basinProfile,
        float[] surfaceDepositProfile,
        float[] regionalDetail,
        float[] boundaryTangentX,
        float[] boundaryTangentY)
    {
        Terrain = terrain;
        Settings = settings;
        _byId = byId;
        VeinProfile = veinProfile;
        BasinProfile = basinProfile;
        SurfaceDepositProfile = surfaceDepositProfile;
        RegionalDetail = regionalDetail;
        BoundaryTangentX = boundaryTangentX;
        BoundaryTangentY = boundaryTangentY;
    }

    public CampaignResourceTerrainSnapshot Terrain { get; }

    public CampaignResourceGenerationSettings Settings { get; }

    internal float[] VeinProfile { get; }

    internal float[] BasinProfile { get; }

    internal float[] SurfaceDepositProfile { get; }

    internal float[] RegionalDetail { get; }

    internal float[] BoundaryTangentX { get; }

    internal float[] BoundaryTangentY { get; }

    public static CampaignResourceSupportFields Build(
        CampaignResourceTerrainSnapshot terrain,
        CampaignResourceGenerationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);
        var definition = terrain.Definition;
        var width = definition.TilesX;
        var height = definition.TilesY;
        var count = checked((int)definition.TileCount);
        var tileKilometers = definition.CampaignTileSizeMeters / 1_000.0;
        var shorterDimensionKilometers = Math.Min(
            definition.WorldWidthMeters,
            definition.WorldHeightMeters) / 1_000.0;
        var regionalWavelength = Math.Max(
            tileKilometers * 3,
            Math.Min(240, Math.Max(60, shorterDimensionKilometers * 0.32)));
        var localWavelength = Math.Max(
            tileKilometers * 2.2,
            Math.Min(65, Math.Max(18, regionalWavelength * 0.32)));

        var temperature = NewField(count);
        var temperatureComfort = NewField(count);
        var moisture = NewField(count);
        var arid = NewField(count);
        var lowland = NewField(count);
        var freshwater = NewField(count);
        var groundwater = NewField(count);
        var biomass = NewField(count);
        var forest = NewField(count);
        var openLand = NewField(count);
        var ecotone = NewField(count);
        var aquatic = NewField(count);
        var relief = NewField(count);
        var exposedRock = NewField(count);
        var coast = NewField(count);
        var evaporative = NewField(count);
        var river = NewField(count);
        var lake = NewField(count);
        var regionalDetail = NewField(count);

        GetClimatePriors(settings.Climate, out var baseTemperatureC, out var latitudeSpanC, out var baseMoisture);
        var samples = terrain.AsSpan();
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                var sample = samples[index];
                var centerXKm = (x + 0.5) * tileKilometers;
                var centerYKm = (y + 0.5) * tileKilometers;
                var normalizedY = (y + 0.5) / height;
                var elevationAboveSeaKm = Math.Max(
                    0,
                    (sample.ElevationMeters - definition.SeaLevelMeters) / 1_000.0);
                var temperatureNoise = CampaignTerrainNoise.Fractal(
                    centerXKm,
                    centerYKm,
                    OffsetSeed(settings.ResourceSeed, 2_101),
                    regionalWavelength,
                    3,
                    persistence: 0.48);
                var temperatureC = baseTemperatureC +
                    (latitudeSpanC * (0.5 - normalizedY)) -
                    (6.5 * elevationAboveSeaKm) +
                    (2.5 * temperatureNoise);
                temperature[index] = ToFloat(Clamp01((temperatureC + 20) / 55));
                temperatureComfort[index] = ToFloat(Math.Exp(-Math.Pow((temperatureC - 16) / 17, 2)));

                var seaInfluence = DistanceInfluence(sample.SeaDistanceKilometers, 120);
                var lakeInfluence = DistanceInfluence(sample.LakeDistanceKilometers, 42);
                var riverInfluence = DistanceInfluence(sample.RiverDistanceKilometers, 22);
                var upwindX = Math.Max(0, x - Math.Max(1, (int)Math.Ceiling(20 / tileKilometers)));
                var upwindElevation = samples[(y * width) + upwindX].ElevationMeters;
                var rainShadow = Clamp01((upwindElevation - sample.ElevationMeters) / 1_800.0) * 0.18;
                var windExposure = Clamp01((sample.ElevationMeters - upwindElevation) / 2_400.0) * 0.05;
                var moistureNoise = CampaignTerrainNoise.Fractal(
                    centerXKm,
                    centerYKm,
                    OffsetSeed(settings.ResourceSeed, 2_129),
                    regionalWavelength * 0.78,
                    4,
                    persistence: 0.52);
                var moistureValue = Clamp01(
                    baseMoisture +
                    (0.20 * seaInfluence) +
                    (0.17 * lakeInfluence) +
                    (0.14 * riverInfluence) +
                    (0.14 * moistureNoise) +
                    windExposure -
                    rainShadow);
                moisture[index] = ToFloat(moistureValue);
                arid[index] = ToFloat(Clamp01((1 - moistureValue) * (0.45 + (0.55 * temperature[index]))));

                var gradeRelief = Clamp01(sample.MaximumCardinalGrade / 0.34);
                var formRelief = sample.Form switch
                {
                    CampaignResourceTerrainForm.Flat => 0.05,
                    CampaignResourceTerrainForm.Rolling => 0.25,
                    CampaignResourceTerrainForm.Hills => 0.55,
                    CampaignResourceTerrainForm.Mountain => 0.85,
                    CampaignResourceTerrainForm.Cliff => 1.0,
                    _ => 0,
                };
                var reliefValue = Clamp01((0.58 * gradeRelief) + (0.42 * formRelief));
                relief[index] = ToFloat(reliefValue);
                var relativeElevation = sample.ElevationMeters - definition.SeaLevelMeters;
                lowland[index] = ToFloat(
                    (1 - SmoothStep(250, 2_000, relativeElevation)) *
                    (1 - (0.60 * reliefValue)));

                river[index] = ToFloat(Math.Max(
                    sample.HasRiver ? 1 : 0,
                    riverInfluence));
                lake[index] = ToFloat(Math.Max(
                    sample.Surface == CampaignResourceSurfaceType.Lake ? 1 : 0,
                    lakeInfluence));
                var freshwaterValue = Clamp01(Math.Max(lakeInfluence, riverInfluence));
                freshwater[index] = ToFloat(freshwaterValue);
                var groundwaterValue = Clamp01(
                    (0.42 * moistureValue) +
                    (0.28 * lowland[index]) +
                    (0.30 * Math.Max(lakeInfluence, riverInfluence)));
                groundwater[index] = ToFloat(groundwaterValue);

                var surfaceBiomass = sample.Surface switch
                {
                    CampaignResourceSurfaceType.Forest => 1.0,
                    CampaignResourceSurfaceType.Wetland => 0.92,
                    CampaignResourceSurfaceType.Grassland => 0.78,
                    CampaignResourceSurfaceType.Tundra => 0.38,
                    CampaignResourceSurfaceType.Desert => 0.17,
                    CampaignResourceSurfaceType.BarrenRock => 0.08,
                    _ => 0.25,
                };
                var biomassValue = sample.Kind == CampaignResourceTerrainKind.Land
                    ? Clamp01(
                        Math.Pow(moistureValue, 0.72) *
                        Math.Pow(Math.Max(0.02, temperatureComfort[index]), 0.60) *
                        surfaceBiomass)
                    : 0;
                biomass[index] = ToFloat(biomassValue);
                var forestSurface = sample.Surface == CampaignResourceSurfaceType.Forest ? 1.0 : 0.68;
                forest[index] = ToFloat(Clamp01(biomassValue * forestSurface * (1 - (0.35 * reliefValue))));
                var openSurface = sample.Surface switch
                {
                    CampaignResourceSurfaceType.Grassland => 1.0,
                    CampaignResourceSurfaceType.Desert => 0.72,
                    CampaignResourceSurfaceType.Tundra => 0.66,
                    CampaignResourceSurfaceType.Wetland => 0.35,
                    _ => 0.20,
                };
                openLand[index] = ToFloat(sample.Kind == CampaignResourceTerrainKind.Land
                    ? Clamp01(openSurface * (1 - (0.38 * forest[index])) * (1 - (0.25 * reliefValue)))
                    : 0);
                ecotone[index] = ToFloat(Clamp01(
                    (1 - Math.Abs(forest[index] - openLand[index])) *
                    (0.35 + (0.65 * biomassValue))));

                var isCoastalLand = sample.IsAdjacentToSea || sample.IsAdjacentToLake;
                var isCoastalWater = sample.CoastFlags.HasFlag(CampaignResourceCoastFlags.CoastalWater);
                var broadWaterCoastResponse = sample.Kind == CampaignResourceTerrainKind.Land
                    ? Math.Max(
                        DistanceInfluence(sample.SeaDistanceKilometers, 24),
                        DistanceInfluence(sample.LakeDistanceKilometers, 14))
                    : sample.Surface == CampaignResourceSurfaceType.Lake
                        ? 0.45
                        : 0.25;
                var coastValue = Math.Max(
                    isCoastalLand || isCoastalWater ? 1 : 0,
                    broadWaterCoastResponse);
                coast[index] = ToFloat(coastValue);
                aquatic[index] = ToFloat(sample.Kind == CampaignResourceTerrainKind.Water
                    ? Clamp01(
                        0.28 +
                        (0.30 * moistureValue) +
                        (0.25 * coastValue) +
                        (sample.Surface == CampaignResourceSurfaceType.Lake ? 0.17 : 0.08))
                    : 0);
                var rockSurface = sample.Surface == CampaignResourceSurfaceType.BarrenRock ? 1 : 0.25;
                exposedRock[index] = ToFloat(sample.Kind == CampaignResourceTerrainKind.Land
                    ? Clamp01((0.52 * reliefValue) + (0.48 * rockSurface))
                    : 0);
                evaporative[index] = ToFloat(sample.Kind == CampaignResourceTerrainKind.Land
                    ? Clamp01((0.68 * arid[index]) + (0.22 * lowland[index]) + (0.10 * coastValue))
                    : 0);
                regionalDetail[index] = ToFloat(Clamp01(
                    0.5 +
                    (0.5 * CampaignTerrainNoise.Fractal(
                        centerXKm,
                        centerYKm,
                        OffsetSeed(settings.ResourceSeed, 2_183),
                        localWavelength,
                        3,
                        persistence: 0.45))));
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var tectonics = CampaignTectonicModel.Build(definition, OffsetSeed(settings.ResourceSeed, 4_001));
        var oldCrust = NewField(count);
        var volcanic = NewField(count);
        var hydrothermal = NewField(count);
        var sedimentary = NewField(count);
        var foldBelt = NewField(count);
        var shear = NewField(count);
        var rift = NewField(count);
        var granitic = NewField(count);
        var burial = NewField(count);
        var erosion = NewField(count);
        var competence = NewField(count);
        var mineralized = NewField(count);
        var veinProfile = NewField(count);
        var basinProfile = NewField(count);
        var surfaceDepositProfile = NewField(count);
        var tangentX = NewField(count);
        var tangentY = NewField(count);
        GetGeologyPriors(
            settings.Geology,
            out var oldCrustPrior,
            out var volcanicPrior,
            out var sedimentaryPrior,
            out var foldPrior,
            out var riftPrior);
        for (var y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                var centerXKm = (x + 0.5) * tileKilometers;
                var centerYKm = (y + 0.5) * tileKilometers;
                var sample = samples[index];
                var provinceNoise = UnitNoise(
                    centerXKm,
                    centerYKm,
                    OffsetSeed(settings.ResourceSeed, 4_103),
                    regionalWavelength * 1.35,
                    3);
                var mineralNoise = UnitNoise(
                    centerXKm,
                    centerYKm,
                    OffsetSeed(settings.ResourceSeed, 4_129),
                    localWavelength * 1.5,
                    3);
                var basinNoise = 1 - UnitNoise(
                    centerXKm,
                    centerYKm,
                    OffsetSeed(settings.ResourceSeed, 4_151),
                    regionalWavelength * 0.85,
                    3);
                var boundary = tectonics.BoundaryStrength[index];
                var oldValue = Clamp01(
                    oldCrustPrior *
                    ((0.62 * (1 - boundary)) + (0.38 * provinceNoise)));
                var foldValue = Clamp01(
                    foldPrior *
                    ((0.62 * tectonics.ConvergentUplift[index]) +
                     (0.23 * tectonics.ShearStrength[index]) +
                     (0.15 * tectonics.BoundaryAlignedRidgeStrength[index])));
                var riftValue = Clamp01(riftPrior *
                    ((0.82 * tectonics.RiftStrength[index]) + (0.18 * boundary * mineralNoise)));
                var volcanicValue = Clamp01(
                    volcanicPrior *
                    ((0.46 * tectonics.ConvergentUplift[index]) +
                     (0.34 * riftValue) +
                     (0.20 * mineralNoise * boundary)));
                var shearValue = Clamp01(
                    (0.78 * tectonics.ShearStrength[index]) +
                    (0.22 * tectonics.BoundaryAlignedRidgeStrength[index] * boundary));
                var sedimentaryValue = Clamp01(
                    sedimentaryPrior *
                    ((0.42 * lowland[index]) +
                     (0.33 * (1 - relief[index])) +
                     (0.25 * basinNoise)));
                var hydrothermalValue = Clamp01(
                    (0.38 * volcanicValue) +
                    (0.26 * riftValue) +
                    (0.22 * shearValue) +
                    (0.14 * mineralNoise));
                var graniticValue = Clamp01(
                    (0.48 * oldValue) +
                    (0.27 * foldValue) +
                    (0.25 * provinceNoise));
                var erosionValue = Clamp01(
                    (0.46 * relief[index]) +
                    (0.24 * river[index]) +
                    (0.16 * coast[index]) +
                    (0.14 * regionalDetail[index]));
                var competenceValue = Clamp01(
                    (0.42 * oldValue) +
                    (0.30 * exposedRock[index]) +
                    (0.18 * relief[index]) +
                    (sample.Surface == CampaignResourceSurfaceType.BarrenRock ? 0.10 : 0));
                var mineralizedValue = Clamp01(Math.Max(
                    Math.Max(hydrothermalValue, foldValue),
                    Math.Max(shearValue, (0.60 * graniticValue) + (0.40 * volcanicValue))));

                oldCrust[index] = ToFloat(oldValue);
                foldBelt[index] = ToFloat(foldValue);
                rift[index] = ToFloat(riftValue);
                volcanic[index] = ToFloat(volcanicValue);
                shear[index] = ToFloat(shearValue);
                sedimentary[index] = ToFloat(sedimentaryValue);
                hydrothermal[index] = ToFloat(hydrothermalValue);
                granitic[index] = ToFloat(graniticValue);
                burial[index] = ToFloat(Clamp01(
                    sedimentaryValue * ((0.52 * biomass[index]) + (0.48 * basinNoise))));
                erosion[index] = ToFloat(erosionValue);
                competence[index] = ToFloat(competenceValue);
                mineralized[index] = ToFloat(mineralizedValue);
                veinProfile[index] = ToFloat(Clamp01(
                    (0.57 * tectonics.BoundaryAlignedRidgeStrength[index]) +
                    (0.28 * mineralizedValue) +
                    (0.15 * mineralNoise)));
                basinProfile[index] = ToFloat(Clamp01(
                    (0.58 * sedimentaryValue) + (0.32 * basinNoise) + (0.10 * lowland[index])));
                surfaceDepositProfile[index] = ToFloat(Clamp01(
                    (0.46 * erosionValue) +
                    (0.24 * Math.Max(river[index], coast[index])) +
                    (0.18 * exposedRock[index]) +
                    (0.12 * regionalDetail[index])));
                tangentX[index] = ToSignedFloat(tectonics.BoundaryTangentX[index]);
                tangentY[index] = ToSignedFloat(tectonics.BoundaryTangentY[index]);
            }
        }

        var byId = new Dictionary<string, float[]>(StringComparer.Ordinal)
        {
            ["aquatic"] = aquatic,
            ["aquatic-productivity"] = aquatic,
            ["arid"] = arid,
            ["biomass"] = biomass,
            ["burial"] = burial,
            ["coast"] = coast,
            ["coast-transport"] = coast,
            ["competence"] = competence,
            ["ecotone"] = ecotone,
            ["erosion"] = erosion,
            ["evaporative"] = evaporative,
            ["evaporative-potential"] = evaporative,
            ["exposed-rock"] = exposedRock,
            ["fold-belt"] = foldBelt,
            ["forest"] = forest,
            ["forest-capability"] = forest,
            ["freshwater"] = freshwater,
            ["granitic"] = granitic,
            ["groundwater"] = groundwater,
            ["hydrothermal"] = hydrothermal,
            ["lake"] = lake,
            ["lowland"] = lowland,
            ["mineralized"] = mineralized,
            ["moist"] = moisture,
            ["moisture"] = moisture,
            ["old-crust"] = oldCrust,
            ["open-land"] = openLand,
            ["relief"] = relief,
            ["rift"] = rift,
            ["river"] = river,
            ["sedimentary"] = sedimentary,
            ["shear"] = shear,
            ["temperature"] = temperature,
            ["temperature-comfort"] = temperatureComfort,
            ["volcanic"] = volcanic,
        };
        return new CampaignResourceSupportFields(
            terrain,
            settings,
            byId,
            veinProfile,
            basinProfile,
            surfaceDepositProfile,
            regionalDetail,
            tangentX,
            tangentY);
    }

    public float GetValue(string factorId, int x, int y)
    {
        if (!_byId.TryGetValue(factorId, out var field))
        {
            throw new KeyNotFoundException($"Unsupported campaign-resource factor '{factorId}'.");
        }

        if ((uint)x >= (uint)Terrain.Definition.TilesX || (uint)y >= (uint)Terrain.Definition.TilesY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Support-field coordinate ({x}, {y}) is outside the campaign grid.");
        }

        return field[(y * Terrain.Definition.TilesX) + x];
    }

    internal bool TryGetValues(string factorId, out float[] values) =>
        _byId.TryGetValue(factorId, out values!);

    private static float[] NewField(int count) => new float[count];

    private static float ToFloat(double value) => (float)Math.Clamp(value, 0, 1);

    private static float ToSignedFloat(double value) => (float)Math.Clamp(value, -1, 1);

    private static double UnitNoise(double x, double y, int seed, double wavelength, int octaves) =>
        Clamp01(0.5 + (0.5 * CampaignTerrainNoise.Fractal(x, y, seed, wavelength, octaves)));

    private static double DistanceInfluence(double distanceKilometers, double scaleKilometers) =>
        double.IsPositiveInfinity(distanceKilometers)
            ? 0
            : Math.Exp(-distanceKilometers / scaleKilometers);

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var amount = Clamp01((value - edge0) / (edge1 - edge0));
        return amount * amount * (3 - (2 * amount));
    }

    private static int OffsetSeed(int seed, int offset) => unchecked(seed + offset);

    private static void GetClimatePriors(
        CampaignResourceClimateProfile profile,
        out double baseTemperatureC,
        out double latitudeSpanC,
        out double baseMoisture)
    {
        (baseTemperatureC, latitudeSpanC, baseMoisture) = profile switch
        {
            CampaignResourceClimateProfile.AutoMixed => (16, 22, 0.52),
            CampaignResourceClimateProfile.Tropical => (27, 8, 0.72),
            CampaignResourceClimateProfile.Temperate => (15, 18, 0.58),
            CampaignResourceClimateProfile.Continental => (11, 30, 0.46),
            CampaignResourceClimateProfile.Arid => (24, 18, 0.20),
            CampaignResourceClimateProfile.Cold => (-3, 18, 0.35),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown climate profile."),
        };
    }

    private static void GetGeologyPriors(
        CampaignResourceGeologyProfile profile,
        out double oldCrust,
        out double volcanic,
        out double sedimentary,
        out double fold,
        out double rift)
    {
        (oldCrust, volcanic, sedimentary, fold, rift) = profile switch
        {
            CampaignResourceGeologyProfile.AutoMixed => (1.0, 1.0, 1.0, 1.0, 1.0),
            CampaignResourceGeologyProfile.AncientCraton => (1.35, 0.72, 0.90, 0.88, 0.70),
            CampaignResourceGeologyProfile.VolcanicArc => (0.86, 1.45, 0.80, 1.16, 0.92),
            CampaignResourceGeologyProfile.SedimentaryBasins => (0.84, 0.70, 1.45, 0.80, 0.88),
            CampaignResourceGeologyProfile.FoldBelt => (0.92, 1.02, 0.88, 1.45, 0.78),
            CampaignResourceGeologyProfile.YoungRift => (0.78, 1.18, 1.04, 0.78, 1.55),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown geology profile."),
        };
    }
}
