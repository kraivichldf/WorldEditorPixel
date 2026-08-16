using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CampaignResourceCommandTests
{
    [Fact]
    public void Stroke_AddUpdateRemoveAndLock_AreOneUndoableSameTileEdit()
    {
        var resources = CreateMap();
        var originalIron = new CampaignResourceOccurrence("iron-ore", 25);
        var originalSilver = new CampaignResourceOccurrence("silver", 30, Locked: true);
        var unrelatedTimber = new CampaignResourceOccurrence("timber", 55);
        resources.Apply(
        [
            CampaignResourceMutation.Upsert(1, 1, originalIron),
            CampaignResourceMutation.Upsert(1, 1, originalSilver),
            CampaignResourceMutation.Upsert(3, 3, unrelatedTimber),
        ]);

        var stroke = new CampaignResourceStrokeBuilder(resources);
        stroke.Upsert(1, 1, new CampaignResourceOccurrence("gold", 42, Locked: true));
        stroke.Upsert(1, 1, new CampaignResourceOccurrence("iron-ore", 76, Locked: true));
        stroke.Remove(1, 1, "silver");
        var revisionAfterLiveEdit = resources.Revision;
        var command = stroke.Complete("Paint resources");
        var history = new CommandHistory();
        history.RecordExecuted(command);

        Assert.Equal(revisionAfterLiveEdit, resources.Revision);
        Assert.Equal(["gold", "iron-ore", "silver"], command.Changes.Select(static change => change.ResourceId));
        AssertOccurrence(resources, 1, 1, "gold", 42, locked: true);
        AssertOccurrence(resources, 1, 1, "iron-ore", 76, locked: true);
        Assert.False(resources.TryGetOccurrence(1, 1, "silver", out _));

        Assert.True(history.Undo());
        Assert.Equal(revisionAfterLiveEdit + 3, resources.Revision);
        Assert.False(resources.TryGetOccurrence(1, 1, "gold", out _));
        AssertOccurrence(resources, 1, 1, "iron-ore", 25, locked: false);
        AssertOccurrence(resources, 1, 1, "silver", 30, locked: true);
        AssertOccurrence(resources, 3, 3, "timber", 55, locked: false);

        Assert.True(history.Redo());
        Assert.Equal(revisionAfterLiveEdit + 6, resources.Revision);
        AssertOccurrence(resources, 1, 1, "gold", 42, locked: true);
        AssertOccurrence(resources, 1, 1, "iron-ore", 76, locked: true);
        Assert.False(resources.TryGetOccurrence(1, 1, "silver", out _));
        AssertOccurrence(resources, 3, 3, "timber", 55, locked: false);
    }

    [Fact]
    public void Stroke_RepeatedTouchesCaptureFirstBeforeLatestAfterAndFilterNetZero()
    {
        var resources = CreateMap();
        var original = new CampaignResourceOccurrence("gold", 20);
        resources.Upsert(0, 0, original);
        var stroke = new CampaignResourceStrokeBuilder(resources);

        stroke.Upsert(0, 0, new CampaignResourceOccurrence("gold", 40));
        stroke.Upsert(0, 0, new CampaignResourceOccurrence("gold", 80, Locked: true));
        stroke.Upsert(0, 0, original);
        stroke.Upsert(0, 0, new CampaignResourceOccurrence("silver", 35));
        stroke.Remove(0, 0, "silver");
        stroke.Remove(0, 0, "coal");
        var command = stroke.Complete("Net-zero resource stroke");
        var history = new CommandHistory();
        history.RecordExecuted(command);

        Assert.Equal(2, stroke.TouchedOccurrenceCount);
        Assert.True(stroke.IsClosed);
        Assert.True(command.IsEmpty);
        Assert.Empty(command.Changes);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        AssertOccurrence(resources, 0, 0, "gold", 20, locked: false);
        Assert.False(resources.TryGetOccurrence(0, 0, "silver", out _));
        Assert.Throws<ObjectDisposedException>(() =>
            stroke.Upsert(0, 0, new CampaignResourceOccurrence("gold", 30)));
    }

    [Fact]
    public void Stroke_CancelRestoresAllFirstValuesAtomicallyAndPermanentlyCloses()
    {
        var resources = CreateMap();
        resources.Apply(
        [
            CampaignResourceMutation.Upsert(0, 0, new CampaignResourceOccurrence("gold", 20)),
            CampaignResourceMutation.Upsert(1, 0, new CampaignResourceOccurrence("silver", 30, Locked: true)),
        ]);
        var stroke = new CampaignResourceStrokeBuilder(resources);
        stroke.Upsert(new CampaignTileCoordinate(0, 0), new CampaignResourceOccurrence("gold", 90, Locked: true));
        stroke.Remove(new CampaignTileCoordinate(1, 0), "silver");
        stroke.Upsert(new CampaignTileCoordinate(2, 0), new CampaignResourceOccurrence("timber", 45));

        stroke.Cancel();

        Assert.True(stroke.IsClosed);
        AssertOccurrence(resources, 0, 0, "gold", 20, locked: false);
        AssertOccurrence(resources, 1, 0, "silver", 30, locked: true);
        Assert.False(resources.TryGetOccurrence(2, 0, "timber", out _));
        Assert.Throws<ObjectDisposedException>(() =>
            stroke.Upsert(0, 0, new CampaignResourceOccurrence("gold", 50)));
        Assert.Throws<ObjectDisposedException>(() => stroke.Remove(0, 0, "gold"));
        Assert.Throws<ObjectDisposedException>(() => stroke.Complete("Closed"));
        Assert.Throws<ObjectDisposedException>(stroke.Cancel);
    }

    [Fact]
    public void Command_DefensivelyCopiesValidatesAndSortsByYThenXThenOrdinalId()
    {
        var resources = CreateMap();
        var source = new[]
        {
            Add(2, 2, "silver", 30),
            Add(1, 0, "iron-ore", 40),
            Add(1, 0, "gold", 50),
            Add(0, 2, "timber", 60),
        };

        var command = new CampaignResourceEditCommand(resources, "Ordered edit", source);
        source[0] = Add(3, 3, "gold", 99);

        Assert.Equal(
        [
            (1, 0, "gold"),
            (1, 0, "iron-ore"),
            (0, 2, "timber"),
            (2, 2, "silver"),
        ], command.Changes.Select(static change => (change.X, change.Y, change.ResourceId)));
        var mutableView = Assert.IsAssignableFrom<IList<CampaignResourceChange>>(command.Changes);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = Add(0, 0, "gold", 10));
    }

    [Fact]
    public void Command_RejectsDuplicateCompositeKeysAndMismatchedOccurrenceIds()
    {
        var resources = CreateMap();

        Assert.Throws<ArgumentException>(() => new CampaignResourceEditCommand(
            resources,
            "Duplicate",
            [
                Add(0, 0, "gold", 20),
                new CampaignResourceChange(0, 0, "gold", null, null),
            ]));
        Assert.Throws<ArgumentException>(() => new CampaignResourceEditCommand(
            resources,
            "Mismatched after",
            [new CampaignResourceChange(
                0,
                0,
                "gold",
                null,
                new CampaignResourceOccurrence("silver", 20))]));
        Assert.Throws<ArgumentException>(() => new CampaignResourceEditCommand(
            resources,
            "Mismatched before",
            [new CampaignResourceChange(
                0,
                0,
                "gold",
                new CampaignResourceOccurrence("silver", 20),
                null)]));
    }

    [Fact]
    public void Command_InvalidBatchIsRejectedBeforeAnyMutation()
    {
        var resources = CreateMap();
        resources.Upsert(3, 3, new CampaignResourceOccurrence("timber", 55));
        var revision = resources.Revision;

        Assert.Throws<ArgumentException>(() => new CampaignResourceEditCommand(
            resources,
            "Invalid batch",
            [
                Add(0, 0, "gold", 20),
                Add(1, 0, "unknown-resource", 30),
            ]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignResourceEditCommand(
            resources,
            "Invalid coordinate",
            [Add(4, 0, "gold", 20)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignResourceEditCommand(
            resources,
            "Invalid potential",
            [new CampaignResourceChange(
                0,
                0,
                "gold",
                null,
                new CampaignResourceOccurrence("gold", 0))]));

        Assert.Equal(revision, resources.Revision);
        Assert.Equal(1, resources.OccurrenceCount);
        AssertOccurrence(resources, 3, 3, "timber", 55, locked: false);
        Assert.False(resources.TryGetOccurrence(0, 0, "gold", out _));
    }

    [Fact]
    public void EmptyCommandDoesNotEnterSharedHistoryOrChangeRevision()
    {
        var resources = CreateMap();
        var occurrence = new CampaignResourceOccurrence("gold", 20, Locked: true);
        resources.Upsert(0, 0, occurrence);
        var revision = resources.Revision;
        var command = new CampaignResourceEditCommand(
            resources,
            "No resource change",
            [new CampaignResourceChange(0, 0, "gold", occurrence, occurrence)]);
        var history = new CommandHistory();

        history.Execute(command);

        Assert.True(command.IsEmpty);
        Assert.Equal(revision, resources.Revision);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void EmptyCommandPreservesAnExistingRedoBranch()
    {
        var resources = CreateMap();
        var history = new CommandHistory();
        var addGold = new CampaignResourceEditCommand(
            resources,
            "Add gold",
            [Add(0, 0, "gold", 25)]);
        history.Execute(addGold);
        Assert.True(history.Undo());
        var revisionBeforeEmptyCommand = resources.Revision;
        var empty = new CampaignResourceEditCommand(
            resources,
            "Empty resource edit",
            [new CampaignResourceChange(0, 0, "gold", null, null)]);

        history.Execute(empty);

        Assert.Equal(revisionBeforeEmptyCommand, resources.Revision);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.Equal("Add gold", history.NextRedoDescription);
        Assert.True(history.Redo());
        AssertOccurrence(resources, 0, 0, "gold", 25, locked: false);
    }

    [Fact]
    public void TerrainAndResourceCommandsInterleaveInOneSharedLifoHistory()
    {
        var definition = CreateDefinition();
        var terrain = new CampaignWorld(definition);
        var resources = new CampaignResourceMap(definition);
        var history = new CommandHistory();
        var terrainAfter = new CampaignTileData(CampaignTileType.Hills, 320);
        var terrainStroke = new CampaignTileStampBuilder(terrain.Tiles);
        terrainStroke.ApplyTile(new CampaignTileCoordinate(1, 1), terrainAfter);
        history.RecordExecuted(terrainStroke.Complete("Paint terrain"));
        var resourceStroke = new CampaignResourceStrokeBuilder(resources);
        resourceStroke.Upsert(1, 1, new CampaignResourceOccurrence("iron-ore", 70));
        history.RecordExecuted(resourceStroke.Complete("Paint resource"));

        Assert.Equal("Paint resource", history.NextUndoDescription);
        Assert.True(history.Undo());
        Assert.Equal(terrainAfter, terrain.Tiles.GetTile(1, 1));
        Assert.False(resources.TryGetOccurrence(1, 1, "iron-ore", out _));
        Assert.Equal("Paint terrain", history.NextUndoDescription);

        Assert.True(history.Undo());
        Assert.Equal(terrain.Tiles.DefaultTile, terrain.Tiles.GetTile(1, 1));
        Assert.False(resources.TryGetOccurrence(1, 1, "iron-ore", out _));

        Assert.True(history.Redo());
        Assert.Equal(terrainAfter, terrain.Tiles.GetTile(1, 1));
        Assert.False(resources.TryGetOccurrence(1, 1, "iron-ore", out _));

        Assert.True(history.Redo());
        Assert.Equal(terrainAfter, terrain.Tiles.GetTile(1, 1));
        AssertOccurrence(resources, 1, 1, "iron-ore", 70, locked: false);
    }

    [Fact]
    public void ResourceCommandPreservesUnrelatedTerrainAndResourceAuthority()
    {
        var definition = CreateDefinition();
        var terrain = new CampaignWorld(definition);
        var resources = new CampaignResourceMap(definition);
        var terrainBefore = new CampaignTileData(CampaignTileType.Forest, 450);
        var unrelatedResource = new CampaignResourceOccurrence("fresh-water", 61, Locked: true);
        terrain.Tiles.SetTile(1, 1, terrainBefore);
        resources.Apply(
        [
            CampaignResourceMutation.Upsert(1, 1, new CampaignResourceOccurrence("iron-ore", 25)),
            CampaignResourceMutation.Upsert(1, 1, unrelatedResource),
        ]);
        var command = new CampaignResourceEditCommand(
            resources,
            "Raise iron potential",
            [new CampaignResourceChange(
                1,
                1,
                "iron-ore",
                new CampaignResourceOccurrence("iron-ore", 25),
                new CampaignResourceOccurrence("iron-ore", 75, Locked: true))]);
        var history = new CommandHistory();

        history.Execute(command);
        Assert.Equal(terrainBefore, terrain.Tiles.GetTile(1, 1));
        Assert.Equal(unrelatedResource, GetOccurrence(resources, 1, 1, "fresh-water"));
        AssertOccurrence(resources, 1, 1, "iron-ore", 75, locked: true);

        Assert.True(history.Undo());
        Assert.Equal(terrainBefore, terrain.Tiles.GetTile(1, 1));
        Assert.Equal(unrelatedResource, GetOccurrence(resources, 1, 1, "fresh-water"));
        AssertOccurrence(resources, 1, 1, "iron-ore", 25, locked: false);

        Assert.True(history.Redo());
        Assert.Equal(terrainBefore, terrain.Tiles.GetTile(1, 1));
        Assert.Equal(unrelatedResource, GetOccurrence(resources, 1, 1, "fresh-water"));
        AssertOccurrence(resources, 1, 1, "iron-ore", 75, locked: true);
    }

    private static CampaignResourceChange Add(
        int x,
        int y,
        string resourceId,
        byte potential) =>
        new(x, y, resourceId, null, new CampaignResourceOccurrence(resourceId, potential));

    private static void AssertOccurrence(
        CampaignResourceMap resources,
        int x,
        int y,
        string resourceId,
        byte potential,
        bool locked)
    {
        var occurrence = GetOccurrence(resources, x, y, resourceId);
        Assert.Equal(potential, occurrence.Potential);
        Assert.Equal(locked, occurrence.Locked);
    }

    private static CampaignResourceOccurrence GetOccurrence(
        CampaignResourceMap resources,
        int x,
        int y,
        string resourceId)
    {
        Assert.True(resources.TryGetOccurrence(x, y, resourceId, out var occurrence));
        return occurrence;
    }

    private static CampaignResourceMap CreateMap() => new(CreateDefinition());

    private static CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: 4_000,
            worldHeightMeters: 4_000,
            campaignTileSizeMeters: 1_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
}
