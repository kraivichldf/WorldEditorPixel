using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Editor.Dialogs;

public sealed partial class CustomResourcesDialog : Window
{
    private static readonly EnumChoice<CampaignResourceCategory>[] CategoryChoices =
        Enum.GetValues<CampaignResourceCategory>()
            .Select(static value => new EnumChoice<CampaignResourceCategory>(value, value.ToString()))
            .ToArray();

    private static readonly EnumChoice<CampaignResourceMedium>[] MediumChoices =
        Enum.GetValues<CampaignResourceMedium>()
            .Select(static value => new EnumChoice<CampaignResourceMedium>(value, value.ToString()))
            .ToArray();

    private static readonly EnumChoice<CampaignResourceDistributionProfile>[] DistributionChoices =
    [
        new(CampaignResourceDistributionProfile.Field, "Field — broad coherent regions"),
        new(CampaignResourceDistributionProfile.Vein, "Vein — narrow oriented belts"),
        new(CampaignResourceDistributionProfile.Basin, "Basin — sedimentary regions"),
        new(CampaignResourceDistributionProfile.SurfaceDeposit, "Surface deposit — compact exposed patches"),
        new(CampaignResourceDistributionProfile.Aquatic, "Aquatic — connected Sea or Lake regions"),
    ];

    private static readonly EnumChoice<CampaignResourceRichness>[] RichnessChoices =
        Enum.GetValues<CampaignResourceRichness>()
            .Select(static value => new EnumChoice<CampaignResourceRichness>(value, value.ToString()))
            .ToArray();

    private static readonly EnumChoice<CampaignResourceConcentration>[] ConcentrationChoices =
    [
        new(CampaignResourceConcentration.FewLarge, "Few large regions"),
        new(CampaignResourceConcentration.Balanced, "Balanced regions"),
        new(CampaignResourceConcentration.ManySmall, "Many small regions"),
    ];

    private static readonly EnumChoice<CampaignResourceSurfaceType>[] SurfaceChoices =
        Enum.GetValues<CampaignResourceSurfaceType>()
            .Where(static value => value != CampaignResourceSurfaceType.Unassigned)
            .Select(static value => new EnumChoice<CampaignResourceSurfaceType>(
                value,
                value == CampaignResourceSurfaceType.BarrenRock ? "Barren rock" : value.ToString()))
            .ToArray();

    private readonly ObservableCollection<CustomResourceEditorItem> _definitions = [];
    private readonly IReadOnlyList<CampaignCustomTerrainDefinition> _customTerrainDefinitions;
    private readonly ListBox _definitionsList;
    private readonly Button _deleteButton;
    private readonly ComboBox _builtInTemplateInput;
    private readonly StackPanel _detailsPanel;
    private readonly TextBlock _usageMessage;
    private readonly TextBox _nameInput;
    private readonly TextBox _idInput;
    private readonly ComboBox _categoryInput;
    private readonly ComboBox _mediumInput;
    private readonly ComboBox _distributionInput;
    private readonly TextBox _symbolInput;
    private readonly TextBox _colorInput;
    private readonly Border _colorPreview;
    private readonly NumericUpDown _mapPriorityInput;
    private readonly NumericUpDown _coverageInput;
    private readonly ComboBox _richnessInput;
    private readonly ComboBox _concentrationInput;
    private readonly CheckBox _elevationRangeToggle;
    private readonly NumericUpDown _elevationMinimumInput;
    private readonly NumericUpDown _elevationMaximumInput;
    private readonly CheckBox _gradeRangeToggle;
    private readonly NumericUpDown _gradeMinimumInput;
    private readonly NumericUpDown _gradeMaximumInput;
    private readonly CheckBox _waterDistanceRangeToggle;
    private readonly NumericUpDown _waterDistanceMinimumInput;
    private readonly NumericUpDown _waterDistanceMaximumInput;
    private readonly CheckBox _regionScaleRangeToggle;
    private readonly NumericUpDown _regionScaleMinimumInput;
    private readonly NumericUpDown _regionScaleMaximumInput;
    private readonly ComboBox _supportedFactorInput;
    private readonly TextBox _preferredTagsInput;
    private readonly TextBox _avoidedTagsInput;
    private readonly ComboBox _excludedSurfaceInput;
    private readonly TextBox _excludedSurfacesInput;
    private readonly TextBox _fieldWeightsInput;
    private readonly TextBox _associationWeightsInput;
    private readonly TextBlock _customTerrainHelpText;
    private readonly TextBox _customTerrainIncludesInput;
    private readonly TextBox _customTerrainExcludesInput;
    private readonly Border _validationPanel;
    private readonly TextBlock _validationText;
    private bool _synchronizing;

    public CustomResourcesDialog()
    {
        AvaloniaXamlLoader.Load(this);
        _customTerrainDefinitions = [];
        _definitionsList = FindRequired<ListBox>("DefinitionsList");
        _deleteButton = FindRequired<Button>("DeleteButton");
        _builtInTemplateInput = FindRequired<ComboBox>("BuiltInTemplateInput");
        _detailsPanel = FindRequired<StackPanel>("DetailsPanel");
        _usageMessage = FindRequired<TextBlock>("UsageMessage");
        _nameInput = FindRequired<TextBox>("NameInput");
        _idInput = FindRequired<TextBox>("IdInput");
        _categoryInput = FindRequired<ComboBox>("CategoryInput");
        _mediumInput = FindRequired<ComboBox>("MediumInput");
        _distributionInput = FindRequired<ComboBox>("DistributionInput");
        _symbolInput = FindRequired<TextBox>("SymbolInput");
        _colorInput = FindRequired<TextBox>("ColorInput");
        _colorPreview = FindRequired<Border>("ColorPreview");
        _mapPriorityInput = FindRequired<NumericUpDown>("MapPriorityInput");
        _coverageInput = FindRequired<NumericUpDown>("CoverageInput");
        _richnessInput = FindRequired<ComboBox>("RichnessInput");
        _concentrationInput = FindRequired<ComboBox>("ConcentrationInput");
        _elevationRangeToggle = FindRequired<CheckBox>("ElevationRangeToggle");
        _elevationMinimumInput = FindRequired<NumericUpDown>("ElevationMinimumInput");
        _elevationMaximumInput = FindRequired<NumericUpDown>("ElevationMaximumInput");
        _gradeRangeToggle = FindRequired<CheckBox>("GradeRangeToggle");
        _gradeMinimumInput = FindRequired<NumericUpDown>("GradeMinimumInput");
        _gradeMaximumInput = FindRequired<NumericUpDown>("GradeMaximumInput");
        _waterDistanceRangeToggle = FindRequired<CheckBox>("WaterDistanceRangeToggle");
        _waterDistanceMinimumInput = FindRequired<NumericUpDown>("WaterDistanceMinimumInput");
        _waterDistanceMaximumInput = FindRequired<NumericUpDown>("WaterDistanceMaximumInput");
        _regionScaleRangeToggle = FindRequired<CheckBox>("RegionScaleRangeToggle");
        _regionScaleMinimumInput = FindRequired<NumericUpDown>("RegionScaleMinimumInput");
        _regionScaleMaximumInput = FindRequired<NumericUpDown>("RegionScaleMaximumInput");
        _supportedFactorInput = FindRequired<ComboBox>("SupportedFactorInput");
        _preferredTagsInput = FindRequired<TextBox>("PreferredTagsInput");
        _avoidedTagsInput = FindRequired<TextBox>("AvoidedTagsInput");
        _excludedSurfaceInput = FindRequired<ComboBox>("ExcludedSurfaceInput");
        _excludedSurfacesInput = FindRequired<TextBox>("ExcludedSurfacesInput");
        _fieldWeightsInput = FindRequired<TextBox>("FieldWeightsInput");
        _associationWeightsInput = FindRequired<TextBox>("AssociationWeightsInput");
        _customTerrainHelpText = FindRequired<TextBlock>("CustomTerrainHelpText");
        _customTerrainIncludesInput = FindRequired<TextBox>("CustomTerrainIncludesInput");
        _customTerrainExcludesInput = FindRequired<TextBox>("CustomTerrainExcludesInput");
        _validationPanel = FindRequired<Border>("ValidationPanel");
        _validationText = FindRequired<TextBlock>("ValidationText");

        _definitionsList.ItemsSource = _definitions;
        _categoryInput.ItemsSource = CategoryChoices;
        _mediumInput.ItemsSource = MediumChoices;
        _distributionInput.ItemsSource = DistributionChoices;
        _richnessInput.ItemsSource = RichnessChoices;
        _concentrationInput.ItemsSource = ConcentrationChoices;
        _builtInTemplateInput.ItemsSource = CampaignResourceCatalog.BuiltInDefinitions
            .OrderBy(static definition => definition.Name, StringComparer.Ordinal)
            .Select(static definition => new BuiltInTemplateChoice(definition))
            .ToArray();
        _builtInTemplateInput.SelectedIndex = 0;
        _supportedFactorInput.ItemsSource = CampaignResourceSupportFieldIds.All;
        _supportedFactorInput.SelectedIndex = 0;
        _excludedSurfaceInput.ItemsSource = SurfaceChoices;
        _excludedSurfaceInput.SelectedIndex = 0;

        _definitionsList.SelectionChanged += (_, _) => UpdateFormFromSelection();
        HookFormChangeEvents();
        UpdateCustomTerrainHelp();
        UpdateFormFromSelection();
    }

    public CustomResourcesDialog(
        IReadOnlyList<CampaignResourceDefinition> definitions,
        IReadOnlyDictionary<string, int>? usageCounts,
        IReadOnlyList<CampaignCustomTerrainDefinition>? customTerrainDefinitions)
        : this()
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _customTerrainDefinitions = customTerrainDefinitions ?? [];
        _definitions.Clear();
        foreach (var definition in definitions)
        {
            var usageCount = usageCounts?.GetValueOrDefault(definition.Id) ?? 0;
            _definitions.Add(CustomResourceEditorItem.FromDefinition(definition, usageCount));
        }

        UpdateCustomTerrainHelp();
        if (_definitions.Count > 0)
        {
            _definitionsList.SelectedIndex = 0;
        }
        else
        {
            UpdateFormFromSelection();
        }
    }

    private CustomResourceEditorItem? SelectedDefinition =>
        _definitionsList.SelectedItem as CustomResourceEditorItem;

    private void Add_OnClick(object? sender, RoutedEventArgs e)
    {
        var item = CustomResourceEditorItem.CreateDefault(MakeUniqueId("custom-resource"));
        _definitions.Add(item);
        _definitionsList.SelectedItem = item;
        HideValidation();
    }

    private void Duplicate_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_builtInTemplateInput.SelectedItem is not BuiltInTemplateChoice template)
        {
            ShowValidation("Choose a built-in resource to duplicate.");
            return;
        }

        var item = CustomResourceEditorItem.Duplicate(
            template.Definition,
            MakeUniqueId(template.Definition.Id + "-custom"),
            template.Definition.Name + " Variant");
        _definitions.Add(item);
        _definitionsList.SelectedItem = item;
        HideValidation();
    }

    private void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedDefinition is not { } definition)
        {
            return;
        }

        if (definition.IsUsed)
        {
            ShowValidation(
                $"'{definition.Name}' is used on {definition.UsageCount:N0} tile(s). " +
                "Erase those occurrences before deleting the definition.");
            return;
        }

        var index = _definitionsList.SelectedIndex;
        _definitions.Remove(definition);
        _definitionsList.SelectedIndex = Math.Min(index, _definitions.Count - 1);
        HideValidation();
    }

    private void Apply_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            WriteSelectedFromForm();
            var requested = _definitions.Select(static definition => definition.BuildDefinition()).ToArray();
            var catalog = new CampaignResourceCatalog(requested);
            var selectedId = SelectedDefinition?.Id.Trim();
            Close(new CustomResourcesDialogResult(catalog.CustomDefinitions, selectedId));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            ShowValidation(exception.Message);
        }
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void AddPreferredFactor_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_supportedFactorInput.SelectedItem is string factor)
        {
            _preferredTagsInput.Text = AppendIdentifier(_preferredTagsInput.Text, factor);
        }
    }

    private void AddAvoidedFactor_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_supportedFactorInput.SelectedItem is string factor)
        {
            _avoidedTagsInput.Text = AppendIdentifier(_avoidedTagsInput.Text, factor);
        }
    }

    private void AddExcludedSurface_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_excludedSurfaceInput.SelectedItem is EnumChoice<CampaignResourceSurfaceType> choice)
        {
            _excludedSurfacesInput.Text = AppendIdentifier(
                _excludedSurfacesInput.Text,
                choice.Value.ToString());
        }
    }

    private void AddFieldWeight_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_supportedFactorInput.SelectedItem is string factor)
        {
            _fieldWeightsInput.Text = AppendWeight(_fieldWeightsInput.Text, factor);
        }
    }

    private void AddAssociationWeight_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_supportedFactorInput.SelectedItem is string factor)
        {
            _associationWeightsInput.Text = AppendWeight(_associationWeightsInput.Text, factor);
        }
    }

    private void HookFormChangeEvents()
    {
        _nameInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _idInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _categoryInput.SelectionChanged += (_, _) => WriteSelectedFromForm();
        _mediumInput.SelectionChanged += (_, _) => WriteSelectedFromForm();
        _distributionInput.SelectionChanged += (_, _) => WriteSelectedFromForm();
        _symbolInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _colorInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _mapPriorityInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _coverageInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _richnessInput.SelectionChanged += (_, _) => WriteSelectedFromForm();
        _concentrationInput.SelectionChanged += (_, _) => WriteSelectedFromForm();
        _elevationRangeToggle.IsCheckedChanged += (_, _) => WriteSelectedFromForm();
        _elevationMinimumInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _elevationMaximumInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _gradeRangeToggle.IsCheckedChanged += (_, _) => WriteSelectedFromForm();
        _gradeMinimumInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _gradeMaximumInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _waterDistanceRangeToggle.IsCheckedChanged += (_, _) => WriteSelectedFromForm();
        _waterDistanceMinimumInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _waterDistanceMaximumInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _regionScaleRangeToggle.IsCheckedChanged += (_, _) => WriteSelectedFromForm();
        _regionScaleMinimumInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _regionScaleMaximumInput.ValueChanged += (_, _) => WriteSelectedFromForm();
        _preferredTagsInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _avoidedTagsInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _excludedSurfacesInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _fieldWeightsInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _associationWeightsInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _customTerrainIncludesInput.TextChanged += (_, _) => WriteSelectedFromForm();
        _customTerrainExcludesInput.TextChanged += (_, _) => WriteSelectedFromForm();
    }

    private void UpdateFormFromSelection()
    {
        _synchronizing = true;
        try
        {
            var definition = SelectedDefinition;
            var enabled = definition is not null;
            _detailsPanel.IsEnabled = enabled;
            _deleteButton.IsEnabled = enabled && !definition!.IsUsed;
            _idInput.IsEnabled = enabled && !definition!.IsUsed;
            _categoryInput.IsEnabled = enabled && !definition!.IsUsed;
            if (definition is null)
            {
                _usageMessage.Text = "Add a new custom resource or duplicate a built-in definition to begin.";
                _nameInput.Text = string.Empty;
                _idInput.Text = string.Empty;
                _symbolInput.Text = string.Empty;
                _colorInput.Text = string.Empty;
                _colorPreview.Background = new SolidColorBrush(Color.Parse("#202B31"));
                _preferredTagsInput.Text = string.Empty;
                _avoidedTagsInput.Text = string.Empty;
                _excludedSurfacesInput.Text = string.Empty;
                _fieldWeightsInput.Text = string.Empty;
                _associationWeightsInput.Text = string.Empty;
                _customTerrainIncludesInput.Text = string.Empty;
                _customTerrainExcludesInput.Text = string.Empty;
                UpdateRangeInputEnablement();
                return;
            }

            _usageMessage.Text = definition.UsageText;
            _nameInput.Text = definition.Name;
            _idInput.Text = definition.Id;
            SelectChoice(_categoryInput, CategoryChoices, definition.Category);
            SelectChoice(_mediumInput, MediumChoices, definition.Medium);
            SelectChoice(_distributionInput, DistributionChoices, definition.DistributionProfile);
            _symbolInput.Text = definition.SymbolId;
            _colorInput.Text = definition.ColorHex;
            _colorPreview.Background = definition.SwatchBrush;
            _mapPriorityInput.Value = definition.MapPriority;
            _coverageInput.Value = definition.CoveragePercent;
            SelectChoice(_richnessInput, RichnessChoices, definition.Richness);
            SelectChoice(_concentrationInput, ConcentrationChoices, definition.Concentration);
            _elevationRangeToggle.IsChecked = definition.HasElevationRange;
            _elevationMinimumInput.Value = ToDecimal(definition.ElevationMinimum);
            _elevationMaximumInput.Value = ToDecimal(definition.ElevationMaximum);
            _gradeRangeToggle.IsChecked = definition.HasGradeRange;
            _gradeMinimumInput.Value = ToDecimal(definition.GradeMinimum);
            _gradeMaximumInput.Value = ToDecimal(definition.GradeMaximum);
            _waterDistanceRangeToggle.IsChecked = definition.HasWaterDistanceRange;
            _waterDistanceMinimumInput.Value = ToDecimal(definition.WaterDistanceMinimum);
            _waterDistanceMaximumInput.Value = ToDecimal(definition.WaterDistanceMaximum);
            _regionScaleRangeToggle.IsChecked = definition.HasRegionScaleRange;
            _regionScaleMinimumInput.Value = ToDecimal(definition.RegionScaleMinimum);
            _regionScaleMaximumInput.Value = ToDecimal(definition.RegionScaleMaximum);
            _preferredTagsInput.Text = definition.PreferredTerrainTagsText;
            _avoidedTagsInput.Text = definition.AvoidedTerrainTagsText;
            _excludedSurfacesInput.Text = definition.ExcludedTerrainSurfacesText;
            _fieldWeightsInput.Text = definition.FieldWeightsText;
            _associationWeightsInput.Text = definition.AssociationWeightsText;
            _customTerrainIncludesInput.Text = definition.CustomTerrainIncludesText;
            _customTerrainExcludesInput.Text = definition.CustomTerrainExcludesText;
            UpdateRangeInputEnablement();
            HideValidation();
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void WriteSelectedFromForm()
    {
        if (_synchronizing || SelectedDefinition is not { } definition)
        {
            return;
        }

        definition.Name = _nameInput.Text ?? string.Empty;
        if (!definition.IsUsed)
        {
            definition.Id = _idInput.Text ?? string.Empty;
            definition.Category = GetChoice(_categoryInput, definition.Category);
        }

        definition.Medium = GetChoice(_mediumInput, definition.Medium);
        definition.DistributionProfile = GetChoice(_distributionInput, definition.DistributionProfile);
        definition.SymbolId = _symbolInput.Text ?? string.Empty;
        definition.ColorHex = _colorInput.Text ?? string.Empty;
        definition.MapPriority = ReadInt(_mapPriorityInput, definition.MapPriority);
        definition.CoveragePercent = ReadInt(_coverageInput, definition.CoveragePercent);
        definition.Richness = GetChoice(_richnessInput, definition.Richness);
        definition.Concentration = GetChoice(_concentrationInput, definition.Concentration);
        definition.HasElevationRange = _elevationRangeToggle.IsChecked == true;
        definition.ElevationMinimum = ReadDouble(_elevationMinimumInput, definition.ElevationMinimum);
        definition.ElevationMaximum = ReadDouble(_elevationMaximumInput, definition.ElevationMaximum);
        definition.HasGradeRange = _gradeRangeToggle.IsChecked == true;
        definition.GradeMinimum = ReadDouble(_gradeMinimumInput, definition.GradeMinimum);
        definition.GradeMaximum = ReadDouble(_gradeMaximumInput, definition.GradeMaximum);
        definition.HasWaterDistanceRange = _waterDistanceRangeToggle.IsChecked == true;
        definition.WaterDistanceMinimum = ReadDouble(_waterDistanceMinimumInput, definition.WaterDistanceMinimum);
        definition.WaterDistanceMaximum = ReadDouble(_waterDistanceMaximumInput, definition.WaterDistanceMaximum);
        definition.HasRegionScaleRange = _regionScaleRangeToggle.IsChecked == true;
        definition.RegionScaleMinimum = ReadDouble(_regionScaleMinimumInput, definition.RegionScaleMinimum);
        definition.RegionScaleMaximum = ReadDouble(_regionScaleMaximumInput, definition.RegionScaleMaximum);
        definition.PreferredTerrainTagsText = _preferredTagsInput.Text ?? string.Empty;
        definition.AvoidedTerrainTagsText = _avoidedTagsInput.Text ?? string.Empty;
        definition.ExcludedTerrainSurfacesText = _excludedSurfacesInput.Text ?? string.Empty;
        definition.FieldWeightsText = _fieldWeightsInput.Text ?? string.Empty;
        definition.AssociationWeightsText = _associationWeightsInput.Text ?? string.Empty;
        definition.CustomTerrainIncludesText = _customTerrainIncludesInput.Text ?? string.Empty;
        definition.CustomTerrainExcludesText = _customTerrainExcludesInput.Text ?? string.Empty;
        _colorPreview.Background = definition.SwatchBrush;
        UpdateRangeInputEnablement();
        HideValidation();
    }

    private void UpdateRangeInputEnablement()
    {
        var hasSelection = SelectedDefinition is not null;
        _elevationMinimumInput.IsEnabled = hasSelection && _elevationRangeToggle.IsChecked == true;
        _elevationMaximumInput.IsEnabled = hasSelection && _elevationRangeToggle.IsChecked == true;
        _gradeMinimumInput.IsEnabled = hasSelection && _gradeRangeToggle.IsChecked == true;
        _gradeMaximumInput.IsEnabled = hasSelection && _gradeRangeToggle.IsChecked == true;
        _waterDistanceMinimumInput.IsEnabled = hasSelection && _waterDistanceRangeToggle.IsChecked == true;
        _waterDistanceMaximumInput.IsEnabled = hasSelection && _waterDistanceRangeToggle.IsChecked == true;
        _regionScaleMinimumInput.IsEnabled = hasSelection && _regionScaleRangeToggle.IsChecked == true;
        _regionScaleMaximumInput.IsEnabled = hasSelection && _regionScaleRangeToggle.IsChecked == true;
    }

    private void UpdateCustomTerrainHelp()
    {
        _customTerrainHelpText.Text = _customTerrainDefinitions.Count == 0
            ? "No custom terrain types currently exist. Portable IDs may still be entered for definitions that will be added later."
            : "Current custom terrain IDs: " + string.Join(
                ", ",
                _customTerrainDefinitions.Select(static definition => definition.Id)) +
              ". Include acts as a whitelist only on custom-terrain cells; exclude always rejects a matching custom type.";
    }

    private string MakeUniqueId(string proposedId)
    {
        var normalized = Slugify(proposedId);
        var candidate = normalized;
        var suffix = 2;
        while (_definitions.Any(definition =>
                   string.Equals(definition.Id, candidate, StringComparison.Ordinal)) ||
               CampaignResourceCatalog.BuiltInDefinitions.Any(definition =>
                   string.Equals(definition.Id, candidate, StringComparison.Ordinal)))
        {
            var suffixText = $"-{suffix++}";
            var prefixLength = CampaignResourceDefinition.MaximumIdentifierLength - suffixText.Length;
            candidate = normalized.Length > prefixLength
                ? normalized[..prefixLength] + suffixText
                : normalized + suffixText;
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var builder = new System.Text.StringBuilder();
        var pendingHyphen = false;
        foreach (var character in value.ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = builder.Length > 0;
            }
        }

        var slug = builder.Length == 0 ? "custom-resource" : builder.ToString();
        if (slug[0] is < 'a' or > 'z')
        {
            slug = "resource-" + slug;
        }

        return slug.Length > CampaignResourceDefinition.MaximumIdentifierLength
            ? slug[..CampaignResourceDefinition.MaximumIdentifierLength]
            : slug;
    }

    private static string AppendIdentifier(string? current, string id)
    {
        var ids = (current ?? string.Empty)
            .Split([',', ';', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
        return ids.Contains(id)
            ? current ?? string.Empty
            : string.IsNullOrWhiteSpace(current) ? id : current.TrimEnd() + ", " + id;
    }

    private static string AppendWeight(string? current, string id)
    {
        var lines = (current ?? string.Empty)
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Any(line =>
                string.Equals(
                    line.Split(['=', ':'], 2)[0].Trim(),
                    id,
                    StringComparison.Ordinal)))
        {
            return current ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(current)
            ? $"{id}=1"
            : current.TrimEnd() + Environment.NewLine + $"{id}=1";
    }

    private void ShowValidation(string message)
    {
        _validationText.Text = message;
        _validationPanel.IsVisible = true;
        _validationPanel.Focus();
    }

    private void HideValidation() => _validationPanel.IsVisible = false;

    private static void SelectChoice<T>(
        ComboBox input,
        IReadOnlyList<EnumChoice<T>> choices,
        T value) where T : struct, Enum =>
        input.SelectedItem = choices.First(choice => EqualityComparer<T>.Default.Equals(choice.Value, value));

    private static T GetChoice<T>(ComboBox input, T fallback) where T : struct, Enum =>
        input.SelectedItem is EnumChoice<T> choice ? choice.Value : fallback;

    private static int ReadInt(NumericUpDown input, int fallback) =>
        input.Value is { } value ? decimal.ToInt32(value) : fallback;

    private static double ReadDouble(NumericUpDown input, double fallback) =>
        input.Value is { } value ? decimal.ToDouble(value) : fallback;

    private static decimal ToDecimal(double value) => Convert.ToDecimal(value);

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Required control '{name}' was not found.");

    private sealed record EnumChoice<T>(T Value, string Label) where T : struct, Enum
    {
        public override string ToString() => Label;
    }

    private sealed record BuiltInTemplateChoice(CampaignResourceDefinition Definition)
    {
        public override string ToString() => $"{Definition.Name} ({Definition.Id})";
    }
}
