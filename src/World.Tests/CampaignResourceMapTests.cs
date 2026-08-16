using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceMapTests
{
    [Fact]
    public void Map_AllowsManyDifferentResourcesPerTileAndUpdatesOnlySelectedId()
    {
        var map = CreateMap();

        Assert.True(map.Upsert(2, 3, new CampaignResourceOccurrence("iron-ore", 72, Locked: true)));
        Assert.True(map.Upsert(2, 3, new CampaignResourceOccurrence("fresh-water", 41)));
        Assert.True(map.Upsert(2, 3, new CampaignResourceOccurrence("iron-ore", 80, Locked: true)));

        Assert.Equal(2, map.OccurrenceCount);
        Assert.Equal(1, map.MaterializedTileCount);
        Assert.Equal(
            ["fresh-water", "iron-ore"],
            map.GetOccurrences(2, 3).Select(static occurrence => occurrence.ResourceId));
        Assert.True(map.TryGetOccurrence(2, 3, "iron-ore", out var iron));
        Assert.Equal((byte)80, iron.Potential);
        Assert.True(iron.Locked);
        Assert.True(map.TryGetOccurrence(2, 3, "fresh-water", out var water));
        Assert.Equal((byte)41, water.Potential);
    }

    [Fact]
    public void Map_NoOpMutationsDoNotChangeRevisionAndLastRemovalClearsTile()
    {
        var map = CreateMap();
        var occurrence = new CampaignResourceOccurrence("gold", 30);

        Assert.True(map.Upsert(1, 1, occurrence));
        var revision = map.Revision;
        Assert.False(map.Upsert(1, 1, occurrence));
        Assert.False(map.Remove(1, 1, "silver"));
        Assert.Equal(revision, map.Revision);

        Assert.True(map.Remove(1, 1, "gold"));
        Assert.Equal(0, map.OccurrenceCount);
        Assert.Equal(0, map.MaterializedTileCount);
    }

    [Fact]
    public void Map_RejectsInvalidPotentialUnknownIdsAndCoordinates()
    {
        var map = CreateMap();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            map.Upsert(0, 0, new CampaignResourceOccurrence("gold", 0)));
        Assert.Throws<ArgumentException>(() =>
            map.Upsert(0, 0, new CampaignResourceOccurrence("unknown-resource", 20)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            map.Upsert(8, 0, new CampaignResourceOccurrence("gold", 20)));
        Assert.Equal(0, map.OccurrenceCount);
    }

    [Fact]
    public void Apply_ValidatesWholeBatchBeforeMutating()
    {
        var map = CreateMap();
        var mutations = new[]
        {
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("gold", 20)),
            CampaignResourceMutation.Upsert(0, 1, new CampaignResourceOccurrence("unknown-resource", 30)),
        };

        Assert.Throws<ArgumentException>(() => map.Apply(mutations));
        Assert.Equal(0, map.OccurrenceCount);
        Assert.Equal(0, map.Revision);
    }

    [Fact]
    public void Apply_RejectsDuplicateCompositeIdentityButAllowsSameTileDifferentIds()
    {
        var map = CreateMap();
        Assert.Equal(2, map.Apply(
        [
            CampaignResourceMutation.Upsert(3, 2, new CampaignResourceOccurrence("gold", 20)),
            CampaignResourceMutation.Upsert(3, 2, new CampaignResourceOccurrence("silver", 30)),
        ]));

        var revision = map.Revision;
        Assert.Throws<ArgumentException>(() => map.Apply(
        [
            CampaignResourceMutation.Upsert(4, 2, new CampaignResourceOccurrence("gold", 40)),
            CampaignResourceMutation.Remove(4, 2, "gold"),
        ]));
        Assert.Equal(revision, map.Revision);
        Assert.False(map.TryGetOccurrence(4, 2, "gold", out _));
    }

    [Fact]
    public void MaterializedOccurrences_AreDeterministicallySortedByYThenXThenId()
    {
        var map = CreateMap();
        map.Apply(
        [
            CampaignResourceMutation.Upsert(4, 3, new CampaignResourceOccurrence("silver", 30)),
            CampaignResourceMutation.Upsert(1, 1, new CampaignResourceOccurrence("gold", 20)),
            CampaignResourceMutation.Upsert(4, 3, new CampaignResourceOccurrence("gold", 40)),
            CampaignResourceMutation.Upsert(0, 3, new CampaignResourceOccurrence("timber", 50)),
        ]);

        Assert.Equal(
        [
            (1, 1, "gold"),
            (0, 3, "timber"),
            (4, 3, "gold"),
            (4, 3, "silver"),
        ], map.GetMaterializedOccurrences()
            .Select(static entry => (entry.X, entry.Y, entry.Occurrence.ResourceId)));
        map.EnsureValid();
    }

    [Fact]
    public void UsageCounts_CountRequestedResourcesInOneSparsePassAndValidateIds()
    {
        var map = CreateMap();
        map.Apply(
        [
            CampaignResourceMutation.Upsert(1, 1, new CampaignResourceOccurrence("gold", 20)),
            CampaignResourceMutation.Upsert(2, 1, new CampaignResourceOccurrence("gold", 30)),
            CampaignResourceMutation.Upsert(2, 1, new CampaignResourceOccurrence("silver", 40)),
            CampaignResourceMutation.Upsert(3, 3, new CampaignResourceOccurrence("timber", 50)),
        ]);

        var counts = map.GetUsageCounts(["silver", "gold", "fish"]);

        Assert.Equal(2, counts["gold"]);
        Assert.Equal(1, counts["silver"]);
        Assert.Equal(0, counts["fish"]);
        Assert.Equal(["fish", "gold", "silver"], counts.Keys);
        Assert.Throws<ArgumentException>(() => map.GetUsageCounts(["gold", "gold"]));
        Assert.Throws<ArgumentException>(() => map.GetUsageCounts(["unknown-resource"]));
    }

    private static CampaignResourceMap CreateMap() =>
        new(CampaignWorldDefinition.Create(
            worldWidthMeters: 8_000,
            worldHeightMeters: 8_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000));
}
