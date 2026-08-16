using Kingdom.World.Core.Campaign.V3;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Tests;

public sealed class CampaignV3TerrainTests
{
    [Fact]
    public void AllCanonicalBaseSurfaces_CanBeStoredWithoutChangingVocabulary()
    {
        var world = CampaignV3TestWorldFactory.Create(1, 1);

        foreach (var surface in Enum.GetValues<CampaignSurfaceType>())
        {
            world.SetSurface(0, 0, surface);
            Assert.Equal(surface, world.Tiles.GetTile(0, 0).Surface);
        }
    }

    [Fact]
    public void TileMap_IsSparseAndResetReturnsToImplicitDefault()
    {
        var world = CampaignV3TestWorldFactory.Create(2, 2, defaultHeight: 25);

        Assert.Equal(
            new CampaignTileDataV3(CampaignSurfaceType.Unassigned, 25),
            world.Tiles.GetTile(1, 1));
        Assert.Equal(0, world.Tiles.MaterializedTileCount);

        world.SetTile(
            1,
            1,
            new CampaignTileDataV3(CampaignSurfaceType.Forest, 450));

        Assert.Equal(
            new CampaignTileDataV3(CampaignSurfaceType.Forest, 450),
            world.Tiles.GetTile(1, 1));
        Assert.Equal(1, world.Tiles.MaterializedTileCount);

        world.SetTile(1, 1, world.Tiles.DefaultTile);

        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(1, 1));
        Assert.Equal(0, world.Tiles.MaterializedTileCount);
    }

    [Fact]
    public void TileMap_RejectsUnknownSurfaceAndOutOfRangeHeight()
    {
        var world = CampaignV3TestWorldFactory.Create(1, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.SetSurface(0, 0, (CampaignSurfaceType)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            world.SetHeight(0, 0, 6_001));
    }

    [Theory]
    [InlineData(0, TerrainForm.Flat)]
    [InlineData(50, TerrainForm.Rolling)]
    [InlineData(200, TerrainForm.Hills)]
    [InlineData(600, TerrainForm.Mountain)]
    [InlineData(1_500, TerrainForm.Cliff)]
    public void TerrainForm_UsesDocumentedGradeThresholds(
        int centerHeight,
        TerrainForm expected)
    {
        var world = CampaignV3TestWorldFactory.Create(3, 3);
        CampaignV3TestWorldFactory.SetLand(world, 1, 1, (short)centerHeight);

        var analysis = world.AnalyzeTerrainForm(1, 1);

        Assert.Equal(expected, analysis.Form);
        Assert.Equal(centerHeight / 5_000.0, analysis.MaximumCardinalGrade, 12);
    }

    [Fact]
    public void TerrainForm_MountainCanBeDerivedFromAbsoluteElevationOnAFlatPlateau()
    {
        var world = CampaignV3TestWorldFactory.Create(3, 3);
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                CampaignV3TestWorldFactory.SetLand(world, x, y, 1_500);
            }
        }

        var analysis = world.AnalyzeTerrainForm(1, 1);

        Assert.Equal(TerrainForm.Mountain, analysis.Form);
        Assert.Equal(0, analysis.MaximumCardinalGrade);
        Assert.Equal(0, analysis.LocalReliefMeters);
        Assert.Equal(0, analysis.LocalProminenceMeters);
    }

    [Fact]
    public void TerrainForm_ClampsNeighborhoodAtWorldEdges()
    {
        var world = CampaignV3TestWorldFactory.Create(1, 1, defaultHeight: 1_000);

        var analysis = world.AnalyzeTerrainForm(0, 0);

        Assert.Equal(TerrainForm.Flat, analysis.Form);
        Assert.Equal(0, analysis.MaximumCardinalGrade);
        Assert.Equal(0, analysis.LocalReliefMeters);
    }

    [Fact]
    public void TerrainFormProfile_RejectsNonFiniteAndUnorderedThresholds()
    {
        var nonFinite = new TerrainFormProfile { RollingMinimumGrade = double.NaN };
        var unordered = new TerrainFormProfile
        {
            RollingMinimumGrade = 0.04,
            HillsMinimumGrade = 0.04,
        };

        Assert.Throws<WorldValidationException>(() => nonFinite.EnsureValid());
        Assert.Throws<WorldValidationException>(() => unordered.EnsureValid());
    }

    [Fact]
    public void DerivedHeight_RemainsContinuousBetweenTileCentres()
    {
        var world = CampaignV3TestWorldFactory.Create(2, 1);
        CampaignV3TestWorldFactory.SetLand(world, 0, 0, 100);
        CampaignV3TestWorldFactory.SetLand(world, 1, 0, 300);

        Assert.Equal(100, world.Tiles.GetDerivedHeight(0.5, 0.5));
        Assert.Equal(200, world.Tiles.GetDerivedHeight(1.0, 0.5));
        Assert.Equal(300, world.Tiles.GetDerivedHeight(1.5, 0.5));
    }
}
