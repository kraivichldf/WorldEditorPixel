using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor;
using Kingdom.World.Editor.Controls;
using Kingdom.World.Editor.Dialogs;

namespace Kingdom.World.Tests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class CampaignSeasonNativeUiCollection
{
    public const string CollectionName = "Campaign Season native UI";
}

[Collection(CampaignSeasonNativeUiCollection.CollectionName)]
public sealed class CampaignSeasonNativeUiTests
{
    [Fact]
    public async Task HeadlessNativeDialogsRemainOperableAtNormalAndNarrowSizes()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            await VerifyNewWorldDialog(timeout.Token);
            await VerifySeasonGenerationDialog(timeout.Token);
            return true;
        }, timeout.Token);
    }

    private static async Task VerifyNewWorldDialog(CancellationToken cancellationToken)
    {
        var dialog = new NewWorldDialog();
        dialog.Show();
        try
        {
            ResizeAndLayout(dialog, 1_120, 800);
            AssertInside(dialog, FindRequired<ScrollViewer>(dialog, "SettingsScrollViewer"));
            AssertInside(dialog, FindRequired<Border>(dialog, "ResourceImpactPanel"));
            CaptureReviewFrame(dialog, "season-new-world-normal.png");

            var generate = FindRequired<Button>(dialog, "GenerateButton");
            var use = FindRequired<Button>(dialog, "UsePreviewButton");
            Assert.True(generate.IsDefault);
            Assert.False(use.IsVisible);

            FindRequired<NumericUpDown>(dialog, "WorldWidthInput").Value = 100;
            FindRequired<NumericUpDown>(dialog, "WorldHeightInput").Value = 100;
            FindRequired<NumericUpDown>(dialog, "CampaignTileInput").Value = 5;
            FindRequired<ComboBox>(dialog, "GenerationPresetInput").SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Assert.True(use.IsVisible);
            Assert.False(use.IsEnabled);
            Assert.True(generate.IsDefault);

            Assert.True(generate.Focus());
            dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await WaitUntil(() => use.IsEnabled, cancellationToken);
            Assert.True(use.IsDefault);
            Assert.False(generate.IsDefault);

            FindRequired<Button>(dialog, "SeasonPreviewButton")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(FindRequired<Image>(dialog, "GenerationPreviewImage").IsVisible);

            ResizeAndLayout(dialog, 900, 680);
            AssertInside(dialog, FindRequired<ScrollViewer>(dialog, "SettingsScrollViewer"));
            AssertInside(dialog, FindRequired<Border>(dialog, "ResourceImpactPanel"));
            AssertInside(dialog, use);
            CaptureReviewFrame(dialog, "season-new-world-narrow.png");

            var seed = FindRequired<NumericUpDown>(dialog, "GenerationSeedInput");
            seed.Value = (seed.Value ?? 0) + 1;
            Dispatcher.UIThread.RunJobs();
            Assert.False(use.IsEnabled);
            Assert.True(generate.IsDefault);
            var retainedPreview = FindRequired<Image>(dialog, "GenerationPreviewImage");
            Assert.True(retainedPreview.IsVisible);
            Assert.NotNull(retainedPreview.Source);
            Assert.Contains(
                "previous result",
                FindRequired<TextBlock>(dialog, "GenerationPreviewStateText").Text,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            dialog.Close();
        }
    }

    private static async Task VerifySeasonGenerationDialog(CancellationToken cancellationToken)
    {
        var dialog = new SeasonGenerationDialog();
        dialog.Show();
        try
        {
            var currentPane = FindRequired<Border>(dialog, "CurrentPane");
            var candidatePane = FindRequired<Border>(dialog, "CandidatePane");
            var narrowSwitch = FindRequired<StackPanel>(dialog, "NarrowPreviewSwitch");
            var previewGrid = FindRequired<Grid>(dialog, "PreviewCanvasesGrid");
            var generate = FindRequired<Button>(dialog, "GenerateButton");
            var use = FindRequired<Button>(dialog, "UseButton");

            ResizeAndLayout(dialog, 1_480, 880);
            Assert.False(narrowSwitch.IsVisible);
            Assert.True(currentPane.IsVisible);
            Assert.True(candidatePane.IsVisible);
            Assert.Equal(3, previewGrid.ColumnDefinitions.Count);
            AssertInside(dialog, currentPane);
            AssertInside(dialog, candidatePane);
            Assert.Equal(
                "Current season preview map",
                AutomationProperties.GetName(FindRequired<WorldCanvas>(dialog, "CurrentCanvas")));
            CaptureReviewFrame(dialog, "season-generation-normal.png");

            ResizeAndLayout(dialog, 980, 700);
            Assert.True(narrowSwitch.IsVisible);
            Assert.True(currentPane.IsVisible);
            Assert.False(candidatePane.IsVisible);
            Assert.Single(previewGrid.ColumnDefinitions);
            AssertInside(dialog, currentPane);

            FindRequired<Button>(dialog, "ShowCandidateButton")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(currentPane.IsVisible);
            Assert.True(candidatePane.IsVisible);
            AssertInside(dialog, candidatePane);
            CaptureReviewFrame(dialog, "season-generation-narrow.png");

            Assert.True(generate.IsDefault);
            Assert.False(use.IsEnabled);
            Assert.True(generate.Focus());
            dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await WaitUntil(() => use.IsEnabled, cancellationToken);
            Assert.True(use.IsDefault);
            Assert.False(generate.IsDefault);
            CaptureReviewFrame(dialog, "season-generation-candidate-narrow.png");

            ResizeAndLayout(dialog, 1_480, 880);
            Assert.True(currentPane.IsVisible);
            Assert.True(candidatePane.IsVisible);
            CaptureReviewFrame(dialog, "season-generation-candidate-normal.png");

            Assert.True(generate.Focus());
            dialog.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            Assert.True(use.IsFocused);
        }
        finally
        {
            dialog.Close();
        }
    }

    private static void ResizeAndLayout(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(width, window.ClientSize.Width, precision: 1);
        Assert.Equal(height, window.ClientSize.Height, precision: 1);
    }

    private static void AssertInside(Window window, Control control)
    {
        Assert.True(control.IsVisible, $"{control.Name ?? control.GetType().Name} should be visible.");
        Assert.True(control.Bounds.Width > 0, $"{control.Name ?? control.GetType().Name} should have width.");
        Assert.True(control.Bounds.Height > 0, $"{control.Name ?? control.GetType().Name} should have height.");
        var origin = control.TranslatePoint(default, window);
        Assert.NotNull(origin);
        Assert.InRange(origin.Value.X, -0.5, window.ClientSize.Width + 0.5);
        Assert.InRange(origin.Value.Y, -0.5, window.ClientSize.Height + 0.5);
        Assert.True(
            origin.Value.X + control.Bounds.Width <= window.ClientSize.Width + 0.5,
            $"{control.Name ?? control.GetType().Name} overflows the window horizontally.");
        Assert.True(
            origin.Value.Y + control.Bounds.Height <= window.ClientSize.Height + 0.5,
            $"{control.Name ?? control.GetType().Name} overflows the window vertically.");
    }

    private static async Task WaitUntil(Func<bool> predicate, CancellationToken cancellationToken)
    {
        while (!predicate())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(20, cancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void CaptureReviewFrame(Window window, string fileName)
    {
        var directory = Environment.GetEnvironmentVariable("WORLD_EDITOR_UI_REVIEW_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, fileName), PngBitmapEncoderOptions.Default);
    }

    private static T FindRequired<T>(Control root, string name) where T : Control =>
        root.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' was not found.");

    private static CampaignWorldDefinition CreateDefinition(int tilesX, int tilesY, int tileSizeMeters) =>
        CampaignWorldDefinition.Create(
            tilesX * tileSizeMeters,
            tilesY * tileSizeMeters,
            tileSizeMeters,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);
}

internal static class CampaignSeasonHeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
