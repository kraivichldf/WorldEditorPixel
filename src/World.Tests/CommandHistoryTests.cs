using Kingdom.World.Core.Brushes;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Tests;

public sealed class CommandHistoryTests
{
    [Fact]
    public void UndoRestoresAndRedoReappliesTerrain()
    {
        var terrain = TestWorldFactory.Create();
        var stroke = new TerrainStrokeBuilder(terrain);
        var settings = new BrushSettings { RadiusSamples = 1, StrengthMeters = 25, Falloff = 0.5 };
        stroke.ApplyStamp(new RaiseTerrainBrush(), new TerrainCoordinate(4, 4), settings);
        var command = stroke.Complete("Raise terrain");
        var history = new CommandHistory();
        history.RecordExecuted(command);

        Assert.Equal(25, terrain.GetHeight(4, 4));
        Assert.True(history.Undo());
        Assert.Equal(0, terrain.GetHeight(4, 4));
        Assert.True(history.Redo());
        Assert.Equal(25, terrain.GetHeight(4, 4));
    }

    [Fact]
    public void ContinuousStroke_CollapsesRepeatedSamplesIntoOneDelta()
    {
        var terrain = TestWorldFactory.Create();
        var stroke = new TerrainStrokeBuilder(terrain);
        var settings = new BrushSettings { RadiusSamples = 0.75, StrengthMeters = 10, Falloff = 0.4 };
        var brush = new RaiseTerrainBrush();

        stroke.ApplyStamp(brush, new TerrainCoordinate(4, 4), settings);
        stroke.ApplyStamp(brush, new TerrainCoordinate(4, 4), settings);
        var command = stroke.Complete("Raise terrain");

        var centerChange = Assert.Single(command.Changes);
        Assert.Equal(0, centerChange.Before);
        Assert.Equal(20, centerChange.After);
    }

    [Fact]
    public void NewCommandClearsRedoStack()
    {
        var terrain = TestWorldFactory.Create();
        var history = new CommandHistory();
        var first = new TerrainStrokeBuilder(terrain);
        first.ApplyStamp(
            new RaiseTerrainBrush(),
            new TerrainCoordinate(2, 2),
            new BrushSettings { RadiusSamples = 1, StrengthMeters = 5, Falloff = 0.5 });
        history.RecordExecuted(first.Complete("First"));
        history.Undo();

        var second = new TerrainStrokeBuilder(terrain);
        second.ApplyStamp(
            new RaiseTerrainBrush(),
            new TerrainCoordinate(6, 6),
            new BrushSettings { RadiusSamples = 1, StrengthMeters = 5, Falloff = 0.5 });
        history.RecordExecuted(second.Complete("Second"));

        Assert.False(history.CanRedo);
    }
}
