using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignSeasonCommandTests
{
    [Fact]
    public void Stroke_PaintLockResetAndRepeatAreOneUndoableCommand()
    {
        var seasons = CampaignSeasonMapTests.CreateMap();
        var stroke = new CampaignSeasonStrokeBuilder(seasons);
        stroke.Paint(new CampaignTileCoordinate(2, 2), "winter", locked: true);
        stroke.SetLocked(new CampaignTileCoordinate(2, 2), locked: false);
        stroke.Paint(new CampaignTileCoordinate(3, 2), "autumn", locked: true);
        stroke.Paint(new CampaignTileCoordinate(3, 2), "summer", locked: true);
        var revisionAfterLiveEdit = seasons.Revision;
        var command = stroke.Complete("Paint seasons");
        var history = new CommandHistory();
        history.RecordExecuted(command);

        Assert.Equal(2, command.Changes.Count);
        Assert.Equal(revisionAfterLiveEdit, seasons.Revision);
        Assert.Equal(new CampaignSeasonTile("winter"), seasons.GetTile(2, 2));
        Assert.Equal(new CampaignSeasonTile("summer", Locked: true), seasons.GetTile(3, 2));

        Assert.True(history.Undo());
        Assert.Equal(new CampaignSeasonTile("spring"), seasons.GetTile(2, 2));
        Assert.Equal(new CampaignSeasonTile("spring"), seasons.GetTile(3, 2));

        Assert.True(history.Redo());
        Assert.Equal(new CampaignSeasonTile("winter"), seasons.GetTile(2, 2));
        Assert.Equal(new CampaignSeasonTile("summer", Locked: true), seasons.GetTile(3, 2));
    }

    [Fact]
    public void Stroke_CancelRestoresInitialValuesAndClosesBuilder()
    {
        var seasons = CampaignSeasonMapTests.CreateMap();
        seasons.Paint(1, 1, "autumn", locked: true);
        var stroke = new CampaignSeasonStrokeBuilder(seasons);
        stroke.Paint(new CampaignTileCoordinate(1, 1), "winter", locked: false);
        stroke.Paint(new CampaignTileCoordinate(2, 1), "summer", locked: true);

        stroke.Cancel();

        Assert.Equal(new CampaignSeasonTile("autumn", Locked: true), seasons.GetTile(1, 1));
        Assert.Equal(new CampaignSeasonTile("spring"), seasons.GetTile(2, 1));
        Assert.Throws<ObjectDisposedException>(() => stroke.ResetToDefault(new CampaignTileCoordinate(0, 0)));
    }

    [Fact]
    public void EmptySeasonCommandPreservesExistingRedoBranch()
    {
        var seasons = CampaignSeasonMapTests.CreateMap();
        var history = new CommandHistory();
        history.Execute(new CampaignSeasonEditCommand(
            seasons,
            "Set winter",
            [
                new CampaignSeasonChange(
                    0,
                    0,
                    new CampaignSeasonTile("spring"),
                    new CampaignSeasonTile("winter")),
            ]));
        Assert.True(history.Undo());

        history.RecordExecuted(new CampaignSeasonEditCommand(
            seasons,
            "No season change",
            [
                new CampaignSeasonChange(
                    1,
                    1,
                    new CampaignSeasonTile("spring"),
                    new CampaignSeasonTile("spring")),
            ]));

        Assert.True(history.CanRedo);
        Assert.True(history.Redo());
        Assert.Equal(new CampaignSeasonTile("winter"), seasons.GetTile(0, 0));
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
        seasonStroke.Paint(new CampaignTileCoordinate(0, 0), "winter");
        history.RecordExecuted(seasonStroke.Complete("Paint season"));

        Assert.True(history.Undo());
        Assert.Equal(new CampaignSeasonTile("spring"), seasons.GetTile(0, 0));
        Assert.Equal(CampaignTileType.Forest, world.Tiles.GetTile(0, 0).Type);
        Assert.True(history.Undo());
        Assert.Equal(world.Tiles.DefaultTile, world.Tiles.GetTile(0, 0));
        Assert.True(history.Redo());
        Assert.True(history.Redo());
        Assert.Equal(CampaignTileType.Forest, world.Tiles.GetTile(0, 0).Type);
        Assert.Equal(new CampaignSeasonTile("winter", Locked: true), seasons.GetTile(0, 0));
    }

    [Fact]
    public void CommandRejectsDuplicatesUnknownIdsAndCoordinatesBeforeMutation()
    {
        var seasons = CampaignSeasonMapTests.CreateMap();

        Assert.Throws<ArgumentException>(() => new CampaignSeasonEditCommand(
            seasons,
            "Duplicates",
            [
                new CampaignSeasonChange(0, 0, new("spring"), new("winter")),
                new CampaignSeasonChange(0, 0, new("spring"), new("summer")),
            ]));
        Assert.Throws<ArgumentException>(() => new CampaignSeasonEditCommand(
            seasons,
            "Unknown",
            [new CampaignSeasonChange(0, 0, new("spring"), new("unknown-season"))]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSeasonEditCommand(
            seasons,
            "Outside",
            [new CampaignSeasonChange(8, 0, new("spring"), new("winter"))]));
        Assert.Equal(0, seasons.Revision);
    }
}
