using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonCommandTests
{
    [Fact]
    public void Stroke_AddLockRemoveAndRepeatAreOneUndoableCommand()
    {
        var seasons = CampaignSeasonMapTests.CreateMap();
        seasons.Upsert(2, 2, new("spring"));
        var baselineRevision = seasons.Revision;
        var stroke = new CampaignSeasonStrokeBuilder(seasons);
        stroke.Upsert(new CampaignTileCoordinate(2, 2), "winter", locked: true);
        stroke.SetLocked(new CampaignTileCoordinate(2, 2), "winter", locked: false);
        stroke.Upsert(new CampaignTileCoordinate(3, 2), "fall", locked: true);
        stroke.Remove(new CampaignTileCoordinate(3, 2), "fall");
        stroke.Upsert(new CampaignTileCoordinate(3, 2), "summer", locked: true);
        var revisionAfterLiveEdit = seasons.Revision;
        var command = stroke.Complete("Edit season occurrences");
        var history = new CommandHistory();
        history.RecordExecuted(command);

        Assert.Equal(2, command.Changes.Count);
        Assert.True(revisionAfterLiveEdit > baselineRevision);
        Assert.True(seasons.TryGetOccurrence(2, 2, "spring", out _));
        Assert.True(seasons.TryGetOccurrence(2, 2, "winter", out var winter));
        Assert.False(winter.Locked);
        Assert.True(seasons.TryGetOccurrence(3, 2, "summer", out var summer));
        Assert.True(summer.Locked);

        Assert.True(history.Undo());
        Assert.True(seasons.TryGetOccurrence(2, 2, "spring", out _));
        Assert.False(seasons.TryGetOccurrence(2, 2, "winter", out _));
        Assert.Empty(seasons.GetOccurrences(3, 2));

        Assert.True(history.Redo());
        Assert.Equal(["spring", "winter"],
            seasons.GetOccurrences(2, 2).Select(static value => value.SeasonId));
        Assert.True(seasons.TryGetOccurrence(3, 2, "summer", out summer));
        Assert.True(summer.Locked);
    }

    [Fact]
    public void Stroke_CancelRestoresEveryIndependentIdentityAndClosesBuilder()
    {
        var seasons = CampaignSeasonMapTests.CreateMap();
        seasons.Upsert(1, 1, new("fall", Locked: true));
        seasons.Upsert(1, 1, new("spring"));
        var stroke = new CampaignSeasonStrokeBuilder(seasons);
        stroke.Remove(new CampaignTileCoordinate(1, 1), "fall");
        stroke.Upsert(new CampaignTileCoordinate(1, 1), "winter");
        stroke.Upsert(new CampaignTileCoordinate(2, 1), "summer", locked: true);

        stroke.Cancel();

        Assert.Equal(["fall", "spring"],
            seasons.GetOccurrences(1, 1).Select(static value => value.SeasonId));
        Assert.True(seasons.TryGetOccurrence(1, 1, "fall", out var fall));
        Assert.True(fall.Locked);
        Assert.Empty(seasons.GetOccurrences(2, 1));
        Assert.Throws<ObjectDisposedException>(() =>
            stroke.Remove(new CampaignTileCoordinate(0, 0), "spring"));
    }

    [Fact]
    public void EmptySeasonCommandPreservesExistingRedoBranch()
    {
        var seasons = CampaignSeasonMapTests.CreateMap();
        var history = new CommandHistory();
        history.Execute(new CampaignSeasonEditCommand(
            seasons,
            "Add winter",
            [new CampaignSeasonChange(0, 0, "winter", null, new("winter"))]));
        Assert.True(history.Undo());

        history.RecordExecuted(new CampaignSeasonEditCommand(
            seasons,
            "No season change",
            [new CampaignSeasonChange(1, 1, "spring", null, null)]));

        Assert.True(history.CanRedo);
        Assert.True(history.Redo());
        Assert.True(seasons.TryGetOccurrence(0, 0, "winter", out _));
    }

    [Fact]
    public void SeasonAndTerrainCommandsInterleaveInSharedHistory()
    {
        var definition = CampaignWorldDefinition.Create(
            worldWidthMeters: 2_000,
            worldHeightMeters: 1_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
        var world = new CampaignWorld(definition);
        var seasons = new CampaignSeasonMap(definition);
        var history = new CommandHistory();

        var terrainStroke = new CampaignTileStampBuilder(world.Tiles);
        terrainStroke.ApplyTile(
            new CampaignTileCoordinate(0, 0),
            new CampaignTileData(CampaignTileType.Forest, 300));
        history.RecordExecuted(terrainStroke.Complete("Paint terrain"));

        var seasonStroke = new CampaignSeasonStrokeBuilder(seasons);
        seasonStroke.Upsert(new CampaignTileCoordinate(0, 0), "winter");
        history.RecordExecuted(seasonStroke.Complete("Add winter"));

        Assert.True(history.Undo());
        Assert.Empty(seasons.GetOccurrences(0, 0));
        Assert.Equal(CampaignTileType.Forest, world.Tiles.GetTile(0, 0).Type);
        Assert.True(history.Undo());
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(0, 0));
        Assert.True(history.Redo());
        Assert.True(history.Redo());
        Assert.Equal(CampaignTileType.Forest, world.Tiles.GetTile(0, 0).Type);
        Assert.True(seasons.TryGetOccurrence(0, 0, "winter", out var winter));
        Assert.True(winter.Locked);
    }

    [Fact]
    public void CommandRejectsDuplicateIdentitiesUnknownIdsAndCoordinates()
    {
        var seasons = CampaignSeasonMapTests.CreateMap();

        Assert.Throws<ArgumentException>(() => new CampaignSeasonEditCommand(
            seasons,
            "Duplicates",
            [
                new CampaignSeasonChange(0, 0, "winter", null, new("winter")),
                new CampaignSeasonChange(0, 0, "winter", null, new("winter", true)),
            ]));
        Assert.Throws<ArgumentException>(() => new CampaignSeasonEditCommand(
            seasons,
            "Unknown",
            [new CampaignSeasonChange(0, 0, "unknown-season", null, new("unknown-season"))]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonEditCommand(
            seasons,
            "Outside",
            [new CampaignSeasonChange(8, 0, "winter", null, new("winter"))]));
        Assert.Equal(0, seasons.Revision);
    }
}
