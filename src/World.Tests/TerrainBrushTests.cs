using Kingdom.World.Core.Brushes;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class TerrainBrushTests
{
    private static readonly TerrainCoordinate Center = new(4, 4);

    [Fact]
    public void RaiseBrush_RaisesCenterWithRadialFalloff()
    {
        var terrain = TestWorldFactory.Create();
        var settings = Settings(strength: 20, radius: 2.5);

        new RaiseTerrainBrush().Apply(terrain, Center, settings);

        Assert.Equal(20, terrain.GetHeight(4, 4));
        Assert.InRange(terrain.GetHeight(5, 4), (short)1, (short)19);
        Assert.Equal(0, terrain.GetHeight(7, 4));
    }

    [Fact]
    public void LowerBrush_LowersCenter()
    {
        var terrain = TestWorldFactory.Create(initialElevation: 100);

        new LowerTerrainBrush().Apply(terrain, Center, Settings(strength: 35));

        Assert.Equal(65, terrain.GetHeight(4, 4));
    }

    [Fact]
    public void FlattenBrush_MovesTowardTargetWithoutOvershoot()
    {
        var terrain = TestWorldFactory.Create(initialElevation: 10);
        var settings = Settings(strength: 25) with { TargetElevationMeters = 100 };
        var brush = new FlattenTerrainBrush();

        brush.Apply(terrain, Center, settings);
        Assert.Equal(35, terrain.GetHeight(4, 4));

        for (var index = 0; index < 10; index++)
        {
            brush.Apply(terrain, Center, settings);
        }

        Assert.Equal(100, terrain.GetHeight(4, 4));
    }

    [Fact]
    public void SmoothBrush_UsesNeighborSnapshotAndReducesSpike()
    {
        var terrain = TestWorldFactory.Create();
        terrain.SetHeight(4, 4, 900);

        new SmoothTerrainBrush().Apply(terrain, Center, Settings(strength: 100, radius: 2));

        Assert.Equal(800, terrain.GetHeight(4, 4));
        Assert.True(terrain.GetHeight(5, 4) > 0);
        Assert.Equal(terrain.GetHeight(3, 4), terrain.GetHeight(5, 4));
    }

    [Fact]
    public void Brushes_ClampToWorldElevationRange()
    {
        var terrain = TestWorldFactory.Create(
            initialElevation: 95,
            minimumElevation: -100,
            maximumElevation: 100);

        new RaiseTerrainBrush().Apply(terrain, Center, Settings(strength: 500));
        Assert.Equal(100, terrain.GetHeight(4, 4));

        new LowerTerrainBrush().Apply(terrain, Center, Settings(strength: 500));
        Assert.Equal(-100, terrain.GetHeight(4, 4));
    }

    private static BrushSettings Settings(double strength, double radius = 1.5) => new()
    {
        RadiusSamples = radius,
        StrengthMeters = strength,
        Falloff = 0.5,
    };
}
