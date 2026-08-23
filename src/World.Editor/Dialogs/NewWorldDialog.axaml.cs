using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;
using Kingdom.World.Editor.Controls;

namespace Kingdom.World.Editor.Dialogs;

public sealed partial class NewWorldDialog : Window
{
    private static readonly Choice<CampaignMapGenerationPreset>[] PresetChoices =
    [
        new(CampaignMapGenerationPreset.Blank, "Blank — paint everything yourself", "Every tile starts Unassigned at the default height."),
        new(CampaignMapGenerationPreset.Continent, "Continental world", "Several unequal major landmasses, broad oceans, regional bays, peninsulas, and island arcs."),
        new(CampaignMapGenerationPreset.Island, "Island", "One compact island surrounded by Sea."),
        new(CampaignMapGenerationPreset.Archipelago, "Archipelago", "Several separated islands with channels of Sea between them."),
        new(CampaignMapGenerationPreset.EastCoast, "East coast", "Sea is guaranteed on the east; the west, north, and south boundaries follow the generated geography."),
        new(CampaignMapGenerationPreset.WestCoast, "West coast", "Sea is guaranteed on the west; the east, north, and south boundaries follow the generated geography."),
        new(CampaignMapGenerationPreset.NorthCoast, "North coast", "Sea is guaranteed on the north; the south, east, and west boundaries follow the generated geography."),
        new(CampaignMapGenerationPreset.SouthCoast, "South coast", "Sea is guaranteed on the south; the north, east, and west boundaries follow the generated geography."),
        new(CampaignMapGenerationPreset.InlandSea, "Sea in center", "Land surrounds a central inland Sea; every outside map edge stays land."),
        new(CampaignMapGenerationPreset.LandOnly, "Land only", "No Sea, Lake, River, Large River, or water-facing Cliff tiles are generated."),
    ];

    private static readonly Choice<CampaignMapTerrainStyle>[] TerrainChoices =
    [
        new(CampaignMapTerrainStyle.Gentle, "Gentle", "Lower relief and fewer steep grades."),
        new(CampaignMapTerrainStyle.Balanced, "Balanced", "A practical mix of lowlands, hills, and mountain country."),
        new(CampaignMapTerrainStyle.Rugged, "Rugged", "Stronger ridges, relief, and steep terrain."),
    ];

    private static readonly Choice<CampaignMapMountainDensity>[] MountainDensityChoices =
    [
        new(CampaignMapMountainDensity.Sparse, "One focused range", "Creates one coherent Mountain system."),
        new(CampaignMapMountainDensity.Balanced, "A few ranges", "Creates up to two separated Mountain systems."),
        new(CampaignMapMountainDensity.Dense, "Several ranges", "Creates up to three separated Mountain systems."),
    ];

    private static readonly Choice<CampaignMapHydrology>[] HydrologyChoices =
    [
        new(CampaignMapHydrology.None, "None", "Do not add basin Lakes or drainage Rivers."),
        new(CampaignMapHydrology.Light, "Light", "A small number of Lakes and drainage networks."),
        new(CampaignMapHydrology.Balanced, "Balanced", "Moderate basins with occasional tributary confluences."),
        new(CampaignMapHydrology.Abundant, "Abundant", "More basins and denser hierarchical River drainage."),
    ];

    private static readonly Choice<CampaignMapTidalInlets>[] TidalInletChoices =
    [
        new(CampaignMapTidalInlets.None, "None", "Keeps the normal coastline."),
        new(CampaignMapTidalInlets.Few, "Few", "Offers one short lowland opportunity; the coast may remain unchanged."),
        new(CampaignMapTidalInlets.Balanced, "Balanced", "Tests a few separated lowland opportunities and accepts only suitable routes."),
        new(CampaignMapTidalInlets.Drowned, "Drowned coast", "Raises opportunity, reach, and mouth width without guaranteeing channels."),
    ];

    private static readonly Choice<CampaignMapCoastlineStyle>[] CoastlineChoices =
    [
        new(CampaignMapCoastlineStyle.Smooth, "Smooth shelf", "Uses broad curves with very few major coastal features."),
        new(CampaignMapCoastlineStyle.FlowingCapes, "Flowing bays and capes", "Creates one smooth mainland with a deep bay and a long tapered, curved cape."),
        new(CampaignMapCoastlineStyle.Natural, "Natural mixed coast", "Adds kilometre-scaled bays, headlands, peninsulas, and occasional island groups."),
        new(CampaignMapCoastlineStyle.Rugged, "Rugged coast", "Adds deeper bays, stronger peninsulas, and more offshore island groups."),
    ];

    private readonly NumericUpDown _worldWidth;
    private readonly NumericUpDown _worldHeight;
    private readonly NumericUpDown _campaignTile;
    private readonly NumericUpDown _seaLevel;
    private readonly NumericUpDown _defaultHeight;
    private readonly NumericUpDown _minimumHeight;
    private readonly NumericUpDown _maximumHeight;
    private readonly ComboBox _generationPreset;
    private readonly ComboBox _terrainStyle;
    private readonly ComboBox _mountainDensity;
    private readonly ComboBox _hydrology;
    private readonly ComboBox _tidalInlets;
    private readonly ComboBox _coastlineStyle;
    private readonly CheckBox _customLandMix;
    private readonly StackPanel _landMixPanel;
    private readonly NumericUpDown _plainsRatio;
    private readonly NumericUpDown _forestRatio;
    private readonly NumericUpDown _steppeRatio;
    private readonly NumericUpDown _desertRatio;
    private readonly NumericUpDown _hillsRatio;
    private readonly NumericUpDown _mountainRatio;
    private readonly TextBlock _landMixTotal;
    private readonly NumericUpDown _generationSeed;
    private readonly Button _randomizeSeedButton;
    private readonly TextBlock _generationDescription;
    private readonly TextBlock _generationConstraint;
    private readonly TextBlock _customTerrainSummary;
    private readonly ScrollViewer _settingsScrollViewer;
    private readonly Image _generationPreviewImage;
    private readonly StackPanel _generationPreviewPlaceholder;
    private readonly ProgressBar _generationPreviewProgress;
    private readonly TextBlock _generationPreviewState;
    private readonly TextBlock _generationPreviewSummary;
    private readonly Button _generateButton;
    private readonly Button _usePreviewButton;
    private readonly TextBlock _tileGridPreview;
    private readonly TextBlock _tileCountPreview;
    private readonly Border _validationPanel;
    private readonly TextBlock _validationText;
    private readonly TextBlock _dialogHeading;
    private readonly TextBlock _dialogDescription;
    private readonly TextBlock _definitionMode;
    private readonly TextBlock _startingWorldHeading;
    private readonly TextBlock _startingWorldDescription;
    private readonly TextBlock _previewCommitNotice;
    private readonly TextBlock _previewChangeNotice;
    private readonly Border _resourceImpactPanel;
    private readonly StackPanel _resourceImpactSection;
    private readonly Border _layerImpactDivider;
    private readonly TextBlock _resourceImpactSummary;
    private readonly TextBlock _resourceImpactWarning;
    private readonly Button _manageSeasonsButton;
    private readonly TextBlock _seasonSettingsSummary;
    private readonly Button _terrainPreviewButton;
    private readonly Button _seasonPreviewButton;
    private readonly TextBlock _seasonImpactSummary;
    private readonly TextBlock _seasonImpactWarning;
    private readonly Button _reviewSeasonDropsButton;
    private readonly IReadOnlyList<Choice<CampaignMapGenerationPreset>> _availablePresetChoices;
    private readonly CampaignResourceWorldRegenerationSource? _resourceRegenerationSource;
    private readonly CampaignSeasonWorldRegenerationSource? _seasonRegenerationSource;
    private readonly List<CampaignCustomTerrainDefinition> _customTerrainDefinitions = [];
    private CampaignSeasonCatalog _seasonCatalog;
    private IReadOnlyList<string> _seasonEnabledIds;
    private CampaignWorld? _previewWorld;
    private CampaignMapGenerationResult? _previewGenerationResult;
    private CampaignResourceWorldRegenerationResult? _previewResourceRegenerationResult;
    private CampaignSeasonNewWorldGenerationResult? _previewNewSeasonGenerationResult;
    private CampaignSeasonWorldRegenerationResult? _previewSeasonRegenerationResult;
    private WriteableBitmap? _previewBitmap;
    private WriteableBitmap? _seasonPreviewBitmap;
    private CancellationTokenSource? _generationCancellation;
    private bool _permitLockedSeasonDrops;
    private bool _showingSeasonPreview;
    private bool _previewMatchesSettings;
    private bool _definitionWithinTileLimit = true;
    private bool _isGenerating;
    private bool _isClosed;

    public NewWorldDialog()
        : this(
            currentWorld: null,
            currentResources: null,
            resourceSettings: null,
            currentSeasons: null,
            seasonEnabledIds: null,
            seasonSavedGeneration: null,
            initialOptions: null,
            isRegeneration: false)
    {
    }

    public NewWorldDialog(
        CampaignWorld currentWorld,
        CampaignMapGenerationOptions? initialOptions)
        : this(
            currentWorld,
            new CampaignResourceMap(currentWorld?.Definition ??
                throw new ArgumentNullException(nameof(currentWorld))),
            resourceSettings: null,
            new CampaignSeasonMap(currentWorld.Definition),
            CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
            seasonSavedGeneration: null,
            initialOptions,
            isRegeneration: true)
    {
    }

    public NewWorldDialog(
        CampaignWorld currentWorld,
        CampaignResourceMap currentResources,
        CampaignResourceGenerationSettings? resourceSettings,
        CampaignMapGenerationOptions? initialOptions)
        : this(
            currentWorld,
            currentResources,
            resourceSettings,
            new CampaignSeasonMap(currentWorld?.Definition ??
                throw new ArgumentNullException(nameof(currentWorld))),
            CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds,
            seasonSavedGeneration: null,
            initialOptions,
            isRegeneration: true)
    {
    }

    public NewWorldDialog(
        CampaignWorld currentWorld,
        CampaignResourceMap currentResources,
        CampaignResourceGenerationSettings? resourceSettings,
        CampaignSeasonMap currentSeasons,
        IReadOnlyList<string> seasonEnabledIds,
        CampaignSeasonSavedGeneration? seasonSavedGeneration,
        CampaignMapGenerationOptions? initialOptions)
        : this(
            currentWorld,
            currentResources,
            resourceSettings,
            currentSeasons,
            seasonEnabledIds,
            seasonSavedGeneration,
            initialOptions,
            isRegeneration: true)
    {
    }

    private NewWorldDialog(
        CampaignWorld? currentWorld,
        CampaignResourceMap? currentResources,
        CampaignResourceGenerationSettings? resourceSettings,
        CampaignSeasonMap? currentSeasons,
        IReadOnlyList<string>? seasonEnabledIds,
        CampaignSeasonSavedGeneration? seasonSavedGeneration,
        CampaignMapGenerationOptions? initialOptions,
        bool isRegeneration)
    {
        if (isRegeneration && currentWorld is null)
        {
            throw new ArgumentNullException(nameof(currentWorld));
        }

        _resourceRegenerationSource = isRegeneration
            ? CampaignResourceWorldRegenerationSource.Capture(
                currentWorld!,
                currentResources ?? throw new ArgumentNullException(nameof(currentResources)),
                resourceSettings)
            : null;
        _seasonRegenerationSource = isRegeneration
            ? CampaignSeasonWorldRegenerationSource.Capture(
                currentWorld!,
                currentSeasons ?? throw new ArgumentNullException(nameof(currentSeasons)),
                seasonEnabledIds ?? throw new ArgumentNullException(nameof(seasonEnabledIds)),
                seasonSavedGeneration)
            : null;
        _seasonCatalog = currentSeasons?.Catalog ?? new CampaignSeasonCatalog();
        _seasonEnabledIds = Array.AsReadOnly(
            (seasonEnabledIds ?? CampaignSeasonGenerationSettings.DefaultEnabledSeasonIds).ToArray());
        _availablePresetChoices = isRegeneration ? PresetChoices[1..] : PresetChoices;
        AvaloniaXamlLoader.Load(this);
        _worldWidth = FindRequired<NumericUpDown>("WorldWidthInput");
        _worldHeight = FindRequired<NumericUpDown>("WorldHeightInput");
        _campaignTile = FindRequired<NumericUpDown>("CampaignTileInput");
        _seaLevel = FindRequired<NumericUpDown>("SeaLevelInput");
        _defaultHeight = FindRequired<NumericUpDown>("DefaultHeightInput");
        _minimumHeight = FindRequired<NumericUpDown>("MinimumHeightInput");
        _maximumHeight = FindRequired<NumericUpDown>("MaximumHeightInput");
        _generationPreset = FindRequired<ComboBox>("GenerationPresetInput");
        _terrainStyle = FindRequired<ComboBox>("TerrainStyleInput");
        _mountainDensity = FindRequired<ComboBox>("MountainDensityInput");
        _hydrology = FindRequired<ComboBox>("HydrologyInput");
        _tidalInlets = FindRequired<ComboBox>("TidalInletsInput");
        _coastlineStyle = FindRequired<ComboBox>("CoastlineStyleInput");
        _customLandMix = FindRequired<CheckBox>("CustomLandMixInput");
        _landMixPanel = FindRequired<StackPanel>("LandMixPanel");
        _plainsRatio = FindRequired<NumericUpDown>("PlainsRatioInput");
        _forestRatio = FindRequired<NumericUpDown>("ForestRatioInput");
        _steppeRatio = FindRequired<NumericUpDown>("SteppeRatioInput");
        _desertRatio = FindRequired<NumericUpDown>("DesertRatioInput");
        _hillsRatio = FindRequired<NumericUpDown>("HillsRatioInput");
        _mountainRatio = FindRequired<NumericUpDown>("MountainRatioInput");
        _landMixTotal = FindRequired<TextBlock>("LandMixTotalText");
        _generationSeed = FindRequired<NumericUpDown>("GenerationSeedInput");
        _randomizeSeedButton = FindRequired<Button>("RandomizeSeedButton");
        _generationDescription = FindRequired<TextBlock>("GenerationDescriptionText");
        _generationConstraint = FindRequired<TextBlock>("GenerationConstraintText");
        _customTerrainSummary = FindRequired<TextBlock>("CustomTerrainSummaryText");
        _settingsScrollViewer = FindRequired<ScrollViewer>("SettingsScrollViewer");
        _generationPreviewImage = FindRequired<Image>("GenerationPreviewImage");
        _generationPreviewPlaceholder = FindRequired<StackPanel>("GenerationPreviewPlaceholder");
        _generationPreviewProgress = FindRequired<ProgressBar>("GenerationPreviewProgress");
        _generationPreviewState = FindRequired<TextBlock>("GenerationPreviewStateText");
        _generationPreviewSummary = FindRequired<TextBlock>("GenerationPreviewSummaryText");
        _generateButton = FindRequired<Button>("GenerateButton");
        _usePreviewButton = FindRequired<Button>("UsePreviewButton");
        _tileGridPreview = FindRequired<TextBlock>("TileGridPreviewText");
        _tileCountPreview = FindRequired<TextBlock>("TileCountPreviewText");
        _validationPanel = FindRequired<Border>("ValidationPanel");
        _validationText = FindRequired<TextBlock>("ValidationText");
        _dialogHeading = FindRequired<TextBlock>("DialogHeadingText");
        _dialogDescription = FindRequired<TextBlock>("DialogDescriptionText");
        _definitionMode = FindRequired<TextBlock>("DefinitionModeText");
        _startingWorldHeading = FindRequired<TextBlock>("StartingWorldHeadingText");
        _startingWorldDescription = FindRequired<TextBlock>("StartingWorldDescriptionText");
        _previewCommitNotice = FindRequired<TextBlock>("PreviewCommitNoticeText");
        _previewChangeNotice = FindRequired<TextBlock>("PreviewChangeNoticeText");
        _resourceImpactPanel = FindRequired<Border>("ResourceImpactPanel");
        _resourceImpactSection = FindRequired<StackPanel>("ResourceImpactSection");
        _layerImpactDivider = FindRequired<Border>("LayerImpactDivider");
        _resourceImpactSummary = FindRequired<TextBlock>("ResourceImpactSummaryText");
        _resourceImpactWarning = FindRequired<TextBlock>("ResourceImpactWarningText");
        _manageSeasonsButton = FindRequired<Button>("ManageSeasonsButton");
        _seasonSettingsSummary = FindRequired<TextBlock>("SeasonSettingsSummaryText");
        _terrainPreviewButton = FindRequired<Button>("TerrainPreviewButton");
        _seasonPreviewButton = FindRequired<Button>("SeasonPreviewButton");
        _seasonImpactSummary = FindRequired<TextBlock>("SeasonImpactSummaryText");
        _seasonImpactWarning = FindRequired<TextBlock>("SeasonImpactWarningText");
        _reviewSeasonDropsButton = FindRequired<Button>("ReviewSeasonDropsButton");

        _generationPreset.ItemsSource = _availablePresetChoices.Select(choice => choice.Label).ToArray();
        _generationPreset.SelectedIndex = 0;
        _terrainStyle.ItemsSource = TerrainChoices.Select(choice => choice.Label).ToArray();
        _terrainStyle.SelectedIndex = 1;
        _mountainDensity.ItemsSource = MountainDensityChoices.Select(choice => choice.Label).ToArray();
        _mountainDensity.SelectedIndex = 0;
        _hydrology.ItemsSource = HydrologyChoices.Select(choice => choice.Label).ToArray();
        _hydrology.SelectedIndex = 2;
        _tidalInlets.ItemsSource = TidalInletChoices.Select(choice => choice.Label).ToArray();
        _tidalInlets.SelectedIndex = 0;
        _coastlineStyle.ItemsSource = CoastlineChoices.Select(choice => choice.Label).ToArray();
        _coastlineStyle.SelectedIndex = 2;
        var defaultLandMix = CampaignMapLandMix.Balanced;
        _plainsRatio.Value = defaultLandMix.PlainsPercent;
        _forestRatio.Value = defaultLandMix.ForestPercent;
        _steppeRatio.Value = defaultLandMix.SteppePercent;
        _desertRatio.Value = defaultLandMix.DesertPercent;
        _hillsRatio.Value = defaultLandMix.HillsPercent;
        _mountainRatio.Value = defaultLandMix.MountainPercent;
        _manageSeasonsButton.IsEnabled = !isRegeneration;

        if (isRegeneration)
        {
            ConfigureRegeneration(currentWorld!, initialOptions);
        }
        else
        {
            _definitionMode.Text = "These values become the editable world's fixed grid and height limits.";
        }

        _worldWidth.ValueChanged += (_, _) => DefinitionSettingChanged();
        _worldHeight.ValueChanged += (_, _) => DefinitionSettingChanged();
        _campaignTile.ValueChanged += (_, _) => DefinitionSettingChanged();
        _seaLevel.ValueChanged += (_, _) => DefinitionSettingChanged();
        _defaultHeight.ValueChanged += (_, _) => DefinitionSettingChanged();
        _minimumHeight.ValueChanged += (_, _) => DefinitionSettingChanged();
        _maximumHeight.ValueChanged += (_, _) => DefinitionSettingChanged();
        _generationPreset.SelectionChanged += (_, _) => GenerationSettingChanged();
        _terrainStyle.SelectionChanged += (_, _) => GenerationSettingChanged();
        _mountainDensity.SelectionChanged += (_, _) => GenerationSettingChanged();
        _hydrology.SelectionChanged += (_, _) => GenerationSettingChanged();
        _tidalInlets.SelectionChanged += (_, _) => GenerationSettingChanged();
        _coastlineStyle.SelectionChanged += (_, _) => GenerationSettingChanged();
        _customLandMix.IsCheckedChanged += (_, _) => GenerationSettingChanged();
        _generationSeed.ValueChanged += (_, _) => MarkPreviewStale();
        foreach (var ratioInput in GetLandMixInputs())
        {
            ratioInput.ValueChanged += (_, _) =>
            {
                MarkPreviewStale();
                UpdateLandMixTotal();
                UpdatePreview();
            };
        }

        Opened += (_, _) =>
        {
            _worldWidth.Focus();
        };
        Closed += (_, _) => DisposePreviewResources();
        UpdateCustomTerrainSummary();
        UpdateSeasonSettingsSummary();
        UpdateGenerationControls();
        UpdateResourceImpact();
        UpdateSeasonImpact();
        UpdateDisplayedPreview();
    }

    private void ConfigureRegeneration(
        CampaignWorld currentWorld,
        CampaignMapGenerationOptions? initialOptions)
    {
        Title = "Regenerate World";
        _dialogHeading.Text = "REGENERATE CURRENT WORLD";
        _dialogDescription.Text =
            "Preview a complete replacement. Current world values are loaded below, and every definition value can be changed.";
        _startingWorldHeading.Text = "REGENERATION SETTINGS";
        _startingWorldDescription.Text =
            "Choose a generated shape and mix. Blank is unavailable because this command regenerates terrain.";
        _previewCommitNotice.Text =
            "Current terrain, resources, and seasons stay untouched until Use this world. Accepting installs the reviewed replacement atomically and clears undo history.";
        _previewChangeNotice.Text =
            "Changing a setting keeps the old preview for comparison. Resource impact and Season lock overlap are recalculated with the next preview.";
        _resourceImpactSummary.Text =
            $"Current resource layer: {_resourceRegenerationSource!.Entries.Count:N0} occurrence(s). " +
            "Generate a terrain preview to review its exact resource impact.";
        _resourceImpactWarning.Text =
            "Same-grid replacements preserve resources exactly. Changed grids remap physical tile centres and name every locked out-of-bounds drop.";
        _definitionMode.Text = initialOptions is null
            ? "Definition values start from the current world and are editable. Saved projects keep tiles and custom types, not a generator recipe, so generation controls start from defaults."
            : "Definition values start from the current world and are editable. The last generator settings from this editor session are loaded below.";

        var definition = currentWorld.Definition;
        _worldWidth.Value = WorldUnits.MetersToKilometers(definition.WorldWidthMeters);
        _worldHeight.Value = WorldUnits.MetersToKilometers(definition.WorldHeightMeters);
        _campaignTile.Value = WorldUnits.MetersToKilometers(definition.CampaignTileSizeMeters);
        _seaLevel.Value = definition.SeaLevelMeters;
        _defaultHeight.Value = definition.DefaultTileHeightMeters;
        _minimumHeight.Value = definition.MinimumHeightMeters;
        _maximumHeight.Value = definition.MaximumHeightMeters;
        _customTerrainDefinitions.AddRange(currentWorld.Tiles.CustomTerrainDefinitions);
        var options = initialOptions ?? new CampaignMapGenerationOptions(
            CampaignMapGenerationPreset.Continent,
            Seed: 17_029);
        SetSelectedChoice(
            _generationPreset,
            _availablePresetChoices,
            options.Preset == CampaignMapGenerationPreset.Blank
                ? CampaignMapGenerationPreset.Continent
                : options.Preset);
        SetSelectedChoice(_terrainStyle, TerrainChoices, options.TerrainStyle);
        SetSelectedChoice(_mountainDensity, MountainDensityChoices, options.MountainDensity);
        SetSelectedChoice(_hydrology, HydrologyChoices, options.Hydrology);
        SetSelectedChoice(_tidalInlets, TidalInletChoices, options.TidalInlets);
        SetSelectedChoice(_coastlineStyle, CoastlineChoices, options.CoastlineStyle);
        _generationSeed.Value = options.Seed;

        var customShare = GetCustomTerrainGenerationShare();
        var requiredDefaultShare = CampaignMapLandMix.RequiredTotalPercent - customShare;
        var landMix = options.LandMix is { } previousMix &&
                      previousMix.TotalPercent == requiredDefaultShare
            ? previousMix
            : ScaleLandMix(CampaignMapLandMix.Balanced, requiredDefaultShare);
        ApplyLandMix(landMix);
        _customLandMix.IsChecked = options.LandMix is not null || customShare > 0;
    }

    private void ApplyLandMix(CampaignMapLandMix landMix)
    {
        _plainsRatio.Value = landMix.PlainsPercent;
        _forestRatio.Value = landMix.ForestPercent;
        _steppeRatio.Value = landMix.SteppePercent;
        _desertRatio.Value = landMix.DesertPercent;
        _hillsRatio.Value = landMix.HillsPercent;
        _mountainRatio.Value = landMix.MountainPercent;
    }

    private static CampaignMapLandMix ScaleLandMix(CampaignMapLandMix basis, int targetTotal)
    {
        targetTotal = Math.Clamp(targetTotal, 0, CampaignMapLandMix.RequiredTotalPercent);
        var source = new[]
        {
            basis.PlainsPercent,
            basis.ForestPercent,
            basis.DesertPercent,
            basis.HillsPercent,
            basis.MountainPercent,
            basis.SteppePercent,
        };
        var sourceTotal = source.Sum();
        if (sourceTotal <= 0 || targetTotal == 0)
        {
            return new CampaignMapLandMix(0, 0, 0, 0, 0, 0);
        }

        var scaled = new int[source.Length];
        var remainders = new int[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var numerator = source[index] * targetTotal;
            scaled[index] = numerator / sourceTotal;
            remainders[index] = numerator % sourceTotal;
        }

        var unassigned = targetTotal - scaled.Sum();
        foreach (var index in Enumerable.Range(0, source.Length)
                     .OrderByDescending(index => remainders[index])
                     .ThenBy(static index => index)
                     .Take(unassigned))
        {
            scaled[index]++;
        }

        return new CampaignMapLandMix(
            scaled[0],
            scaled[1],
            scaled[2],
            scaled[3],
            scaled[4],
            scaled[5]);
    }

    private async void Generate_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isGenerating)
        {
            return;
        }

        try
        {
            var definition = CreateDefinition();
            var generationOptions = ReadGenerationOptions();
            CampaignMapGenerator.EnsureCanGenerate(definition, generationOptions);
            if (generationOptions.Preset == CampaignMapGenerationPreset.Blank)
            {
                var generationResult = CampaignMapGenerator.Generate(definition, generationOptions);
                var world = new CampaignWorld(definition, generationResult.CustomTerrainDefinitions);
                var seasons = new CampaignSeasonMap(definition, _seasonCatalog);
                Close(new NewWorldDialogResult(
                    world,
                    generationResult,
                    seasons,
                    _seasonEnabledIds,
                    SeasonSavedGeneration: null,
                    SeasonSupportFields: null));
                return;
            }

            await GeneratePreviewAsync(definition, generationOptions);
        }
        catch (Exception exception) when (
            exception is ArgumentException or OverflowException or
            InvalidOperationException or CampaignTileTopologyException or WorldValidationException)
        {
            ShowValidation(exception.Message);
        }
    }

    private void UsePreview_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isGenerating || !_previewMatchesSettings ||
            _previewWorld is null || _previewGenerationResult is null ||
            (_resourceRegenerationSource is not null &&
             _previewResourceRegenerationResult is null) ||
            (_seasonRegenerationSource is null
                ? _previewNewSeasonGenerationResult is null
                : _previewSeasonRegenerationResult is null ||
                  !_previewSeasonRegenerationResult.Report.CanAccept))
        {
            return;
        }

        var seasonMap = _seasonRegenerationSource is null
            ? _previewNewSeasonGenerationResult!.CandidateMap
            : _previewSeasonRegenerationResult!.CandidateMap;
        var seasonSavedGeneration = _seasonRegenerationSource is null
            ? _previewNewSeasonGenerationResult!.SavedGeneration
            : _previewSeasonRegenerationResult!.SavedGeneration;
        var seasonSupportFields = _seasonRegenerationSource is null
            ? _previewNewSeasonGenerationResult!.GenerationResult.SupportFields
            : _previewSeasonRegenerationResult!.SupportFields;
        Close(new NewWorldDialogResult(
            _previewWorld,
            _previewGenerationResult,
            seasonMap,
            _seasonEnabledIds,
            seasonSavedGeneration,
            seasonSupportFields,
            _previewResourceRegenerationResult,
            _previewSeasonRegenerationResult,
            _previewNewSeasonGenerationResult));
    }

    private async Task GeneratePreviewAsync(
        CampaignWorldDefinition definition,
        CampaignMapGenerationOptions generationOptions)
    {
        _generationCancellation?.Cancel();
        _generationCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _generationCancellation = cancellation;
        _permitLockedSeasonDrops = false;
        SetGenerationBusy(true);

        try
        {
            var preview = await Task.Run(() =>
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var generationResult = CampaignMapGenerator.Generate(definition, generationOptions);
                var world = new CampaignWorld(
                    definition,
                    generationResult.CustomTerrainDefinitions);
                if (generationResult.Tiles.Count > 0)
                {
                    world.Tiles.SetTiles(generationResult.Tiles);
                }

                cancellation.Token.ThrowIfCancellationRequested();
                var resourceRegenerationResult = _resourceRegenerationSource is null
                    ? null
                    : new CampaignResourceWorldRegenerator().Generate(
                        _resourceRegenerationSource,
                        world,
                        cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                var seasonSettings = ResolveSeasonSettings(generationResult);
                var seasonRegenerator = new CampaignSeasonWorldRegenerator();
                CampaignSeasonNewWorldGenerationResult? newSeasonResult = null;
                CampaignSeasonWorldRegenerationResult? seasonRegenerationResult = null;
                if (_seasonRegenerationSource is null)
                {
                    newSeasonResult = seasonRegenerator.GenerateNewWorld(
                        world,
                        _seasonCatalog,
                        seasonSettings,
                        cancellation.Token);
                }
                else
                {
                    seasonRegenerationResult = seasonRegenerator.Generate(
                        _seasonRegenerationSource,
                        world,
                        seasonSettings,
                        cancellationToken: cancellation.Token);
                }

                var seasonMap = newSeasonResult?.CandidateMap ??
                    seasonRegenerationResult!.CandidateMap;
                var savedGeneration = newSeasonResult?.SavedGeneration ??
                    seasonRegenerationResult!.SavedGeneration;
                var supportFields = newSeasonResult?.GenerationResult.SupportFields ??
                    seasonRegenerationResult!.SupportFields;
                cancellation.Token.ThrowIfCancellationRequested();
                return new NewWorldDialogResult(
                    world,
                    generationResult,
                    seasonMap,
                    _seasonEnabledIds,
                    savedGeneration,
                    supportFields,
                    resourceRegenerationResult,
                    seasonRegenerationResult,
                    newSeasonResult);
            }, cancellation.Token);

            cancellation.Token.ThrowIfCancellationRequested();
            _previewWorld = preview.World;
            _previewGenerationResult = preview.GenerationResult;
            _previewResourceRegenerationResult = preview.ResourceRegenerationResult;
            _previewNewSeasonGenerationResult = preview.NewSeasonGenerationResult;
            _previewSeasonRegenerationResult = preview.SeasonRegenerationResult;
            _previewMatchesSettings = true;
            _previewBitmap?.Dispose();
            _previewBitmap = CampaignWorldPreviewRenderer.Render(preview.World);
            _seasonPreviewBitmap?.Dispose();
            _seasonPreviewBitmap = CampaignWorldPreviewRenderer.RenderSeasons(preview.SeasonMap);
            UpdateDisplayedPreview();
            _generationPreviewImage.IsVisible = true;
            _generationPreviewPlaceholder.IsVisible = false;
            _validationPanel.IsVisible = false;
            UpdatePreviewSummary();
            UpdateResourceImpact();
            UpdateSeasonImpact();
        }
        catch (OperationCanceledException)
        {
            // Closing the dialog invalidates the in-flight preview result.
        }
        finally
        {
            if (ReferenceEquals(_generationCancellation, cancellation))
            {
                _generationCancellation.Dispose();
                _generationCancellation = null;
                if (!_isClosed)
                {
                    SetGenerationBusy(false);
                }
            }
        }
    }

    private void RandomizeSeed_OnClick(object? sender, RoutedEventArgs e)
    {
        _generationSeed.Value = Random.Shared.Next(-999_999_999, 1_000_000_000);
        _validationPanel.IsVisible = false;
    }

    private CampaignSeasonGenerationSettings ResolveSeasonSettings(
        CampaignMapGenerationResult terrainResult)
    {
        if (_seasonRegenerationSource?.SavedGeneration is { } saved)
        {
            return saved.Settings;
        }

        return new CampaignSeasonGenerationSettings(
            CampaignSeasonSeed.FromTerrainSeed(terrainResult.Seed),
            seedDerivedFromTerrain: true,
            enabledSeasonIds: _seasonEnabledIds);
    }

    private async void ManageSeasons_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isGenerating || _seasonRegenerationSource is not null)
        {
            return;
        }

        var usageCounts = _seasonCatalog.Definitions.ToDictionary(
            static definition => definition.Id,
            static _ => 0,
            StringComparer.Ordinal);
        var result = await new CustomSeasonsDialog(
                _seasonCatalog,
                _seasonEnabledIds,
                usageCounts)
            .ShowDialog<CustomSeasonsDialogResult?>(this);
        if (result is null)
        {
            return;
        }

        try
        {
            var nextCatalog = new CampaignSeasonCatalog(
                result.CustomDefinitions,
                result.BuiltInDefinitions);
            _seasonCatalog = nextCatalog;
            _seasonEnabledIds = Array.AsReadOnly(
                result.EnabledSeasonIds.Order(StringComparer.Ordinal).ToArray());
            MarkPreviewStale();
            UpdateSeasonSettingsSummary();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            ShowValidation(exception.Message);
        }
    }

    private void UpdateSeasonSettingsSummary()
    {
        _seasonSettingsSummary.Text = _seasonRegenerationSource is null
            ? "Blank worlds start with no Season Occurrences. Generated worlds derive annual Earth-like climate from the terrain seed; " +
              $"{_seasonEnabledIds.Count:N0} enabled definition(s) evaluate independently."
            : "The Season Catalog and generated selection stay fixed while this preview is open. " +
              "Same-grid regeneration preserves every occurrence and lock; changed grids remap locked occurrences and regenerate unlocked ones.";
    }

    private void TerrainPreview_OnClick(object? sender, RoutedEventArgs e)
    {
        _showingSeasonPreview = false;
        UpdateDisplayedPreview();
    }

    private void SeasonPreview_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_seasonPreviewBitmap is null)
        {
            return;
        }

        _showingSeasonPreview = true;
        UpdateDisplayedPreview();
    }

    private void UpdateDisplayedPreview()
    {
        _generationPreviewImage.Source = _showingSeasonPreview && _seasonPreviewBitmap is not null
            ? _seasonPreviewBitmap
            : _previewBitmap;
        SetButtonClass(_terrainPreviewButton, "primary", !_showingSeasonPreview);
        SetButtonClass(_terrainPreviewButton, "quiet", _showingSeasonPreview);
        SetButtonClass(_seasonPreviewButton, "primary", _showingSeasonPreview);
        SetButtonClass(_seasonPreviewButton, "quiet", !_showingSeasonPreview);
        _seasonPreviewButton.IsEnabled = _seasonPreviewBitmap is not null;
    }

    private async void CustomTerrainTypes_OnClick(object? sender, RoutedEventArgs e)
    {
        var updated = await new CustomTerrainTypesDialog(_customTerrainDefinitions)
            .ShowDialog<IReadOnlyList<CampaignCustomTerrainDefinition>?>(this);
        if (updated is null)
        {
            return;
        }

        _customTerrainDefinitions.Clear();
        _customTerrainDefinitions.AddRange(updated);
        MarkPreviewStale();
        if (GetChoice(_availablePresetChoices, _generationPreset.SelectedIndex).Value != CampaignMapGenerationPreset.Blank &&
            GetCustomTerrainGenerationShare() > 0)
        {
            _customLandMix.IsChecked = true;
        }

        UpdateCustomTerrainSummary();
        UpdateGenerationControls();
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void DefinitionSettingChanged()
    {
        MarkPreviewStale();
        UpdatePreview();
    }

    private void GenerationSettingChanged()
    {
        MarkPreviewStale();
        UpdateGenerationControls();
    }

    private void MarkPreviewStale()
    {
        if (_previewGenerationResult is not null)
        {
            _previewMatchesSettings = false;
        }

        _validationPanel.IsVisible = false;
        UpdatePreviewState();
        UpdateResourceImpact();
        UpdateSeasonImpact();
    }

    private void SetGenerationBusy(bool isBusy)
    {
        _isGenerating = isBusy;
        _settingsScrollViewer.IsEnabled = !isBusy;
        _generationPreviewProgress.IsVisible = isBusy;
        UpdatePreviewState();
        UpdateSeasonImpact();
    }

    private void UpdatePreviewState()
    {
        var preset = GetChoice(_availablePresetChoices, _generationPreset.SelectedIndex).Value;
        var isGenerated = preset != CampaignMapGenerationPreset.Blank;
        _usePreviewButton.IsVisible = isGenerated;
        var canUsePreview =
            isGenerated && !_isGenerating && _previewMatchesSettings && _previewWorld is not null &&
            (_resourceRegenerationSource is null || _previewResourceRegenerationResult is not null) &&
            (_seasonRegenerationSource is null
                ? _previewNewSeasonGenerationResult is not null
                : _previewSeasonRegenerationResult?.Report.CanAccept == true);
        _usePreviewButton.IsEnabled = canUsePreview;
        _generateButton.IsEnabled = !_isGenerating && _definitionWithinTileLimit;
        _generateButton.IsDefault = !canUsePreview;
        _usePreviewButton.IsDefault = canUsePreview;
        SetButtonClass(_generateButton, "primary", !canUsePreview);
        SetButtonClass(_generateButton, "quiet", canUsePreview);
        SetButtonClass(_usePreviewButton, "primary", canUsePreview);
        SetButtonClass(_usePreviewButton, "quiet", !canUsePreview);
        _generateButton.Content = isGenerated
            ? _previewGenerationResult is null ? "Generate preview" : "Regenerate preview"
            : "Create blank world";

        if (_isGenerating)
        {
            _generationPreviewState.Text = "Generating preview… Your current editable world has not been replaced.";
        }
        else if (!isGenerated)
        {
            _generationPreviewState.Text = "Blank worlds do not need a generated preview and are created directly.";
        }
        else if (_previewGenerationResult is null)
        {
            _generationPreviewState.Text = "Choose your settings, then generate a preview.";
        }
        else if (!_previewMatchesSettings)
        {
            _generationPreviewState.Text =
                "Settings changed — this is the previous result. Regenerate before using the world.";
        }
        else
        {
            _generationPreviewState.Text =
                $"Preview ready · seed {_previewGenerationResult.Seed:N0}. Use it or adjust settings and regenerate.";
        }
    }

    private void UpdatePreviewSummary()
    {
        if (_previewWorld is null || _previewGenerationResult is null)
        {
            _generationPreviewSummary.Text = "Generated terrain counts and seed will appear here.";
            return;
        }

        var definition = _previewWorld.Definition;
        var result = _previewGenerationResult;
        var totalTiles = Math.Max(1, result.LandTileCount + result.SeaTileCount + result.LakeTileCount);
        var waterTileCount = result.SeaTileCount + result.LakeTileCount;
        var landPercent = result.LandTileCount * 100.0 / totalTiles;
        var waterPercent = waterTileCount * 100.0 / totalTiles;
        _generationPreviewSummary.Text =
            $"{definition.TilesX:N0} × {definition.TilesY:N0} tiles · " +
            $"{result.LandTileCount:N0} land ({landPercent:0.#}%) · " +
            $"{waterTileCount:N0} water ({waterPercent:0.#}%: " +
            $"{result.SeaTileCount:N0} Sea, {result.LakeTileCount:N0} Lake) · " +
            $"{result.RiverTileCount:N0} River " +
            $"({result.LargeRiverTileCount:N0} large, {result.RiverJunctionTileCount:N0} junctions) · " +
            $"{result.CliffTileCount:N0} Cliff · " +
            $"{result.TectonicProvinceCount:N0} tectonic provinces · " +
            $"{result.ErosionPassCount:N0} erosion passes.";
    }

    private void UpdateResourceImpact()
    {
        if (_resourceRegenerationSource is null)
        {
            _resourceImpactSection.IsVisible = false;
            _layerImpactDivider.IsVisible = false;
            return;
        }

        _resourceImpactPanel.IsVisible = true;
        _resourceImpactSection.IsVisible = true;
        _layerImpactDivider.IsVisible = true;
        if (_previewResourceRegenerationResult is null)
        {
            _resourceImpactSummary.Text =
                $"Current resource layer: {_resourceRegenerationSource.Entries.Count:N0} occurrence(s). " +
                "Generate a terrain preview to review its exact resource impact.";
            _resourceImpactWarning.Text =
                "Same-grid replacements preserve resources exactly. Changed grids remap physical tile centres and name every locked out-of-bounds drop.";
            return;
        }

        var report = _previewResourceRegenerationResult.Report;
        var stalePrefix = _previewMatchesSettings
            ? string.Empty
            : "Previous resource impact — settings changed. ";
        _resourceImpactSummary.Text = report.Mode switch
        {
            CampaignResourceLatticeRemapMode.PreserveSameLattice =>
                $"{stalePrefix}Same grid · {report.FinalOccurrenceCount:N0} occurrence(s) keep exact coordinates, potential, locks, and saved generation settings.",
            CampaignResourceLatticeRemapMode.RemapAllOccurrences =>
                $"{stalePrefix}Changed grid · {report.MovedSourceOccurrenceCount:N0} moved · " +
                $"{report.MergedOccurrenceCount:N0} merged · {report.DroppedOccurrenceCount:N0} outside · " +
                $"{report.FinalOccurrenceCount:N0} final. No saved resource recipe exists, so every in-bounds occurrence was remapped.",
            CampaignResourceLatticeRemapMode.RemapLocksAndRegenerateUnlocked =>
                $"{stalePrefix}Changed grid · {report.LockedRetainedOccurrenceCount:N0} locked target(s) retained " +
                $"({report.MovedSourceOccurrenceCount:N0} moved) · " +
                $"{report.ReplacedUnlockedSourceOccurrenceCount:N0} old unlocked replaced · " +
                $"{report.RegeneratedUnlockedOccurrenceCount:N0} unlocked regenerated · " +
                $"{report.FinalOccurrenceCount:N0} final.",
            _ => throw new ArgumentOutOfRangeException(nameof(report.Mode)),
        };

        var warnings = new List<string>();
        if (report.LockedDroppedOccurrenceCount > 0)
        {
            var namedDrops = string.Join(
                ", ",
                report.LockedDrops.Take(8).Select(static drop =>
                    $"{drop.ResourceId} ({drop.SourceX}, {drop.SourceY})"));
            var remaining = report.LockedDrops.Count - Math.Min(8, report.LockedDrops.Count);
            warnings.Add(
                $"WARNING: {report.LockedDroppedOccurrenceCount:N0} locked occurrence(s) lie outside the replacement world: " +
                namedDrops + (remaining > 0 ? $", plus {remaining:N0} more" : string.Empty) + ".");
        }

        var unlockedDrops = report.DroppedOccurrenceCount - report.LockedDroppedOccurrenceCount;
        if (unlockedDrops > 0)
        {
            warnings.Add($"{unlockedDrops:N0} unlocked occurrence(s) lie outside the replacement world.");
        }

        if (report.MergedOccurrenceCount > 0)
        {
            warnings.Add(
                $"{report.MergedOccurrenceCount:N0} same-ID source occurrence(s) merge into coarser target tiles; " +
                "the highest potential is kept and any lock survives.");
        }

        var shortfalls = report.GenerationReports
            .Where(static item =>
                item.RequestedTileCount > 0 &&
                item.ActualOccurrenceCount < item.RequestedTileCount &&
                item.ShortfallReason is not null)
            .ToArray();
        if (shortfalls.Length > 0)
        {
            var names = string.Join(
                ", ",
                shortfalls.Take(4).Select(item =>
                    _resourceRegenerationSource.Catalog.Get(item.ResourceId).Name));
            warnings.Add(
                $"{shortfalls.Length:N0} resource type(s) are below their requested upper target on the new terrain: " +
                names + (shortfalls.Length > 4 ? ", and others." : "."));
        }

        _resourceImpactWarning.Text = warnings.Count == 0
            ? "No same-ID merges or out-of-bounds resource drops in this candidate."
            : string.Join(" ", warnings);
    }

    private void UpdateSeasonImpact()
    {
        var stalePrefix = _previewMatchesSettings
            ? string.Empty
            : "Previous Season impact — settings changed. ";
        _reviewSeasonDropsButton.IsVisible = false;
        if (_seasonRegenerationSource is null)
        {
            if (_previewNewSeasonGenerationResult is null)
            {
                _seasonImpactSummary.Text =
                    "Generated worlds create independent Earth-like Season Occurrences after terrain. " +
                    "Blank worlds start with an empty Season Layer.";
                _seasonImpactWarning.Text =
                    "Terrain and seasons are accepted together; a failure or cancellation applies neither.";
                return;
            }

            var result = _previewNewSeasonGenerationResult;
            _seasonImpactSummary.Text = stalePrefix +
                $"Complete candidate · seed {result.SavedGeneration.Settings.SeasonSeed:N0} · " +
                GetSeasonDistribution(result.CandidateMap);
            _seasonImpactWarning.Text =
                "No Season locks exist in a newly generated world. Every generated occurrence remains editable.";
            return;
        }

        if (_previewSeasonRegenerationResult is null)
        {
            _seasonImpactSummary.Text =
                $"Current Season Layer: {_seasonRegenerationSource.Entries.Count:N0} occurrence(s), " +
                $"{_seasonRegenerationSource.Entries.Count(static value => value.Occurrence.Locked):N0} locked. " +
                "Generate a terrain preview to review exact Season impact.";
            _seasonImpactWarning.Text =
                "Same grids preserve exact authority. Changed grids remap locked occurrences by physical centre and regenerate unlocked occurrences.";
            return;
        }

        var report = _previewSeasonRegenerationResult.Report;
        _seasonImpactSummary.Text = report.Mode switch
        {
            CampaignSeasonLatticeRemapMode.PreserveSameLattice =>
                $"{stalePrefix}Same grid · all {_previewSeasonRegenerationResult.CandidateMap.OccurrenceCount:N0} occurrences " +
                $"and {report.FinalLockedOccurrenceCount:N0} lock(s) are preserved exactly.",
            CampaignSeasonLatticeRemapMode.RemapLocksAndRegenerateUnlocked =>
                $"{stalePrefix}Changed grid · {report.SourceLockedOccurrenceCount:N0} source lock(s) → " +
                $"{report.FinalLockedOccurrenceCount:N0} target lock(s) · {report.MovedLockedOccurrenceCount:N0} moved · " +
                $"{report.MergedLockedOccurrenceCount:N0} same-ID merge(s). " +
                GetSeasonDistribution(_previewSeasonRegenerationResult.CandidateMap),
            _ => throw new ArgumentOutOfRangeException(nameof(report.Mode)),
        };

        var warnings = new List<string>();
        if (report.LockedDrops.Count > 0)
        {
            warnings.Add(report.DropsPermitted
                ? $"Reviewed: {report.LockedDrops.Count:N0} locked source occurrence(s) outside the new world will be dropped."
                : $"BLOCKED: {report.LockedDrops.Count:N0} locked source occurrence(s) have no target inside the new world.");
        }

        if (warnings.Count == 0)
        {
            warnings.Add(report.SameLattice
                ? "No lock remap is required."
                : "Different Season Occurrences coexist on each tile; only out-of-bounds locked occurrences require review.");
        }

        _seasonImpactWarning.Text = string.Join(" ", warnings);
        _reviewSeasonDropsButton.IsVisible =
            report.LockedDrops.Count > 0;
        _reviewSeasonDropsButton.IsEnabled = _previewMatchesSettings && !_isGenerating;
        _reviewSeasonDropsButton.Content = report.CanAccept
            ? "Review locked Season drops…"
            : "Review locked Season drops…";
    }

    private string GetSeasonDistribution(
        CampaignSeasonMap seasons,
        IEnumerable<CampaignTileCoordinate>? excludedCoordinates = null)
    {
        var counts = seasons.GetUsageCounts(
            seasons.Catalog.Definitions.Select(static definition => definition.Id));
        var countedTileCount = seasons.Definition.TileCount;
        var populated = seasons.Catalog.Definitions
            .Select(definition => new
            {
                definition.Name,
                Count = counts[definition.Id],
            })
            .Where(static value => value.Count > 0)
            .OrderByDescending(static value => value.Count)
            .ThenBy(static value => value.Name, StringComparer.Ordinal)
            .ToArray();
        var shown = string.Join(
            ", ",
            populated.Take(6).Select(value =>
                $"{value.Name} {value.Count * 100d / Math.Max(1, countedTileCount):0.#}%"));
        var distribution = populated.Length > 6 ? shown + ", others" : shown;
        return distribution.Length == 0 ? "no occurrences" : distribution;
    }

    private async void ReviewSeasonDrops_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isGenerating ||
            !_previewMatchesSettings ||
            _seasonRegenerationSource is null ||
            _previewSeasonRegenerationResult is not { } current ||
            current.Report.SameLattice)
        {
            return;
        }

        var decision = await new ChoiceDialog(
                "Locked Season Occurrences",
                "Permit locked occurrences outside the new world to be dropped?",
                $"{current.Report.LockedDrops.Count:N0} locked Season Occurrence(s) have no target inside the replacement world. " +
                "Accepting this loss affects only those out-of-bounds identities; all remaining Season IDs may coexist on their target tiles.",
                "Permit drops",
                cancelText: "Keep blocked")
            .ShowDialog<DialogChoice>(this);
        if (decision != DialogChoice.Primary)
        {
            return;
        }

        _permitLockedSeasonDrops = true;
        await RegenerateSeasonCandidateAsync();
    }

    private async Task RegenerateSeasonCandidateAsync()
    {
        if (_previewWorld is not { } world ||
            _previewGenerationResult is not { } terrainResult ||
            _seasonRegenerationSource is null)
        {
            return;
        }

        _generationCancellation?.Cancel();
        _generationCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _generationCancellation = cancellation;
        SetGenerationBusy(true);
        try
        {
            var result = await Task.Run(
                () => new CampaignSeasonWorldRegenerator().Generate(
                    _seasonRegenerationSource,
                    world,
                    ResolveSeasonSettings(terrainResult),
                    permitLockedDrops: _permitLockedSeasonDrops,
                    cancellationToken: cancellation.Token),
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            _previewSeasonRegenerationResult = result;
            _seasonPreviewBitmap?.Dispose();
            _seasonPreviewBitmap = CampaignWorldPreviewRenderer.RenderSeasons(result.CandidateMap);
            UpdateDisplayedPreview();
            _validationPanel.IsVisible = false;
            UpdateSeasonImpact();
        }
        catch (OperationCanceledException)
        {
            // Closing or starting another preview invalidates this result.
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or WorldValidationException)
        {
            ShowValidation(exception.Message);
        }
        finally
        {
            if (ReferenceEquals(_generationCancellation, cancellation))
            {
                _generationCancellation.Dispose();
                _generationCancellation = null;
                if (!_isClosed)
                {
                    SetGenerationBusy(false);
                }
            }
        }
    }

    private void ShowValidation(string message)
    {
        _validationText.Text = message;
        _validationPanel.IsVisible = true;
    }

    private void DisposePreviewResources()
    {
        _isClosed = true;
        _generationCancellation?.Cancel();
        _generationPreviewImage.Source = null;
        _previewBitmap?.Dispose();
        _previewBitmap = null;
        _seasonPreviewBitmap?.Dispose();
        _seasonPreviewBitmap = null;
    }

    private static void SetButtonClass(Button button, string className, bool isEnabled)
    {
        if (isEnabled && !button.Classes.Contains(className))
        {
            button.Classes.Add(className);
        }
        else if (!isEnabled)
        {
            button.Classes.Remove(className);
        }
    }

    private void UpdateGenerationControls()
    {
        var presetChoice = GetChoice(_availablePresetChoices, _generationPreset.SelectedIndex);
        var isGenerated = presetChoice.Value != CampaignMapGenerationPreset.Blank;
        var isLandOnly = presetChoice.Value == CampaignMapGenerationPreset.LandOnly;
        var isDirectionalCoast = IsDirectionalCoast(presetChoice.Value);
        var requiresCombinedMix = isGenerated && GetCustomTerrainGenerationShare() > 0;
        if (requiresCombinedMix && _customLandMix.IsChecked != true)
        {
            _customLandMix.IsChecked = true;
        }

        _generationDescription.Text = presetChoice.Description;
        _terrainStyle.IsEnabled = isGenerated;
        _mountainDensity.IsEnabled = isGenerated;
        _hydrology.IsEnabled = isGenerated && !isLandOnly;
        _tidalInlets.IsEnabled = isGenerated && !isLandOnly;
        _coastlineStyle.IsEnabled = isGenerated && isDirectionalCoast;
        _customLandMix.IsEnabled = isGenerated && !requiresCombinedMix;
        _landMixPanel.IsVisible = isGenerated && (_customLandMix.IsChecked == true || requiresCombinedMix);
        _generationSeed.IsEnabled = isGenerated;
        _randomizeSeedButton.IsEnabled = isGenerated;
        UpdateLandMixTotal();
        UpdatePreview();
        UpdatePreviewState();
    }

    private void UpdatePreview()
    {
        _definitionWithinTileLimit = false;
        try
        {
            var widthKilometers = ReadWholeKilometers(_worldWidth);
            var heightKilometers = ReadWholeKilometers(_worldHeight);
            var tileKilometers = ReadWholeKilometers(_campaignTile);
            if (widthKilometers <= 0 || heightKilometers <= 0 || tileKilometers <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileKilometers));
            }

            if (widthKilometers % tileKilometers != 0 || heightKilometers % tileKilometers != 0)
            {
                _tileGridPreview.Text = "World dimensions must divide exactly by tile size";
                _tileCountPreview.Text = "Adjust width, height, or tile size so there are no partial campaign tiles.";
                _generationConstraint.Text = string.Empty;
                return;
            }

            var tilesX = widthKilometers / tileKilometers;
            var tilesY = heightKilometers / tileKilometers;
            var tileCount = checked(tilesX * tilesY);
            _definitionWithinTileLimit = tileCount <= CampaignWorldDefinition.MaximumTileCount;
            _tileGridPreview.Text = $"{tilesX:N0} × {tilesY:N0} campaign tiles";
            _tileCountPreview.Text =
                $"{tileCount:N0} total · each stamp fills one {tileKilometers:N0} × {tileKilometers:N0} km tile.";
            var preset = GetChoice(_availablePresetChoices, _generationPreset.SelectedIndex).Value;
            if (tileCount > CampaignWorldDefinition.MaximumTileCount)
            {
                _generationConstraint.Text =
                    $"The editor supports up to {CampaignWorldDefinition.MaximumTileCount:N0} tiles; " +
                    "increase tile size or reduce the world dimensions.";
            }
            else if (preset == CampaignMapGenerationPreset.Blank)
            {
                _generationConstraint.Text =
                    $"Blank creates no tile overrides; all {tileCount:N0} tiles remain inside the shared editor limit.";
            }
            else if (tilesX < CampaignMapGenerator.MinimumGeneratedTilesPerAxis ||
                     tilesY < CampaignMapGenerator.MinimumGeneratedTilesPerAxis)
            {
                _generationConstraint.Text =
                    $"Generation needs at least {CampaignMapGenerator.MinimumGeneratedTilesPerAxis} × " +
                    $"{CampaignMapGenerator.MinimumGeneratedTilesPerAxis} tiles.";
            }
            else if (preset == CampaignMapGenerationPreset.LandOnly)
            {
                _generationConstraint.Text = _customLandMix.IsChecked == true
                    ? GetCustomMixConstraint(
                        "Land only suppresses hydrology, so the ratios apply to the complete map.")
                    : "Land only suppresses hydrology so every generated tile remains land.";
            }
            else
            {
                var terrain = GetChoice(TerrainChoices, _terrainStyle.SelectedIndex);
                var mountainDensity = GetChoice(MountainDensityChoices, _mountainDensity.SelectedIndex);
                var hydrology = GetChoice(HydrologyChoices, _hydrology.SelectedIndex);
                var tidalInlets = GetChoice(TidalInletChoices, _tidalInlets.SelectedIndex);
                var coastline = GetChoice(CoastlineChoices, _coastlineStyle.SelectedIndex);
                var coastlineDescription = IsDirectionalCoast(preset)
                    ? $"{coastline.Description} "
                    : string.Empty;
                _generationConstraint.Text = _customLandMix.IsChecked == true
                    ? GetCustomMixConstraint(
                        $"{terrain.Description} {mountainDensity.Description} {coastlineDescription}{hydrology.Description} {tidalInlets.Description}")
                    : $"{terrain.Description} {mountainDensity.Description} {coastlineDescription}{hydrology.Description} {tidalInlets.Description} " +
                      "Same settings and seed reproduce the same start.";
            }

            _validationPanel.IsVisible = false;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            _tileGridPreview.Text = "Preview unavailable";
            _tileCountPreview.Text = exception.Message;
            _generationConstraint.Text = string.Empty;
        }
        finally
        {
            UpdatePreviewState();
        }
    }

    private CampaignWorldDefinition CreateDefinition() =>
        CampaignWorldDefinition.Create(
            ReadMetersFromKilometers(_worldWidth),
            ReadMetersFromKilometers(_worldHeight),
            ReadCampaignTileMeters(_campaignTile),
            ReadShort(_seaLevel),
            ReadShort(_minimumHeight),
            ReadShort(_maximumHeight),
            ReadShort(_defaultHeight));

    private CampaignMapGenerationOptions ReadGenerationOptions()
    {
        var preset = GetChoice(_availablePresetChoices, _generationPreset.SelectedIndex).Value;
        var terrain = GetChoice(TerrainChoices, _terrainStyle.SelectedIndex).Value;
        var mountainDensity = GetChoice(MountainDensityChoices, _mountainDensity.SelectedIndex).Value;
        var hydrology = preset is CampaignMapGenerationPreset.Blank or CampaignMapGenerationPreset.LandOnly
            ? CampaignMapHydrology.None
            : GetChoice(HydrologyChoices, _hydrology.SelectedIndex).Value;
        var tidalInlets = preset is CampaignMapGenerationPreset.Blank or CampaignMapGenerationPreset.LandOnly
            ? CampaignMapTidalInlets.None
            : GetChoice(TidalInletChoices, _tidalInlets.SelectedIndex).Value;
        var coastlineStyle = IsDirectionalCoast(preset)
            ? GetChoice(CoastlineChoices, _coastlineStyle.SelectedIndex).Value
            : CampaignMapCoastlineStyle.Natural;
        CampaignMapLandMix? landMix = preset != CampaignMapGenerationPreset.Blank && _customLandMix.IsChecked == true
            ? ReadLandMix()
            : null;
        return new CampaignMapGenerationOptions(
            preset,
            decimal.ToInt32(_generationSeed.Value ?? 0),
            terrain,
            hydrology,
            mountainDensity,
            landMix,
            tidalInlets,
            _customTerrainDefinitions.ToArray(),
            coastlineStyle);
    }

    private void UpdateCustomTerrainSummary()
    {
        if (_customTerrainDefinitions.Count == 0)
        {
            _customTerrainSummary.Text = "No custom types. Add one for manual painting or deterministic generation.";
            return;
        }

        var generatedCount = _customTerrainDefinitions.Count(definition => definition.GenerationSharePercent > 0);
        var generatedShare = GetCustomTerrainGenerationShare();
        _customTerrainSummary.Text = generatedCount == 0
            ? $"{_customTerrainDefinitions.Count:N0} paint-only type(s). Set a share to include them in generation."
            : $"{_customTerrainDefinitions.Count:N0} type(s) · {generatedCount:N0} in generation · " +
              $"{generatedShare:N0}% of the inland mix. Set the default types to the remaining " +
              $"{CampaignMapLandMix.RequiredTotalPercent - generatedShare:N0}%.";
    }

    private CampaignMapLandMix ReadLandMix() => new(
        ReadWholePercent(_plainsRatio),
        ReadWholePercent(_forestRatio),
        ReadWholePercent(_desertRatio),
        ReadWholePercent(_hillsRatio),
        ReadWholePercent(_mountainRatio),
        ReadWholePercent(_steppeRatio));

    private IReadOnlyList<NumericUpDown> GetLandMixInputs() =>
    [
        _plainsRatio,
        _forestRatio,
        _steppeRatio,
        _desertRatio,
        _hillsRatio,
        _mountainRatio,
    ];

    private void UpdateLandMixTotal()
    {
        var defaultTotal = GetLandMixInputs().Sum(input => decimal.ToInt32(input.Value ?? 0));
        var customTotal = GetCustomTerrainGenerationShare();
        var total = defaultTotal + customTotal;
        _landMixTotal.Text = customTotal == 0
            ? total == CampaignMapLandMix.RequiredTotalPercent
                ? $"Total: {total}% — ready"
                : $"Total: {total}% — adjust to {CampaignMapLandMix.RequiredTotalPercent}%"
            : total == CampaignMapLandMix.RequiredTotalPercent
                ? $"Total: {defaultTotal}% default + {customTotal}% custom = {total}% — ready"
                : $"Total: {defaultTotal}% default + {customTotal}% custom = {total}% — adjust to {CampaignMapLandMix.RequiredTotalPercent}%";
    }

    private string GetCustomMixConstraint(string prefix)
    {
        var mix = ReadLandMix();
        var customShare = GetCustomTerrainGenerationShare();
        var total = mix.TotalPercent + customShare;
        return total == CampaignMapLandMix.RequiredTotalPercent
            ? $"{prefix} Default and custom terrain types form one inland mix. Same settings and seed reproduce the same start."
            : $"Default ratios ({mix.TotalPercent}%) plus custom terrain ({customShare}%) total {total}%; " +
              $"adjust them to {CampaignMapLandMix.RequiredTotalPercent}% before generating.";
    }

    private int GetCustomTerrainGenerationShare() =>
        _customTerrainDefinitions.Sum(static definition => definition.GenerationSharePercent);

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Required control '{name}' was not found.");

    private static Choice<T> GetChoice<T>(IReadOnlyList<Choice<T>> choices, int selectedIndex) =>
        choices[Math.Clamp(selectedIndex, 0, choices.Count - 1)];

    private static void SetSelectedChoice<T>(
        ComboBox comboBox,
        IReadOnlyList<Choice<T>> choices,
        T value)
    {
        var index = -1;
        for (var choiceIndex = 0; choiceIndex < choices.Count; choiceIndex++)
        {
            if (EqualityComparer<T>.Default.Equals(choices[choiceIndex].Value, value))
            {
                index = choiceIndex;
                break;
            }
        }

        comboBox.SelectedIndex = index >= 0 ? index : 0;
    }

    private static bool IsDirectionalCoast(CampaignMapGenerationPreset preset) =>
        preset is CampaignMapGenerationPreset.EastCoast or
            CampaignMapGenerationPreset.WestCoast or
            CampaignMapGenerationPreset.NorthCoast or
            CampaignMapGenerationPreset.SouthCoast;

    private static long ReadMetersFromKilometers(NumericUpDown input) =>
        WorldUnits.KilometersToMeters(ReadWholeKilometers(input));

    private static int ReadCampaignTileMeters(NumericUpDown input) =>
        checked((int)ReadMetersFromKilometers(input));

    private static long ReadWholeKilometers(NumericUpDown input)
    {
        var value = input.Value ?? 0;
        if (decimal.Truncate(value) != value)
        {
            throw new ArgumentException("World dimensions and campaign tile size must use whole kilometres.");
        }

        return decimal.ToInt64(value);
    }

    private static short ReadShort(NumericUpDown input) => decimal.ToInt16(input.Value ?? 0);

    private static int ReadWholePercent(NumericUpDown input)
    {
        var value = input.Value ?? 0;
        if (decimal.Truncate(value) != value)
        {
            throw new ArgumentException("Tile ratios must use whole percentages.");
        }

        return decimal.ToInt32(value);
    }

    private sealed record Choice<T>(T Value, string Label, string Description);

    private sealed record SeasonChoice(string Id, string Name)
    {
        public override string ToString() => $"{Name} ({Id})";
    }
}
