using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class WorldUnitsTests
{
    [Theory]
    [InlineData(1, 1_000)]
    [InlineData(10, 10_000)]
    [InlineData(1_000, 1_000_000)]
    public void KilometersToMeters_UsesExactThousandMeterScale(long kilometers, long expectedMeters)
    {
        Assert.Equal(expectedMeters, WorldUnits.KilometersToMeters(kilometers));
    }

    [Fact]
    public void MetersToKilometers_PreservesSubKilometerDistances()
    {
        Assert.Equal(0.25m, WorldUnits.MetersToKilometers(250));
    }

    [Fact]
    public void KilometersToMeters_RejectsOverflow()
    {
        Assert.Throws<OverflowException>(() => WorldUnits.KilometersToMeters(long.MaxValue));
    }
}
