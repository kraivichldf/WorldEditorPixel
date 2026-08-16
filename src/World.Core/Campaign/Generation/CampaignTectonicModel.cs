using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Generation;

internal static class CampaignTectonicModel
{
    private const int MinimumProvinceCount = 4;
    private const int MaximumProvinceCount = 12;

    public static CampaignTectonicField Build(CampaignWorldDefinition definition, int seed)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CampaignWorldDefinition.EnsureValid(definition);

        var width = definition.TilesX;
        var height = definition.TilesY;
        var count = checked(width * height);
        var worldWidthKilometers = definition.WorldWidthMeters / 1_000.0;
        var worldHeightKilometers = definition.WorldHeightMeters / 1_000.0;
        var tileKilometers = definition.CampaignTileSizeMeters / 1_000.0;
        var shorterWorldDimension = Math.Min(worldWidthKilometers, worldHeightKilometers);
        var characteristicLength = Math.Sqrt(worldWidthKilometers * worldHeightKilometers);
        var provinceCount = Math.Clamp(
            (int)Math.Round(characteristicLength / 180.0) + 2,
            MinimumProvinceCount,
            MaximumProvinceCount);
        var provinces = CreateProvinces(
            provinceCount,
            worldWidthKilometers,
            worldHeightKilometers,
            seed);
        var boundaryWidthKilometers = Math.Clamp(
            shorterWorldDimension * 0.055,
            Math.Max(tileKilometers * 1.5, 12.0),
            45.0);
        var provinceWarpWavelength = Math.Max(
            tileKilometers * 6,
            Math.Min(260, shorterWorldDimension * 0.36));
        var provinceWarpScale = Math.Max(
            tileKilometers * 0.2,
            Math.Min(tileKilometers * 4, shorterWorldDimension * 0.028));
        var boundaryTextureWavelength = Math.Max(
            tileKilometers * 5,
            Math.Min(100, shorterWorldDimension * 0.11));
        var rangeWavelength = Math.Max(
            tileKilometers * 10,
            Math.Min(150, shorterWorldDimension * 0.18));
        var regionalRidgeWavelength = Math.Max(
            tileKilometers * 8,
            Math.Min(115, shorterWorldDimension * 0.14));

        var uplift = new double[count];
        var rift = new double[count];
        var shear = new double[count];
        var boundary = new double[count];
        var boundaryTangentX = new double[count];
        var boundaryTangentY = new double[count];
        var boundaryAlignedRidge = new double[count];
        var terrainRidge = new double[count];
        var provinceBias = new double[count];
        var provinceIds = new int[count];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                var tileCenterX = (x + 0.5) * tileKilometers;
                var tileCenterY = (y + 0.5) * tileKilometers;
                var sampleX = tileCenterX +
                    (CampaignTerrainNoise.Fractal(
                        tileCenterX,
                        tileCenterY,
                        OffsetSeed(seed, 31_337),
                        provinceWarpWavelength,
                        3) * provinceWarpScale);
                var sampleY = tileCenterY +
                    (CampaignTerrainNoise.Fractal(
                        tileCenterX,
                        tileCenterY,
                        OffsetSeed(seed, 31_411),
                        provinceWarpWavelength,
                        3) * provinceWarpScale);

                FindNearestProvinces(
                    provinces,
                    sampleX,
                    sampleY,
                    out var nearest,
                    out var second,
                    out var nearestDistanceSquared,
                    out var secondDistanceSquared);
                provinceIds[index] = nearest.Id;

                var firstProvince = nearest.Id < second.Id ? nearest : second;
                var secondProvince = nearest.Id < second.Id ? second : nearest;
                var centerDeltaX = secondProvince.CenterXKilometers - firstProvince.CenterXKilometers;
                var centerDeltaY = secondProvince.CenterYKilometers - firstProvince.CenterYKilometers;
                var centerDistance = Math.Sqrt(
                    (centerDeltaX * centerDeltaX) + (centerDeltaY * centerDeltaY));
                var inverseCenterDistance = centerDistance <= double.Epsilon ? 0 : 1 / centerDistance;
                var normalX = centerDeltaX * inverseCenterDistance;
                var normalY = centerDeltaY * inverseCenterDistance;
                var tangentX = -normalY;
                var tangentY = normalX;
                var distanceToBoundary = centerDistance <= double.Epsilon
                    ? double.PositiveInfinity
                    : Math.Abs(secondDistanceSquared - nearestDistanceSquared) / (2 * centerDistance);
                var boundaryInfluence = Math.Exp(-Math.Pow(
                    distanceToBoundary / boundaryWidthKilometers,
                    2));
                var relativeVelocityX = firstProvince.VelocityX - secondProvince.VelocityX;
                var relativeVelocityY = firstProvince.VelocityY - secondProvince.VelocityY;
                var normalMotion = ((relativeVelocityX * normalX) + (relativeVelocityY * normalY)) * 0.5;
                var tangentMotion = Math.Abs(
                    (-relativeVelocityX * normalY) + (relativeVelocityY * normalX)) * 0.5;
                var convergence = Math.Sqrt(Math.Clamp(normalMotion, 0, 1));
                var divergence = Math.Sqrt(Math.Clamp(-normalMotion, 0, 1));
                var shearMotion = Math.Sqrt(Math.Clamp(tangentMotion, 0, 1));
                var boundaryTexture = 0.88 + (0.12 * ((CampaignTerrainNoise.Fractal(
                    tileCenterX,
                    tileCenterY,
                    OffsetSeed(seed, 31_499),
                    boundaryTextureWavelength,
                    3) + 1) * 0.5));
                var ridgeWarpX = CampaignTerrainNoise.Fractal(
                    tileCenterX,
                    tileCenterY,
                    OffsetSeed(seed, 31_553),
                    rangeWavelength * 2.2,
                    2) * rangeWavelength * 0.10;
                var ridgeWarpY = CampaignTerrainNoise.Fractal(
                    tileCenterX,
                    tileCenterY,
                    OffsetSeed(seed, 31_571),
                    rangeWavelength * 2.2,
                    2) * rangeWavelength * 0.10;
                var alongBoundary = ((tileCenterX + ridgeWarpX) * tangentX) +
                    ((tileCenterY + ridgeWarpY) * tangentY);
                var acrossBoundary = ((tileCenterX + ridgeWarpX) * normalX) +
                    ((tileCenterY + ridgeWarpY) * normalY);

                boundary[index] = boundaryInfluence;
                boundaryTangentX[index] = tangentX;
                boundaryTangentY[index] = tangentY;
                boundaryAlignedRidge[index] = CampaignTerrainNoise.Ridged(
                    alongBoundary,
                    acrossBoundary * 3.4,
                    OffsetSeed(seed, 31_603),
                    rangeWavelength,
                    3,
                    persistence: 0.55);
                var regionalRidge = CampaignTerrainNoise.Ridged(
                    tileCenterX,
                    tileCenterY,
                    OffsetSeed(seed, 14_011),
                    regionalRidgeWavelength,
                    4,
                    persistence: 0.52);
                uplift[index] = Math.Clamp(
                    boundaryInfluence * boundaryTexture * ((0.82 * convergence) + (0.18 * shearMotion)),
                    0,
                    1);
                rift[index] = Math.Clamp(boundaryInfluence * divergence, 0, 1);
                shear[index] = Math.Clamp(boundaryInfluence * shearMotion, 0, 1);
                var activeBoundary = Math.Clamp(
                    uplift[index] +
                    (0.45 * shear[index]) +
                    (0.15 * boundaryInfluence),
                    0,
                    1);
                var alignment = SmoothStep(0.08, 0.58, boundaryInfluence) *
                    (0.35 + (0.65 * activeBoundary));
                terrainRidge[index] = Math.Clamp(
                    Lerp(regionalRidge, boundaryAlignedRidge[index], alignment),
                    0,
                    1);
                var boundaryBlend = boundaryInfluence * 0.5;
                provinceBias[index] = Lerp(
                    nearest.ElevationBias,
                    second.ElevationBias,
                    boundaryBlend);
            }
        }

        return new CampaignTectonicField(
            provinceCount,
            provinceIds,
            boundary,
            boundaryTangentX,
            boundaryTangentY,
            boundaryAlignedRidge,
            terrainRidge,
            uplift,
            rift,
            shear,
            provinceBias);
    }

    private static TectonicProvince[] CreateProvinces(
        int count,
        double worldWidthKilometers,
        double worldHeightKilometers,
        int seed)
    {
        var aspect = worldWidthKilometers / Math.Max(1, worldHeightKilometers);
        var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * aspect)));
        var rows = Math.Max(1, (int)Math.Ceiling(count / (double)columns));
        var provinces = new TectonicProvince[count];
        for (var index = 0; index < count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var jitterX = 0.18 + (0.64 * HashUnit(seed, index, 31_601));
            var jitterY = 0.18 + (0.64 * HashUnit(seed, index, 31_607));
            var centerX = ((column + jitterX) / columns) * worldWidthKilometers;
            var centerY = ((row + jitterY) / rows) * worldHeightKilometers;
            var angle = HashUnit(seed, index, 31_621) * Math.Tau;
            var speed = 0.55 + (0.45 * HashUnit(seed, index, 31_633));
            provinces[index] = new TectonicProvince(
                index,
                centerX,
                centerY,
                Math.Cos(angle) * speed,
                Math.Sin(angle) * speed,
                HashSigned(seed, index, 31_643) * 0.18);
        }

        return provinces;
    }

    private static void FindNearestProvinces(
        IReadOnlyList<TectonicProvince> provinces,
        double xKilometers,
        double yKilometers,
        out TectonicProvince nearest,
        out TectonicProvince second,
        out double nearestDistanceSquared,
        out double secondDistanceSquared)
    {
        nearest = provinces[0];
        second = provinces[Math.Min(1, provinces.Count - 1)];
        nearestDistanceSquared = double.PositiveInfinity;
        secondDistanceSquared = double.PositiveInfinity;
        foreach (var province in provinces)
        {
            var deltaX = xKilometers - province.CenterXKilometers;
            var deltaY = yKilometers - province.CenterYKilometers;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared < nearestDistanceSquared)
            {
                second = nearest;
                secondDistanceSquared = nearestDistanceSquared;
                nearest = province;
                nearestDistanceSquared = distanceSquared;
            }
            else if (distanceSquared < secondDistanceSquared)
            {
                second = province;
                secondDistanceSquared = distanceSquared;
            }
        }
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

    private static double Lerp(double left, double right, double amount) =>
        left + ((right - left) * amount);

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        var amount = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return amount * amount * (3 - (2 * amount));
    }

    private sealed record TectonicProvince(
        int Id,
        double CenterXKilometers,
        double CenterYKilometers,
        double VelocityX,
        double VelocityY,
        double ElevationBias);
}

internal sealed record CampaignTectonicField(
    int ProvinceCount,
    int[] ProvinceIds,
    double[] BoundaryStrength,
    double[] BoundaryTangentX,
    double[] BoundaryTangentY,
    double[] BoundaryAlignedRidgeStrength,
    double[] TerrainRidgeStrength,
    double[] ConvergentUplift,
    double[] RiftStrength,
    double[] ShearStrength,
    double[] ProvinceElevationBias);
