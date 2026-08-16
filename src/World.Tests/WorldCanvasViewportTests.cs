using Kingdom.World.Editor.Controls;

namespace Kingdom.World.Tests;

public sealed class WorldCanvasViewportTests
{
    [Fact]
    public void ApplyViewport_ClampsZoomAndPreservesFiniteOrigins()
    {
        var canvas = new WorldCanvas();

        canvas.ApplyViewport(new WorldCanvasViewport(0, -12.5, 48.25));
        Assert.Equal(
            new WorldCanvasViewport(0.000001, -12.5, 48.25),
            canvas.CaptureViewport());

        canvas.ApplyViewport(new WorldCanvasViewport(1_000, 8, -4));
        Assert.Equal(
            new WorldCanvasViewport(256, 8, -4),
            canvas.CaptureViewport());
    }

    [Fact]
    public void ApplyViewport_RaisesOnlyWhenRequested()
    {
        var canvas = new WorldCanvas();
        var raised = new List<WorldCanvasViewport>();
        canvas.ViewportChanged += (_, args) => raised.Add(args.Viewport);

        canvas.ApplyViewport(new WorldCanvasViewport(2, 3, 4));
        canvas.ApplyViewport(new WorldCanvasViewport(5, 6, 7), raiseEvent: true);

        Assert.Equal([new WorldCanvasViewport(5, 6, 7)], raised);
    }

    [Theory]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(double.PositiveInfinity, 0, 0)]
    [InlineData(1, double.NegativeInfinity, 0)]
    [InlineData(1, 0, double.NaN)]
    public void ApplyViewport_RejectsNonFiniteValues(double zoom, double originX, double originY)
    {
        var canvas = new WorldCanvas();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => canvas.ApplyViewport(new WorldCanvasViewport(zoom, originX, originY)));
    }
}
