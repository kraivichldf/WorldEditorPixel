using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Dialogs;

namespace Kingdom.World.Tests;

[Collection(CampaignSeasonNativeUiCollection.CollectionName)]
public sealed class GenerationBusyStateNativeUiTests
{
    [Fact]
    public async Task ResourceGenerationRendersBusyStateBeforeOwnerThreadCapture()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await session.Dispatch(async () =>
        {
            var definition = CreateDefinition();
            var world = new CampaignWorld(definition);
            var resources = new CampaignResourceMap(definition);
            ResourceGenerationDialog? dialog = null;
            var renderTurnObserved = false;
            var probe = new CaptureProbe(() =>
            {
                var progress = FindRequired<ProgressBar>(dialog!, "GenerationProgress");
                var generate = FindRequired<Button>(dialog!, "GenerateButton");
                return new CaptureObservation(
                    renderTurnObserved,
                    progress.IsVisible,
                    progress.Bounds.Width > 0 && progress.Bounds.Height > 0,
                    generate.IsEnabled);
            });
            var query = new ProbeResourceTerrainQuery(definition, probe);
            dialog = new ResourceGenerationDialog(
                world,
                resources,
                query,
                new CampaignResourceGenerationSettings(17, seedDerivedFromWorld: false));
            dialog.Show();

            try
            {
                Dispatcher.UIThread.RunJobs();
                FindRequired<CheckBox>(dialog, "SeedDerivedToggle").IsChecked = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, probe.ObservationCount);
                Dispatcher.UIThread.Post(
                    () => renderTurnObserved = true,
                    DispatcherPriority.Render);

                FindRequired<Button>(dialog, "GenerateButton")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                var observation = await probe.FirstObservation.WaitAsync(timeout.Token);
                Assert.True(observation.RenderTurnObserved, observation.ToString());
                Assert.True(observation.ProgressVisible, observation.ToString());
                Assert.True(observation.ProgressArranged, observation.ToString());
                Assert.False(observation.GenerateEnabled, observation.ToString());
            }
            finally
            {
                dialog.Close();
            }

            return true;
        }, timeout.Token);
    }

    [Fact]
    public async Task SeasonGenerationRendersBusyStateBeforeOwnerThreadCapture()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await session.Dispatch(async () =>
        {
            var definition = CreateDefinition();
            var world = new CampaignWorld(definition);
            var seasons = new CampaignSeasonMap(definition);
            SeasonGenerationDialog? dialog = null;
            var renderTurnObserved = false;
            var probe = new CaptureProbe(() =>
            {
                var progress = FindRequired<ProgressBar>(dialog!, "GenerationProgress");
                var generate = FindRequired<Button>(dialog!, "GenerateButton");
                return new CaptureObservation(
                    renderTurnObserved,
                    progress.IsVisible,
                    progress.Bounds.Width > 0 && progress.Bounds.Height > 0,
                    generate.IsEnabled);
            });
            var query = new ProbeSeasonTerrainQuery(definition, probe);
            dialog = new SeasonGenerationDialog(
                world,
                seasons,
                query,
                new CampaignSeasonGenerationSettings(17, seedDerivedFromTerrain: false));
            dialog.Show();

            try
            {
                Dispatcher.UIThread.RunJobs();
                FindRequired<CheckBox>(dialog, "SeedDerivedToggle").IsChecked = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, probe.ObservationCount);
                Dispatcher.UIThread.Post(
                    () => renderTurnObserved = true,
                    DispatcherPriority.Render);

                FindRequired<Button>(dialog, "GenerateButton")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                var observation = await probe.FirstObservation.WaitAsync(timeout.Token);
                Assert.True(observation.RenderTurnObserved, observation.ToString());
                Assert.True(observation.ProgressVisible, observation.ToString());
                Assert.True(observation.ProgressArranged, observation.ToString());
                Assert.False(observation.GenerateEnabled, observation.ToString());
            }
            finally
            {
                dialog.Close();
            }

            return true;
        }, timeout.Token);
    }

    [Fact]
    public async Task ClosingGenerationDialogsDuringBusyRenderTurnCancelsBeforeCapture()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(CampaignSeasonHeadlessAppBuilder));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await session.Dispatch(async () =>
        {
            var definition = CreateDefinition();
            var resourceProbe = new CaptureProbe(() => new CaptureObservation(false, false, false, true));
            var resourceDialog = new ResourceGenerationDialog(
                new CampaignWorld(definition),
                new CampaignResourceMap(definition),
                new ProbeResourceTerrainQuery(definition, resourceProbe),
                new CampaignResourceGenerationSettings(17, seedDerivedFromWorld: false));
            resourceDialog.Show();
            Dispatcher.UIThread.RunJobs();

            FindRequired<Button>(resourceDialog, "GenerateButton")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            resourceDialog.Close();
            await Avalonia.Threading.Dispatcher.Yield(DispatcherPriority.Background);
            Assert.Equal(0, resourceProbe.ObservationCount);

            var seasonProbe = new CaptureProbe(() => new CaptureObservation(false, false, false, true));
            var seasonDialog = new SeasonGenerationDialog(
                new CampaignWorld(definition),
                new CampaignSeasonMap(definition),
                new ProbeSeasonTerrainQuery(definition, seasonProbe),
                new CampaignSeasonGenerationSettings(17, seedDerivedFromTerrain: false));
            seasonDialog.Show();
            Dispatcher.UIThread.RunJobs();

            FindRequired<Button>(seasonDialog, "GenerateButton")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            seasonDialog.Close();
            await Avalonia.Threading.Dispatcher.Yield(DispatcherPriority.Background);
            Assert.Equal(0, seasonProbe.ObservationCount);

            return true;
        }, timeout.Token);
    }

    private static CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            worldWidthMeters: 5_000,
            worldHeightMeters: 5_000,
            campaignTileSizeMeters: 5_000,
            seaLevelMeters: 0,
            minimumHeightMeters: -1_000,
            maximumHeightMeters: 6_000);

    private static T FindRequired<T>(Control root, string name) where T : Control =>
        root.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' was not found.");

    private sealed record CaptureObservation(
        bool RenderTurnObserved,
        bool ProgressVisible,
        bool ProgressArranged,
        bool GenerateEnabled);

    private sealed class CaptureProbe(Func<CaptureObservation> observe)
    {
        private readonly TaskCompletionSource<CaptureObservation> _firstObservation =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _observationCount;

        public Task<CaptureObservation> FirstObservation => _firstObservation.Task;

        public int ObservationCount => _observationCount;

        public void Observe()
        {
            _observationCount++;
            _firstObservation.TrySetResult(observe());
        }
    }

    private sealed class ProbeResourceTerrainQuery(
        CampaignWorldDefinition definition,
        CaptureProbe probe) : ICampaignResourceTerrainQuery
    {
        public CampaignWorldDefinition Definition { get; } = definition;

        public long Revision => 0;

        public CampaignResourceTerrainSample GetSample(int x, int y)
        {
            probe.Observe();
            return new CampaignResourceTerrainSample(
                CampaignResourceTerrainKind.Land,
                CampaignResourceSurfaceType.Grassland,
                CampaignResourceTerrainForm.Flat,
                CustomTerrainId: null,
                ElevationMeters: 0,
                MaximumCardinalGrade: 0,
                SeaDistanceKilometers: double.PositiveInfinity,
                LakeDistanceKilometers: double.PositiveInfinity,
                RiverDistanceKilometers: double.PositiveInfinity,
                CampaignResourceRiverFeatures.None,
                CampaignResourceCoastFlags.None);
        }
    }

    private sealed class ProbeSeasonTerrainQuery(
        CampaignWorldDefinition definition,
        CaptureProbe probe) : ICampaignSeasonTerrainQuery
    {
        public CampaignWorldDefinition Definition { get; } = definition;

        public long Revision => 0;

        public CampaignSeasonTerrainSample GetSample(int x, int y)
        {
            probe.Observe();
            return new CampaignSeasonTerrainSample(
                CampaignTileType.Plains,
                CustomTerrainId: null,
                ElevationMeters: 0,
                CampaignSeasonWaterFeatures.None);
        }
    }
}
