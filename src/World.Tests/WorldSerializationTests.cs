using Kingdom.World.Core.Serialization;
using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Tests;

public sealed class WorldSerializationTests
{
    [Fact]
    public async Task SaveLoadRoundtrip_PreservesMetadataAndEveryTerrainValue()
    {
        using var temporary = new TemporaryDirectory();
        var terrain = TestWorldFactory.Create(samplesX: 10, samplesY: 7, chunkSize: 4, initialElevation: -5);
        terrain.CampaignTiles.SetTileType(0, 0, CampaignTileType.Plains);
        terrain.CampaignTiles.SetTileType(1, 1, CampaignTileType.Forest);
        for (var y = 0; y < terrain.Definition.HeightSamplesY; y++)
        {
            for (var x = 0; x < terrain.Definition.HeightSamplesX; x++)
            {
                if ((x + y) % 3 == 0)
                {
                    terrain.SetHeight(x, y, (short)(x * 37 - y * 19));
                }
            }
        }

        await WorldProjectSerializer.SaveAsync(terrain, temporary.Path);
        var loaded = await WorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.Equal(terrain.Definition, loaded.Definition);
        Assert.Equal(CampaignTileType.Plains, loaded.CampaignTiles.GetTileType(0, 0));
        Assert.Equal(CampaignTileType.Forest, loaded.CampaignTiles.GetTileType(1, 1));
        for (var y = 0; y < terrain.Definition.HeightSamplesY; y++)
        {
            for (var x = 0; x < terrain.Definition.HeightSamplesX; x++)
            {
                Assert.Equal(terrain.GetHeight(x, y), loaded.GetHeight(x, y));
            }
        }
    }

    [Fact]
    public async Task MissingCampaignTileFile_LoadsAsBackwardCompatibleUnassignedLayer()
    {
        using var temporary = new TemporaryDirectory();
        var terrain = TestWorldFactory.Create();
        terrain.CampaignTiles.SetTileType(0, 0, CampaignTileType.Mountain);
        await WorldProjectSerializer.SaveAsync(terrain, temporary.Path);
        File.Delete(System.IO.Path.Combine(temporary.Path, WorldProjectSerializer.CampaignTileFileName));

        var loaded = await WorldProjectSerializer.LoadAsync(temporary.Path);

        Assert.Equal(0, loaded.CampaignTiles.AssignedTileCount);
        Assert.Equal(CampaignTileType.Unassigned, loaded.CampaignTiles.GetTileType(0, 0));
    }

    [Fact]
    public async Task LoadRejectsDuplicateCampaignTileAssignments()
    {
        using var temporary = new TemporaryDirectory();
        var terrain = TestWorldFactory.Create();
        await WorldProjectSerializer.SaveAsync(terrain, temporary.Path);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(temporary.Path, WorldProjectSerializer.CampaignTileFileName),
            """
            {
              "version": 1,
              "tiles": [
                { "x": 0, "y": 0, "type": "plains" },
                { "x": 0, "y": 0, "type": "forest" }
              ]
            }
            """);

        await Assert.ThrowsAsync<WorldFormatException>(() => WorldProjectSerializer.LoadAsync(temporary.Path));
    }

    [Fact]
    public async Task LoadRejectsCampaignTileOutsideDerivedGrid()
    {
        using var temporary = new TemporaryDirectory();
        var terrain = TestWorldFactory.Create();
        await WorldProjectSerializer.SaveAsync(terrain, temporary.Path);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(temporary.Path, WorldProjectSerializer.CampaignTileFileName),
            """
            {
              "version": 1,
              "tiles": [
                { "x": 2, "y": 0, "type": "plains" }
              ]
            }
            """);

        await Assert.ThrowsAsync<WorldFormatException>(() => WorldProjectSerializer.LoadAsync(temporary.Path));
    }

    [Fact]
    public async Task ChunkFile_IsRawLittleEndianSignedInt16()
    {
        using var temporary = new TemporaryDirectory();
        var terrain = TestWorldFactory.Create(chunkSize: 4);
        terrain.SetHeight(0, 0, -400);

        await WorldProjectSerializer.SaveAsync(terrain, temporary.Path);
        var bytes = await File.ReadAllBytesAsync(System.IO.Path.Combine(temporary.Path, "chunks", "0_0.bin"));

        Assert.Equal(0x70, bytes[0]);
        Assert.Equal(0xFE, bytes[1]);
    }

    [Fact]
    public async Task LoadRejectsChunkWithWrongByteLength()
    {
        using var temporary = new TemporaryDirectory();
        var terrain = TestWorldFactory.Create(chunkSize: 4);
        terrain.SetHeight(0, 0, 1);
        await WorldProjectSerializer.SaveAsync(terrain, temporary.Path);
        await File.WriteAllBytesAsync(
            System.IO.Path.Combine(temporary.Path, "chunks", "0_0.bin"),
            [0x01]);

        await Assert.ThrowsAsync<WorldFormatException>(() => WorldProjectSerializer.LoadAsync(temporary.Path));
    }

    [Fact]
    public async Task LoadRejectsElevationOutsideConfiguredRange()
    {
        using var temporary = new TemporaryDirectory();
        var terrain = TestWorldFactory.Create(chunkSize: 4, maximumElevation: 100);
        terrain.SetHeight(0, 0, 1);
        await WorldProjectSerializer.SaveAsync(terrain, temporary.Path);
        var chunkPath = System.IO.Path.Combine(temporary.Path, "chunks", "0_0.bin");
        var bytes = await File.ReadAllBytesAsync(chunkPath);
        bytes[0] = 0xD0;
        bytes[1] = 0x07;
        await File.WriteAllBytesAsync(chunkPath, bytes);

        await Assert.ThrowsAsync<WorldFormatException>(() => WorldProjectSerializer.LoadAsync(temporary.Path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"KingdomWorldTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
