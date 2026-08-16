using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignMapGeneratorTests
{
    [Fact]
    public void Blank_GeneratesNoOverrides()
    {
        var result = CampaignMapGenerator.Generate(CreateDefinition(), CampaignMapGenerationOptions.Blank);

        Assert.Empty(result.Tiles);
        Assert.Equal(0, result.GeneratedTileCount);
        Assert.Equal(CampaignMapHydrology.None, result.Hydrology);
    }

    [Fact]
    public void SameInputs_GenerateIdenticalOrderedTiles()
    {
        var definition = CreateDefinition();
        var options = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.Island,
            Seed: 730_241,
            CampaignMapTerrainStyle.Rugged,
            CampaignMapHydrology.Balanced);

        var first = CampaignMapGenerator.Generate(definition, options);
        var second = CampaignMapGenerator.Generate(definition, options);

        Assert.Equal(first.Tiles.ToArray(), second.Tiles.ToArray());
        Assert.Equal(first.LandTileCount, second.LandTileCount);
        Assert.Equal(first.SeaTileCount, second.SeaTileCount);
        Assert.Equal(first.LakeTileCount, second.LakeTileCount);
        Assert.Equal(first.RiverTileCount, second.RiverTileCount);
        Assert.Equal(first.LargeRiverTileCount, second.LargeRiverTileCount);
        Assert.Equal(first.RiverJunctionTileCount, second.RiverJunctionTileCount);
    }

    [Fact]
    public void DifferentSeeds_ChangeTheGeneratedWorld()
    {
        var definition = CreateDefinition();
        var first = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(CampaignMapGenerationPreset.Island, 101));
        var second = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(CampaignMapGenerationPreset.Island, 202));

        Assert.NotEqual(first.Tiles.ToArray(), second.Tiles.ToArray());
    }

    [Fact]
    public void TectonicModel_ProducesDeterministicCoherentBoundaryFields()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

        var first = CampaignTectonicModel.Build(definition, 17_029);
        var second = CampaignTectonicModel.Build(definition, 17_029);

        Assert.InRange(first.ProvinceCount, 4, 12);
        Assert.Equal(first.ProvinceIds, second.ProvinceIds);
        Assert.Equal(first.BoundaryStrength, second.BoundaryStrength);
        Assert.Equal(first.BoundaryTangentX, second.BoundaryTangentX);
        Assert.Equal(first.BoundaryTangentY, second.BoundaryTangentY);
        Assert.Equal(first.BoundaryAlignedRidgeStrength, second.BoundaryAlignedRidgeStrength);
        Assert.Equal(first.TerrainRidgeStrength, second.TerrainRidgeStrength);
        Assert.Equal(first.ConvergentUplift, second.ConvergentUplift);
        Assert.Equal(first.RiftStrength, second.RiftStrength);
        Assert.Contains(first.ConvergentUplift, value => value > 0.20);
        Assert.Contains(first.RiftStrength, value => value > 0.20);
        Assert.Contains(first.ShearStrength, value => value > 0.20);

        var strongUplift = Enumerable.Range(0, first.ConvergentUplift.Length)
            .Where(index => first.ConvergentUplift[index] > 0.20)
            .ToArray();
        var connectedStrongUplift = strongUplift.Count(index =>
        {
            var x = index % definition.TilesX;
            var y = index / definition.TilesX;
            return CardinalNeighbors(x, y, definition)
                .Any(coordinate =>
                    first.ConvergentUplift[(coordinate.Y * definition.TilesX) + coordinate.X] > 0.15);
        });
        Assert.True(
            connectedStrongUplift * 10 >= strongUplift.Length * 9,
            "Convergent uplift should form continuous plate-boundary belts rather than isolated noise pixels.");
    }

    [Fact]
    public void TerrainNoise_UsesDeterministicPhysicalWavelengths()
    {
        var broadSamples = new List<double>();
        var fineSamples = new List<double>();
        var alternateSeedSamples = new List<double>();
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var xKilometers = 7.5 + (x * 5.0);
                var yKilometers = 12.5 + (y * 5.0);
                broadSamples.Add(CampaignTerrainNoise.Fractal(
                    xKilometers,
                    yKilometers,
                    seed: 17_029,
                    wavelengthKilometers: 120,
                    octaves: 4));
                fineSamples.Add(CampaignTerrainNoise.Fractal(
                    xKilometers,
                    yKilometers,
                    seed: 17_029,
                    wavelengthKilometers: 24,
                    octaves: 4));
                alternateSeedSamples.Add(CampaignTerrainNoise.Fractal(
                    xKilometers,
                    yKilometers,
                    seed: 17_030,
                    wavelengthKilometers: 120,
                    octaves: 4));
            }
        }

        Assert.All(broadSamples, value => Assert.InRange(value, -1, 1));
        Assert.NotEqual(broadSamples, alternateSeedSamples);
        Assert.Equal(
            broadSamples[37],
            CampaignTerrainNoise.Fractal(32.5, 22.5, 17_029, 120, 4));

        var broadNeighborVariation = GetGridNeighborVariation(broadSamples, 16, 16);
        var fineNeighborVariation = GetGridNeighborVariation(fineSamples, 16, 16);
        Assert.True(
            broadNeighborVariation < fineNeighborVariation * 0.70,
            $"A 120 km field should vary more slowly over 5 km samples than a 24 km field; " +
            $"observed {broadNeighborVariation:F4} and {fineNeighborVariation:F4}.");
    }

    [Fact]
    public void TectonicRidgeNoise_IsMoreContinuousAlongBoundariesThanAcrossThem()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var field = CampaignTectonicModel.Build(definition, 17_029);
        var tangentVariation = new List<double>();
        var normalVariation = new List<double>();

        for (var y = 1; y < definition.TilesY - 1; y++)
        {
            for (var x = 1; x < definition.TilesX - 1; x++)
            {
                var index = (y * definition.TilesX) + x;
                if (field.BoundaryStrength[index] < 0.35)
                {
                    continue;
                }

                var tangentIsHorizontal = Math.Abs(field.BoundaryTangentX[index]) >=
                    Math.Abs(field.BoundaryTangentY[index]);
                var tangentNeighbor = tangentIsHorizontal ? index + 1 : index + definition.TilesX;
                var normalNeighbor = tangentIsHorizontal ? index + definition.TilesX : index + 1;
                if (field.BoundaryStrength[tangentNeighbor] < 0.15 ||
                    field.BoundaryStrength[normalNeighbor] < 0.15)
                {
                    continue;
                }

                tangentVariation.Add(Math.Abs(
                    field.BoundaryAlignedRidgeStrength[index] -
                    field.BoundaryAlignedRidgeStrength[tangentNeighbor]));
                normalVariation.Add(Math.Abs(
                    field.BoundaryAlignedRidgeStrength[index] -
                    field.BoundaryAlignedRidgeStrength[normalNeighbor]));
            }
        }

        Assert.True(tangentVariation.Count >= 100);
        Assert.True(
            tangentVariation.Average() < normalVariation.Average() * 0.80,
            $"Boundary ridges should change more slowly along strike than across strike; " +
            $"observed {tangentVariation.Average():F4} and {normalVariation.Average():F4}.");
    }

    [Fact]
    public void TerrainErosion_IsDeterministicAndCarvesAFlowingSlope()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 8_000,
            worldHeightMeters: 8_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var isLand = new bool[64];
        var isSea = new bool[64];
        var initial = new double[64];
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var index = (y * 8) + x;
                isSea[index] = y == 7;
                isLand[index] = !isSea[index];
                initial[index] = isSea[index] ? -200 : 3_200 - (y * 320) + (Math.Abs(x - 4) * 35);
            }
        }

        var first = (double[])initial.Clone();
        var second = (double[])initial.Clone();
        var options = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.SouthCoast,
            Seed: 902_117,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapHydrology.Balanced);

        CampaignMapGenerator.ApplyTerrainErosion(definition, options, isLand, isSea, first);
        CampaignMapGenerator.ApplyTerrainErosion(definition, options, isLand, isSea, second);

        Assert.Equal(first, second);
        Assert.Contains(
            Enumerable.Range(0, first.Length).Where(index => isLand[index]),
            index => first[index] < initial[index] - 1);
        Assert.All(
            Enumerable.Range(0, first.Length).Where(index => isLand[index]),
            index => Assert.InRange(first[index], 1, definition.MaximumHeightMeters));
    }

    [Fact]
    public void GeologicPipeline_ReportsTectonicsAndScalesReliefByTerrainStyle()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var baseOptions = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.LandOnly,
            Seed: 44_081,
            CampaignMapTerrainStyle.Gentle,
            CampaignMapHydrology.None,
            CampaignMapMountainDensity.Balanced);

        var gentle = CampaignMapGenerator.Generate(definition, baseOptions);
        var rugged = CampaignMapGenerator.Generate(
            definition,
            baseOptions with { TerrainStyle = CampaignMapTerrainStyle.Rugged });
        var gentleHeights = gentle.Tiles.Select(entry => entry.Data.HeightMeters).Order().ToArray();
        var ruggedHeights = rugged.Tiles.Select(entry => entry.Data.HeightMeters).Order().ToArray();
        var gentleSpread = gentleHeights[(int)(gentleHeights.Length * 0.90)] -
            gentleHeights[(int)(gentleHeights.Length * 0.10)];
        var ruggedSpread = ruggedHeights[(int)(ruggedHeights.Length * 0.90)] -
            ruggedHeights[(int)(ruggedHeights.Length * 0.10)];

        Assert.InRange(gentle.TectonicProvinceCount, 4, 12);
        Assert.Equal(gentle.TectonicProvinceCount, rugged.TectonicProvinceCount);
        Assert.True(gentle.ErosionPassCount > 0);
        Assert.True(rugged.ErosionPassCount >= gentle.ErosionPassCount);
        Assert.True(ruggedSpread > gentleSpread);
    }

    [Fact]
    public void Island_HasSeaBoundaryAndEditableLandInterior()
    {
        var definition = CreateDefinition();
        var result = Generate(definition, CampaignMapGenerationPreset.Island, hydrology: CampaignMapHydrology.None);

        AssertBoundary(result, definition, IsSea);
        Assert.False(IsWater(GetTile(result, definition.TilesX / 2, definition.TilesY / 2, definition).Type));
        Assert.True(result.LandTileCount > 0);
        Assert.True(result.SeaTileCount > 0);
    }

    [Theory]
    [InlineData(CampaignMapGenerationPreset.EastCoast)]
    [InlineData(CampaignMapGenerationPreset.WestCoast)]
    [InlineData(CampaignMapGenerationPreset.NorthCoast)]
    [InlineData(CampaignMapGenerationPreset.SouthCoast)]
    public void DirectionalCoast_GuaranteesNamedSeaEdgeButLeavesOppositeEdgeNatural(
        CampaignMapGenerationPreset preset)
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                preset,
                Seed: 35,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Balanced,
                CoastlineStyle: CampaignMapCoastlineStyle.Natural));

        IEnumerable<CampaignTileData> oppositeEdge;

        switch (preset)
        {
            case CampaignMapGenerationPreset.EastCoast:
                AssertVerticalEdge(result, definition, definition.TilesX - 1, IsSea);
                oppositeEdge = Enumerable.Range(0, definition.TilesY)
                    .Select(y => GetTile(result, 0, y, definition));
                break;
            case CampaignMapGenerationPreset.WestCoast:
                AssertVerticalEdge(result, definition, 0, IsSea);
                oppositeEdge = Enumerable.Range(0, definition.TilesY)
                    .Select(y => GetTile(result, definition.TilesX - 1, y, definition));
                break;
            case CampaignMapGenerationPreset.NorthCoast:
                AssertHorizontalEdge(result, definition, 0, IsSea);
                oppositeEdge = Enumerable.Range(0, definition.TilesX)
                    .Select(x => GetTile(result, x, definition.TilesY - 1, definition));
                break;
            case CampaignMapGenerationPreset.SouthCoast:
                AssertHorizontalEdge(result, definition, definition.TilesY - 1, IsSea);
                oppositeEdge = Enumerable.Range(0, definition.TilesX)
                    .Select(x => GetTile(result, x, 0, definition));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
        }

        var oppositeTiles = oppositeEdge.ToArray();
        Assert.Contains(oppositeTiles, data => IsWater(data.Type));
        Assert.Contains(oppositeTiles, data => !IsWater(data.Type));
    }

    [Fact]
    public void DirectionalCoastlineStyle_ControlsBoundaryComplexity()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var options = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.EastCoast,
            Seed: 17_029,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapHydrology.None,
            CampaignMapMountainDensity.Balanced,
            CoastlineStyle: CampaignMapCoastlineStyle.Smooth);

        var smooth = CampaignMapGenerator.Generate(definition, options);
        var natural = CampaignMapGenerator.Generate(
            definition,
            options with { CoastlineStyle = CampaignMapCoastlineStyle.Natural });
        var rugged = CampaignMapGenerator.Generate(
            definition,
            options with { CoastlineStyle = CampaignMapCoastlineStyle.Rugged });

        Assert.Equal(CampaignMapCoastlineStyle.Smooth, smooth.CoastlineStyle);
        Assert.Equal(CampaignMapCoastlineStyle.Natural, natural.CoastlineStyle);
        Assert.Equal(CampaignMapCoastlineStyle.Rugged, rugged.CoastlineStyle);
        Assert.True(
            CountLandWaterBoundaryEdges(natural, definition) > CountLandWaterBoundaryEdges(smooth, definition),
            "Natural coast should have more shoreline detail than Smooth coast.");
        Assert.True(
            CountLandWaterBoundaryEdges(rugged, definition) > CountLandWaterBoundaryEdges(natural, definition),
            "Rugged coast should have more shoreline detail than Natural coast.");
    }

    [Theory]
    [InlineData(CampaignMapCoastlineStyle.Smooth)]
    [InlineData(CampaignMapCoastlineStyle.FlowingCapes)]
    [InlineData(CampaignMapCoastlineStyle.Natural)]
    [InlineData(CampaignMapCoastlineStyle.Rugged)]
    public void DirectionalCoastlineStyle_CreatesAttachedPeninsulaWithWaterOnBothFlanks(
        CampaignMapCoastlineStyle coastlineStyle)
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                Seed: 17_029,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Balanced,
                CoastlineStyle: coastlineStyle));

        AssertAttachedPeninsulaWithWaterOnBothFlanks(result, definition, coastlineStyle);
    }

    [Fact]
    public void NaturalDirectionalCoast_IsDeterministicAndKeepsOceanConnected()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var options = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.EastCoast,
            Seed: 91_337,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapHydrology.None,
            CampaignMapMountainDensity.Balanced,
            CoastlineStyle: CampaignMapCoastlineStyle.Natural);

        var first = CampaignMapGenerator.Generate(definition, options);
        var second = CampaignMapGenerator.Generate(definition, options);

        Assert.Equal(first.Tiles.ToArray(), second.Tiles.ToArray());
        AssertVerticalEdge(first, definition, definition.TilesX - 1, IsSea);
        AssertEverySeaTileReachesEastEdge(first, definition);
    }

    [Fact]
    public void DirectionalCoast_SeedVariesLandWaterBalanceWithoutBreakingEdgeGuarantees()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 400_000,
            worldHeightMeters: 320_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var landShares = new List<double>();
        var oppositeEdgeWaterCounts = new List<int>();

        for (var seed = 1; seed <= 12; seed++)
        {
            var result = CampaignMapGenerator.Generate(
                definition,
                new CampaignMapGenerationOptions(
                    CampaignMapGenerationPreset.EastCoast,
                    seed,
                    CampaignMapTerrainStyle.Balanced,
                    CampaignMapHydrology.None,
                    CampaignMapMountainDensity.Balanced,
                    CoastlineStyle: CampaignMapCoastlineStyle.Natural));

            landShares.Add(result.LandTileCount / (double)definition.TileCount);
            oppositeEdgeWaterCounts.Add(Enumerable.Range(0, definition.TilesY)
                .Count(y => IsWater(GetTile(result, 0, y, definition).Type)));
            AssertVerticalEdge(result, definition, definition.TilesX - 1, IsSea);
        }

        Assert.True(
            landShares.Max() - landShares.Min() >= 0.15,
            $"Seeded Coast generation should vary land share by at least 15 percentage points; " +
            $"observed {landShares.Min():P1} to {landShares.Max():P1}.");
        Assert.All(landShares, share => Assert.InRange(share, 0.35, 0.90));
        Assert.Contains(oppositeEdgeWaterCounts, count => count > 0);
        Assert.True(
            oppositeEdgeWaterCounts.Distinct().Count() > 1,
            "The opposite edge should be generated geography, not one fixed land mask.");
    }

    [Fact]
    public void NaturalDirectionalCoast_ContainsLandmarkScaleGeography()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                Seed: 17_029,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Balanced,
                CoastlineStyle: CampaignMapCoastlineStyle.Natural));
        var shoreline = Enumerable.Range(0, definition.TilesY)
            .Select(y => result.Tiles
                .Where(entry => entry.Y == y && entry.Data.Type == CampaignTileType.Sea)
                .Min(entry => entry.X))
            .ToArray();

        Assert.True(
            shoreline.Max() - shoreline.Min() >= 20,
            "Natural coast should include a gulf or cape with at least 100 km of coast-normal excursion.");
    }

    [Fact]
    public void NaturalDirectionalCoast_AtMaximumWorldScaleCreatesHierarchicalCoastAndArchipelagos()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 10_000_000,
            worldHeightMeters: 10_000_000,
            campaignTileSizeMeters: 20_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                Seed: 17_029,
                CampaignMapTerrainStyle.Gentle,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Sparse,
                CoastlineStyle: CampaignMapCoastlineStyle.Natural));

        Assert.Equal(500, definition.TilesX);
        Assert.Equal(500, definition.TilesY);
        Assert.Equal(250_000, result.Tiles.Count);
        AssertVerticalEdge(result, definition, definition.TilesX - 1, IsSea);

        var eastmostLandByRow = Enumerable.Repeat(-1, definition.TilesY).ToArray();
        foreach (var entry in result.Tiles.Where(entry => !IsWater(entry.Data.Type)))
        {
            eastmostLandByRow[entry.Y] = Math.Max(eastmostLandByRow[entry.Y], entry.X);
        }

        var interiorShoreline = eastmostLandByRow
            .Skip(definition.TilesY / 10)
            .Take(definition.TilesY * 8 / 10)
            .Where(x => x >= 0)
            .ToArray();
        var bandMeans = Enumerable.Range(1, 8)
            .Select(band => eastmostLandByRow
                .Skip(band * definition.TilesY / 10)
                .Take(definition.TilesY / 10)
                .Where(x => x >= 0)
                .Average())
            .ToArray();

        Assert.True(
            interiorShoreline.Max() - interiorShoreline.Min() >= 75,
            "The 10,000 km coast should have at least 1,500 km of interior gulf-to-cape relief.");
        Assert.True(
            bandMeans.Max() - bandMeans.Min() >= 45,
            "Kilometre-scale coast structure should retain at least 900 km of broad relief after averaging 1,000 km bands.");
        Assert.True(
            CountLandWaterBoundaryEdges(result, definition) >= 2_000,
            "Natural large-world coast should retain meso-scale shoreline detail at 20 km tile resolution.");
        Assert.True(
            CountLandComponents(result, definition) >= 5,
            "Natural large-world coast should include several offshore islands or island chains.");
    }

    [Fact]
    public void FlowingCapeCoast_ProducesOneSmoothMainlandWithATaperedPeninsula()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var options = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.EastCoast,
            Seed: 17_029,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapHydrology.None,
            CampaignMapMountainDensity.Balanced,
            CoastlineStyle: CampaignMapCoastlineStyle.FlowingCapes);

        var first = CampaignMapGenerator.Generate(definition, options);
        var second = CampaignMapGenerator.Generate(definition, options);
        var eastmostLandByRow = Enumerable.Range(0, definition.TilesY)
            .Select(y => first.Tiles
                .Where(entry => entry.Y == y && !IsWater(entry.Data.Type))
                .Select(entry => entry.X)
                .DefaultIfEmpty(-1)
                .Max())
            .ToArray();
        var landRows = eastmostLandByRow.Where(x => x >= 0).ToArray();
        Assert.Equal(CampaignMapCoastlineStyle.FlowingCapes, first.CoastlineStyle);
        Assert.Equal(first.Tiles.ToArray(), second.Tiles.ToArray());
        Assert.Equal(1, CountLandComponents(first, definition));
        Assert.True(landRows.Length >= definition.TilesY / 2);
        Assert.True(
            landRows.Max() - landRows.Min() >= 45,
            "The flowing profile should combine a deep bay and projecting cape across at least 225 km.");
        AssertVerticalEdge(first, definition, definition.TilesX - 1, IsSea);
        AssertEverySeaTileReachesEastEdge(first, definition);
    }

    [Fact]
    public void InlandSea_HasCentralSeaAndLandBoundary()
    {
        var definition = CreateDefinition();
        var result = Generate(definition, CampaignMapGenerationPreset.InlandSea, hydrology: CampaignMapHydrology.None);

        Assert.Equal(
            CampaignTileType.Sea,
            GetTile(result, definition.TilesX / 2, definition.TilesY / 2, definition).Type);
        AssertBoundary(result, definition, data => !IsWater(data.Type));
    }

    [Fact]
    public void LandOnly_ContainsNoWaterShoreOrRiverEvenWhenHydrologyWasRequested()
    {
        var definition = CreateDefinition();
        var result = Generate(
            definition,
            CampaignMapGenerationPreset.LandOnly,
            hydrology: CampaignMapHydrology.Abundant);

        Assert.Equal(CampaignMapHydrology.None, result.Hydrology);
        Assert.Equal(definition.TileCount, result.LandTileCount);
        Assert.Equal(0, result.SeaTileCount);
        Assert.Equal(0, result.LakeTileCount);
        Assert.Equal(0, result.RiverTileCount);
        Assert.All(result.Tiles, entry => Assert.DoesNotContain(
            entry.Data.Type,
            new[]
            {
                CampaignTileType.Sea,
                CampaignTileType.Lake,
                CampaignTileType.River,
                CampaignTileType.LargeRiver,
                CampaignTileType.Cliff,
            }));
    }

    [Fact]
    public void Archipelago_ProducesSeveralSeparateLandComponents()
    {
        var definition = CreateDefinition();
        var result = Generate(
            definition,
            CampaignMapGenerationPreset.Archipelago,
            seed: 44_801,
            hydrology: CampaignMapHydrology.None);

        Assert.True(CountLandComponents(result, definition) >= 3);
    }

    [Theory]
    [InlineData(17_029)]
    [InlineData(91_337)]
    [InlineData(902_117)]
    public void ContinentalWorld_ProducesHierarchicalLandmassesSeparatedByBroadOceans(int seed)
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 350_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var result = Generate(
            definition,
            CampaignMapGenerationPreset.Continent,
            seed,
            CampaignMapHydrology.None);
        var componentSizes = GetLandComponentSizes(result, definition);
        var majorComponentMinimum = definition.TileCount / 50;
        var majorComponents = componentSizes
            .Where(size => size >= majorComponentMinimum)
            .ToArray();
        var landShare = result.LandTileCount / (double)definition.TileCount;

        Assert.InRange(landShare, 0.24, 0.46);
        Assert.True(
            majorComponents.Length >= 3,
            $"A continental world should contain at least three major landmasses; " +
            $"observed component sizes [{string.Join(", ", componentSizes.Take(10))}].");
        Assert.True(
            majorComponents[0] >= majorComponents[1] * 1.35,
            "The landmasses should have a readable size hierarchy rather than five equal cookie islands.");
        Assert.True(
            GetLongestHorizontalSeaRun(result, definition) >= definition.TilesX * 0.18,
            "At least one latitude should cross a broad ocean basin between major landmasses.");
    }

    [Fact]
    public void ContinentalWorld_SeedChangesMacroLayoutAndCanCropLandAtTheMapEdge()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 350_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var first = Generate(
            definition,
            CampaignMapGenerationPreset.Continent,
            seed: 17_029,
            CampaignMapHydrology.None);
        var second = Generate(
            definition,
            CampaignMapGenerationPreset.Continent,
            seed: 91_337,
            CampaignMapHydrology.None);
        var changedSurfaceTiles = Enumerable.Range(0, first.Tiles.Count)
            .Count(index => IsWater(first.Tiles[index].Data.Type) != IsWater(second.Tiles[index].Data.Type));

        Assert.True(
            changedSurfaceTiles >= definition.TileCount * 0.12,
            $"Seeded macro geography should change at least 12% of land/water cells; " +
            $"observed {changedSurfaceTiles / (double)definition.TileCount:P1}.");
        Assert.True(CountBoundaryLand(second, definition) > 0);
        Assert.True(CountBoundarySea(second, definition) > 0);
    }

    [Fact]
    public void ContinentalWorld_UsesPhysicalCoastScalesAcrossGridResolutions()
    {
        var fineDefinition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 350_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var coarseDefinition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 350_000,
            campaignTileSizeMeters: 10_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var fine = Generate(
            fineDefinition,
            CampaignMapGenerationPreset.Continent,
            seed: 17_029,
            CampaignMapHydrology.None);
        var coarse = Generate(
            coarseDefinition,
            CampaignMapGenerationPreset.Continent,
            seed: 17_029,
            CampaignMapHydrology.None);
        var matchingCells = 0;
        for (var y = 0; y < coarseDefinition.TilesY; y++)
        {
            for (var x = 0; x < coarseDefinition.TilesX; x++)
            {
                var fineLandCount = 0;
                for (var offsetY = 0; offsetY < 2; offsetY++)
                {
                    for (var offsetX = 0; offsetX < 2; offsetX++)
                    {
                        fineLandCount += IsWater(GetTile(
                            fine,
                            (x * 2) + offsetX,
                            (y * 2) + offsetY,
                            fineDefinition).Type)
                            ? 0
                            : 1;
                    }
                }

                var coarseIsLand = !IsWater(GetTile(coarse, x, y, coarseDefinition).Type);
                if (coarseIsLand == (fineLandCount >= 2))
                {
                    matchingCells++;
                }
            }
        }

        Assert.True(
            matchingCells >= coarseDefinition.TileCount * 0.86,
            $"Equivalent physical worlds should retain their macro coastline when tile size changes; " +
            $"observed {matchingCells / (double)coarseDefinition.TileCount:P1} agreement.");
    }

    [Theory]
    [InlineData(CampaignMapGenerationPreset.Continent)]
    [InlineData(CampaignMapGenerationPreset.Island)]
    [InlineData(CampaignMapGenerationPreset.Archipelago)]
    [InlineData(CampaignMapGenerationPreset.EastCoast)]
    [InlineData(CampaignMapGenerationPreset.WestCoast)]
    [InlineData(CampaignMapGenerationPreset.NorthCoast)]
    [InlineData(CampaignMapGenerationPreset.SouthCoast)]
    [InlineData(CampaignMapGenerationPreset.InlandSea)]
    [InlineData(CampaignMapGenerationPreset.LandOnly)]
    public void GeneratedPreset_ProducesCompleteValidHeightBoundedTileGrid(
        CampaignMapGenerationPreset preset)
    {
        var definition = CreateDefinition();
        var result = Generate(definition, preset);

        Assert.Equal(definition.TileCount, result.GeneratedTileCount);
        Assert.Equal(definition.TileCount, result.LandTileCount + result.SeaTileCount + result.LakeTileCount);
        Assert.All(result.Tiles, entry =>
        {
            Assert.InRange(entry.X, 0, definition.TilesX - 1);
            Assert.InRange(entry.Y, 0, definition.TilesY - 1);
            Assert.InRange(
                entry.Data.HeightMeters,
                definition.MinimumHeightMeters,
                definition.MaximumHeightMeters);
        });

        var world = new CampaignWorld(definition);
        Assert.Equal(result.GeneratedTileCount, world.Tiles.SetTiles(result.Tiles));
        Assert.All(result.Tiles.Where(entry => entry.Data.Type.IsRiver()), entry =>
            Assert.InRange(
                CountRiverNeighbors(world, entry.X, entry.Y),
                0,
                entry.Data.Type.MaximumRiverExitCount()));
    }

    [Fact]
    public void HydrologyNone_ProducesNoLakesOrRivers()
    {
        var result = Generate(
            CreateDefinition(),
            CampaignMapGenerationPreset.Continent,
            hydrology: CampaignMapHydrology.None);

        Assert.Equal(0, result.LakeTileCount);
        Assert.Equal(0, result.RiverTileCount);
        Assert.Equal(0, result.LargeRiverTileCount);
        Assert.Equal(0, result.RiverJunctionTileCount);
    }

    [Fact]
    public void RuggedTerrain_ProducesMoreCoastalCliffsThanGentleTerrain()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var gentle = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.Island,
                17_029,
                CampaignMapTerrainStyle.Gentle,
                CampaignMapHydrology.None));
        var rugged = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.Island,
                17_029,
                CampaignMapTerrainStyle.Rugged,
                CampaignMapHydrology.None));

        Assert.True(gentle.CliffTileCount > 0);
        Assert.True(rugged.CliffTileCount > gentle.CliffTileCount);
    }

    [Fact]
    public void MountainDensity_ControlsMountainCoverageWithoutChangingTheCoastline()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var sparse = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                3,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Sparse));
        var balanced = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                3,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Balanced));
        var dense = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                3,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Dense));

        var sparseMountains = sparse.Tiles.Count(entry => entry.Data.Type == CampaignTileType.Mountain);
        var balancedMountains = balanced.Tiles.Count(entry => entry.Data.Type == CampaignTileType.Mountain);
        var denseMountains = dense.Tiles.Count(entry => entry.Data.Type == CampaignTileType.Mountain);

        Assert.True(
            sparseMountains < balancedMountains,
            $"Expected sparse < balanced Mountains, observed {sparseMountains} and {balancedMountains}.");
        Assert.True(
            balancedMountains < denseMountains,
            $"Expected balanced < dense Mountains, observed {balancedMountains} and {denseMountains}.");
        Assert.Equal(sparse.SeaTileCount, balanced.SeaTileCount);
        Assert.Equal(sparse.SeaTileCount, dense.SeaTileCount);
        Assert.True(sparseMountains < sparse.LandTileCount / 10);
        Assert.True(denseMountains * 8 < dense.LandTileCount);
        Assert.Equal(1, CountTerrainComponents(sparse, definition, CampaignTileType.Mountain));
        Assert.InRange(CountTerrainComponents(balanced, definition, CampaignTileType.Mountain), 1, 2);
        Assert.InRange(CountTerrainComponents(dense, definition, CampaignTileType.Mountain), 1, 3);
    }

    [Fact]
    public void MountainSystems_FormRidgeCoresWithFoothillTransitions()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                17_029,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Balanced));
        var mountains = result.Tiles
            .Where(entry => entry.Data.Type == CampaignTileType.Mountain)
            .Select(entry => (entry.X, entry.Y))
            .ToHashSet();

        Assert.NotEmpty(mountains);
        var interiorMountainCount = mountains.Count(coordinate =>
            CardinalNeighbors(coordinate.X, coordinate.Y, definition)
                .Count(mountains.Contains) >= 3);
        Assert.True(
            interiorMountainCount * 5 < mountains.Count,
            $"Mountain cores should extend as ridges instead of thick paint blobs; " +
            $"{interiorMountainCount:N0} of {mountains.Count:N0} tiles have at least three Mountain neighbors.");

        var exposedFoothills = mountains
            .SelectMany(coordinate => CardinalNeighbors(coordinate.X, coordinate.Y, definition))
            .Distinct()
            .Where(coordinate => !mountains.Contains(coordinate))
            .Select(coordinate => GetTile(result, coordinate.X, coordinate.Y, definition).Type)
            .Where(type => !IsWater(type) && !type.IsRiver() && type != CampaignTileType.Cliff)
            .ToArray();
        Assert.NotEmpty(exposedFoothills);
        Assert.All(exposedFoothills, type => Assert.Equal(CampaignTileType.Hills, type));
    }

    [Fact]
    public void DryInlandLowlands_ProduceLocalizedDesertWithoutReplacingTerrainForms()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                17_029,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Balanced));
        var deserts = result.Tiles
            .Where(entry => entry.Data.Type == CampaignTileType.Desert)
            .ToArray();

        Assert.NotEmpty(deserts);
        Assert.True(
            deserts.Length * 8 < result.LandTileCount,
            $"Desert must remain a localized lowland type; found {deserts.Length} of {result.LandTileCount} land tiles.");
        Assert.All(deserts, entry =>
        {
            Assert.True(entry.Data.HeightMeters <= 1_440);
            Assert.DoesNotContain(
                CardinalNeighbors(entry.X, entry.Y, definition),
                coordinate => IsWater(GetTile(result, coordinate.X, coordinate.Y, definition).Type));
        });
    }

    [Fact]
    public void SemiAridLowlands_ProduceSteppeBetweenPlainsAndDesert()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                17_029,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Balanced));
        var steppes = result.Tiles
            .Where(entry => entry.Data.Type == CampaignTileType.Steppe)
            .ToArray();

        Assert.NotEmpty(steppes);
        Assert.True(
            steppes.Length * 2 < result.LandTileCount,
            $"Steppe should remain a regional transition rather than replace most land; " +
            $"found {steppes.Length:N0} of {result.LandTileCount:N0} land tiles.");
        Assert.All(steppes, entry => Assert.DoesNotContain(
            CardinalNeighbors(entry.X, entry.Y, definition),
            coordinate => IsWater(GetTile(result, coordinate.X, coordinate.Y, definition).Type)));
    }

    [Fact]
    public void CustomLandMix_UsesExactRatiosForUnconstrainedLandTypes()
    {
        var definition = CreateDefinition();
        var mix = new CampaignMapLandMix(
            PlainsPercent: 50,
            ForestPercent: 30,
            DesertPercent: 0,
            HillsPercent: 20,
            MountainPercent: 0);

        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.LandOnly,
                Seed: 17_029,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Sparse,
                mix));

        Assert.Equal(mix, result.RequestedLandMix);
        Assert.Equal(definition.TileCount * 50 / 100, CountTiles(result, CampaignTileType.Plains));
        Assert.Equal(definition.TileCount * 30 / 100, CountTiles(result, CampaignTileType.Forest));
        Assert.Equal(definition.TileCount * 20 / 100, CountTiles(result, CampaignTileType.Hills));
        Assert.Equal(0, CountTiles(result, CampaignTileType.Desert));
        Assert.Equal(0, CountTiles(result, CampaignTileType.Mountain));
    }

    [Fact]
    public void CustomLandMix_AssignsSteppeAsAnIndependentRatio()
    {
        var definition = CreateDefinition();
        var mix = new CampaignMapLandMix(
            PlainsPercent: 35,
            ForestPercent: 20,
            DesertPercent: 0,
            HillsPercent: 15,
            MountainPercent: 0,
            SteppePercent: 30);

        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.LandOnly,
                Seed: 17_029,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Sparse,
                mix));

        Assert.Equal(definition.TileCount * 30 / 100, CountTiles(result, CampaignTileType.Steppe));
        Assert.Equal(definition.TileCount * 35 / 100, CountTiles(result, CampaignTileType.Plains));
        Assert.Equal(0, CountTiles(result, CampaignTileType.Desert));
    }

    [Fact]
    public void BalancedLandMix_ReservesAVisibleSteppeShareAndStillTotalsOneHundredPercent()
    {
        var mix = CampaignMapLandMix.Balanced;

        Assert.Equal(12, mix.SteppePercent);
        Assert.Equal(CampaignMapLandMix.RequiredTotalPercent, mix.TotalPercent);
        mix.EnsureValid();
    }

    [Fact]
    public void CustomLandMix_DoesNotChangeWaterDrainageOrCliffTopology()
    {
        var definition = CreateDefinition(tilesX: 100, tilesY: 80);
        var baselineOptions = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.EastCoast,
            Seed: 902_117,
            CampaignMapTerrainStyle.Rugged,
            CampaignMapHydrology.Balanced,
            CampaignMapMountainDensity.Balanced);
        var customOptions = baselineOptions with
        {
            LandMix = new CampaignMapLandMix(
                PlainsPercent: 60,
                ForestPercent: 25,
                DesertPercent: 5,
                HillsPercent: 8,
                MountainPercent: 2),
        };

        var baseline = CampaignMapGenerator.Generate(definition, baselineOptions);
        var custom = CampaignMapGenerator.Generate(definition, customOptions);

        Assert.Equal(baseline.SeaTileCount, custom.SeaTileCount);
        Assert.Equal(baseline.LakeTileCount, custom.LakeTileCount);
        Assert.Equal(baseline.RiverTileCount, custom.RiverTileCount);
        Assert.Equal(baseline.CliffTileCount, custom.CliffTileCount);
        for (var index = 0; index < baseline.Tiles.Count; index++)
        {
            var baselineType = baseline.Tiles[index].Data.Type;
            if (baselineType is CampaignTileType.Sea or CampaignTileType.Lake or CampaignTileType.River or
                CampaignTileType.LargeRiver or
                CampaignTileType.Cliff)
            {
                Assert.Equal(baselineType, custom.Tiles[index].Data.Type);
            }
        }
    }

    [Fact]
    public void CustomLandTerrain_GeneratesAsIndependentInlandMixCategories()
    {
        var definition = CreateDefinition();
        var farmland = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#91A85A",
            GenerationSharePercent: 30);
        var volcanicHighlands = new CampaignCustomTerrainDefinition(
            "volcanic-highlands",
            "Volcanic Highlands",
            CampaignTileType.Mountain,
            "#754C45",
            GenerationSharePercent: 20);
        var options = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.LandOnly,
            Seed: 17_029,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapHydrology.None,
            CampaignMapMountainDensity.Sparse,
            new CampaignMapLandMix(50, 0, 0, 0, 0),
            CustomTerrainDefinitions: [farmland, volcanicHighlands]);

        var first = CampaignMapGenerator.Generate(definition, options);
        var second = CampaignMapGenerator.Generate(definition, options);
        var farmlandTiles = first.Tiles
            .Where(entry => entry.Data.CustomTerrainId == "farmland")
            .ToArray();
        var volcanicTiles = first.Tiles
            .Where(entry => entry.Data.CustomTerrainId == "volcanic-highlands")
            .ToArray();

        Assert.Equal(first.Tiles, second.Tiles);
        Assert.Equal([farmland, volcanicHighlands], first.CustomTerrainDefinitions);
        Assert.Equal(definition.TileCount * 30 / 100, farmlandTiles.Length);
        Assert.Equal(definition.TileCount * 20 / 100, volcanicTiles.Length);
        Assert.Equal(definition.TileCount * 50 / 100, first.Tiles.Count(entry =>
            entry.Data.Type == CampaignTileType.Plains && entry.Data.CustomTerrainId is null));
        Assert.Equal(first.CustomTerrainTileCount, farmlandTiles.Length + volcanicTiles.Length);
        Assert.All(farmlandTiles, entry => Assert.Equal(CampaignTileType.Plains, entry.Data.Type));
        Assert.All(volcanicTiles, entry => Assert.Equal(CampaignTileType.Mountain, entry.Data.Type));
        Assert.Equal(0, first.Tiles.Count(entry =>
            entry.Data.Type == CampaignTileType.Mountain && entry.Data.CustomTerrainId is null));
    }

    [Fact]
    public void GeneratedCustomLand_CanRetainItsIdentityOnAnAutomaticCoast()
    {
        var definition = CreateDefinition(tilesX: 100, tilesY: 80);
        var farmland = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#91A85A",
            GenerationSharePercent: 100);
        var result = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                Seed: 17_029,
                CampaignMapTerrainStyle.Gentle,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Sparse,
                new CampaignMapLandMix(0, 0, 0, 0, 0),
                CustomTerrainDefinitions: [farmland]));

        var customCoast = result.Tiles.First(entry =>
            entry.Data.CustomTerrainId == farmland.Id &&
            CardinalNeighbors(entry.X, entry.Y, definition)
                .Any(coordinate => IsWater(GetTile(result, coordinate.X, coordinate.Y, definition).Type)));
        var world = new CampaignWorld(definition, [farmland]);
        world.Tiles.SetTiles(result.Tiles);

        Assert.Equal(CampaignTileType.Plains, customCoast.Data.Type);
        Assert.Equal(farmland.Id, customCoast.Data.CustomTerrainId);
        Assert.Contains(
            new[]
            {
                world.Tiles.GetAutomaticCoastSurfaceMaterial(customCoast.X, customCoast.Y, 0.5, 0.05),
                world.Tiles.GetAutomaticCoastSurfaceMaterial(customCoast.X, customCoast.Y, 0.95, 0.5),
                world.Tiles.GetAutomaticCoastSurfaceMaterial(customCoast.X, customCoast.Y, 0.5, 0.95),
                world.Tiles.GetAutomaticCoastSurfaceMaterial(customCoast.X, customCoast.Y, 0.05, 0.5),
            },
            material => material is AutomaticCoastSurfaceMaterial.Sea or AutomaticCoastSurfaceMaterial.Lake);
    }

    [Fact]
    public void Generation_RequiresCustomSharesToParticipateInTheWholeInlandMix()
    {
        var farmland = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#91A85A",
            GenerationSharePercent: 10);
        var noLandMix = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.LandOnly,
            Seed: 1,
            CustomTerrainDefinitions: [farmland]);
        var overfilledMix = noLandMix with
        {
            LandMix = new CampaignMapLandMix(100, 0, 0, 0, 0),
        };
        var validMix = noLandMix with
        {
            LandMix = new CampaignMapLandMix(90, 0, 0, 0, 0),
        };

        Assert.Throws<ArgumentException>(() => CampaignMapGenerator.Generate(CreateDefinition(), noLandMix));
        Assert.Throws<ArgumentException>(() => CampaignMapGenerator.Generate(CreateDefinition(), overfilledMix));
        CampaignMapGenerator.EnsureCanGenerate(CreateDefinition(), validMix);
    }

    [Fact]
    public void CustomTerrainDefinitions_RejectACombinedShareAboveTheInlandPool()
    {
        var farmland = new CampaignCustomTerrainDefinition(
            "farmland",
            "Farmland",
            CampaignTileType.Plains,
            "#91A85A",
            GenerationSharePercent: 60);
        var ancientForest = new CampaignCustomTerrainDefinition(
            "ancient-forest",
            "Ancient Forest",
            CampaignTileType.Forest,
            "#315A3B",
            GenerationSharePercent: 60);

        Assert.Throws<ArgumentException>(() =>
            CampaignCustomTerrainDefinition.ValidateAll([farmland, ancientForest]));
    }

    [Fact]
    public void Generation_RejectsInvalidCustomLandMix()
    {
        var invalidTotal = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.LandOnly,
            Seed: 1,
            LandMix: new CampaignMapLandMix(50, 20, 10, 10, 5));
        var excessiveMountains = invalidTotal with
        {
            LandMix = new CampaignMapLandMix(67, 10, 5, 5, 13),
        };

        Assert.Throws<ArgumentException>(() => CampaignMapGenerator.Generate(CreateDefinition(), invalidTotal));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CampaignMapGenerator.Generate(CreateDefinition(), excessiveMountains));
    }

    [Fact]
    public void BalancedHydrology_ProducesBasinLakeAndRiverThatReachesWater()
    {
        var definition = CreateDefinition(tilesX: 100, tilesY: 80);
        var result = Generate(
            definition,
            CampaignMapGenerationPreset.EastCoast,
            seed: 902_117,
            hydrology: CampaignMapHydrology.Balanced);
        var world = new CampaignWorld(definition);
        world.Tiles.SetTiles(result.Tiles);

        Assert.True(result.LakeTileCount > 0);
        Assert.True(result.RiverTileCount > 0);
        Assert.True(result.LargeRiverTileCount > 0);
        Assert.Equal(
            result.LargeRiverTileCount,
            result.Tiles.Count(entry => entry.Data.Type == CampaignTileType.LargeRiver));
        Assert.InRange(result.LargeRiverTileCount, 1, result.RiverTileCount - 1);
        Assert.Contains(
            result.Tiles.Where(entry => entry.Data.Type == CampaignTileType.LargeRiver),
            entry => CardinalNeighbors(entry.X, entry.Y, definition)
                .Any(coordinate =>
                    GetTile(result, coordinate.X, coordinate.Y, definition).Type == CampaignTileType.River));
        Assert.All(result.Tiles.Where(entry => entry.Data.Type.IsRiver()), entry =>
            Assert.InRange(
                CountRiverNeighbors(world, entry.X, entry.Y),
                0,
                entry.Data.Type.MaximumRiverExitCount()));
        AssertRiverComponentsReachWater(result, definition);
    }

    [Fact]
    public void GeneratedDrainage_CanMergeTributariesThroughExplicitJunctions()
    {
        var definition = CreateDefinition();
        var result = Generate(
            definition,
            CampaignMapGenerationPreset.WestCoast,
            seed: 1,
            hydrology: CampaignMapHydrology.Balanced);
        var world = new CampaignWorld(definition);
        world.Tiles.SetTiles(result.Tiles);
        var junctions = result.Tiles
            .Where(entry => entry.Data.Type == CampaignTileType.RiverJunction)
            .ToArray();

        Assert.True(
            junctions.Length > 0,
            $"Expected at least one generated confluence; observed {result.RiverTileCount} River tiles " +
            $"across {result.RiverJunctionTileCount} junctions.");
        Assert.Equal(junctions.Length, result.RiverJunctionTileCount);
        Assert.All(
            junctions,
            entry => Assert.Equal(3, CountRiverNeighbors(world, entry.X, entry.Y)));
        AssertRiverComponentsReachWater(result, definition);
    }

    [Fact]
    public void GeneratedCliffTilesAlwaysFaceSeaOrLakeAndCoastalIsNeverStored()
    {
        var definition = CreateDefinition();
        var result = Generate(definition, CampaignMapGenerationPreset.Island);

        Assert.DoesNotContain(result.Tiles, entry => entry.Data.Type == CampaignTileType.Coastal);
        foreach (var entry in result.Tiles.Where(entry => entry.Data.Type == CampaignTileType.Cliff))
        {
            Assert.Contains(
                CardinalNeighbors(entry.X, entry.Y, definition),
                coordinate => IsWater(GetTile(result, coordinate.X, coordinate.Y, definition).Type));
        }

        var world = new CampaignWorld(definition);
        world.Tiles.SetTiles(result.Tiles);
        var ordinaryCoast = Assert.Single(result.Tiles.Where(entry =>
                !entry.Data.Type.IsWater() &&
                !entry.Data.Type.IsRiver() &&
                entry.Data.Type != CampaignTileType.Cliff &&
                CardinalNeighbors(entry.X, entry.Y, definition)
                    .Any(coordinate => IsWater(GetTile(result, coordinate.X, coordinate.Y, definition).Type)))
            .Take(1));
        var waterNeighbor = CardinalNeighbors(ordinaryCoast.X, ordinaryCoast.Y, definition)
            .First(coordinate => IsWater(GetTile(result, coordinate.X, coordinate.Y, definition).Type));
        var localX = waterNeighbor.X < ordinaryCoast.X ? 0.05 : waterNeighbor.X > ordinaryCoast.X ? 0.95 : 0.5;
        var localY = waterNeighbor.Y < ordinaryCoast.Y ? 0.05 : waterNeighbor.Y > ordinaryCoast.Y ? 0.95 : 0.5;
        Assert.NotEqual(
            AutomaticCoastSurfaceMaterial.Original,
            world.Tiles.GetAutomaticCoastSurfaceMaterial(
                ordinaryCoast.X,
                ordinaryCoast.Y,
                localX,
                localY));
    }

    [Fact]
    public void GeneratedTiles_CanBeRepaintedImmediately()
    {
        var definition = CreateDefinition();
        var result = Generate(definition, CampaignMapGenerationPreset.Island);
        var world = new CampaignWorld(definition);
        world.Tiles.SetTiles(result.Tiles);

        world.Tiles.SetTile(3, 3, new CampaignTileData(CampaignTileType.Forest, 321));

        Assert.Equal(
            new CampaignTileData(CampaignTileType.Forest, 321),
            world.Tiles.GetTile(3, 3));
    }

    [Fact]
    public void Generation_RejectsTooSmallAndTooLargeGrids()
    {
        var tooSmall = CreateDefinition(tilesX: 7, tilesY: 8);
        var tooLarge = CreateDefinition(tilesX: 501, tilesY: 500);
        var options = new CampaignMapGenerationOptions(CampaignMapGenerationPreset.Island, 1);

        Assert.Throws<ArgumentException>(() => CampaignMapGenerator.Generate(tooSmall, options));
        Assert.Throws<ArgumentException>(() => CampaignMapGenerator.Generate(tooLarge, options));
        CampaignMapGenerator.Generate(tooSmall, CampaignMapGenerationOptions.Blank);
    }

    [Fact]
    public void Generation_RejectsUnknownMountainDensity()
    {
        var options = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.Island,
            1,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapHydrology.None,
            (CampaignMapMountainDensity)99);

        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignMapGenerator.Generate(CreateDefinition(), options));
    }

    [Fact]
    public void DrownedCoast_CarvesAdditionalSeaConnectedLowlandInlets()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var baselineOptions = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.EastCoast,
            Seed: 91_337,
            CampaignMapTerrainStyle.Balanced,
            CampaignMapHydrology.None,
            CampaignMapMountainDensity.Balanced);
        var drownedOptions = baselineOptions with
        {
            TidalInlets = CampaignMapTidalInlets.Drowned,
        };

        var baseline = CampaignMapGenerator.Generate(definition, baselineOptions);
        var first = CampaignMapGenerator.Generate(definition, drownedOptions);
        var second = CampaignMapGenerator.Generate(definition, drownedOptions);

        Assert.Equal(CampaignMapTidalInlets.Drowned, first.TidalInlets);
        Assert.Equal(first.Tiles.ToArray(), second.Tiles.ToArray());
        Assert.True(first.SeaTileCount > baseline.SeaTileCount);
        Assert.True(
            CountAdditionalSeaComponents(baseline, first, definition) > 0,
            "This stable Drowned-coast seed should accept at least one suitable inlet opportunity.");
        AssertVerticalEdge(first, definition, definition.TilesX - 1, IsSea);
        AssertEverySeaTileReachesEastEdge(first, definition);
    }

    [Fact]
    public void TidalInlets_AreSeededOpportunitiesRatherThanGuaranteedQuotas()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 700_000,
            worldHeightMeters: 700_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var fewCounts = new List<int>();
        var balancedCounts = new List<int>();
        var drownedCounts = new List<int>();
        foreach (var seed in new[] { 17, 17_029, 91_337, 814_227, 101 })
        {
            var options = new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.EastCoast,
                seed,
                CampaignMapTerrainStyle.Balanced,
                CampaignMapHydrology.None,
                CampaignMapMountainDensity.Balanced,
                CoastlineStyle: CampaignMapCoastlineStyle.Natural);
            var baseline = CampaignMapGenerator.Generate(definition, options);
            fewCounts.Add(CountAdditionalSeaComponents(
                baseline,
                CampaignMapGenerator.Generate(
                    definition,
                    options with { TidalInlets = CampaignMapTidalInlets.Few }),
                definition));
            balancedCounts.Add(CountAdditionalSeaComponents(
                baseline,
                CampaignMapGenerator.Generate(
                    definition,
                    options with { TidalInlets = CampaignMapTidalInlets.Balanced }),
                definition));
            drownedCounts.Add(CountAdditionalSeaComponents(
                baseline,
                CampaignMapGenerator.Generate(
                    definition,
                    options with { TidalInlets = CampaignMapTidalInlets.Drowned }),
                definition));
        }

        Assert.Contains(0, fewCounts);
        Assert.Contains(1, fewCounts);
        Assert.All(fewCounts, count => Assert.InRange(count, 0, 1));
        Assert.Contains(0, balancedCounts);
        Assert.Contains(balancedCounts, count => count > 0);
        Assert.All(balancedCounts, count => Assert.InRange(count, 0, 3));
        Assert.Contains(0, drownedCounts);
        Assert.Contains(drownedCounts, count => count > 0);
        Assert.All(drownedCounts, count => Assert.InRange(count, 0, 3));
    }

    [Fact]
    public void TidalInlets_DefaultNoneMatchesAnExplicitNoneSetting()
    {
        var definition = CreateDefinition();
        var defaultOptions = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.Island,
            Seed: 814_227,
            CampaignMapTerrainStyle.Rugged,
            CampaignMapHydrology.Balanced,
            CampaignMapMountainDensity.Dense);
        var explicitNone = defaultOptions with
        {
            TidalInlets = CampaignMapTidalInlets.None,
        };

        var defaultResult = CampaignMapGenerator.Generate(definition, defaultOptions);
        var explicitNoneResult = CampaignMapGenerator.Generate(definition, explicitNone);

        Assert.Equal(CampaignMapTidalInlets.None, defaultResult.TidalInlets);
        Assert.Equal(defaultResult.Tiles.ToArray(), explicitNoneResult.Tiles.ToArray());
    }

    [Fact]
    public void TidalInlets_AreIgnoredForLandOnly()
    {
        var definition = CreateDefinition();
        var baseline = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.LandOnly,
                Seed: 17_029,
                TidalInlets: CampaignMapTidalInlets.None));
        var requestedDrownedCoast = CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                CampaignMapGenerationPreset.LandOnly,
                Seed: 17_029,
                TidalInlets: CampaignMapTidalInlets.Drowned));

        Assert.Equal(CampaignMapTidalInlets.None, requestedDrownedCoast.TidalInlets);
        Assert.Equal(baseline.Tiles.ToArray(), requestedDrownedCoast.Tiles.ToArray());
    }

    [Fact]
    public void Generation_RejectsUnknownTidalInletSetting()
    {
        var options = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.Island,
            Seed: 1,
            TidalInlets: (CampaignMapTidalInlets)99);

        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignMapGenerator.Generate(CreateDefinition(), options));
    }

    [Fact]
    public void Generation_RejectsUnknownCoastlineStyle()
    {
        var options = new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.EastCoast,
            Seed: 1,
            CoastlineStyle: (CampaignMapCoastlineStyle)99);

        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignMapGenerator.Generate(CreateDefinition(), options));
    }

    private static CampaignMapGenerationResult Generate(
        CampaignWorldDefinition definition,
        CampaignMapGenerationPreset preset,
        int seed = 17_029,
        CampaignMapHydrology hydrology = CampaignMapHydrology.Balanced) =>
        CampaignMapGenerator.Generate(
            definition,
            new CampaignMapGenerationOptions(
                preset,
                seed,
                CampaignMapTerrainStyle.Balanced,
                hydrology));

    private static CampaignWorldDefinition CreateDefinition(int tilesX = 80, int tilesY = 64) =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: tilesX * 4_000L,
            worldHeightMeters: tilesY * 4_000L,
            campaignTileSizeMeters: 4_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 5_000);

    private static CampaignTileData GetTile(
        CampaignMapGenerationResult result,
        int x,
        int y,
        CampaignWorldDefinition definition)
    {
        var entry = result.Tiles[(y * definition.TilesX) + x];
        Assert.Equal(x, entry.X);
        Assert.Equal(y, entry.Y);
        return entry.Data;
    }

    private static bool IsSea(CampaignTileData data) => data.Type == CampaignTileType.Sea;

    private static bool IsWater(CampaignTileType type) =>
        type is CampaignTileType.Sea or CampaignTileType.Lake;

    private static int GetMinimumSeaX(CampaignMapGenerationResult result) =>
        result.Tiles
            .Where(entry => entry.Data.Type == CampaignTileType.Sea)
            .Min(entry => entry.X);

    private static void AssertEverySeaTileReachesEastEdge(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition)
    {
        var visited = new bool[result.Tiles.Count];
        var queue = new Queue<(int X, int Y)>();
        for (var y = 0; y < definition.TilesY; y++)
        {
            if (GetTile(result, definition.TilesX - 1, y, definition).Type != CampaignTileType.Sea)
            {
                continue;
            }

            var index = (y * definition.TilesX) + definition.TilesX - 1;
            visited[index] = true;
            queue.Enqueue((definition.TilesX - 1, y));
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in CardinalNeighbors(current.X, current.Y, definition))
            {
                var index = (neighbor.Y * definition.TilesX) + neighbor.X;
                if (visited[index] || GetTile(result, neighbor.X, neighbor.Y, definition).Type != CampaignTileType.Sea)
                {
                    continue;
                }

                visited[index] = true;
                queue.Enqueue(neighbor);
            }
        }

        foreach (var entry in result.Tiles.Where(entry => entry.Data.Type == CampaignTileType.Sea))
        {
            Assert.True(visited[(entry.Y * definition.TilesX) + entry.X],
                $"Sea tile ({entry.X}, {entry.Y}) is not connected to the East Coast ocean edge.");
        }
    }

    private static int CountTiles(CampaignMapGenerationResult result, CampaignTileType type) =>
        result.Tiles.Count(entry => entry.Data.Type == type);

    private static double GetGridNeighborVariation(
        IReadOnlyList<double> values,
        int width,
        int height)
    {
        var totalVariation = 0.0;
        var comparisonCount = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                if (x + 1 < width)
                {
                    totalVariation += Math.Abs(values[index] - values[index + 1]);
                    comparisonCount++;
                }

                if (y + 1 < height)
                {
                    totalVariation += Math.Abs(values[index] - values[index + width]);
                    comparisonCount++;
                }
            }
        }

        return totalVariation / comparisonCount;
    }

    private static int CountLandWaterBoundaryEdges(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition)
    {
        var boundaryEdges = 0;
        for (var y = 0; y < definition.TilesY; y++)
        {
            for (var x = 0; x < definition.TilesX; x++)
            {
                var isWater = IsWater(GetTile(result, x, y, definition).Type);
                if (x + 1 < definition.TilesX &&
                    isWater != IsWater(GetTile(result, x + 1, y, definition).Type))
                {
                    boundaryEdges++;
                }

                if (y + 1 < definition.TilesY &&
                    isWater != IsWater(GetTile(result, x, y + 1, definition).Type))
                {
                    boundaryEdges++;
                }
            }
        }

        return boundaryEdges;
    }

    private static void AssertBoundary(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition,
        Func<CampaignTileData, bool> predicate)
    {
        AssertHorizontalEdge(result, definition, 0, predicate);
        AssertHorizontalEdge(result, definition, definition.TilesY - 1, predicate);
        AssertVerticalEdge(result, definition, 0, predicate);
        AssertVerticalEdge(result, definition, definition.TilesX - 1, predicate);
    }

    private static void AssertHorizontalEdge(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition,
        int y,
        Func<CampaignTileData, bool> predicate)
    {
        for (var x = 0; x < definition.TilesX; x++)
        {
            Assert.True(predicate(GetTile(result, x, y, definition)), $"Unexpected tile at ({x}, {y}).");
        }
    }

    private static void AssertVerticalEdge(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition,
        int x,
        Func<CampaignTileData, bool> predicate)
    {
        for (var y = 0; y < definition.TilesY; y++)
        {
            Assert.True(predicate(GetTile(result, x, y, definition)), $"Unexpected tile at ({x}, {y}).");
        }
    }

    private static int CountLandComponents(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition) =>
        GetLandComponentSizes(result, definition).Count;

    private static IReadOnlyList<int> GetLandComponentSizes(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition)
    {
        var visited = new bool[result.Tiles.Count];
        var queue = new Queue<(int X, int Y)>();
        var sizes = new List<int>();
        for (var y = 0; y < definition.TilesY; y++)
        {
            for (var x = 0; x < definition.TilesX; x++)
            {
                var index = (y * definition.TilesX) + x;
                if (visited[index] || IsWater(GetTile(result, x, y, definition).Type))
                {
                    continue;
                }

                var size = 0;
                visited[index] = true;
                queue.Enqueue((x, y));
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    size++;
                    foreach (var neighbor in CardinalNeighbors(current.X, current.Y, definition))
                    {
                        var neighborIndex = (neighbor.Y * definition.TilesX) + neighbor.X;
                        if (!visited[neighborIndex] && !IsWater(GetTile(result, neighbor.X, neighbor.Y, definition).Type))
                        {
                            visited[neighborIndex] = true;
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                sizes.Add(size);
            }
        }

        return sizes.OrderDescending().ToArray();
    }

    private static int GetLongestHorizontalSeaRun(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition)
    {
        var longest = 0;
        for (var y = 0; y < definition.TilesY; y++)
        {
            var current = 0;
            for (var x = 0; x < definition.TilesX; x++)
            {
                if (GetTile(result, x, y, definition).Type == CampaignTileType.Sea)
                {
                    current++;
                    longest = Math.Max(longest, current);
                }
                else
                {
                    current = 0;
                }
            }
        }

        return longest;
    }

    private static int CountBoundaryLand(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition) =>
        CountBoundaryTiles(result, definition, type => !IsWater(type));

    private static int CountBoundarySea(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition) =>
        CountBoundaryTiles(result, definition, type => type == CampaignTileType.Sea);

    private static int CountBoundaryTiles(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition,
        Func<CampaignTileType, bool> predicate)
    {
        var count = 0;
        for (var x = 0; x < definition.TilesX; x++)
        {
            count += predicate(GetTile(result, x, 0, definition).Type) ? 1 : 0;
            count += predicate(GetTile(result, x, definition.TilesY - 1, definition).Type) ? 1 : 0;
        }

        for (var y = 1; y + 1 < definition.TilesY; y++)
        {
            count += predicate(GetTile(result, 0, y, definition).Type) ? 1 : 0;
            count += predicate(GetTile(result, definition.TilesX - 1, y, definition).Type) ? 1 : 0;
        }

        return count;
    }

    private static int CountAdditionalSeaComponents(
        CampaignMapGenerationResult baseline,
        CampaignMapGenerationResult withInlets,
        CampaignWorldDefinition definition)
    {
        var isAdditionalSea = Enumerable.Range(0, baseline.Tiles.Count)
            .Select(index =>
                baseline.Tiles[index].Data.Type != CampaignTileType.Sea &&
                withInlets.Tiles[index].Data.Type == CampaignTileType.Sea)
            .ToArray();
        var visited = new bool[isAdditionalSea.Length];
        var queue = new Queue<(int X, int Y)>();
        var count = 0;
        for (var y = 0; y < definition.TilesY; y++)
        {
            for (var x = 0; x < definition.TilesX; x++)
            {
                var index = (y * definition.TilesX) + x;
                if (!isAdditionalSea[index] || visited[index])
                {
                    continue;
                }

                count++;
                visited[index] = true;
                queue.Enqueue((x, y));
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in CardinalNeighbors(current.X, current.Y, definition))
                    {
                        var neighborIndex = (neighbor.Y * definition.TilesX) + neighbor.X;
                        if (!visited[neighborIndex] && isAdditionalSea[neighborIndex])
                        {
                            visited[neighborIndex] = true;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }

        return count;
    }

    private static void AssertAttachedPeninsulaWithWaterOnBothFlanks(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition,
        CampaignMapCoastlineStyle coastlineStyle)
    {
        var mainland = new bool[result.Tiles.Count];
        var queue = new Queue<(int X, int Y)>();
        for (var y = 0; y < definition.TilesY; y++)
        {
            if (IsWater(GetTile(result, 0, y, definition).Type))
            {
                continue;
            }

            mainland[y * definition.TilesX] = true;
            queue.Enqueue((0, y));
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in CardinalNeighbors(current.X, current.Y, definition))
            {
                var index = (neighbor.Y * definition.TilesX) + neighbor.X;
                if (mainland[index] || IsWater(GetTile(result, neighbor.X, neighbor.Y, definition).Type))
                {
                    continue;
                }

                mainland[index] = true;
                queue.Enqueue(neighbor);
            }
        }

        var farthestMainlandX = Enumerable.Range(0, mainland.Length)
            .Where(index => mainland[index])
            .Select(index => index % definition.TilesX)
            .Max();
        var probeX = farthestMainlandX - 3;
        Assert.True(
            probeX >= (int)Math.Round(definition.TilesX * 0.75),
            $"{coastlineStyle} should project an attached peninsula across at least 75% of the map width.");

        var projectionRows = Enumerable.Range(0, definition.TilesY)
            .Where(y => mainland[(y * definition.TilesX) + probeX])
            .ToArray();
        Assert.NotEmpty(projectionRows);
        var firstProjectionRow = projectionRows.Min();
        var lastProjectionRow = projectionRows.Max();
        Assert.InRange(firstProjectionRow, 4, definition.TilesY - 5);
        Assert.InRange(lastProjectionRow, 4, definition.TilesY - 5);
        Assert.True(
            IsWater(GetTile(result, probeX, firstProjectionRow - 1, definition).Type),
            $"{coastlineStyle} peninsula should have water on its north flank.");
        Assert.True(
            IsWater(GetTile(result, probeX, lastProjectionRow + 1, definition).Type),
            $"{coastlineStyle} peninsula should have water on its south flank.");
        Assert.True(
            lastProjectionRow - firstProjectionRow + 1 <= definition.TilesY * 0.60,
            $"{coastlineStyle} projection should remain a peninsula, not become a second full-height coast wall.");
    }

    private static int CountTerrainComponents(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition,
        CampaignTileType type)
    {
        var visited = new bool[result.Tiles.Count];
        var queue = new Queue<(int X, int Y)>();
        var count = 0;
        for (var y = 0; y < definition.TilesY; y++)
        {
            for (var x = 0; x < definition.TilesX; x++)
            {
                var index = (y * definition.TilesX) + x;
                if (visited[index] || GetTile(result, x, y, definition).Type != type)
                {
                    continue;
                }

                count++;
                visited[index] = true;
                queue.Enqueue((x, y));
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in CardinalNeighbors(current.X, current.Y, definition))
                    {
                        var neighborIndex = (neighbor.Y * definition.TilesX) + neighbor.X;
                        if (!visited[neighborIndex] &&
                            GetTile(result, neighbor.X, neighbor.Y, definition).Type == type)
                        {
                            visited[neighborIndex] = true;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }

        return count;
    }

    private static void AssertRiverComponentsReachWater(
        CampaignMapGenerationResult result,
        CampaignWorldDefinition definition)
    {
        var visited = new HashSet<(int X, int Y)>();
        foreach (var entry in result.Tiles.Where(entry => entry.Data.Type.IsRiver()))
        {
            if (!visited.Add((entry.X, entry.Y)))
            {
                continue;
            }

            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((entry.X, entry.Y));
            var reachesWater = false;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in CardinalNeighbors(current.X, current.Y, definition))
                {
                    var type = GetTile(result, neighbor.X, neighbor.Y, definition).Type;
                    reachesWater |= IsWater(type);
                    if (type.IsRiver() && visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            Assert.True(reachesWater);
        }
    }

    private static int CountRiverNeighbors(CampaignWorld world, int x, int y) =>
        CardinalNeighbors(x, y, world.Definition)
            .Count(coordinate => world.Tiles.GetTile(coordinate.X, coordinate.Y).Type.IsRiver());

    private static IEnumerable<(int X, int Y)> CardinalNeighbors(
        int x,
        int y,
        CampaignWorldDefinition definition)
    {
        if (y > 0)
        {
            yield return (x, y - 1);
        }

        if (x + 1 < definition.TilesX)
        {
            yield return (x + 1, y);
        }

        if (y + 1 < definition.TilesY)
        {
            yield return (x, y + 1);
        }

        if (x > 0)
        {
            yield return (x - 1, y);
        }
    }
}
