using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Editor.Dialogs;

public sealed partial class CustomSeasonsDialog : Window
{
    private readonly ObservableCollection<SeasonDefinitionEditorItem> _definitions = [];
    private readonly Dictionary<string, string> _deletedReplacements = new(StringComparer.Ordinal);
    private readonly ListBox _definitionsList;
    private readonly ComboBox _replacementInput;
    private readonly Button _deleteButton;
    private readonly StackPanel _detailsPanel;
    private readonly TextBlock _identityHelpText;
    private readonly TextBox _nameInput;
    private readonly TextBox _idInput;
    private readonly ComboBox _fallbackInput;
    private readonly TextBox _colorInput;
    private readonly Border _colorPreview;
    private readonly NumericUpDown _tintInput;
    private readonly NumericUpDown _effectInput;
    private readonly CheckBox _generationEnabledInput;
    private readonly TextBox _latitudeInput;
    private readonly TextBox _elevationInput;
    private readonly TextBox _temperatureInput;
    private readonly TextBox _moistureInput;
    private readonly TextBox _warmTemperatureInput;
    private readonly TextBox _coldTemperatureInput;
    private readonly TextBox _annualRangeInput;
    private readonly TextBox _seasonalityInput;
    private readonly TextBox _seaDistanceInput;
    private readonly TextBox _lakeDistanceInput;
    private readonly TextBox _riverDistanceInput;
    private readonly TextBox _terrainIncludesInput;
    private readonly TextBox _terrainExcludesInput;
    private readonly TextBox _customIncludesInput;
    private readonly TextBox _customExcludesInput;
    private readonly Border _validationPanel;
    private readonly TextBlock _validationText;
    private SeasonDefinitionEditorItem? _formItem;
    private bool _synchronizing;

    public CustomSeasonsDialog()
    {
        AvaloniaXamlLoader.Load(this);
        _definitionsList = FindRequired<ListBox>("DefinitionsList");
        _replacementInput = FindRequired<ComboBox>("ReplacementInput");
        _deleteButton = FindRequired<Button>("DeleteButton");
        _detailsPanel = FindRequired<StackPanel>("DetailsPanel");
        _identityHelpText = FindRequired<TextBlock>("IdentityHelpText");
        _nameInput = FindRequired<TextBox>("NameInput");
        _idInput = FindRequired<TextBox>("IdInput");
        _fallbackInput = FindRequired<ComboBox>("FallbackInput");
        _colorInput = FindRequired<TextBox>("ColorInput");
        _colorPreview = FindRequired<Border>("ColorPreview");
        _tintInput = FindRequired<NumericUpDown>("TintInput");
        _effectInput = FindRequired<NumericUpDown>("EffectInput");
        _generationEnabledInput = FindRequired<CheckBox>("GenerationEnabledInput");
        _latitudeInput = FindRequired<TextBox>("LatitudeInput");
        _elevationInput = FindRequired<TextBox>("ElevationInput");
        _temperatureInput = FindRequired<TextBox>("TemperatureInput");
        _moistureInput = FindRequired<TextBox>("MoistureInput");
        _warmTemperatureInput = FindRequired<TextBox>("WarmTemperatureInput");
        _coldTemperatureInput = FindRequired<TextBox>("ColdTemperatureInput");
        _annualRangeInput = FindRequired<TextBox>("AnnualRangeInput");
        _seasonalityInput = FindRequired<TextBox>("SeasonalityInput");
        _seaDistanceInput = FindRequired<TextBox>("SeaDistanceInput");
        _lakeDistanceInput = FindRequired<TextBox>("LakeDistanceInput");
        _riverDistanceInput = FindRequired<TextBox>("RiverDistanceInput");
        _terrainIncludesInput = FindRequired<TextBox>("TerrainIncludesInput");
        _terrainExcludesInput = FindRequired<TextBox>("TerrainExcludesInput");
        _customIncludesInput = FindRequired<TextBox>("CustomIncludesInput");
        _customExcludesInput = FindRequired<TextBox>("CustomExcludesInput");
        _validationPanel = FindRequired<Border>("ValidationPanel");
        _validationText = FindRequired<TextBlock>("ValidationText");

        _definitionsList.ItemsSource = _definitions;
        _fallbackInput.ItemsSource = Enum.GetValues<CampaignBuiltInSeason>();
        _definitionsList.SelectionChanged += (_, _) => SelectionChanged();
        _colorInput.TextChanged += (_, _) => UpdateColorPreview(_colorInput.Text);
        UpdateFormFromSelection();
    }

    public CustomSeasonsDialog(
        CampaignSeasonCatalog catalog,
        IReadOnlyList<string> enabledSeasonIds,
        IReadOnlyDictionary<string, int> usageCounts)
        : this()
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(enabledSeasonIds);
        ArgumentNullException.ThrowIfNull(usageCounts);
        var enabled = enabledSeasonIds.ToHashSet(StringComparer.Ordinal);
        if (enabled.Count != enabledSeasonIds.Count || enabled.Any(id => !catalog.Contains(id)))
        {
            throw new ArgumentException("Enabled seasons contain an unknown or duplicate ID.", nameof(enabledSeasonIds));
        }

        foreach (var definition in catalog.Definitions)
        {
            _definitions.Add(SeasonDefinitionEditorItem.FromDefinition(
                definition,
                catalog.IsBuiltIn(definition.Id),
                usageCounts.GetValueOrDefault(definition.Id),
                enabled.Contains(definition.Id),
                canEditId: false));
        }

        _definitionsList.SelectedIndex = _definitions.Count > 0 ? 0 : -1;
        RefreshReplacementChoices();
        UpdateFormFromSelection();
    }

    private void SelectionChanged()
    {
        SaveFormToCurrentItem();
        UpdateFormFromSelection();
    }

    private void MoveUp_OnClick(object? sender, RoutedEventArgs e) => MoveSelected(-1);

    private void MoveDown_OnClick(object? sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int offset)
    {
        SaveFormToCurrentItem();
        if (_definitionsList.SelectedItem is not SeasonDefinitionEditorItem selected)
        {
            return;
        }

        var index = _definitions.IndexOf(selected);
        var target = index + offset;
        if ((uint)target >= (uint)_definitions.Count)
        {
            return;
        }

        _definitions.Move(index, target);
        _definitionsList.SelectedItem = selected;
    }

    private void Add_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveFormToCurrentItem();
        var id = CreateUniqueId("custom-season");
        var item = SeasonDefinitionEditorItem.FromDefinition(
            new CampaignSeasonDefinition(
                id,
                "Custom Season",
                CampaignBuiltInSeason.Spring,
                "#7DAA62",
                tintStrengthPercent: 45,
                effectIntensityPercent: 40),
            isBuiltIn: false,
            usageCount: 0,
            generationEnabled: false,
            canEditId: true);
        _definitions.Add(item);
        _definitionsList.SelectedItem = item;
        RefreshReplacementChoices();
    }

    private void Duplicate_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveFormToCurrentItem();
        if (_definitionsList.SelectedItem is not SeasonDefinitionEditorItem selected)
        {
            return;
        }

        var definition = selected.ToDefinition();
        var id = CreateUniqueId(definition.Id + "-copy");
        var duplicate = SeasonDefinitionEditorItem.FromDefinition(
            new CampaignSeasonDefinition(
                id,
                definition.Name + " Copy",
                definition.Fallback,
                definition.ColorHex,
                definition.TintStrengthPercent,
                definition.EffectIntensityPercent,
                definition.Rule),
            isBuiltIn: false,
            usageCount: 0,
            generationEnabled: false,
            canEditId: true);
        _definitions.Add(duplicate);
        _definitionsList.SelectedItem = duplicate;
        RefreshReplacementChoices();
    }

    private void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveFormToCurrentItem();
        if (_definitionsList.SelectedItem is not SeasonDefinitionEditorItem selected || selected.IsBuiltIn)
        {
            ShowValidation("Built-in season identity cannot be deleted.");
            return;
        }

        SeasonDefinitionEditorItem? replacementItem = null;
        if (RequiresReplacement(selected))
        {
            if (_replacementInput.SelectedItem is not SeasonReplacementChoice replacement ||
                string.Equals(replacement.Id, selected.Id, StringComparison.Ordinal))
            {
                ShowValidation(
                    $"{selected.Name} is referenced by Season Occurrences. " +
                    "Choose a remaining replacement first.");
                return;
            }

            _deletedReplacements[selected.OriginalId] = replacement.Id;
            replacementItem = _definitions.First(item =>
                string.Equals(item.Id, replacement.Id, StringComparison.Ordinal));
        }

        var index = _definitions.IndexOf(selected);
        if (selected.GenerationEnabled && replacementItem is not null)
        {
            var replacementIndex = _definitions.IndexOf(replacementItem);
            _definitions.Remove(selected);
            _definitions.Remove(replacementItem);
            var targetIndex = replacementIndex < index ? index - 1 : index;
            replacementItem.GenerationEnabled = true;
            replacementItem.RefreshDerived();
            _definitions.Insert(Math.Min(targetIndex, _definitions.Count), replacementItem);
            _definitionsList.SelectedItem = replacementItem;
        }
        else
        {
            _definitions.Remove(selected);
            _definitionsList.SelectedItem = replacementItem;
            if (_definitionsList.SelectedItem is null)
            {
                _definitionsList.SelectedIndex = _definitions.Count == 0
                    ? -1
                    : Math.Min(index, _definitions.Count - 1);
            }
        }

        RefreshReplacementChoices();
        HideValidation();
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void Apply_OnClick(object? sender, RoutedEventArgs e)
    {
        SaveFormToCurrentItem();
        try
        {
            var definitions = _definitions.Select(static item => item.ToDefinition()).ToArray();
            var duplicateId = definitions
                .GroupBy(static value => value.Id, StringComparer.Ordinal)
                .FirstOrDefault(static group => group.Count() > 1)?.Key;
            if (duplicateId is not null)
            {
                throw new ArgumentException($"Season stable ID '{duplicateId}' appears more than once.");
            }

            var builtIns = _definitions
                .Zip(definitions)
                .Where(static pair => pair.First.IsBuiltIn)
                .Select(static pair => pair.Second)
                .ToArray();
            var custom = _definitions
                .Zip(definitions)
                .Where(static pair => !pair.First.IsBuiltIn)
                .Select(static pair => pair.Second)
                .ToArray();
            var catalog = new CampaignSeasonCatalog(custom, builtIns);
            var enabledIds = _definitions
                .Zip(definitions)
                .Where(static pair => pair.First.GenerationEnabled)
                .Select(static pair => pair.Second.Id)
                .ToArray();
            new CampaignSeasonGenerationSettings(0, enabledSeasonIds: enabledIds).EnsureValid(catalog);

            foreach (var (removedId, replacementId) in _deletedReplacements)
            {
                if (!catalog.Contains(replacementId))
                {
                    throw new ArgumentException(
                        $"Deleted season '{removedId}' points to replacement '{replacementId}', which is no longer in the catalog.");
                }
            }

            var selectedId = _definitionsList.SelectedItem is SeasonDefinitionEditorItem selected
                ? selected.Id
                : null;
            Close(new CustomSeasonsDialogResult(
                builtIns,
                custom,
                enabledIds,
                new Dictionary<string, string>(_deletedReplacements, StringComparer.Ordinal),
                selectedId));
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or FormatException)
        {
            ShowValidation(exception.Message);
        }
    }

    private void SaveFormToCurrentItem()
    {
        if (_synchronizing || _formItem is null)
        {
            return;
        }

        _formItem.Name = _nameInput.Text ?? string.Empty;
        _formItem.Id = _idInput.Text ?? string.Empty;
        _formItem.Fallback = _fallbackInput.SelectedItem is CampaignBuiltInSeason fallback
            ? fallback
            : CampaignBuiltInSeason.Spring;
        _formItem.ColorHex = _colorInput.Text ?? string.Empty;
        _formItem.TintStrengthPercent = DecimalToInt(_tintInput.Value);
        _formItem.EffectIntensityPercent = DecimalToInt(_effectInput.Value);
        _formItem.GenerationEnabled = _generationEnabledInput.IsChecked == true;
        _formItem.Latitude = _latitudeInput.Text ?? string.Empty;
        _formItem.Elevation = _elevationInput.Text ?? string.Empty;
        _formItem.Temperature = _temperatureInput.Text ?? string.Empty;
        _formItem.Moisture = _moistureInput.Text ?? string.Empty;
        _formItem.WarmTemperature = _warmTemperatureInput.Text ?? string.Empty;
        _formItem.ColdTemperature = _coldTemperatureInput.Text ?? string.Empty;
        _formItem.AnnualTemperatureRange = _annualRangeInput.Text ?? string.Empty;
        _formItem.Seasonality = _seasonalityInput.Text ?? string.Empty;
        _formItem.SeaDistance = _seaDistanceInput.Text ?? string.Empty;
        _formItem.LakeDistance = _lakeDistanceInput.Text ?? string.Empty;
        _formItem.RiverDistance = _riverDistanceInput.Text ?? string.Empty;
        _formItem.TerrainIncludes = _terrainIncludesInput.Text ?? string.Empty;
        _formItem.TerrainExcludes = _terrainExcludesInput.Text ?? string.Empty;
        _formItem.CustomIncludes = _customIncludesInput.Text ?? string.Empty;
        _formItem.CustomExcludes = _customExcludesInput.Text ?? string.Empty;
        _formItem.RefreshDerived();
        RefreshReplacementChoices();
    }

    private void UpdateFormFromSelection()
    {
        _synchronizing = true;
        try
        {
            _formItem = _definitionsList.SelectedItem as SeasonDefinitionEditorItem;
            _detailsPanel.IsEnabled = _formItem is not null;
            _deleteButton.IsEnabled = _formItem is { IsBuiltIn: false };
            if (_formItem is null)
            {
                return;
            }

            var identityLocked = !_formItem.CanEditId;
            _identityHelpText.Text = _formItem.IsBuiltIn
                ? "Built-in name, ID, and fallback are canonical. Project color, tint, effect, rules, and generated state remain editable."
                : _formItem.UsageCount > 0
                    ? $"Used by {_formItem.UsageCount:N0} tile(s). Stable ID is locked; delete only with a replacement."
                    : _formItem.CanEditId
                        ? "New custom draft. Its stable ID remains editable until this dialog is applied."
                        : "Existing custom season. Its stable ID is immutable; name, fallback, appearance, rules, and generated state remain editable.";
            _nameInput.IsEnabled = !_formItem.IsBuiltIn;
            _idInput.IsEnabled = !identityLocked;
            _fallbackInput.IsEnabled = !_formItem.IsBuiltIn;
            _nameInput.Text = _formItem.Name;
            _idInput.Text = _formItem.Id;
            _fallbackInput.SelectedItem = _formItem.Fallback;
            _colorInput.Text = _formItem.ColorHex;
            _tintInput.Value = _formItem.TintStrengthPercent;
            _effectInput.Value = _formItem.EffectIntensityPercent;
            _generationEnabledInput.IsChecked = _formItem.GenerationEnabled;
            _latitudeInput.Text = _formItem.Latitude;
            _elevationInput.Text = _formItem.Elevation;
            _temperatureInput.Text = _formItem.Temperature;
            _moistureInput.Text = _formItem.Moisture;
            _warmTemperatureInput.Text = _formItem.WarmTemperature;
            _coldTemperatureInput.Text = _formItem.ColdTemperature;
            _annualRangeInput.Text = _formItem.AnnualTemperatureRange;
            _seasonalityInput.Text = _formItem.Seasonality;
            _seaDistanceInput.Text = _formItem.SeaDistance;
            _lakeDistanceInput.Text = _formItem.LakeDistance;
            _riverDistanceInput.Text = _formItem.RiverDistance;
            _terrainIncludesInput.Text = _formItem.TerrainIncludes;
            _terrainExcludesInput.Text = _formItem.TerrainExcludes;
            _customIncludesInput.Text = _formItem.CustomIncludes;
            _customExcludesInput.Text = _formItem.CustomExcludes;
            UpdateColorPreview(_formItem.ColorHex);
            RefreshReplacementChoices();
            HideValidation();
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void RefreshReplacementChoices()
    {
        var selectedId = (_replacementInput.SelectedItem as SeasonReplacementChoice)?.Id;
        var excluded = _definitionsList.SelectedItem as SeasonDefinitionEditorItem;
        var choices = _definitions
            .Where(item => !ReferenceEquals(item, excluded))
            .Select(static item => new SeasonReplacementChoice(item.Id, item.Name))
            .ToArray();
        _replacementInput.ItemsSource = choices;
        _replacementInput.SelectedItem = choices.FirstOrDefault(value =>
            string.Equals(value.Id, selectedId, StringComparison.Ordinal)) ?? choices.FirstOrDefault();
    }

    private string CreateUniqueId(string baseId)
    {
        var normalized = new string(baseId.ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .ToArray()).Trim('-');
        if (normalized.Length == 0 || !char.IsAsciiLetter(normalized[0]))
        {
            normalized = "season-" + normalized;
        }

        normalized = normalized[..Math.Min(normalized.Length, 54)];
        var candidate = normalized;
        for (var suffix = 2; _definitions.Any(item =>
                 string.Equals(item.Id, candidate, StringComparison.Ordinal)); suffix++)
        {
            candidate = $"{normalized}-{suffix}";
        }

        return candidate;
    }

    private void UpdateColorPreview(string? value)
    {
        _colorPreview.Background = TryParseColor(value, out var color)
            ? new SolidColorBrush(color)
            : new SolidColorBrush(Color.Parse("#FF00FF"));
    }

    private void ShowValidation(string message)
    {
        _validationText.Text = message;
        _validationPanel.IsVisible = true;
    }

    private void HideValidation()
    {
        _validationText.Text = string.Empty;
        _validationPanel.IsVisible = false;
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' was not found.");

    private static int DecimalToInt(decimal? value) =>
        (int)Math.Round(value ?? 0, MidpointRounding.AwayFromZero);

    private static bool TryParseColor(string? value, out Color color)
    {
        try
        {
            color = Color.Parse(value ?? string.Empty);
            return value is { Length: 7 } && value[0] == '#';
        }
        catch (FormatException)
        {
            color = default;
            return false;
        }
    }

    internal static CampaignSeasonRange? ParseRange(string value, string name)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var separator = trimmed.IndexOf("..", StringComparison.Ordinal);
        if (separator <= 0 || separator + 2 >= trimmed.Length)
        {
            throw new FormatException($"{name} must use min..max, for example -5..22.");
        }

        if (!double.TryParse(
                trimmed[..separator].Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var minimum) ||
            !double.TryParse(
                trimmed[(separator + 2)..].Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var maximum))
        {
            throw new FormatException($"{name} contains a value that is not a number.");
        }

        var range = new CampaignSeasonRange(minimum, maximum);
        range.EnsureValid(name);
        return range;
    }

    internal static string FormatRange(CampaignSeasonRange? range) =>
        range is { } value
            ? $"{value.Minimum.ToString("0.###", CultureInfo.InvariantCulture)}.." +
              value.Maximum.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Empty;

    internal static CampaignTileType[] ParseTerrainTypes(string value, string name) =>
        SplitValues(value).Select(item =>
            Enum.TryParse<CampaignTileType>(item, ignoreCase: true, out var terrain)
                ? terrain
                : throw new FormatException($"{name} contains unknown terrain type '{item}'."))
        .ToArray();

    internal static string[] SplitValues(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal static bool RequiresReplacement(SeasonDefinitionEditorItem item) =>
        item.UsageCount > 0;
}

public sealed class SeasonDefinitionEditorItem : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _colorHex = "#808080";
    private bool _generationEnabled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public required string OriginalId { get; init; }

    public required bool IsBuiltIn { get; init; }

    public required int UsageCount { get; init; }

    public bool CanEditId { get; init; }

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public CampaignBuiltInSeason Fallback { get; set; }

    public string ColorHex
    {
        get => _colorHex;
        set => SetField(ref _colorHex, value);
    }

    public int TintStrengthPercent { get; set; }

    public int EffectIntensityPercent { get; set; }

    public bool GenerationEnabled
    {
        get => _generationEnabled;
        set => SetField(ref _generationEnabled, value);
    }

    public string Latitude { get; set; } = string.Empty;

    public string Elevation { get; set; } = string.Empty;

    public string Temperature { get; set; } = string.Empty;

    public string Moisture { get; set; } = string.Empty;

    public string WarmTemperature { get; set; } = string.Empty;

    public string ColdTemperature { get; set; } = string.Empty;

    public string AnnualTemperatureRange { get; set; } = string.Empty;

    public string Seasonality { get; set; } = string.Empty;

    public string SeaDistance { get; set; } = string.Empty;

    public string LakeDistance { get; set; } = string.Empty;

    public string RiverDistance { get; set; } = string.Empty;

    public string TerrainIncludes { get; set; } = string.Empty;

    public string TerrainExcludes { get; set; } = string.Empty;

    public string CustomIncludes { get; set; } = string.Empty;

    public string CustomExcludes { get; set; } = string.Empty;

    public string IdText => $"ID: {Id}";

    public string SourceAndUsageText =>
        $"{(IsBuiltIn ? "Built-in" : "Custom")} · {UsageCount:N0} occurrence(s)";

    public string GenerationStateText => GenerationEnabled ? "Generated" : "Manual only";

    public IBrush SwatchBrush => TryGetColor(out var color)
        ? new SolidColorBrush(color)
        : new SolidColorBrush(Color.Parse("#FF00FF"));

    public static SeasonDefinitionEditorItem FromDefinition(
        CampaignSeasonDefinition definition,
        bool isBuiltIn,
        int usageCount,
        bool generationEnabled,
        bool canEditId = false) =>
        new()
        {
            OriginalId = definition.Id,
            IsBuiltIn = isBuiltIn,
            UsageCount = usageCount,
            CanEditId = canEditId,
            Id = definition.Id,
            Name = definition.Name,
            Fallback = definition.Fallback,
            ColorHex = definition.ColorHex,
            TintStrengthPercent = definition.TintStrengthPercent,
            EffectIntensityPercent = definition.EffectIntensityPercent,
            GenerationEnabled = generationEnabled,
            Latitude = CustomSeasonsDialog.FormatRange(definition.Rule.LatitudeDegrees),
            Elevation = CustomSeasonsDialog.FormatRange(definition.Rule.ElevationMeters),
            Temperature = CustomSeasonsDialog.FormatRange(definition.Rule.TemperatureCelsius),
            WarmTemperature = CustomSeasonsDialog.FormatRange(definition.Rule.WarmSeasonTemperatureCelsius),
            ColdTemperature = CustomSeasonsDialog.FormatRange(definition.Rule.ColdSeasonTemperatureCelsius),
            AnnualTemperatureRange = CustomSeasonsDialog.FormatRange(definition.Rule.AnnualTemperatureRangeCelsius),
            Moisture = CustomSeasonsDialog.FormatRange(definition.Rule.Moisture),
            Seasonality = CustomSeasonsDialog.FormatRange(definition.Rule.Seasonality),
            SeaDistance = CustomSeasonsDialog.FormatRange(definition.Rule.SeaDistanceKilometers),
            LakeDistance = CustomSeasonsDialog.FormatRange(definition.Rule.LakeDistanceKilometers),
            RiverDistance = CustomSeasonsDialog.FormatRange(definition.Rule.RiverDistanceKilometers),
            TerrainIncludes = string.Join(", ", definition.Rule.TerrainIncludes),
            TerrainExcludes = string.Join(", ", definition.Rule.TerrainExcludes),
            CustomIncludes = string.Join(", ", definition.Rule.CustomTerrainIncludes),
            CustomExcludes = string.Join(", ", definition.Rule.CustomTerrainExcludes),
        };

    public CampaignSeasonDefinition ToDefinition() =>
        new(
            Id,
            Name,
            Fallback,
            ColorHex,
            TintStrengthPercent,
            EffectIntensityPercent,
            new CampaignSeasonRule(
                CustomSeasonsDialog.ParseRange(Latitude, "Latitude"),
                CustomSeasonsDialog.ParseRange(Elevation, "Elevation"),
                CustomSeasonsDialog.ParseRange(Temperature, "Temperature"),
                CustomSeasonsDialog.ParseRange(WarmTemperature, "Warm-season temperature"),
                CustomSeasonsDialog.ParseRange(ColdTemperature, "Cold-season temperature"),
                CustomSeasonsDialog.ParseRange(AnnualTemperatureRange, "Annual temperature range"),
                CustomSeasonsDialog.ParseRange(Moisture, "Moisture"),
                CustomSeasonsDialog.ParseRange(Seasonality, "Seasonality"),
                CustomSeasonsDialog.ParseRange(SeaDistance, "Sea distance"),
                CustomSeasonsDialog.ParseRange(LakeDistance, "Lake distance"),
                CustomSeasonsDialog.ParseRange(RiverDistance, "River distance"),
                CustomSeasonsDialog.ParseTerrainTypes(TerrainIncludes, "Terrain includes"),
                CustomSeasonsDialog.ParseTerrainTypes(TerrainExcludes, "Terrain excludes"),
                CustomSeasonsDialog.SplitValues(CustomIncludes),
                CustomSeasonsDialog.SplitValues(CustomExcludes)));

    public void RefreshDerived()
    {
        OnPropertyChanged(nameof(IdText));
        OnPropertyChanged(nameof(SourceAndUsageText));
        OnPropertyChanged(nameof(GenerationStateText));
        OnPropertyChanged(nameof(SwatchBrush));
    }

    private bool TryGetColor(out Color color)
    {
        try
        {
            color = Color.Parse(ColorHex);
            return ColorHex.Length == 7 && ColorHex[0] == '#';
        }
        catch (FormatException)
        {
            color = default;
            return false;
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record SeasonReplacementChoice(string Id, string Name)
{
    public override string ToString() => $"{Name} ({Id})";
}

public sealed record CustomSeasonsDialogResult(
    IReadOnlyList<CampaignSeasonDefinition> BuiltInDefinitions,
    IReadOnlyList<CampaignSeasonDefinition> CustomDefinitions,
    IReadOnlyList<string> EnabledSeasonIds,
    IReadOnlyDictionary<string, string> DeletedSeasonReplacements,
    string? SelectedSeasonId);
