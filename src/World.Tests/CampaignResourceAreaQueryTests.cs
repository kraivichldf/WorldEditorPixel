using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceAreaQueryTests
{
    [Fact]
    public void AreaQuery_ReturnsOnlyBoundedOccurrencesInStableOrder()
    {
        var map = CreateMap(8, 8);
        map.Apply(
        [
            CampaignResourceMutation.Upsert(4, 3, new CampaignResourceOccurrence("silver", 30)),
            CampaignResourceMutation.Upsert(1, 1, new CampaignResourceOccurrence("gold", 20)),
            CampaignResourceMutation.Upsert(4, 3, new CampaignResourceOccurrence("gold", 40)),
            CampaignResourceMutation.Upsert(0, 3, new CampaignResourceOccurrence("timber", 50)),
            CampaignResourceMutation.Upsert(7, 7, new CampaignResourceOccurrence("gold", 60)),
        ]);

        var entries = map.GetOccurrences(new CampaignTileArea(0, 1, 4, 3));

        Assert.Equal(
        [
            (1, 1, "gold", (byte)20),
            (0, 3, "timber", (byte)50),
            (4, 3, "gold", (byte)40),
            (4, 3, "silver", (byte)30),
        ], entries.Select(static entry =>
            (entry.X, entry.Y, entry.Occurrence.ResourceId, entry.Occurrence.Potential)));
    }

    [Fact]
    public void AreaQuery_OptionalResourceIdReturnsOnlyThatResource()
    {
        var map = CreateMap(4, 4);
        map.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("gold", 10)),
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("silver", 20)),
            CampaignResourceMutation.Upsert(2, 1, new CampaignResourceOccurrence("gold", 30)),
            CampaignResourceMutation.Upsert(3, 3, new CampaignResourceOccurrence("gold", 40)),
        ]);

        var entries = map.GetOccurrences(new CampaignTileArea(0, 0, 2, 2), "gold");

        Assert.Equal(
        [
            (0, 0, (byte)10),
            (2, 1, (byte)30),
        ], entries.Select(static entry =>
            (entry.X, entry.Y, entry.Occurrence.Potential)));
    }

    [Fact]
    public void AreaQuery_DenseMapAndSparseMapProduceTheSameOrderingContract()
    {
        var sparse = CreateMap(10, 10);
        var dense = CreateMap(10, 10);
        var target = new CampaignTileArea(4, 4, 5, 5);
        var targetMutations = new[]
        {
            CampaignResourceMutation.Upsert(5, 4, new CampaignResourceOccurrence("silver", 44)),
            CampaignResourceMutation.Upsert(4, 5, new CampaignResourceOccurrence("gold", 55)),
            CampaignResourceMutation.Upsert(5, 5, new CampaignResourceOccurrence("gold", 66)),
        };
        sparse.Apply(targetMutations);
        dense.Apply(targetMutations);
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                if (x is >= 4 and <= 5 && y is >= 4 and <= 5)
                {
                    continue;
                }

                dense.Upsert(x, y, new CampaignResourceOccurrence("timber", 25));
            }
        }

        var sparseResult = sparse.GetOccurrences(target);
        var denseResult = dense.GetOccurrences(target);

        Assert.Equal(sparseResult, denseResult);
    }

    [Fact]
    public void AreaQuery_RejectsOutOfBoundsAndUnknownResourceWithoutMutation()
    {
        var map = CreateMap(4, 4);
        map.Upsert(1, 1, new CampaignResourceOccurrence("gold", 25));
        var revision = map.Revision;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            map.GetOccurrences(new CampaignTileArea(-1, 0, 1, 1)));
        Assert.Throws<ArgumentException>(() =>
            map.GetOccurrences(new CampaignTileArea(0, 0, 1, 1), "unknown-resource"));
        Assert.Equal(revision, map.Revision);
        Assert.Equal(1, map.OccurrenceCount);
    }

    private static CampaignResourceMap CreateMap(int tilesX, int tilesY) =>
        new(CampaignWorldDefinition.Create(
            worldWidthMeters: tilesX * 1_000L,
            worldHeightMeters: tilesY * 1_000L,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000));
}
