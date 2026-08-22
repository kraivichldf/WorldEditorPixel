using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonMapTests
{
    [Fact]
    public void Map_StartsEmptyAndSparse()
    {
        var map = CreateMap();

        Assert.Equal(64, map.TileCount);
        Assert.Equal(0, map.OccurrenceCount);
        Assert.Equal(0, map.MaterializedTileCount);
        Assert.Equal(0, map.LockedOccurrenceCount);
        Assert.Empty(map.GetOccurrences(2, 3));
        map.EnsureValid();
    }

    [Fact]
    public void Tile_CanContainThreeOrFourIndependentSeasons()
    {
        var map = CreateMap();

        map.Apply(
        [
            CampaignSeasonMutation.Upsert(2, 3, new("spring")),
            CampaignSeasonMutation.Upsert(2, 3, new("summer")),
            CampaignSeasonMutation.Upsert(2, 3, new("fall")),
            CampaignSeasonMutation.Upsert(4, 5, new("spring")),
            CampaignSeasonMutation.Upsert(4, 5, new("summer")),
            CampaignSeasonMutation.Upsert(4, 5, new("fall")),
            CampaignSeasonMutation.Upsert(4, 5, new("winter")),
        ]);

        Assert.Equal(["fall", "spring", "summer"],
            map.GetOccurrences(2, 3).Select(static value => value.SeasonId));
        Assert.Equal(["fall", "spring", "summer", "winter"],
            map.GetOccurrences(4, 5).Select(static value => value.SeasonId));
        Assert.Equal(7, map.OccurrenceCount);
        Assert.Equal(2, map.MaterializedTileCount);
    }

    [Fact]
    public void AddingAndRemovingSelectedSeason_DoesNotReplaceOtherSeasons()
    {
        var map = CreateMap();
        map.Upsert(1, 1, new("spring"));
        map.Upsert(1, 1, new("summer"));
        map.Upsert(1, 1, new("fall"));

        Assert.True(map.Upsert(1, 1, new("winter", Locked: true)));
        Assert.Equal(4, map.GetOccurrences(1, 1).Count);

        Assert.True(map.Remove(1, 1, "summer"));
        Assert.Equal(["fall", "spring", "winter"],
            map.GetOccurrences(1, 1).Select(static value => value.SeasonId));
        Assert.True(map.TryGetOccurrence(1, 1, "winter", out var winter));
        Assert.True(winter.Locked);
    }

    [Fact]
    public void LocksArePerOccurrenceAndNoOpsPreserveRevision()
    {
        var map = CreateMap();
        map.Upsert(1, 1, new("spring"));
        map.Upsert(1, 1, new("winter"));
        var revision = map.Revision;

        Assert.False(map.Upsert(1, 1, new("spring")));
        Assert.False(map.SetLocked(1, 1, "fall", locked: true));
        Assert.Equal(revision, map.Revision);

        Assert.True(map.SetLocked(1, 1, "winter", locked: true));
        Assert.Equal(revision + 1, map.Revision);
        Assert.True(map.TryGetOccurrence(1, 1, "winter", out var winter));
        Assert.True(winter.Locked);
        Assert.True(map.TryGetOccurrence(1, 1, "spring", out var spring));
        Assert.False(spring.Locked);
        Assert.Equal(1, map.LockedOccurrenceCount);
    }

    [Fact]
    public void Apply_ValidatesWholeBatchAndIdentityUniqueness()
    {
        var map = CreateMap();

        Assert.Throws<ArgumentException>(() => map.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("winter")),
            CampaignSeasonMutation.Upsert(1, 0, new("missing-season")),
        ]));
        Assert.Empty(map.GetMaterializedOccurrences());
        Assert.Equal(0, map.Revision);

        Assert.Throws<ArgumentException>(() => map.Apply(
        [
            CampaignSeasonMutation.Upsert(2, 2, new("winter")),
            CampaignSeasonMutation.Remove(2, 2, "winter"),
        ]));

        Assert.Equal(2, map.Apply(
        [
            CampaignSeasonMutation.Upsert(2, 2, new("winter")),
            CampaignSeasonMutation.Upsert(2, 2, new("summer")),
        ]));
        Assert.Equal(2, map.GetOccurrences(2, 2).Count);
    }

    [Fact]
    public void Entries_AreRowMajorThenSeasonIdAndAreaQueriesStayBounded()
    {
        var map = CreateMap();
        map.Apply(
        [
            CampaignSeasonMutation.Upsert(4, 3, new("winter", Locked: true)),
            CampaignSeasonMutation.Upsert(1, 1, new("summer")),
            CampaignSeasonMutation.Upsert(1, 1, new("fall")),
            CampaignSeasonMutation.Upsert(0, 3, new("spring")),
        ]);

        Assert.Equal(
        [
            (1, 1, "fall"),
            (1, 1, "summer"),
            (0, 3, "spring"),
            (4, 3, "winter"),
        ], map.GetMaterializedOccurrences()
            .Select(static entry => (entry.X, entry.Y, entry.Occurrence.SeasonId)));

        var area = map.GetOccurrences(new CampaignTileArea(0, 2, 4, 3));
        Assert.Equal([(0, 3, "spring"), (4, 3, "winter")],
            area.Select(static entry => (entry.X, entry.Y, entry.Occurrence.SeasonId)));
        Assert.Single(map.GetOccurrences(new CampaignTileArea(0, 0, 7, 7), "summer"));
    }

    [Fact]
    public void UsageCountsAreIndependentAndValidateRequestedIds()
    {
        var map = CreateMap();
        map.Apply(
        [
            CampaignSeasonMutation.Upsert(0, 0, new("winter")),
            CampaignSeasonMutation.Upsert(1, 0, new("winter")),
            CampaignSeasonMutation.Upsert(1, 0, new("summer")),
            CampaignSeasonMutation.Upsert(2, 0, new("summer")),
        ]);

        var counts = map.GetUsageCounts(["winter", "spring", "summer"]);

        Assert.Equal(2, counts["winter"]);
        Assert.Equal(2, counts["summer"]);
        Assert.Equal(0, counts["spring"]);
        Assert.Equal(["spring", "summer", "winter"], counts.Keys);
        Assert.Throws<ArgumentException>(() => map.GetUsageCounts(["winter", "winter"]));
        Assert.Throws<ArgumentException>(() => map.GetUsageCounts(["unknown-season"]));
    }

    [Fact]
    public void Map_RejectsUnknownIdsAndOutOfBoundsCoordinates()
    {
        var map = CreateMap();

        Assert.Throws<ArgumentException>(() => map.Upsert(0, 0, new("missing")));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetOccurrences(8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            map.GetOccurrences(new CampaignTileArea(0, 0, 8, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            map.Upsert(-1, 0, new("winter")));
    }

    [Fact]
    public void MaximumGeneratedGrid_RemainsSparseUntilOccurrencesAreAdded()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 10_000_000,
            worldHeightMeters: 10_000_000,
            campaignTileSizeMeters: 20_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var map = new CampaignSeasonMap(definition);

        Assert.Equal(250_000, map.TileCount);
        Assert.Equal(0, map.OccurrenceCount);
        Assert.Empty(map.GetOccurrences(499, 499));
        map.Upsert(499, 499, new("winter"));
        Assert.Single(map.GetOccurrences(499, 499));
    }

    internal static CampaignSeasonMap CreateMap(CampaignSeasonCatalog? catalog = null) =>
        new(
            CampaignWorldDefinition.Create(
                worldWidthMeters: 8_000,
                worldHeightMeters: 8_000,
                campaignTileSizeMeters: 1_000,
                seaLevelMeters: 0,
                minimumHeightMeters: -1_000,
                maximumHeightMeters: 6_000),
            catalog);
}
