namespace Kingdom.World.Editor.Controls;

public readonly record struct WorldCanvasViewport(
    double Zoom,
    double OriginX,
    double OriginY);

public sealed class WorldCanvasViewportChangedEventArgs(
    WorldCanvasViewport viewport) : EventArgs
{
    public WorldCanvasViewport Viewport { get; } = viewport;
}
