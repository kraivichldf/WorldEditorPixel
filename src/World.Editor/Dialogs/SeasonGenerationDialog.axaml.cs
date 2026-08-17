using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Controls;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Editor.Dialogs;

public sealed partial class SeasonGenerationDialog : Window
{
    private sealed record DesignContext(
        CampaignWorld World,
        CampaignSeasonMap SeasonMap,
        ICampaignSeasonTerrainQuery TerrainQuery,
        CampaignSeasonGenerationSettings Settings);

    private static readonly CampaignWorldDefinition PlaceholderDefinition = CampaignWorldDefinition.Create(
        worldWidthMeters: 20_000,
        worldHeightMeters: 20_000,
        campaignTileSizeMeters: 5_000,
        seaLevelMeters: 0,
        minimumHeightMeters: -1_000,
        maximumHeightMeters: 6_000);

    private static readonly Choice<CampaignSeasonCoverageMode>[] CoverageChoices =
    [
        new(
            CampaignSeasonCoverageMode.WholeGlobe,
            "Whole globe",
            "Map north to +90 degrees and south to -90 degrees; longitude noise is periodic."),
        new(
            CampaignSeasonCoverageMode.Regional,
            "Regional window",
            "Interpret physical north-south kilometres around one centre latitude without wrapping."),
    ];

    private readonly CampaignWorld _world;
    private readonly CampaignSeasonMap _currentMap;
    private readonly ICampaignSeasonTerrainQuery _terrainQuery;
    private readonly CampaignSeasonGenerationSettings _initialSettings;
    private readonly ObservableCollection<CampaignSeasonOption> _seasonOptions = [];
    private readonly ObservableCollection<SeasonClimateInputRow> _climateRows = [];
    private readonly ScrollViewer _settingsScrollViewer;
    private readonly ComboBox _scopeModeInput;
    private readonly StackPanel _rectangleScopePanel;
    private readonly NumericUpDown _scopeMinimumXInput;
    private readonly NumericUpDown _scopeMinimumYInput;
    private readonly NumericUpDown _scopeMaximumXInput;
    private readonly NumericUpDown _scopeMaximumYInput;
    private readonly TextBlock _scopeSummaryText;
    private readonly CheckBox _seedDerivedToggle;
    private readonly NumericUpDown _seedInput;
    private readonly Button _randomizeSeedButton;
    private readonly TextBlock _seedHelpText;
    private readonly ComboBox _coverageModeInput;
    private readonly StackPanel _regionalCenterPanel;
    private readonly NumericUpDown _regionalCenterInput;
    private readonly NumericUpDown _axialTiltInput;
    private readonly TextBlock _coverageHelpText;
    private readonly TextBlock _prioritySummaryText;
    private readonly ItemsControl _climateInputList;
    private readonly Border _validationPanel;
    private readonly TextBlock _validationText;
    private readonly ComboBox _previewSeasonInput;
    private readonly CheckBox _showGridToggle;
    private readonly CheckBox _showLabelsToggle;
    private readonly CheckBox _blendBoundariesToggle;
    private readonly TextBlock _previewStateText;
    private readonly StackPanel _narrowPreviewSwitch;
    private readonly Button _showCurrentButton;
    private readonly Button _showCandidateButton;
    private readonly Grid _previewCanvasesGrid;
    private readonly Border _currentPane;
    private readonly Border _candidatePane;
    private readonly WorldCanvas _currentCanvas;
    private readonly WorldCanvas _candidateCanvas;
    private readonly WorldCanvasViewportSynchronizer _viewportSynchronizer;
    private readonly TextBlock _currentCanvasSummaryText;
    private readonly TextBlock _candidateCanvasSummaryText;
    private readonly StackPanel _candidatePlaceholder;
    private readonly ProgressBar _generationProgress;
    private readonly TextBlock _previewGeneralSummaryText;
    private readonly TextBlock _previewSeasonSummaryText;
    private readonly Button _generateButton;
    private readonly Button _useButton;
    private CampaignSeasonGenerationResult? _candidateResult;
    private CampaignSeasonOption? _previewSeason;
    private CancellationTokenSource? _generationCancellation;
    private int? _derivedSeedCache;
    private bool _candidateMatchesInputs;
    private bool _isGenerating;
    private bool _isClosed;
    private bool _syncingScope;
    private bool _isNarrow;
    private bool _showCandidateInNarrowMode;

    public SeasonGenerationDialog()
        : this(CreateDesignContext())
    {
    }

    private SeasonGenerationDialog(DesignContext context)
        : this(context.World, context.SeasonMap, context.TerrainQuery, context.Settings)
    {
    }

    public SeasonGenerationDialog(
        CampaignWorld world,
        CampaignSeasonMap currentMap,
        ICampaignSeasonTerrainQuery terrainQuery,
        CampaignSeasonGenerationSettings initialSettings)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _currentMap = currentMap ?? throw new ArgumentNullException(nameof(currentMap));
        _terrainQuery = terrainQuery ?? throw new ArgumentNullException(nameof(terrainQuery));
        _initialSettings = initialSettings ?? throw new ArgumentNullException(nameof(initialSettings));
        if (_world.Definition != _currentMap.Definition ||
            _world.Definition != _terrainQuery.Definition)
        {
            throw new ArgumentException(
                "World, Season Layer, and season terrain query must describe the same value-equal campaign definition.");
        }

        _currentMap.EnsureValid();
        _initialSettings.EnsureValid(_currentMap.Catalog, _world.Definition);

        AvaloniaXamlLoader.Load(this);
        _settingsScrollViewer = FindRequired<ScrollViewer>("SettingsScrollViewer");
        _scopeModeInput = FindRequired<ComboBox>("ScopeModeInput");
        _rectangleScopePanel = FindRequired<StackPanel>("RectangleScopePanel");
        _scopeMinimumXInput = FindRequired<NumericUpDown>("ScopeMinimumXInput");
        _scopeMinimumYInput = FindRequired<NumericUpDown>("ScopeMinimumYInput");
        _scopeMaximumXInput = FindRequired<NumericUpDown>("ScopeMaximumXInput");
        _scopeMaximumYInput = FindRequired<NumericUpDown>("ScopeMaximumYInput");
        _scopeSummaryText = FindRequired<TextBlock>("ScopeSummaryText");
        _seedDerivedToggle = FindRequired<CheckBox>("SeedDerivedToggle");
        _seedInput = FindRequired<NumericUpDown>("SeedInput");
        _randomizeSeedButton = FindRequired<Button>("RandomizeSeedButton");
        _seedHelpText = FindRequired<TextBlock>("SeedHelpText");
        _coverageModeInput = FindRequired<ComboBox>("CoverageModeInput");
        _regionalCenterPanel = FindRequired<StackPanel>("RegionalCenterPanel");
        _regionalCenterInput = FindRequired<NumericUpDown>("RegionalCenterInput");
        _axialTiltInput = FindRequired<NumericUpDown>("AxialTiltInput");
        _coverageHelpText = FindRequired<TextBlock>("CoverageHelpText");
        _prioritySummaryText = FindRequired<TextBlock>("PrioritySummaryText");
        _climateInputList = FindRequired<ItemsControl>("ClimateInputList");
        _validationPanel = FindRequired<Border>("ValidationPanel");
        _validationText = FindRequired<TextBlock>("ValidationText");
        _previewSeasonInput = FindRequired<ComboBox>("PreviewSeasonInput");
        _showGridToggle = FindRequired<CheckBox>("ShowGridToggle");
        _showLabelsToggle = FindRequired<CheckBox>("ShowLabelsToggle");
        _blendBoundariesToggle = FindRequired<CheckBox>("BlendBoundariesToggle");
        _previewStateText = FindRequired<TextBlock>("PreviewStateText");
        _narrowPreviewSwitch = FindRequired<StackPanel>("NarrowPreviewSwitch");
        _showCurrentButton = FindRequired<Button>("ShowCurrentButton");
        _showCandidateButton = FindRequired<Button>("ShowCandidateButton");
        _previewCanvasesGrid = FindRequired<Grid>("PreviewCanvasesGrid");
        _currentPane = FindRequired<Border>("CurrentPane");
        _candidatePane = FindRequired<Border>("CandidatePane");
        _currentCanvas = FindRequired<WorldCanvas>("CurrentCanvas");
        _candidateCanvas = FindRequired<WorldCanvas>("CandidateCanvas");
        _viewportSynchronizer = new WorldCanvasViewportSynchronizer(
            _currentCanvas,
            _candidateCanvas,
            callback => Dispatcher.UIThread.Post(callback, DispatcherPriority.Background));
        _currentCanvasSummaryText = FindRequired<TextBlock>("CurrentCanvasSummaryText");
        _candidateCanvasSummaryText = FindRequired<TextBlock>("CandidateCanvasSummaryText");
        _candidatePlaceholder = FindRequired<StackPanel>("CandidatePlaceholder");
        _generationProgress = FindRequired<ProgressBar>("GenerationProgress");
        _previewGeneralSummaryText = FindRequired<TextBlock>("PreviewGeneralSummaryText");
        _previewSeasonSummaryText = FindRequired<TextBlock>("PreviewSeasonSummaryText");
        _generateButton = FindRequired<Button>("GenerateButton");
        _useButton = FindRequired<Button>("UseButton");

        ConfigureChoices();
        ConfigureInitialValues();
        WireEvents();
        ConfigureCanvases();
        UpdateSeedPresentation();
        UpdateCoveragePresentation();
        UpdateScopePresentation();
        UpdatePreviewPresentation();

        Opened += SeasonGenerationDialog_OnOpened;
        Closed += SeasonGenerationDialog_OnClosed;
        SizeChanged += (_, _) => UpdateResponsiveLayout();
    }

    private void ConfigureChoices()
    {
        _scopeModeInput.ItemsSource = new[] { "All tiles", "Rectangle" };
        _coverageModeInput.ItemsSource = CoverageChoices.Select(static choice => choice.Label).ToArray();
        foreach (var definition in _currentMap.Catalog.Definitions)
        {
            _seasonOptions.Add(new CampaignSeasonOption(
                definition.Id,
                definition.Name,
                definition.Fallback,
                new SolidColorBrush(Color.Parse(definition.ColorHex)),
                !_currentMap.Catalog.IsBuiltIn(definition.Id),
                _initialSettings.IsGenerationEnabled(definition.Id)));
        }

        _previewSeasonInput.ItemsSource = _seasonOptions;
        CreateClimateRows(_initialSettings.Climate);
        _climateInputList.ItemsSource = _climateRows;
    }

    private void ConfigureInitialValues()
    {
        var maximumX = _world.Definition.TilesX - 1;
        var maximumY = _world.Definition.TilesY - 1;
        foreach (var input in new[] { _scopeMinimumXInput, _scopeMaximumXInput })
        {
            input.Minimum = 0;
            input.Maximum = maximumX;
        }

        foreach (var input in new[] { _scopeMinimumYInput, _scopeMaximumYInput })
        {
            input.Minimum = 0;
            input.Maximum = maximumY;
        }

        _scopeModeInput.SelectedIndex = 0;
        _scopeMinimumXInput.Value = 0;
        _scopeMinimumYInput.Value = 0;
        _scopeMaximumXInput.Value = maximumX;
        _scopeMaximumYInput.Value = maximumY;
        _seedDerivedToggle.IsChecked = _initialSettings.SeedDerivedFromTerrain;
        _seedInput.Value = _initialSettings.SeasonSeed;
        _derivedSeedCache = _initialSettings.SeedDerivedFromTerrain
            ? _initialSettings.SeasonSeed
            : null;
        SetSelectedChoice(_coverageModeInput, CoverageChoices, _initialSettings.CoverageMode);
        _regionalCenterInput.Value = ToDecimal(_initialSettings.RegionalCenterLatitudeDegrees ?? 0);
        _axialTiltInput.Value = ToDecimal(_initialSettings.AxialTiltDegrees);
        _previewSeason = _seasonOptions.FirstOrDefault(option =>
            string.Equals(option.Id, _initialSettings.CatchAllSeasonId, StringComparison.Ordinal)) ??
            _seasonOptions.FirstOrDefault();
        _previewSeasonInput.SelectedItem = _previewSeason;
        _prioritySummaryText.Text = string.Join(
            Environment.NewLine,
            _initialSettings.PriorityIds.Select((id, index) =>
            {
                var definition = _currentMap.Catalog.Get(id);
                var suffix = index == _initialSettings.PriorityIds.Count - 1 ? " — Catch-all" : string.Empty;
                return $"{index + 1:N0}. {definition.Name} ({id}){suffix}";
            }));
    }

    private void WireEvents()
    {
        _scopeModeInput.SelectionChanged += (_, _) =>
        {
            UpdateScopePresentation();
            MarkCandidateStale();
        };
        foreach (var input in new[]
                 {
                     _scopeMinimumXInput,
                     _scopeMinimumYInput,
                     _scopeMaximumXInput,
                     _scopeMaximumYInput,
                 })
        {
            input.ValueChanged += (_, _) => OnScopeBoundsChanged();
        }

        _seedDerivedToggle.IsCheckedChanged += (_, _) =>
        {
            UpdateSeedPresentation();
            MarkCandidateStale();
        };
        _seedInput.ValueChanged += (_, _) =>
        {
            if (_seedDerivedToggle.IsChecked != true)
            {
                MarkCandidateStale();
            }
        };
        _coverageModeInput.SelectionChanged += (_, _) =>
        {
            UpdateCoveragePresentation();
            MarkCandidateStale();
        };
        _regionalCenterInput.ValueChanged += (_, _) => MarkCandidateStale();
        _axialTiltInput.ValueChanged += (_, _) => MarkCandidateStale();
        foreach (var row in _climateRows)
        {
            row.ValueChanged += (_, _) => MarkCandidateStale();
        }

        _previewSeasonInput.SelectionChanged += (_, _) =>
        {
            _previewSeason = _previewSeasonInput.SelectedItem as CampaignSeasonOption;
            RefreshPreviewSeasonDisplay();
            UpdateCanvasSummaries();
            UpdateSeasonSummary();
        };
        _showGridToggle.IsCheckedChanged += (_, _) => RefreshDisplayOptions();
        _showLabelsToggle.IsCheckedChanged += (_, _) => RefreshDisplayOptions();
        _blendBoundariesToggle.IsCheckedChanged += (_, _) => RefreshDisplayOptions();
        _currentCanvas.ViewportChanged += (_, args) =>
            _viewportSynchronizer.RequestFromCurrent(args.Viewport);
        _candidateCanvas.ViewportChanged += (_, args) =>
            _viewportSynchronizer.RequestFromCandidate(args.Viewport);
        _currentCanvas.TileAreaSelected += (_, args) => ApplyCanvasAreaSelection(args.Area);
        _candidateCanvas.TileAreaSelected += (_, args) => ApplyCanvasAreaSelection(args.Area);
    }

    private void ConfigureCanvases()
    {
        _currentCanvas.World = _world;
        _candidateCanvas.World = _world;
        _currentCanvas.SeasonMap = _currentMap;
        _currentCanvas.NotifyWorldChanged();
        _candidateCanvas.NotifyWorldChanged();
        RefreshDisplayOptions();
        RefreshPreviewSeasonDisplay();
    }

    private void SeasonGenerationDialog_OnOpened(object? sender, EventArgs e)
    {
        _currentCanvas.ZoomToFit();
        _candidateCanvas.ApplyViewport(_currentCanvas.CaptureViewport());
        UpdateResponsiveLayout();
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void Use_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_candidateResult is null || _isGenerating)
        {
            return;
        }

        _candidateMatchesInputs = InputsMatchCandidate(_candidateResult);
        UpdatePreviewPresentation();
        if (_candidateMatchesInputs)
        {
            Close(new SeasonGenerationDialogResult(_candidateResult));
        }
    }

    private void RandomizeSeed_OnClick(object? sender, RoutedEventArgs e)
    {
        _seedDerivedToggle.IsChecked = false;
        _seedInput.Value = Random.Shared.Next(int.MinValue, int.MaxValue);
        MarkCandidateStale();
    }

    private async void Generate_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isGenerating)
        {
            return;
        }

        CampaignSeasonGenerationSource source;
        CampaignSeasonGenerationSettings settings;
        CampaignSeasonGenerationScope scope;
        try
        {
            scope = BuildScope();
            source = CampaignSeasonGenerationSource.Capture(_terrainQuery, _currentMap);
            settings = BuildSettings(source);
            settings.EnsureValid(_currentMap.Catalog, _world.Definition);
            _validationPanel.IsVisible = false;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            ShowValidation(exception.Message);
            return;
        }

        CancelGeneration();
        var cancellation = new CancellationTokenSource();
        _generationCancellation = cancellation;
        SetBusy(true);
        try
        {
            var result = await Task.Run(
                () => CampaignSeasonGenerator.Generate(
                    source,
                    _currentMap.Catalog,
                    settings,
                    scope,
                    cancellation.Token),
                cancellation.Token);
            if (_isClosed || cancellation.IsCancellationRequested)
            {
                return;
            }

            _candidateResult = result;
            _candidateMatchesInputs = InputsMatchCandidate(result);
            _candidateCanvas.SeasonMap = result.CandidateMap;
            _candidateCanvas.NotifyWorldChanged();
            _candidateCanvas.ApplyViewport(_currentCanvas.CaptureViewport());
            _candidatePlaceholder.IsVisible = false;
            UpdatePreviewPresentation();
        }
        catch (OperationCanceledException)
        {
            // Closing the modal cancels worker generation without changing current authority.
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            ShowValidation(exception.Message);
        }
        finally
        {
            if (ReferenceEquals(_generationCancellation, cancellation))
            {
                cancellation.Dispose();
                _generationCancellation = null;
            }

            if (!_isClosed)
            {
                SetBusy(false);
            }
        }
    }

    private void ShowCurrent_OnClick(object? sender, RoutedEventArgs e)
    {
        _showCandidateInNarrowMode = false;
        UpdateResponsiveLayout();
    }

    private void ShowCandidate_OnClick(object? sender, RoutedEventArgs e)
    {
        _showCandidateInNarrowMode = true;
        UpdateResponsiveLayout();
    }

    private void UpdateSeedPresentation()
    {
        var derived = _seedDerivedToggle.IsChecked == true;
        _seedInput.IsEnabled = !derived;
        _randomizeSeedButton.IsEnabled = !derived;
        if (derived)
        {
            _seedInput.Value = ResolveDerivedSeed();
        }

        _seedHelpText.Text = derived
            ? $"Use the reproducible terrain-derived seed. Resolved value: {ResolveDerivedSeed():N0}."
            : "An explicit seed reproduces the same orbital phase when terrain, rules, scope, and climate settings match.";
    }

    private int ResolveDerivedSeed()
    {
        if (_derivedSeedCache is { } cached)
        {
            return cached;
        }

        var source = CampaignSeasonGenerationSource.Capture(_terrainQuery, _currentMap);
        var derived = CampaignSeasonSeed.FromCurrentWorld(source);
        _derivedSeedCache = derived;
        return derived;
    }

    private void UpdateCoveragePresentation()
    {
        var choice = GetChoice(CoverageChoices, _coverageModeInput.SelectedIndex);
        _regionalCenterPanel.IsVisible = choice.Value == CampaignSeasonCoverageMode.Regional;
        _coverageHelpText.Text = choice.Description;
    }

    private void UpdateScopePresentation()
    {
        var rectangle = _scopeModeInput.SelectedIndex == 1;
        _rectangleScopePanel.IsVisible = rectangle;
        _currentCanvas.AllowAreaSelection = rectangle;
        _candidateCanvas.AllowAreaSelection = rectangle;
        if (rectangle)
        {
            SyncCanvasSelectionFromInputs();
        }
        else
        {
            _currentCanvas.SelectedArea = null;
            _candidateCanvas.SelectedArea = null;
            _scopeSummaryText.Text = string.Empty;
        }
    }

    private void OnScopeBoundsChanged()
    {
        if (_syncingScope)
        {
            return;
        }

        SyncCanvasSelectionFromInputs();
        MarkCandidateStale();
    }

    private void ApplyCanvasAreaSelection(CampaignTileArea area)
    {
        if (_scopeModeInput.SelectedIndex != 1)
        {
            return;
        }

        _syncingScope = true;
        try
        {
            _scopeMinimumXInput.Value = area.MinimumX;
            _scopeMinimumYInput.Value = area.MinimumY;
            _scopeMaximumXInput.Value = area.MaximumX;
            _scopeMaximumYInput.Value = area.MaximumY;
            SetSelectedArea(area);
        }
        finally
        {
            _syncingScope = false;
        }

        MarkCandidateStale();
    }

    private void SyncCanvasSelectionFromInputs()
    {
        try
        {
            SetSelectedArea(BuildArea());
            _validationPanel.IsVisible = false;
        }
        catch (ArgumentException exception)
        {
            _scopeSummaryText.Text = exception.Message;
            _currentCanvas.SelectedArea = null;
            _candidateCanvas.SelectedArea = null;
        }
    }

    private void SetSelectedArea(CampaignTileArea area)
    {
        _currentCanvas.SelectedArea = area;
        _candidateCanvas.SelectedArea = area;
        _scopeSummaryText.Text =
            $"Tiles X {area.MinimumX:N0}–{area.MaximumX:N0}, Y {area.MinimumY:N0}–{area.MaximumY:N0} · " +
            $"{area.Width:N0} × {area.Height:N0} = {(long)area.Width * area.Height:N0} tile(s)";
    }

    private CampaignTileArea BuildArea()
    {
        var area = new CampaignTileArea(
            GetInteger(_scopeMinimumXInput),
            GetInteger(_scopeMinimumYInput),
            GetInteger(_scopeMaximumXInput),
            GetInteger(_scopeMaximumYInput));
        CampaignSeasonGenerationScope.ForArea(area).EnsureValid(_world.Definition);
        return area;
    }

    private CampaignSeasonGenerationScope BuildScope() =>
        _scopeModeInput.SelectedIndex == 1
            ? CampaignSeasonGenerationScope.ForArea(BuildArea())
            : CampaignSeasonGenerationScope.All;

    private CampaignSeasonGenerationSettings BuildSettings(
        CampaignSeasonGenerationSource? capturedSource = null)
    {
        var derived = _seedDerivedToggle.IsChecked == true;
        var seed = derived
            ? (_derivedSeedCache ??= capturedSource is null
                ? ResolveDerivedSeed()
                : CampaignSeasonSeed.FromCurrentWorld(capturedSource))
            : GetInteger(_seedInput);
        if (derived)
        {
            _seedInput.Value = seed;
        }

        var coverage = GetChoice(CoverageChoices, _coverageModeInput.SelectedIndex).Value;
        double? regionalCenter = coverage == CampaignSeasonCoverageMode.Regional
            ? ToDouble(_regionalCenterInput)
            : null;
        return new CampaignSeasonGenerationSettings(
            seed,
            derived,
            coverage,
            regionalCenter,
            ToDouble(_axialTiltInput),
            BuildClimateSettings(),
            _initialSettings.PriorityIds);
    }

    private CampaignSeasonClimateSettings BuildClimateSettings() => new(
        GetClimate("lapse-rate"),
        GetClimate("sea-maritime-strength"),
        GetClimate("sea-maritime-radius"),
        GetClimate("lake-maritime-strength"),
        GetClimate("lake-maritime-radius"),
        GetClimate("phase-lag"),
        GetClimate("amplitude-reduction"),
        GetClimate("temperature-noise"),
        GetClimate("sea-moisture-strength"),
        GetClimate("sea-moisture-radius"),
        GetClimate("lake-moisture-strength"),
        GetClimate("lake-moisture-radius"),
        GetClimate("river-moisture-strength"),
        GetClimate("river-moisture-radius"),
        GetClimate("rain-shadow-strength"),
        GetClimate("moisture-noise"),
        GetClimate("temperature-wavelength"),
        GetClimate("moisture-wavelength"),
        GetClimate("rain-shadow-fetch"),
        GetClimate("rain-shadow-relief"),
        GetClimate("wind-perturbation"));

    private double GetClimate(string key) =>
        _climateRows.First(row => string.Equals(row.Key, key, StringComparison.Ordinal)).DoubleValue;

    private void MarkCandidateStale()
    {
        if (_candidateResult is not null)
        {
            _candidateMatchesInputs = InputsMatchCandidate(_candidateResult);
        }

        _validationPanel.IsVisible = false;
        UpdatePreviewPresentation();
    }

    private bool InputsMatchCandidate(CampaignSeasonGenerationResult candidate)
    {
        try
        {
            var currentSettings = BuildSettings();
            return candidate.IsCurrent(_terrainQuery, _currentMap) &&
                candidate.Scope.Equals(BuildScope()) &&
                string.Equals(
                    CampaignSeasonGenerationFingerprint.GetInputFingerprint(
                        _currentMap.Catalog,
                        candidate.Settings),
                    CampaignSeasonGenerationFingerprint.GetInputFingerprint(
                        _currentMap.Catalog,
                        currentSettings),
                    StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private void UpdatePreviewPresentation()
    {
        UpdatePreviewStateText();
        UpdateCanvasSummaries();
        UpdateGeneralSummary();
        UpdateSeasonSummary();
        UpdateButtonState();
    }

    private void UpdatePreviewStateText()
    {
        if (_isGenerating)
        {
            _previewStateText.Text =
                "Generating candidate. Terrain, resources, and the current Season Layer remain unchanged.";
        }
        else if (_candidateResult is null)
        {
            _previewStateText.Text =
                "Generate a candidate to compare it against the current Season Layer.";
        }
        else if (!_candidateMatchesInputs)
        {
            _previewStateText.Text =
                "Previous result — settings or source changed. The old candidate stays visible, but Use seasons is disabled until you regenerate.";
        }
        else
        {
            _previewStateText.Text =
                $"Candidate ready · {_candidateResult.ChangedTileCount:N0} changed tile(s) · " +
                $"seed {_candidateResult.Settings.SeasonSeed:N0}.";
        }
    }

    private void UpdateCanvasSummaries()
    {
        if (_previewSeason is null)
        {
            _currentCanvasSummaryText.Text = "No report season selected.";
            _candidateCanvasSummaryText.Text = _candidateResult is null
                ? "No candidate map."
                : "Choose a report season.";
            return;
        }

        _currentCanvasSummaryText.Text =
            $"{_previewSeason.Name} in current map: {_currentMap.GetUsageCount(_previewSeason.Id):N0} tile(s).";
        if (_candidateResult is null)
        {
            _candidateCanvasSummaryText.Text = "No candidate map yet.";
            return;
        }

        var count = _candidateResult.CandidateMap.GetUsageCount(_previewSeason.Id);
        _candidateCanvasSummaryText.Text = _candidateMatchesInputs
            ? $"{_previewSeason.Name} in candidate: {count:N0} tile(s)."
            : $"{_previewSeason.Name} in stale candidate: {count:N0} tile(s).";
    }

    private void UpdateGeneralSummary()
    {
        if (_candidateResult is null)
        {
            _previewGeneralSummaryText.Text =
                "Scope totals, changes, and preserved locks will appear here.";
            return;
        }

        var scopeCount = _candidateResult.Reports.FirstOrDefault()?.ScopeTileCount ?? 0;
        var preservedLocks = _candidateResult.Reports.Sum(static report => report.PreservedLockCount);
        var lockedOverrides = _candidateResult.Reports.Sum(static report => report.LockedOverrideCount);
        var scope = _candidateResult.Scope.Kind == CampaignSeasonGenerationScopeKind.All
            ? "All tiles"
            : $"Rectangle {_candidateResult.Scope.Area!.Value.Width:N0} × " +
              $"{_candidateResult.Scope.Area.Value.Height:N0}";
        _previewGeneralSummaryText.Text =
            $"{scope} · {scopeCount:N0} reviewed tile(s) · {_candidateResult.ChangedTileCount:N0} changed · " +
            $"{preservedLocks:N0} locked tile(s) preserved · {lockedOverrides:N0} lock override warning(s).";
    }

    private void UpdateSeasonSummary()
    {
        if (_candidateResult is null)
        {
            _previewSeasonSummaryText.Text =
                "Select a Season Definition to inspect its generation report.";
            return;
        }

        if (_previewSeason is null)
        {
            _previewSeasonSummaryText.Text = "Choose a report season.";
            return;
        }

        var report = _candidateResult.Reports.First(candidate =>
            string.Equals(candidate.SeasonId, _previewSeason.Id, StringComparison.Ordinal));
        var zero = string.IsNullOrWhiteSpace(report.ZeroReason)
            ? string.Empty
            : $" Zero result: {report.ZeroReason}";
        var warnings = report.Warnings.Count == 0
            ? string.Empty
            : " Warnings: " + string.Join(" ", report.Warnings);
        _previewSeasonSummaryText.Text =
            $"{_previewSeason.Name} · current {report.CurrentTileCount:N0} -> candidate {report.CandidateTileCount:N0} " +
            $"({report.CandidateCoveragePercent:0.##}%) · environmental matches {report.EnvironmentalMatchCount:N0} · " +
            $"priority wins {report.PriorityWinCount:N0} · generated unlocked {report.GeneratedTileCount:N0} · " +
            $"shadowed matches {report.ShadowedMatchCount:N0} · preserved locks {report.PreservedLockCount:N0} · " +
            $"changed to this season {report.ChangedToSeasonCount:N0}." + zero + warnings;
    }

    private void RefreshDisplayOptions()
    {
        var showGrid = _showGridToggle.IsChecked == true;
        var showLabels = _showLabelsToggle.IsChecked == true;
        var blend = _blendBoundariesToggle.IsChecked == true;
        foreach (var canvas in new[] { _currentCanvas, _candidateCanvas })
        {
            canvas.ShowCampaignGrid = showGrid;
            canvas.ShowSeasonLabels = showLabels;
            canvas.BlendSeasonBoundaries = blend;
            canvas.NotifyWorldChanged();
        }
    }

    private void RefreshPreviewSeasonDisplay()
    {
        var seasonId = _previewSeason?.Id;
        _currentCanvas.SelectedSeasonId = seasonId;
        _candidateCanvas.SelectedSeasonId = seasonId;
        _currentCanvas.NotifyWorldChanged();
        _candidateCanvas.NotifyWorldChanged();
    }

    private void UpdateButtonState()
    {
        _useButton.IsEnabled = !_isGenerating && _candidateResult is not null && _candidateMatchesInputs;
        _generateButton.IsDefault = !_useButton.IsEnabled;
        _useButton.IsDefault = _useButton.IsEnabled;
        _generateButton.Content = _candidateResult is null ? "Generate candidate" : "Regenerate candidate";
        SetButtonClass(_generateButton, "primary", !_useButton.IsEnabled);
        SetButtonClass(_generateButton, "quiet", _useButton.IsEnabled);
        SetButtonClass(_useButton, "primary", _useButton.IsEnabled);
        SetButtonClass(_useButton, "quiet", !_useButton.IsEnabled);
    }

    private void SetBusy(bool isBusy)
    {
        _isGenerating = isBusy;
        _settingsScrollViewer.IsEnabled = !isBusy;
        _generateButton.IsEnabled = !isBusy;
        _generationProgress.IsVisible = isBusy;
        UpdatePreviewPresentation();
    }

    private void UpdateResponsiveLayout()
    {
        var narrow = Bounds.Width > 0 && Bounds.Width < 1_190;
        if (_isNarrow != narrow)
        {
            _isNarrow = narrow;
            _previewCanvasesGrid.ColumnDefinitions.Clear();
            if (narrow)
            {
                _previewCanvasesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                Grid.SetColumn(_currentPane, 0);
                Grid.SetColumn(_candidatePane, 0);
            }
            else
            {
                _previewCanvasesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _previewCanvasesGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(10)));
                _previewCanvasesGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                Grid.SetColumn(_currentPane, 0);
                Grid.SetColumn(_candidatePane, 2);
            }
        }

        _narrowPreviewSwitch.IsVisible = narrow;
        _currentPane.IsVisible = !narrow || !_showCandidateInNarrowMode;
        _candidatePane.IsVisible = !narrow || _showCandidateInNarrowMode;
        SetButtonClass(_showCurrentButton, "primary", narrow && !_showCandidateInNarrowMode);
        SetButtonClass(_showCandidateButton, "primary", narrow && _showCandidateInNarrowMode);
        SetButtonClass(_showCurrentButton, "quiet", !narrow || _showCandidateInNarrowMode);
        SetButtonClass(_showCandidateButton, "quiet", !narrow || !_showCandidateInNarrowMode);
    }

    private void ShowValidation(string message)
    {
        _validationText.Text = message;
        _validationPanel.IsVisible = true;
    }

    private void CancelGeneration()
    {
        _generationCancellation?.Cancel();
        _generationCancellation?.Dispose();
        _generationCancellation = null;
    }

    private void SeasonGenerationDialog_OnClosed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _viewportSynchronizer.Dispose();
        CancelGeneration();
    }

    private void CreateClimateRows(CampaignSeasonClimateSettings climate)
    {
        AddClimate("lapse-rate", "Lapse rate", climate.LapseRateCelsiusPerKilometer, 0, 20, 0.1, "°C/km");
        AddClimate("sea-maritime-strength", "Sea maritime strength", climate.SeaMaritimeStrength, 0, 2, 0.05, string.Empty);
        AddClimate("sea-maritime-radius", "Sea maritime radius", climate.SeaMaritimeRadiusKilometers, 1, 20_000, 10, "km");
        AddClimate("lake-maritime-strength", "Lake maritime strength", climate.LakeMaritimeStrength, 0, 2, 0.05, string.Empty);
        AddClimate("lake-maritime-radius", "Lake maritime radius", climate.LakeMaritimeRadiusKilometers, 1, 20_000, 10, "km");
        AddClimate("phase-lag", "Maximum phase lag", climate.MaximumPhaseLagOrbitFraction, 0, 0.25, 0.01, "orbit");
        AddClimate("amplitude-reduction", "Maritime amplitude reduction", climate.MaritimeAmplitudeReduction, 0, 1, 0.05, string.Empty);
        AddClimate("temperature-noise", "Temperature noise", climate.TemperatureNoiseCelsius, 0, 30, 0.25, "°C");
        AddClimate("sea-moisture-strength", "Sea moisture strength", climate.SeaMoistureStrength, 0, 1, 0.05, string.Empty);
        AddClimate("sea-moisture-radius", "Sea moisture radius", climate.SeaMoistureRadiusKilometers, 1, 20_000, 10, "km");
        AddClimate("lake-moisture-strength", "Lake moisture strength", climate.LakeMoistureStrength, 0, 1, 0.05, string.Empty);
        AddClimate("lake-moisture-radius", "Lake moisture radius", climate.LakeMoistureRadiusKilometers, 1, 20_000, 10, "km");
        AddClimate("river-moisture-strength", "River moisture strength", climate.RiverMoistureStrength, 0, 1, 0.05, string.Empty);
        AddClimate("river-moisture-radius", "River moisture radius", climate.RiverMoistureRadiusKilometers, 1, 20_000, 5, "km");
        AddClimate("rain-shadow-strength", "Rain-shadow strength", climate.RainShadowStrength, 0, 1, 0.05, string.Empty);
        AddClimate("moisture-noise", "Moisture noise strength", climate.MoistureNoiseStrength, 0, 1, 0.05, string.Empty);
        AddClimate("temperature-wavelength", "Temperature wavelength", climate.TemperatureNoiseWavelengthKilometers, 1, 50_000, 50, "km");
        AddClimate("moisture-wavelength", "Moisture wavelength", climate.MoistureNoiseWavelengthKilometers, 1, 50_000, 50, "km");
        AddClimate("rain-shadow-fetch", "Rain-shadow fetch", climate.RainShadowFetchKilometers, 1, 20_000, 10, "km");
        AddClimate("rain-shadow-relief", "Rain-shadow relief", climate.RainShadowReliefMeters, 1, 20_000, 50, "m");
        AddClimate("wind-perturbation", "Wind perturbation", climate.WindPerturbationDegrees, 0, 45, 1, "°");
    }

    private void AddClimate(
        string key,
        string label,
        double value,
        double minimum,
        double maximum,
        double increment,
        string unit) =>
        _climateRows.Add(new SeasonClimateInputRow(
            key,
            label,
            ToDecimal(value),
            ToDecimal(minimum),
            ToDecimal(maximum),
            ToDecimal(increment),
            unit));

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' was not found.");

    private static int GetInteger(NumericUpDown input) => decimal.ToInt32(input.Value ?? 0);

    private static double ToDouble(NumericUpDown input) => decimal.ToDouble(input.Value ?? 0);

    private static decimal ToDecimal(double value) => checked((decimal)value);

    private static Choice<T> GetChoice<T>(IReadOnlyList<Choice<T>> choices, int selectedIndex) =>
        selectedIndex >= 0 && selectedIndex < choices.Count
            ? choices[selectedIndex]
            : throw new InvalidOperationException("A required generation choice is not selected.");

    private static void SetSelectedChoice<T>(
        ComboBox input,
        IReadOnlyList<Choice<T>> choices,
        T value)
    {
        var index = choices
            .Select((choice, choiceIndex) => (choice, choiceIndex))
            .FirstOrDefault(pair => EqualityComparer<T>.Default.Equals(pair.choice.Value, value))
            .choiceIndex;
        input.SelectedIndex = index;
    }

    private static void SetButtonClass(Button button, string className, bool enabled)
    {
        if (enabled)
        {
            if (!button.Classes.Contains(className))
            {
                button.Classes.Add(className);
            }
        }
        else
        {
            button.Classes.Remove(className);
        }
    }

    private static DesignContext CreateDesignContext()
    {
        var world = new CampaignWorld(PlaceholderDefinition);
        var seasons = new CampaignSeasonMap(PlaceholderDefinition);
        var query = new CampaignSeasonTerrainQueryV2(world);
        var settings = new CampaignSeasonGenerationSettings(0);
        return new DesignContext(world, seasons, query, settings);
    }

    private sealed record Choice<T>(T Value, string Label, string Description);
}

public sealed class SeasonClimateInputRow : INotifyPropertyChanged
{
    private decimal? _value;

    public SeasonClimateInputRow(
        string key,
        string label,
        decimal value,
        decimal minimum,
        decimal maximum,
        decimal increment,
        string unit)
    {
        Key = key;
        Label = label;
        _value = value;
        Minimum = minimum;
        Maximum = maximum;
        Increment = increment;
        Unit = unit;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? ValueChanged;

    public string Key { get; }

    public string Label { get; }

    public decimal Minimum { get; }

    public decimal Maximum { get; }

    public decimal Increment { get; }

    public string Unit { get; }

    public decimal? Value
    {
        get => _value;
        set
        {
            var normalized = Math.Clamp(value ?? Minimum, Minimum, Maximum);
            if (_value == normalized)
            {
                return;
            }

            _value = normalized;
            OnPropertyChanged();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double DoubleValue => decimal.ToDouble(Value ?? Minimum);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
