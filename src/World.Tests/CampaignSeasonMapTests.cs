using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonMapTests
{
    [Fact]
    public void Map_IsCompleteSpringAndUnlockedByDefault()
    {
        var map = CreateMap();

        Assert.Equal(64, map.TileCount);
        Assert.Equal(64, map.GetUsageCount("spring"));
        Assert.Equal(0, map.LockedTileCount);
        Assert.All(map.GetAllTiles(), static entry =>
            Assert.Equal(new CampaignSeasonTile("spring"), entry.Tile));
        map.EnsureValid();
    }

    [Fact]
    public void Map_AllowsCustomDefaultPaintLockAndReset()
    {
        var monsoon = CampaignSeasonCatalogTests.CreateCustom("monsoon");
        var catalog = new CampaignSeasonCatalog([monsoon]);
        var map = CreateMap(catalog, defaultSeasonId: "monsoon");

        Assert.Equal(new CampaignSeasonTile("monsoon"), map.GetTile(2, 3));
        Assert.True(map.Paint(2, 3, "winter", locked: true));
        Assert.Equal(new CampaignSeasonTile("winter", Locked: true), map.GetTile(2, 3));
        Assert.Equal(1, map.LockedTileCount);
        Assert.True(map.ResetToDefault(2, 3));
        Assert.Equal(new CampaignSeasonTile("monsoon"), map.GetTile(2, 3));
        Assert.Equal(0, map.LockedTileCount);
    }

    [Fact]
    public void Map_NoOpDoesNotChangeRevisionAndLockOnlyEditDoes()
    {
        var map = CreateMap();
        var revision = map.Revision;

        Assert.False(map.SetTile(1, 1, new CampaignSeasonTile("spring")));
        Assert.Equal(revision, map.Revision);
        Assert.True(map.SetLocked(1, 1, locked: true));
        Assert.Equal(revision + 1, map.Revision);
        Assert.Equal(new CampaignSeasonTile("spring", Locked: true), map.GetTile(1, 1));
        Assert.False(map.SetLocked(1, 1, locked: true));
        Assert.Equal(revision + 1, map.Revision);
    }

    [Fact]
    public void Apply_ValidatesWholeBatchAndRejectsDuplicateCoordinates()
    {
        var map = CreateMap();
        var invalid = new[]
        {
            new CampaignSeasonMutation(0, 0, new CampaignSeasonTile("winter")),
            new CampaignSeasonMutation(1, 0, new CampaignSeasonTile("missing-season")),
        };

        Assert.Throws<ArgumentException>(() => map.Apply(invalid));
        Assert.Equal(new CampaignSeasonTile("spring"), map.GetTile(0, 0));
        Assert.Equal(0, map.Revision);

        Assert.Throws<ArgumentException>(() => map.Apply(
        [
            new CampaignSeasonMutation(2, 2, new CampaignSeasonTile("winter")),
            new CampaignSeasonMutation(2, 2, new CampaignSeasonTile("summer")),
        ]));
        Assert.Equal(new CampaignSeasonTile("spring"), map.GetTile(2, 2));
        Assert.Equal(0, map.Revision);
    }

    [Fact]
    public void Entries_AreRowMajorAndAreaQueriesStayBounded()
    {
        var map = CreateMap();
        map.Apply(
        [
            new CampaignSeasonMutation(4, 3, new CampaignSeasonTile("winter", Locked: true)),
            new CampaignSeasonMutation(1, 1, new CampaignSeasonTile("autumn")),
            new CampaignSeasonMutation(0, 3, new CampaignSeasonTile("summer")),
        ]);

        Assert.Equal(
        [
            (1, 1, "autumn"),
            (0, 3, "summer"),
            (4, 3, "winter"),
        ], map.GetAllTiles()
            .Where(static entry => entry.Tile.SeasonId != "spring")
            .Select(static entry => (entry.X, entry.Y, entry.Tile.SeasonId)));

        var area = map.GetTiles(new CampaignTileArea(0, 2, 4, 3));
        Assert.Equal(10, area.Count);
        Assert.Equal((0, 2), (area[0].X, area[0].Y));
        Assert.Equal((4, 3), (area[^1].X, area[^1].Y));
        Assert.DoesNotContain(area, static entry => entry.Y is < 2 or > 3);
    }

    [Fact]
    public void UsageCountsUseOneDensePassAndValidateRequestedIds()
    {
        var map = CreateMap();
        map.Apply(
        [
            new CampaignSeasonMutation(0, 0, new CampaignSeasonTile("winter")),
            new CampaignSeasonMutation(1, 0, new CampaignSeasonTile("winter")),
            new CampaignSeasonMutation(2, 0, new CampaignSeasonTile("summer")),
        ]);

        var counts = map.GetUsageCounts(["winter", "spring", "summer"]);

        Assert.Equal(2, counts["winter"]);
        Assert.Equal(1, counts["summer"]);
        Assert.Equal(61, counts["spring"]);
        Assert.Equal(["spring", "summer", "winter"], counts.Keys);
        Assert.Throws<ArgumentException>(() => map.GetUsageCounts(["winter", "winter"]));
        Assert.Throws<ArgumentException>(() => map.GetUsageCounts(["unknown-season"]));
    }

    [Fact]
    public void Map_RejectsUnknownDefaultAndOutOfBoundsCoordinates()
    {
        Assert.Throws<ArgumentException>(() => CreateMap(defaultSeasonId: "missing"));
        var map = CreateMap();
        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetTile(8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetTiles(new CampaignTileArea(0, 0, 8, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            map.SetTile(-1, 0, new CampaignSeasonTile("winter")));
    }

    [Fact]
    public void Map_SupportsTheCurrentMaximumGeneratedGridAsDenseAuthority()
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
        Assert.Equal(250_000, map.GetUsageCount("spring"));
        Assert.Equal(new CampaignSeasonTile("spring"), map.GetTile(499, 499));
    }

    internal static CampaignSeasonMap CreateMap(
        CampaignSeasonCatalog? catalog = null,
        string defaultSeasonId = CampaignSeasonCatalog.SpringId) =>
        new(
            CampaignWorldDefinition.Create(
                worldWidthMeters: 8_000,
                worldHeightMeters: 8_000,
                campaignTileSizeMeters: 1_000,
                seaLevelMeters: 0,
                minimumHeightMeters: -1_000,
                maximumHeightMeters: 6_000),
            catalog,
            defaultSeasonId);
}
