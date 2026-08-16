using Kingdom.World.Editor.Controls;

namespace Kingdom.World.Tests;

public sealed class WorldCanvasViewportSynchronizerTests
{
    [Fact]
    public void RequestDefersPeerInvalidationUntilQueuedCallbackRuns()
    {
        var current = new WorldCanvas();
        var candidate = new WorldCanvas();
        var queued = new List<Action>();
        using var synchronizer = new WorldCanvasViewportSynchronizer(
            current,
            candidate,
            queued.Add);
        var requested = new WorldCanvasViewport(2, 3, 4);
        var before = candidate.CaptureViewport();

        synchronizer.RequestFromCurrent(requested);

        Assert.Equal(before, candidate.CaptureViewport());
        var callback = Assert.Single(queued);
        callback();
        Assert.Equal(requested, candidate.CaptureViewport());
    }

    [Fact]
    public void RepeatedRequestsCoalesceToLatestViewport()
    {
        var current = new WorldCanvas();
        var candidate = new WorldCanvas();
        var queued = new List<Action>();
        using var synchronizer = new WorldCanvasViewportSynchronizer(
            current,
            candidate,
            queued.Add);

        synchronizer.RequestFromCurrent(new WorldCanvasViewport(2, 3, 4));
        synchronizer.RequestFromCurrent(new WorldCanvasViewport(5, 6, 7));

        var callback = Assert.Single(queued);
        callback();
        Assert.Equal(new WorldCanvasViewport(5, 6, 7), candidate.CaptureViewport());
    }

    [Fact]
    public void CandidateRequestUpdatesCurrentCanvasAfterQueueFlush()
    {
        var current = new WorldCanvas();
        var candidate = new WorldCanvas();
        var queued = new List<Action>();
        using var synchronizer = new WorldCanvasViewportSynchronizer(
            current,
            candidate,
            queued.Add);
        var requested = new WorldCanvasViewport(3, -5, 11);

        synchronizer.RequestFromCandidate(requested);
        Assert.NotEqual(requested, current.CaptureViewport());

        Assert.Single(queued)();
        Assert.Equal(requested, current.CaptureViewport());
    }

    [Fact]
    public void DisposeCancelsQueuedViewportMutation()
    {
        var current = new WorldCanvas();
        var candidate = new WorldCanvas();
        var queued = new List<Action>();
        var synchronizer = new WorldCanvasViewportSynchronizer(
            current,
            candidate,
            queued.Add);
        var before = candidate.CaptureViewport();
        synchronizer.RequestFromCurrent(new WorldCanvasViewport(2, 3, 4));

        synchronizer.Dispose();
        Assert.Single(queued)();
        queued.Clear();

        Assert.Equal(before, candidate.CaptureViewport());
        synchronizer.RequestFromCurrent(new WorldCanvasViewport(4, 5, 6));
        Assert.Empty(queued);
    }

    [Fact]
    public void ConstructorRejectsSynchronizingCanvasWithItself()
    {
        var canvas = new WorldCanvas();

        Assert.Throws<ArgumentException>(() =>
            new WorldCanvasViewportSynchronizer(canvas, canvas, static callback => callback()));
    }
}
