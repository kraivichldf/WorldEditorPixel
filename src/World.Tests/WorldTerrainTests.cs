using Kingdom.World.Core.Chunks;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Tests;

public sealed class WorldTerrainTests
{
    [Fact]
    public void CoordinateValidation_IncludesBothWorldEndpoints()
    {
        var terrain = TestWorldFactory.Create(samplesX: 9, samplesY: 7);

        Assert.True(terrain.IsValidCoordinate(0, 0));
        Assert.True(terrain.IsValidCoordinate(8, 6));
        Assert.False(terrain.IsValidCoordinate(-1, 0));
        Assert.False(terrain.IsValidCoordinate(9, 0));
        Assert.False(terrain.IsValidCoordinate(0, 7));
    }

    [Fact]
    public void InvalidCoordinate_GetAndSetThrow()
    {
        var terrain = TestWorldFactory.Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.GetHeight(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.SetHeight(9, 0, 20));
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(3, 0, 0, 3)]
    [InlineData(4, 1, 0, 0)]
    [InlineData(9, 2, 0, 1)]
    public void GlobalCoordinate_MapsToExpectedChunkAndLocalX(
        int globalX,
        int expectedChunkX,
        int expectedChunkY,
        int expectedLocalX)
    {
        var address = TerrainChunkAddress.FromGlobal(globalX, 0, chunkSize: 4);

        Assert.Equal(expectedChunkX, address.ChunkX);
        Assert.Equal(expectedChunkY, address.ChunkY);
        Assert.Equal(expectedLocalX, address.LocalX);
        Assert.Equal(0, address.LocalY);
    }

    [Fact]
    public void HeightSetAndGet_MaterializesOnlyOwningChunks()
    {
        var terrain = TestWorldFactory.Create(chunkSize: 4, initialElevation: 12);

        Assert.Equal(12, terrain.GetHeight(2, 2));
        Assert.Equal(0, terrain.AllocatedChunkCount);

        terrain.SetHeight(2, 2, 345);

        Assert.Equal(345, terrain.GetHeight(2, 2));
        Assert.Equal(12, terrain.GetHeight(3, 2));
        Assert.Equal(1, terrain.AllocatedChunkCount);
    }

    [Fact]
    public void ChunkBoundaryEditing_IsSeamlessAndIndependent()
    {
        var terrain = TestWorldFactory.Create(chunkSize: 4);

        terrain.SetHeight(3, 2, 111);
        terrain.SetHeight(4, 2, 222);

        Assert.Equal(111, terrain.GetHeight(3, 2));
        Assert.Equal(222, terrain.GetHeight(4, 2));
        Assert.Equal(2, terrain.AllocatedChunkCount);
        Assert.Equal(new TerrainChunkAddress(0, 0, 3, 2), terrain.ResolveAddress(3, 2));
        Assert.Equal(new TerrainChunkAddress(1, 0, 0, 2), terrain.ResolveAddress(4, 2));
    }

    [Fact]
    public void CampaignTileAtFinalSample_StaysInsideLastTile()
    {
        var terrain = TestWorldFactory.Create(samplesX: 9, samplesY: 9);

        var tile = terrain.Definition.GetCampaignTile(new Kingdom.World.Core.Models.TerrainCoordinate(8, 8));

        Assert.Equal((1, 1), tile);
    }

    [Fact]
    public void WorldDefinition_RejectsCampaignCoordinatesBeyondInt32Range()
    {
        var spacing = int.MaxValue;
        var width = (int.MaxValue - 1L) * spacing;

        Assert.Throws<WorldValidationException>(() => WorldDefinition.Create(
            width,
            width,
            spacing,
            campaignTileSizeMeters: 1,
            seaLevelMeters: 0,
            minimumElevationMeters: -100,
            maximumElevationMeters: 100));
    }
}
