namespace Kingdom.World.Editor.Controls;

/// <summary>
/// Coalesces cross-canvas viewport updates behind an injected dispatcher queue.
/// A viewport notification may originate while one canvas is rendering, so the
/// peer canvas must never be invalidated synchronously from that notification.
/// </summary>
public sealed class WorldCanvasViewportSynchronizer : IDisposable
{
    private readonly WorldCanvas _currentCanvas;
    private readonly WorldCanvas _candidateCanvas;
    private readonly Action<Action> _enqueue;
    private WorldCanvasViewport _pendingViewport;
    private bool _pendingFromCurrent;
    private bool _hasPending;
    private bool _flushScheduled;
    private bool _disposed;

    public WorldCanvasViewportSynchronizer(
        WorldCanvas currentCanvas,
        WorldCanvas candidateCanvas,
        Action<Action> enqueue)
    {
        _currentCanvas = currentCanvas ?? throw new ArgumentNullException(nameof(currentCanvas));
        _candidateCanvas = candidateCanvas ?? throw new ArgumentNullException(nameof(candidateCanvas));
        _enqueue = enqueue ?? throw new ArgumentNullException(nameof(enqueue));
        if (ReferenceEquals(currentCanvas, candidateCanvas))
        {
            throw new ArgumentException(
                "Viewport synchronization requires two different canvases.",
                nameof(candidateCanvas));
        }
    }

    public void RequestFromCurrent(WorldCanvasViewport viewport) =>
        Request(fromCurrent: true, viewport);

    public void RequestFromCandidate(WorldCanvasViewport viewport) =>
        Request(fromCurrent: false, viewport);

    public void Dispose()
    {
        _disposed = true;
        _hasPending = false;
    }

    private void Request(bool fromCurrent, WorldCanvasViewport viewport)
    {
        if (_disposed)
        {
            return;
        }

        _pendingFromCurrent = fromCurrent;
        _pendingViewport = viewport;
        _hasPending = true;
        if (_flushScheduled)
        {
            return;
        }

        _flushScheduled = true;
        _enqueue(Flush);
    }

    private void Flush()
    {
        _flushScheduled = false;
        if (_disposed || !_hasPending)
        {
            return;
        }

        var fromCurrent = _pendingFromCurrent;
        var viewport = _pendingViewport;
        _hasPending = false;
        if (fromCurrent)
        {
            _candidateCanvas.ApplyViewport(viewport);
        }
        else
        {
            _currentCanvas.ApplyViewport(viewport);
        }
    }
}
