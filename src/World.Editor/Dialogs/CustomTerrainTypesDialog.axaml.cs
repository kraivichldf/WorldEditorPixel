using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Editor.Dialogs;

public sealed partial class CustomTerrainTypesDialog : Window
{
    private static readonly BaseTerrainChoice[] BaseTerrainChoices =
    [
        new(CampaignTileType.Plains, "Plains"),
        new(CampaignTileType.Steppe, "Steppe"),
        new(CampaignTileType.Desert, "Desert"),
        new(CampaignTileType.Forest, "Forest"),
        new(CampaignTileType.Hills, "Hills"),
        new(CampaignTileType.Mountain, "Mountain"),
    ];

    private readonly ObservableCollection<CustomTerrainTypeEditorItem> _definitions = [];
    private IReadOnlySet<string> _usedIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly ListBox _definitionsList;
    private readonly Button _deleteButton;
    private readonly TextBox _nameInput;
    private readonly ComboBox _baseTypeInput;
    private readonly TextBox _colorInput;
    private readonly Border _colorPreview;
    private readonly NumericUpDown _generationShareInput;
    private readonly TextBlock _usageMessage;
    private readonly Border _validationPanel;
    private readonly TextBlock _validationText;
    private bool _synchronizing;

    public CustomTerrainTypesDialog()
    {
        AvaloniaXamlLoader.Load(this);
        _definitionsList = FindRequired<ListBox>("DefinitionsList");
        _deleteButton = FindRequired<Button>("DeleteButton");
        _nameInput = FindRequired<TextBox>("NameInput");
        _baseTypeInput = FindRequired<ComboBox>("BaseTypeInput");
        _colorInput = FindRequired<TextBox>("ColorInput");
        _colorPreview = FindRequired<Border>("ColorPreview");
        _generationShareInput = FindRequired<NumericUpDown>("GenerationShareInput");
        _usageMessage = FindRequired<TextBlock>("UsageMessage");
        _validationPanel = FindRequired<Border>("ValidationPanel");
        _validationText = FindRequired<TextBlock>("ValidationText");

        _definitionsList.ItemsSource = _definitions;
        _baseTypeInput.ItemsSource = BaseTerrainChoices;
        _definitionsList.SelectionChanged += (_, _) => UpdateFormFromSelection();
        _nameInput.TextChanged += (_, _) => UpdateNameFromInput();
        _baseTypeInput.SelectionChanged += (_, _) => UpdateBaseTypeFromInput();
        _colorInput.TextChanged += (_, _) => UpdateColorFromInput();
        _generationShareInput.ValueChanged += (_, _) => UpdateGenerationShareFromInput();
    }

    public CustomTerrainTypesDialog(
        IReadOnlyList<CampaignCustomTerrainDefinition> definitions,
        IReadOnlySet<string>? usedIds = null)
        : this()
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _usedIds = usedIds ?? new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            _definitions.Add(new CustomTerrainTypeEditorItem(
                definition.Id,
                definition.Name,
                definition.BaseType,
                definition.ColorHex,
                definition.GenerationSharePercent,
                _usedIds.Contains(definition.Id)));
        }

        if (_definitions.Count > 0)
        {
            _definitionsList.SelectedIndex = 0;
        }
        else
        {
            UpdateFormFromSelection();
        }
    }

    private CustomTerrainTypeEditorItem? SelectedDefinition =>
        _definitionsList.SelectedItem as CustomTerrainTypeEditorItem;

    private void Add_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_definitions.Count >= CampaignCustomTerrainDefinition.MaximumDefinitionCount)
        {
            ShowValidation($"A world can define at most {CampaignCustomTerrainDefinition.MaximumDefinitionCount} custom tile types.");
            return;
        }

        var name = "Custom terrain";
        var definition = new CustomTerrainTypeEditorItem(
            MakeUniqueId(Slugify(name), current: null),
            name,
            CampaignTileType.Plains,
            "#8B9A5B",
            0,
            isUsed: false);
        _definitions.Add(definition);
        _definitionsList.SelectedItem = definition;
        _validationPanel.IsVisible = false;
    }

    private void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedDefinition is not { } definition)
        {
            return;
        }

        if (definition.IsUsed)
        {
            ShowValidation("This type is already painted. Repaint those tiles before deleting the type.");
            return;
        }

        var index = _definitionsList.SelectedIndex;
        _definitions.Remove(definition);
        _definitionsList.SelectedIndex = Math.Min(index, _definitions.Count - 1);
        _validationPanel.IsVisible = false;
    }

    private void Apply_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var definitions = _definitions.Select(definition => new CampaignCustomTerrainDefinition(
                definition.Id,
                definition.Name.Trim(),
                definition.BaseType,
                definition.ColorHex.Trim(),
                definition.GenerationSharePercent)).ToArray();
            CampaignCustomTerrainDefinition.ValidateAll(definitions);
            Close(definitions);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            ShowValidation(exception.Message);
        }
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(null);

    private void UpdateFormFromSelection()
    {
        _synchronizing = true;
        try
        {
            var definition = SelectedDefinition;
            var enabled = definition is not null;
            _nameInput.IsEnabled = enabled;
            _baseTypeInput.IsEnabled = enabled && !definition!.IsUsed;
            _colorInput.IsEnabled = enabled;
            _generationShareInput.IsEnabled = enabled;
            _deleteButton.IsEnabled = enabled && !definition!.IsUsed;
            if (definition is null)
            {
                _nameInput.Text = string.Empty;
                _colorInput.Text = string.Empty;
                _generationShareInput.Value = 0;
                _colorPreview.Background = new SolidColorBrush(Color.Parse("#202B31"));
                _usageMessage.Text = "Add a type to make a named, colored safe-land variant.";
                return;
            }

            _nameInput.Text = definition.Name;
            _baseTypeInput.SelectedIndex = Array.FindIndex(
                BaseTerrainChoices,
                choice => choice.Type == definition.BaseType);
            _colorInput.Text = definition.ColorHex;
            _generationShareInput.Value = definition.GenerationSharePercent;
            _colorPreview.Background = definition.SwatchBrush;
            _usageMessage.Text = definition.IsUsed
                ? "This type is already painted. Its name, color, and terrain mix can change, but its base and identity are locked."
                : "This type is not painted yet. You can change its base or remove it.";
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void UpdateNameFromInput()
    {
        if (_synchronizing || SelectedDefinition is not { } definition)
        {
            return;
        }

        definition.Name = _nameInput.Text ?? string.Empty;
        if (!definition.IsUsed)
        {
            definition.Id = MakeUniqueId(Slugify(definition.Name), definition);
        }

        _validationPanel.IsVisible = false;
    }

    private void UpdateBaseTypeFromInput()
    {
        if (_synchronizing || SelectedDefinition is not { } definition || definition.IsUsed)
        {
            return;
        }

        if (_baseTypeInput.SelectedIndex >= 0 && _baseTypeInput.SelectedIndex < BaseTerrainChoices.Length)
        {
            definition.BaseType = BaseTerrainChoices[_baseTypeInput.SelectedIndex].Type;
        }

        _validationPanel.IsVisible = false;
    }

    private void UpdateColorFromInput()
    {
        if (_synchronizing || SelectedDefinition is not { } definition)
        {
            return;
        }

        definition.ColorHex = _colorInput.Text ?? string.Empty;
        _colorPreview.Background = definition.SwatchBrush;
        _validationPanel.IsVisible = false;
    }

    private void UpdateGenerationShareFromInput()
    {
        if (_synchronizing || SelectedDefinition is not { } definition)
        {
            return;
        }

        definition.GenerationSharePercent = decimal.ToInt32(_generationShareInput.Value ?? 0);
        _validationPanel.IsVisible = false;
    }

    private string MakeUniqueId(string proposedId, CustomTerrainTypeEditorItem? current)
    {
        var normalized = string.IsNullOrWhiteSpace(proposedId) ? "custom-terrain" : proposedId;
        var candidate = normalized;
        var suffix = 2;
        while (_definitions.Any(definition =>
                   !ReferenceEquals(definition, current) &&
                   string.Equals(definition.Id, candidate, StringComparison.Ordinal)))
        {
            var suffixText = $"-{suffix++}";
            var prefixLength = CampaignCustomTerrainDefinition.MaximumIdentifierLength - suffixText.Length;
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

        var slug = builder.Length == 0 ? "custom-terrain" : builder.ToString();
        if (slug[0] is < 'a' or > 'z')
        {
            slug = "terrain-" + slug;
        }

        return slug.Length > CampaignCustomTerrainDefinition.MaximumIdentifierLength
            ? slug[..CampaignCustomTerrainDefinition.MaximumIdentifierLength]
            : slug;
    }

    private void ShowValidation(string message)
    {
        _validationText.Text = message;
        _validationPanel.IsVisible = true;
    }

    private T FindRequired<T>(string name) where T : Control =>
        this.FindControl<T>(name) ?? throw new InvalidOperationException($"Required control '{name}' was not found.");

    private sealed record BaseTerrainChoice(CampaignTileType Type, string Name)
    {
        public override string ToString() => Name;
    }

}

public sealed class CustomTerrainTypeEditorItem : INotifyPropertyChanged
{
    private string _id;
    private string _name;
    private CampaignTileType _baseType;
    private string _colorHex;
    private int _generationSharePercent;
    private IBrush _swatchBrush;

    public CustomTerrainTypeEditorItem(
        string id,
        string name,
        CampaignTileType baseType,
        string colorHex,
        int generationSharePercent,
        bool isUsed)
    {
        _id = id;
        _name = name;
        _baseType = baseType;
        _colorHex = colorHex;
        _generationSharePercent = generationSharePercent;
        IsUsed = isUsed;
        _swatchBrush = CreateSwatch(colorHex);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsUsed { get; }

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public CampaignTileType BaseType
    {
        get => _baseType;
        set
        {
            if (SetProperty(ref _baseType, value))
            {
                OnPropertyChanged(nameof(BaseLabel));
            }
        }
    }

    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (SetProperty(ref _colorHex, value))
            {
                SwatchBrush = CreateSwatch(value);
            }
        }
    }

    public int GenerationSharePercent
    {
        get => _generationSharePercent;
        set
        {
            if (SetProperty(ref _generationSharePercent, value))
            {
                OnPropertyChanged(nameof(BaseLabel));
            }
        }
    }

    public IBrush SwatchBrush
    {
        get => _swatchBrush;
        private set => SetProperty(ref _swatchBrush, value);
    }

    public string BaseLabel => $"{BaseType} fallback · {GenerationSharePercent}% mix";

    private static IBrush CreateSwatch(string colorHex)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(colorHex));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return new SolidColorBrush(Color.Parse("#6E4646"));
        }
    }

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
