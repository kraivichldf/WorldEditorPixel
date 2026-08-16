using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Controls;

namespace Kingdom.World.Editor.Dialogs;

public sealed partial class ResourceGenerationDialog : Window
{
    private sealed record DesignContext(
        CampaignWorld World,
        CampaignResourceMap ResourceMap,
        ICampaignResourceTerrainQuery TerrainQuery,
        CampaignResourceGenerationSettings Settings);

    private static readonly CampaignWorldDefinition PlaceholderDefinition = CampaignWorldDefinition.Create(
        worldWidthMeters: 5_000,
        worldHeightMeters: 5_000,
        campaignTileSizeMeters: 5_000,
        seaLevelMeters: 0,
        minimumHeightMeters: -1_000,
        maximumHeightMeters: 6_000);

    private static readonly Choice<ResourceListFilter>[] FilterChoices =
    [
        new(ResourceListFilter.All, "All resources", "Show every resource definition."),
        new(ResourceListFilter.Renewable, "Renewable", "Show only renewable resources."),
        new(ResourceListFilter.Finite, "Finite", "Show only finite resources."),
    ];

    private static readonly Choice<CampaignResourceAbundance>[] AbundanceChoices =
    [
        new(CampaignResourceAbundance.Sparse, "Sparse", "Lower overall coverage and lower potential shift."),
        new(CampaignResourceAbundance.Balanced, "Balanced", "Use the default coverage and potential profile."),
        new(CampaignResourceAbundance.Abundant, "Abundant", "Raise overall coverage and potential shift."),
        new(CampaignResourceAbundance.Custom, "Custom tuned", "Keep exact per-resource coverage without a global abundance multiplier."),
    ];

    private static readonly Choice<CampaignResourceClimateProfile>[] ClimateChoices =
    [
        new(CampaignResourceClimateProfile.AutoMixed, "Auto mixed", "Derive mixed support from the current terrain."),
        new(CampaignResourceClimateProfile.Tropical, "Tropical", "Bias support toward warm and wet terrain."),
        new(CampaignResourceClimateProfile.Temperate, "Temperate", "Bias support toward moderate mixed climates."),
        new(CampaignResourceClimateProfile.Continental, "Continental", "Bias support toward stronger inland seasonality."),
        new(CampaignResourceClimateProfile.Arid, "Arid", "Bias support toward dry terrain."),
        new(CampaignResourceClimateProfile.Cold, "Cold", "Bias support toward colder terrain."),
    ];

    private static readonly Choice<CampaignResourceGeologyProfile>[] GeologyChoices =
    [
        new(CampaignResourceGeologyProfile.AutoMixed, "Auto mixed", "Derive mixed geology support from the terrain snapshot."),
        new(CampaignResourceGeologyProfile.AncientCraton, "Ancient craton", "Bias support toward old stable crust."),
        new(CampaignResourceGeologyProfile.VolcanicArc, "Volcanic arc", "Bias support toward volcanic and hydrothermal belts."),
        new(CampaignResourceGeologyProfile.SedimentaryBasins, "Sedimentary basins", "Bias support toward broad basinal accumulation."),
        new(CampaignResourceGeologyProfile.FoldBelt, "Fold belt", "Bias support toward uplifted and mineralized belts."),
        new(CampaignResourceGeologyProfile.YoungRift, "Young rift", "Bias support toward rifted and extensional zones."),
    ];

    private static readonly Choice<CampaignResourceRichness>[] RichnessChoices =
    [
        new(CampaignResourceRichness.Poor, "Poor", "Shift this resource toward lower potential values."),
        new(CampaignResourceRichness.Balanced, "Balanced", "Keep the default potential profile."),
        new(CampaignResourceRichness.Rich, "Rich", "Shift this resource toward higher potential values."),
    ];

    private static readonly Choice<CampaignResourceConcentration>[] ConcentrationChoices =
    [
        new(CampaignResourceConcentration.FewLarge, "Few large regions", "Favor broader separated regions."),
        new(CampaignResourceConcentration.Balanced, "Balanced", "Use balanced region size and spacing."),
        new(CampaignResourceConcentration.ManySmall, "Many small regions", "Favor more numerous compact regions."),
    ];

    private readonly CampaignWorld _world;
    private readonly CampaignResourceMap _currentMap;
    private readonly ICampaignResourceTerrainQuery _terrainQuery;
    private readonly CampaignResourceGenerationSettings _initialSettings;
    private readonly ObservableCollection<ResourceDefinitionItem> _allResources = [];
    private readonly ObservableCollection<ResourceDefinitionItem> _includedResources = [];
    private readonly ObservableCollection<ResourceDefinitionItem> _excludedResources = [];
    private readonly HashSet<string> _includedResourceIds = new(StringComparer.Ordinal);
    private readonly ScrollViewer _settingsScrollViewer;
    private readonly CheckBox _seedDerivedToggle;
    private readonly NumericUpDown _seedInput;
    private readonly Button _randomizeSeedButton;
    private readonly TextBlock _seedHelpText;
    private readonly ComboBox _abundanceInput;
    private readonly ComboBox _climateInput;
    private readonly ComboBox _geologyInput;
    private readonly ComboBox _resourceFilterInput;
    private readonly TextBox _resourceSearchInput;
    private readonly ListBox _includedResourceList;
    private readonly ListBox _excludedResourceList;
    private readonly TextBlock _resourceSelectionHelpText;
    private readonly Button _includeSelectedButton;
    private readonly Button _excludeSelectedButton;
    private readonly TextBlock _selectedResourceHeadingText;
    private readonly CheckBox _selectedEnabledToggle;
    private readonly NumericUpDown _coverageInput;
    private readonly ComboBox _richnessInput;
    private readonly NumericUpDown _biasInput;
    private readonly ComboBox _concentrationInput;
    private readonly NumericUpDown _mapPriorityInput;
    private readonly TextBlock _selectedResourceKindText;
    private readonly TextBlock _selectedResourceDescriptionText;
    private readonly Border _validationPanel;
    private readonly TextBlock _validationText;
    private readonly ComboBox _previewResourceInput;
    private readonly TextBlock _previewStateText;
    private readonly WorldCanvas _currentCanvas;
    private readonly WorldCanvas _candidateCanvas;
    private readonly WorldCanvasViewportSynchronizer _viewportSynchronizer;
    private readonly TextBlock _currentCanvasSummaryText;
    private readonly TextBlock _candidateCanvasSummaryText;
    private readonly StackPanel _candidatePlaceholder;
    private readonly ProgressBar _generationProgress;
    private readonly TextBlock _previewGeneralSummaryText;
    private readonly TextBlock _previewResourceSummaryText;
    private readonly Button _generateButton;
    private readonly Button _useButton;
    private CampaignResourceGenerationResult? _candidateResult;
    private CancellationTokenSource? _generationCancellation;
    private ResourceDefinitionItem? _selectedResource;
    private ResourceDefinitionItem? _previewResource;
    private bool _previewSelectionPinned;
    private bool _candidateMatchesInputs;
    private bool _isGenerating;
    private bool _isClosed;
    private bool _syncingSelectedResourceControls;
    private bool _syncingResourceListSelection;
    private int? _derivedSeedCache;

    public ResourceGenerationDialog()
        : this(CreateDesignContext())
    {
    }

    private ResourceGenerationDialog(DesignContext designContext)
        : this(
            designContext.World,
            designContext.ResourceMap,
            designContext.TerrainQuery,
            designContext.Settings)
    {
    }

    public ResourceGenerationDialog(
        CampaignWorld world,
        CampaignResourceMap currentMap,
        ICampaignResourceTerrainQuery terrainQuery,
        CampaignResourceGenerationSettings initialSettings)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _currentMap = currentMap ?? throw new ArgumentNullException(nameof(currentMap));
        _terrainQuery = terrainQuery ?? throw new ArgumentNullException(nameof(terrainQuery));
        _initialSettings = initialSettings ?? throw new ArgumentNullException(nameof(initialSettings));

        if (_world.Definition != _currentMap.Definition || _world.Definition != _terrainQuery.Definition)
        {
            throw new ArgumentException(
                "World, resource map, and terrain query must describe the same value-equal campaign definition.");
        }

        _currentMap.EnsureValid();
        _initialSettings.EnsureValid(_currentMap.Catalog);

        AvaloniaXamlLoader.Load(this);
        _settingsScrollViewer = FindRequired<ScrollViewer>("SettingsScrollViewer");
        _seedDerivedToggle = FindRequired<CheckBox>("SeedDerivedToggle");
        _seedInput = FindRequired<NumericUpDown>("SeedInput");
        _randomizeSeedButton = FindRequired<Button>("RandomizeSeedButton");
        _seedHelpText = FindRequired<TextBlock>("SeedHelpText");
        _abundanceInput = FindRequired<ComboBox>("AbundanceInput");
        _climateInput = FindRequired<ComboBox>("ClimateInput");
        _geologyInput = FindRequired<ComboBox>("GeologyInput");
        _resourceFilterInput = FindRequired<ComboBox>("ResourceFilterInput");
        _resourceSearchInput = FindRequired<TextBox>("ResourceSearchInput");
        _includedResourceList = FindRequired<ListBox>("IncludedResourceList");
        _excludedResourceList = FindRequired<ListBox>("ExcludedResourceList");
        _resourceSelectionHelpText = FindRequired<TextBlock>("ResourceSelectionHelpText");
        _includeSelectedButton = FindRequired<Button>("IncludeSelectedButton");
        _excludeSelectedButton = FindRequired<Button>("ExcludeSelectedButton");
        _selectedResourceHeadingText = FindRequired<TextBlock>("SelectedResourceHeadingText");
        _selectedEnabledToggle = FindRequired<CheckBox>("SelectedEnabledToggle");
        _coverageInput = FindRequired<NumericUpDown>("CoverageInput");
        _richnessInput = FindRequired<ComboBox>("RichnessInput");
        _biasInput = FindRequired<NumericUpDown>("BiasInput");
        _concentrationInput = FindRequired<ComboBox>("ConcentrationInput");
        _mapPriorityInput = FindRequired<NumericUpDown>("MapPriorityInput");
        _selectedResourceKindText = FindRequired<TextBlock>("SelectedResourceKindText");
        _selectedResourceDescriptionText = FindRequired<TextBlock>("SelectedResourceDescriptionText");
        _validationPanel = FindRequired<Border>("ValidationPanel");
        _validationText = FindRequired<TextBlock>("ValidationText");
        _previewResourceInput = FindRequired<ComboBox>("PreviewResourceInput");
        _previewStateText = FindRequired<TextBlock>("PreviewStateText");
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
        _previewResourceSummaryText = FindRequired<TextBlock>("PreviewResourceSummaryText");
        _generateButton = FindRequired<Button>("GenerateButton");
        _useButton = FindRequired<Button>("UseButton");

        ConfigureChoices();
        LoadResources();
        ConfigureInitialSelections();
        WireEvents();
        ConfigureCanvases();
        UpdateSeedPresentation();
        UpdateSelectedResourcePresentation();
        UpdatePreviewPresentation();

        Opened += ResourceGenerationDialog_OnOpened;
        Closed += ResourceGenerationDialog_OnClosed;
    }

    private void ConfigureChoices()
    {
        _abundanceInput.ItemsSource = AbundanceChoices.Select(static choice => choice.Label).ToArray();
        _climateInput.ItemsSource = ClimateChoices.Select(static choice => choice.Label).ToArray();
        _geologyInput.ItemsSource = GeologyChoices.Select(static choice => choice.Label).ToArray();
        _resourceFilterInput.ItemsSource = FilterChoices.Select(static choice => choice.Label).ToArray();
        _richnessInput.ItemsSource = RichnessChoices.Select(static choice => choice.Label).ToArray();
        _concentrationInput.ItemsSource = ConcentrationChoices.Select(static choice => choice.Label).ToArray();
        _includedResourceList.ItemsSource = _includedResources;
        _excludedResourceList.ItemsSource = _excludedResources;
        _previewResourceInput.ItemsSource = _allResources;
    }

    private void LoadResources()
    {
        foreach (var definition in _currentMap.Catalog.Definitions)
        {
            var effective = _initialSettings.GetEffective(definition);
            var item = new ResourceDefinitionItem(
                definition,
                effective.Enabled,
                effective.CoveragePercent,
                effective.Richness,
                effective.RichnessBias,
                effective.Concentration,
                effective.MapPriority,
                _currentMap.Catalog.IsBuiltIn(definition.Id) is false);
            _allResources.Add(item);
            _includedResourceIds.Add(definition.Id);
        }
    }

    private void ConfigureInitialSelections()
    {
        _resourceFilterInput.SelectedIndex = 0;
        SetSelectedChoice(_abundanceInput, AbundanceChoices, _initialSettings.Abundance);
        SetSelectedChoice(_climateInput, ClimateChoices, _initialSettings.Climate);
        SetSelectedChoice(_geologyInput, GeologyChoices, _initialSettings.Geology);
        _seedDerivedToggle.IsChecked = _initialSettings.SeedDerivedFromWorld;
        _seedInput.Value = _initialSettings.ResourceSeed;
        _derivedSeedCache = _initialSettings.SeedDerivedFromWorld
            ? _initialSettings.ResourceSeed
            : null;

        RefreshResourceSelectionLists();
        var first = _includedResources.FirstOrDefault() ?? _allResources.FirstOrDefault();
        if (first is not null)
        {
            _includedResourceList.SelectedItem = first;
            _previewResourceInput.SelectedItem = first;
            _selectedResource = first;
            _previewResource = first;
        }
    }

    private void WireEvents()
    {
        _seedDerivedToggle.IsCheckedChanged += (_, _) =>
        {
            UpdateSeedPresentation();
            MarkCandidateStale();
        };
        _seedInput.ValueChanged += (_, _) =>
        {
            if (_seedDerivedToggle.IsChecked == true)
            {
                return;
            }

            MarkCandidateStale();
            _validationPanel.IsVisible = false;
        };
        _abundanceInput.SelectionChanged += (_, _) => MarkCandidateStale();
        _climateInput.SelectionChanged += (_, _) => MarkCandidateStale();
        _geologyInput.SelectionChanged += (_, _) => MarkCandidateStale();
        _resourceFilterInput.SelectionChanged += (_, _) => RefreshResourceSelectionLists();
        _resourceSearchInput.TextChanged += (_, _) => RefreshResourceSelectionLists();
        _includedResourceList.SelectionChanged += (_, _) => OnIncludedResourceChanged();
        _excludedResourceList.SelectionChanged += (_, _) => OnExcludedResourceChanged();
        _previewResourceInput.SelectionChanged += (_, _) => OnPreviewResourceChanged();
        _selectedEnabledToggle.IsCheckedChanged += (_, _) => WriteSelectedResourceOverrides();
        _coverageInput.ValueChanged += (_, _) => WriteSelectedResourceOverrides();
        _richnessInput.SelectionChanged += (_, _) => WriteSelectedResourceOverrides();
        _biasInput.ValueChanged += (_, _) => WriteSelectedResourceOverrides();
        _concentrationInput.SelectionChanged += (_, _) => WriteSelectedResourceOverrides();
        _mapPriorityInput.ValueChanged += (_, _) => WriteSelectedResourceOverrides();
        _currentCanvas.ViewportChanged += (_, args) =>
            _viewportSynchronizer.RequestFromCurrent(args.Viewport);
        _candidateCanvas.ViewportChanged += (_, args) =>
            _viewportSynchronizer.RequestFromCandidate(args.Viewport);
    }

    private void ConfigureCanvases()
    {
        _currentCanvas.World = _world;
        _candidateCanvas.World = _world;
        _currentCanvas.ResourceMap = _currentMap;
        _currentCanvas.NotifyWorldChanged();
        _candidateCanvas.NotifyWorldChanged();
        RefreshPreviewResourceDisplay();
    }

    private void ResourceGenerationDialog_OnOpened(object? sender, EventArgs e)
    {
        _currentCanvas.ZoomToFit();
        _candidateCanvas.ApplyViewport(_currentCanvas.CaptureViewport());
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void Use_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_candidateResult is null || !_candidateMatchesInputs || _isGenerating)
        {
            return;
        }

        Close(new ResourceGenerationDialogResult(_candidateResult));
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

        CampaignResourceGenerationSource source;
        CampaignResourceGenerationSettings settings;
        CampaignResourceGenerationScope scope;

        try
        {
            scope = BuildScope();
            source = CampaignResourceGenerationSource.Capture(
                _terrainQuery,
                _currentMap,
                CancellationToken.None);
            settings = BuildSettings(source);
            settings.EnsureValid(_currentMap.Catalog);
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
            var generator = new CampaignResourceGenerator();
            var result = await Task.Run(
                () => generator.Generate(source, _currentMap.Catalog, settings, scope, cancellation.Token),
                cancellation.Token);

            if (_isClosed || cancellation.IsCancellationRequested)
            {
                return;
            }

            _candidateResult = result;
            _candidateMatchesInputs = InputsMatchCandidate(result);
            _candidateCanvas.ResourceMap = result.CandidateMap;
            _candidateCanvas.NotifyWorldChanged();
            _candidateCanvas.ApplyViewport(_currentCanvas.CaptureViewport());
            _candidatePlaceholder.IsVisible = false;
            UpdatePreviewPresentation();
        }
        catch (OperationCanceledException)
        {
            // Closing the dialog or explicitly canceling invalidates the in-flight candidate.
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or CampaignResourceGenerationLimitException)
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

    private void OnIncludedResourceChanged()
    {
        if (_syncingResourceListSelection ||
            _includedResourceList.SelectedItem is not ResourceDefinitionItem selected)
        {
            return;
        }

        _syncingResourceListSelection = true;
        try
        {
            _excludedResourceList.SelectedItem = null;
        }
        finally
        {
            _syncingResourceListSelection = false;
        }

        SelectResource(selected);
    }

    private void OnExcludedResourceChanged()
    {
        if (_syncingResourceListSelection ||
            _excludedResourceList.SelectedItem is not ResourceDefinitionItem selected)
        {
            return;
        }

        _syncingResourceListSelection = true;
        try
        {
            _includedResourceList.SelectedItem = null;
        }
        finally
        {
            _syncingResourceListSelection = false;
        }

        SelectResource(selected);
    }

    private void SelectResource(ResourceDefinitionItem selected)
    {
        var previous = _selectedResource;
        _selectedResource = selected;
        if (_selectedResource is not null &&
            (!_previewSelectionPinned ||
             _previewResource is null ||
             ReferenceEquals(_previewResource, previous)))
        {
            _previewResourceInput.SelectedItem = _selectedResource;
        }

        UpdateSelectedResourcePresentation();
        UpdateResourceSelectionPresentation();
    }

    private void OnPreviewResourceChanged()
    {
        _previewResource = _previewResourceInput.SelectedItem as ResourceDefinitionItem;
        _previewSelectionPinned = true;
        RefreshPreviewResourceDisplay();
        UpdatePreviewPresentation();
    }

    private void WriteSelectedResourceOverrides()
    {
        if (_syncingSelectedResourceControls || _selectedResource is null)
        {
            return;
        }

        _selectedResource.Enabled = _selectedEnabledToggle.IsChecked ?? true;
        _selectedResource.CoveragePercent = decimal.ToInt32(_coverageInput.Value ?? _selectedResource.CoveragePercent);
        _selectedResource.Richness = GetChoice(RichnessChoices, _richnessInput.SelectedIndex).Value;
        _selectedResource.RichnessBias = decimal.ToInt32(_biasInput.Value ?? _selectedResource.RichnessBias);
        _selectedResource.Concentration = GetChoice(ConcentrationChoices, _concentrationInput.SelectedIndex).Value;
        _selectedResource.MapPriority = decimal.ToInt32(_mapPriorityInput.Value ?? _selectedResource.MapPriority);
        UpdateSelectedResourcePresentation();
        MarkCandidateStale();
    }

    private void RefreshResourceSelectionLists()
    {
        var previous = _selectedResource;
        _syncingResourceListSelection = true;
        try
        {
            _includedResources.Clear();
            _excludedResources.Clear();
            foreach (var resource in _allResources.Where(IsVisibleInFilter))
            {
                if (_includedResourceIds.Contains(resource.Definition.Id))
                {
                    _includedResources.Add(resource);
                }
                else
                {
                    _excludedResources.Add(resource);
                }
            }

            _includedResourceList.SelectedItem = previous is not null && _includedResources.Contains(previous)
                ? previous
                : null;
            _excludedResourceList.SelectedItem = previous is not null && _excludedResources.Contains(previous)
                ? previous
                : null;
        }
        finally
        {
            _syncingResourceListSelection = false;
        }

        UpdateResourceSelectionPresentation();
    }

    private bool IsVisibleInFilter(ResourceDefinitionItem item)
    {
        var matchesCategory = GetChoice(FilterChoices, _resourceFilterInput.SelectedIndex).Value switch
        {
            ResourceListFilter.All => true,
            ResourceListFilter.Renewable => item.Definition.Category == CampaignResourceCategory.Renewable,
            ResourceListFilter.Finite => item.Definition.Category == CampaignResourceCategory.Finite,
            _ => true,
        };

        var search = _resourceSearchInput.Text?.Trim();
        return matchesCategory &&
            (string.IsNullOrEmpty(search) ||
             item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             item.Definition.Id.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateResourceSelectionPresentation()
    {
        var excludedCount = _allResources.Count - _includedResourceIds.Count;
        _resourceSelectionHelpText.Text =
            $"{_includedResourceIds.Count:N0} included; {excludedCount:N0} excluded. " +
            $"Current filter shows {_includedResources.Count:N0} included and {_excludedResources.Count:N0} excluded.";
        _excludeSelectedButton.IsEnabled = _includedResourceList.SelectedItem is not null;
        _includeSelectedButton.IsEnabled = _excludedResourceList.SelectedItem is not null;
    }

    private void IncludeAll_OnClick(object? sender, RoutedEventArgs e) =>
        SetIncludedResources(static _ => true);

    private void IncludeRenewable_OnClick(object? sender, RoutedEventArgs e) =>
        SetIncludedResources(static item => item.Definition.Category == CampaignResourceCategory.Renewable);

    private void IncludeFinite_OnClick(object? sender, RoutedEventArgs e) =>
        SetIncludedResources(static item => item.Definition.Category == CampaignResourceCategory.Finite);

    private void IncludeOnlySelected_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedResource is null)
        {
            ShowValidation("Choose a resource before using Only selected.");
            return;
        }

        var selectedId = _selectedResource.Definition.Id;
        SetIncludedResources(item => string.Equals(item.Definition.Id, selectedId, StringComparison.Ordinal));
    }

    private void ExcludeAll_OnClick(object? sender, RoutedEventArgs e) =>
        SetIncludedResources(static _ => false);

    private void ExcludeSelected_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_includedResourceList.SelectedItem is ResourceDefinitionItem selected)
        {
            SetResourceIncluded(selected, included: false);
        }
    }

    private void IncludeSelected_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_excludedResourceList.SelectedItem is ResourceDefinitionItem selected)
        {
            SetResourceIncluded(selected, included: true);
        }
    }

    private void SetIncludedResources(Func<ResourceDefinitionItem, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var replacement = _allResources
            .Where(predicate)
            .Select(static item => item.Definition.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (_includedResourceIds.SetEquals(replacement))
        {
            return;
        }

        _includedResourceIds.Clear();
        _includedResourceIds.UnionWith(replacement);
        RefreshResourceSelectionLists();
        UpdateSelectedResourcePresentation();
        MarkCandidateStale();
    }

    private void SetResourceIncluded(ResourceDefinitionItem item, bool included)
    {
        var changed = included
            ? _includedResourceIds.Add(item.Definition.Id)
            : _includedResourceIds.Remove(item.Definition.Id);
        if (!changed)
        {
            return;
        }

        RefreshResourceSelectionLists();
        UpdateSelectedResourcePresentation();
        MarkCandidateStale();
    }

    private void UpdateSeedPresentation()
    {
        var derived = _seedDerivedToggle.IsChecked == true;
        _seedInput.IsEnabled = !derived;
        _randomizeSeedButton.IsEnabled = !derived;
        _seedHelpText.Text = derived
            ? $"Use the reproducible seed resolved for this world. " +
              $"Resolved value: {ResolveDerivedSeed():N0}."
            : "Explicit seed values reproduce the same candidate when the terrain and generation settings match.";
        if (derived)
        {
            _seedInput.Value = ResolveDerivedSeed();
        }
    }

    private int ResolveDerivedSeed()
    {
        if (_derivedSeedCache is { } cached)
        {
            return cached;
        }

        var source = CampaignResourceGenerationSource.Capture(_terrainQuery, _currentMap);
        var derived = CampaignResourceSeed.FromCurrentWorld(source);
        _derivedSeedCache = derived;
        return derived;
    }

    private void UpdateSelectedResourcePresentation()
    {
        _syncingSelectedResourceControls = true;
        try
        {
            if (_selectedResource is null)
            {
                _selectedResourceHeadingText.Text = "Choose a resource from the list.";
                _selectedEnabledToggle.IsEnabled = false;
                _coverageInput.IsEnabled = false;
                _richnessInput.IsEnabled = false;
                _biasInput.IsEnabled = false;
                _concentrationInput.IsEnabled = false;
                _mapPriorityInput.IsEnabled = false;
                _selectedEnabledToggle.IsChecked = true;
                _coverageInput.Value = 0;
                _biasInput.Value = 0;
                _mapPriorityInput.Value = CampaignResourceDefinition.MinimumMapPriority;
                _selectedResourceKindText.Text = string.Empty;
                _selectedResourceDescriptionText.Text =
                    "Choose a resource to edit its deterministic generation override.";
                return;
            }

            _selectedEnabledToggle.IsEnabled = true;
            _coverageInput.IsEnabled = true;
            _richnessInput.IsEnabled = true;
            _biasInput.IsEnabled = true;
            _concentrationInput.IsEnabled = true;
            _mapPriorityInput.IsEnabled = true;
            _selectedResourceHeadingText.Text = $"{_selectedResource.Name} ({_selectedResource.Definition.Id})";
            _selectedEnabledToggle.IsChecked = _selectedResource.Enabled;
            _coverageInput.Value = _selectedResource.CoveragePercent;
            SetSelectedChoice(_richnessInput, RichnessChoices, _selectedResource.Richness);
            _biasInput.Value = _selectedResource.RichnessBias;
            SetSelectedChoice(_concentrationInput, ConcentrationChoices, _selectedResource.Concentration);
            _mapPriorityInput.Value = _selectedResource.MapPriority;
            _selectedResourceKindText.Text =
                $"{_selectedResource.Definition.Category} · {_selectedResource.Definition.DistributionProfile} · " +
                $"{(_selectedResource.IsCustom ? "custom" : "built-in")} · " +
                $"{(_includedResourceIds.Contains(_selectedResource.Definition.Id) ? "included" : "excluded")}";
            var preferred = FormatTerrainFactors(
                _selectedResource.Definition.Rules.PreferredTerrainTags);
            var avoided = FormatTerrainFactors(
                _selectedResource.Definition.Rules.AvoidedTerrainTags);
            var excluded = _selectedResource.Definition.Rules.ExcludedTerrainSurfaces.Count == 0
                ? "none"
                : string.Join(", ", _selectedResource.Definition.Rules.ExcludedTerrainSurfaces);
            _selectedResourceDescriptionText.Text =
                (_includedResourceIds.Contains(_selectedResource.Definition.Id)
                    ? "This resource will be regenerated. "
                    : "This resource will stay unchanged; edited settings are kept for a future included run. ") +
                $"Default coverage {_selectedResource.Definition.CoveragePercent:N0}% · " +
                $"default richness {_selectedResource.Definition.Richness} · " +
                $"default concentration {_selectedResource.Definition.Concentration}. " +
                $"Prefers: {preferred}. Avoids: {avoided}. Hard excludes: {excluded}. " +
                "Preferred and avoided lists combine alternative soft cues; " +
                "hard exclusions forbid generated placement.";
        }
        finally
        {
            _syncingSelectedResourceControls = false;
        }
    }

    private static string FormatTerrainFactors(IReadOnlyList<string> factors) =>
        factors.Count == 0 ? "none" : string.Join(", ", factors);

    private void RefreshPreviewResourceDisplay()
    {
        var resourceId = (_previewResource ?? _selectedResource)?.Definition.Id;
        _currentCanvas.SelectedResourceId = resourceId;
        _candidateCanvas.SelectedResourceId = resourceId;
        _currentCanvas.NotifyWorldChanged();
        _candidateCanvas.NotifyWorldChanged();
    }

    private void UpdatePreviewPresentation()
    {
        UpdateResourceSelectionPresentation();
        RefreshPreviewResourceDisplay();
        UpdatePreviewStateText();
        UpdateCanvasSummaries();
        UpdateGeneralSummary();
        UpdateResourceSummary();
        UpdateButtonState();
    }

    private void UpdatePreviewStateText()
    {
        if (_isGenerating)
        {
            _previewStateText.Text = "Generating candidate. The current resource map remains authoritative until Use resources.";
            return;
        }

        if (_candidateResult is null)
        {
            _previewStateText.Text = "Generate a candidate to compare it against the current resource map.";
            return;
        }

        if (!_candidateMatchesInputs)
        {
            _previewStateText.Text =
                "Inputs changed after this candidate was generated. The old candidate stays visible for comparison, but Use resources is disabled until you regenerate.";
            return;
        }

        _previewStateText.Text =
            $"Candidate ready · {_candidateResult.CandidateMap.OccurrenceCount:N0} occurrence(s) · " +
            $"seed {_candidateResult.Settings.ResourceSeed:N0}.";
    }

    private void UpdateCanvasSummaries()
    {
        var preview = _previewResource ?? _selectedResource;
        if (preview is null)
        {
            _currentCanvasSummaryText.Text = "No display resource selected.";
            _candidateCanvasSummaryText.Text = _candidateResult is null
                ? "No candidate map."
                : "Choose a display resource to inspect candidate heatmap.";
            return;
        }

        var currentCount = _currentMap.GetUsageCount(preview.Definition.Id);
        _currentCanvasSummaryText.Text =
            $"{preview.Name} in current map: {currentCount:N0} occupied tile(s).";

        if (_candidateResult is null)
        {
            _candidateCanvasSummaryText.Text = "No candidate map yet.";
            return;
        }

        var candidateCount = _candidateResult.CandidateMap.GetUsageCount(preview.Definition.Id);
        _candidateCanvasSummaryText.Text = _candidateMatchesInputs
            ? $"{preview.Name} in candidate: {candidateCount:N0} occupied tile(s)."
            : $"{preview.Name} in stale candidate: {candidateCount:N0} occupied tile(s).";
    }

    private void UpdateGeneralSummary()
    {
        if (_candidateResult is null)
        {
            _previewGeneralSummaryText.Text =
                "Whole-map counts and generation scope will appear here.";
            return;
        }

        var scopeText = DescribeScope(_candidateResult.Scope);
        var preservedLocks = _candidateResult.Reports.Sum(static report => report.PreservedLockCount);
        var generatedUnlocked = _candidateResult.Reports.Sum(static report => report.GeneratedOccurrenceCount);
        _previewGeneralSummaryText.Text =
            $"{scopeText} · current {_currentMap.OccurrenceCount:N0} occurrence(s) -> " +
            $"candidate {_candidateResult.CandidateMap.OccurrenceCount:N0} occurrence(s) · " +
            $"{generatedUnlocked:N0} unlocked generated · {preservedLocks:N0} preserved locks.";
    }

    private void UpdateResourceSummary()
    {
        if (_candidateResult is null)
        {
            _previewResourceSummaryText.Text =
                "Selected resource report details will appear here after generation.";
            return;
        }

        var preview = _previewResource ?? _selectedResource;
        if (preview is null)
        {
            _previewResourceSummaryText.Text =
                "Choose a display resource to inspect one report.";
            return;
        }

        var report = _candidateResult.Reports.FirstOrDefault(candidate =>
            string.Equals(candidate.ResourceId, preview.Definition.Id, StringComparison.Ordinal));
        if (report is null)
        {
            _previewResourceSummaryText.Text =
                $"{preview.Name} was outside the selected generation scope, so its current map entries stayed unchanged.";
            return;
        }

        var warnings = report.Warnings.Count == 0
            ? string.Empty
            : " Warnings: " + string.Join(" ", report.Warnings);
        var shortfall = string.IsNullOrWhiteSpace(report.ShortfallReason)
            ? string.Empty
            : $" Shortfall: {report.ShortfallReason}";
        _previewResourceSummaryText.Text =
            $"{preview.Name} · eligible {report.EligibleTileCount:N0} · " +
            $"target {report.RequestedTileCount:N0} · actual {report.ActualOccurrenceCount:N0} " +
            $"({report.GeneratedOccurrenceCount:N0} generated + {report.PreservedLockCount:N0} locked) · " +
            $"regions {report.RegionCount:N0} · mean {report.MeanPotential:0.#} / 100 · " +
            $"max {report.MaximumPotential:N0} · effective coverage {report.EffectiveCoveragePercent:0.#}% · " +
            $"actual coverage {report.ActualCoveragePercent:0.#}%." +
            shortfall +
            warnings;
    }

    private void UpdateButtonState()
    {
        _useButton.IsEnabled = !_isGenerating && _candidateResult is not null && _candidateMatchesInputs;
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

    private void MarkCandidateStale()
    {
        if (_candidateResult is not null)
        {
            _candidateMatchesInputs = InputsMatchCandidate(_candidateResult);
        }

        _validationPanel.IsVisible = false;
        UpdatePreviewPresentation();
    }

    private bool InputsMatchCandidate(CampaignResourceGenerationResult candidate)
    {
        try
        {
            return candidate.Scope.Equals(BuildScope()) &&
                candidate.Settings.ResourceSeed == ResolveSeedValue() &&
                candidate.Settings.SeedDerivedFromWorld == (_seedDerivedToggle.IsChecked == true) &&
                candidate.Settings.Abundance == GetChoice(AbundanceChoices, _abundanceInput.SelectedIndex).Value &&
                candidate.Settings.Climate == GetChoice(ClimateChoices, _climateInput.SelectedIndex).Value &&
                candidate.Settings.Geology == GetChoice(GeologyChoices, _geologyInput.SelectedIndex).Value &&
                SettingsOverridesMatch(candidate.Settings);
        }
        catch
        {
            return false;
        }
    }

    private bool SettingsOverridesMatch(CampaignResourceGenerationSettings settings)
    {
        if (settings.Overrides.Count != _allResources.Count(item => item.DiffersFromDefault))
        {
            return false;
        }

        foreach (var item in _allResources)
        {
            var effective = settings.GetEffective(item.Definition);
            if (effective.Enabled != item.Enabled ||
                effective.CoveragePercent != item.CoveragePercent ||
                effective.Richness != item.Richness ||
                effective.RichnessBias != item.RichnessBias ||
                effective.Concentration != item.Concentration ||
                effective.MapPriority != item.MapPriority)
            {
                return false;
            }
        }

        return true;
    }

    private CampaignResourceGenerationScope BuildScope()
    {
        return CampaignResourceGenerationScope.ForResources(_includedResourceIds);
    }

    private CampaignResourceGenerationSettings BuildSettings(
        CampaignResourceGenerationSource? capturedSource = null)
    {
        var overrides = _allResources
            .Where(static item => item.DiffersFromDefault)
            .Select(static item => item.ToOverride())
            .ToArray();
        var seedDerived = _seedDerivedToggle.IsChecked == true;
        var seed = seedDerived
            ? (_derivedSeedCache ??= capturedSource is null
                ? ResolveDerivedSeed()
                : CampaignResourceSeed.FromCurrentWorld(capturedSource))
            : ResolveSeedValue();
        _derivedSeedCache = seedDerived ? seed : _derivedSeedCache;
        _seedInput.Value = seed;
        return new CampaignResourceGenerationSettings(
            seed,
            seedDerived,
            GetChoice(AbundanceChoices, _abundanceInput.SelectedIndex).Value,
            GetChoice(ClimateChoices, _climateInput.SelectedIndex).Value,
            GetChoice(GeologyChoices, _geologyInput.SelectedIndex).Value,
            overrides);
    }

    private int ResolveSeedValue() =>
        _seedDerivedToggle.IsChecked == true
            ? ResolveDerivedSeed()
            : decimal.ToInt32(_seedInput.Value ?? 0);

    private string DescribeScope(CampaignResourceGenerationScope scope) => scope.Kind switch
    {
        CampaignResourceGenerationScopeKind.All => "Scope: all resources",
        CampaignResourceGenerationScopeKind.Category => $"Scope: {scope.Category}",
        CampaignResourceGenerationScopeKind.Resource => $"Scope: {scope.ResourceId}",
        CampaignResourceGenerationScopeKind.Selection =>
            $"Scope: {scope.ResourceIds.Count:N0} included, " +
            $"{_currentMap.Catalog.Definitions.Count - scope.ResourceIds.Count:N0} unchanged",
        _ => "Scope: unknown",
    };

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

    private void ResourceGenerationDialog_OnClosed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _viewportSynchronizer.Dispose();
        CancelGeneration();
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Required control '{name}' was not found.");

    private static Choice<T> GetChoice<T>(IReadOnlyList<Choice<T>> choices, int selectedIndex) =>
        choices[Math.Clamp(selectedIndex, 0, choices.Count - 1)];

    private static void SetSelectedChoice<T>(
        ComboBox comboBox,
        IReadOnlyList<Choice<T>> choices,
        T value)
    {
        for (var index = 0; index < choices.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(choices[index].Value, value))
            {
                comboBox.SelectedIndex = index;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
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
        var resourceMap = new CampaignResourceMap(PlaceholderDefinition);
        var terrainQuery = new CampaignResourceTerrainQueryV2(world);
        var settings = new CampaignResourceGenerationSettings(resourceSeed: 0);
        return new DesignContext(world, resourceMap, terrainQuery, settings);
    }

    private sealed record Choice<T>(T Value, string Label, string Description);
}

public sealed class ResourceDefinitionItem : INotifyPropertyChanged
{
    private bool _enabled;
    private int _coveragePercent;
    private CampaignResourceRichness _richness;
    private int _richnessBias;
    private CampaignResourceConcentration _concentration;
    private int _mapPriority;

    public ResourceDefinitionItem(
        CampaignResourceDefinition definition,
        bool enabled,
        int coveragePercent,
        CampaignResourceRichness richness,
        int richnessBias,
        CampaignResourceConcentration concentration,
        int mapPriority,
        bool isCustom)
    {
        Definition = definition;
        _enabled = enabled;
        _coveragePercent = coveragePercent;
        _richness = richness;
        _richnessBias = richnessBias;
        _concentration = concentration;
        _mapPriority = mapPriority;
        IsCustom = isCustom;
        SwatchBrush = new SolidColorBrush(Color.Parse(definition.ColorHex));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CampaignResourceDefinition Definition { get; }

    public string Name => Definition.Name;

    public string IdText => $"ID: {Definition.Id}";

    public string CategoryText => Definition.Category.ToString();

    public IBrush SwatchBrush { get; }

    public bool IsCustom { get; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value))
            {
                OnPropertyChanged(nameof(CoverageSummary));
                OnPropertyChanged(nameof(DiffersFromDefault));
            }
        }
    }

    public int CoveragePercent
    {
        get => _coveragePercent;
        set
        {
            var clamped = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _coveragePercent, clamped))
            {
                OnPropertyChanged(nameof(CoverageSummary));
                OnPropertyChanged(nameof(DiffersFromDefault));
            }
        }
    }

    public CampaignResourceRichness Richness
    {
        get => _richness;
        set
        {
            if (SetProperty(ref _richness, value))
            {
                OnPropertyChanged(nameof(DiffersFromDefault));
            }
        }
    }

    public int RichnessBias
    {
        get => _richnessBias;
        set
        {
            var clamped = Math.Clamp(
                value,
                CampaignResourceGenerationOverride.MinimumRichnessBias,
                CampaignResourceGenerationOverride.MaximumRichnessBias);
            if (SetProperty(ref _richnessBias, clamped))
            {
                OnPropertyChanged(nameof(DiffersFromDefault));
            }
        }
    }

    public CampaignResourceConcentration Concentration
    {
        get => _concentration;
        set
        {
            if (SetProperty(ref _concentration, value))
            {
                OnPropertyChanged(nameof(DiffersFromDefault));
            }
        }
    }

    public int MapPriority
    {
        get => _mapPriority;
        set
        {
            var clamped = Math.Clamp(
                value,
                CampaignResourceDefinition.MinimumMapPriority,
                CampaignResourceDefinition.MaximumMapPriority);
            if (SetProperty(ref _mapPriority, clamped))
            {
                OnPropertyChanged(nameof(DiffersFromDefault));
            }
        }
    }

    public string CoverageSummary => Enabled
        ? $"{CoveragePercent:N0}%"
        : "Off";

    public bool DiffersFromDefault =>
        Enabled != true ||
        CoveragePercent != Definition.CoveragePercent ||
        Richness != Definition.Richness ||
        RichnessBias != 0 ||
        Concentration != Definition.Concentration ||
        MapPriority != Definition.MapPriority;

    public CampaignResourceGenerationOverride ToOverride() => new(
        Definition.Id,
        Enabled,
        CoveragePercent,
        Richness,
        RichnessBias,
        Concentration,
        MapPriority);

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum ResourceListFilter
{
    All = 0,
    Renewable = 1,
    Finite = 2,
}
