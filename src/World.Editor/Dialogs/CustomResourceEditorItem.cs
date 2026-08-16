using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Kingdom.World.Core.Campaign.Resources;

namespace Kingdom.World.Editor.Dialogs;

public sealed class CustomResourceEditorItem : INotifyPropertyChanged
{
    private string _id;
    private string _name;
    private CampaignResourceCategory _category;
    private CampaignResourceDistributionProfile _distributionProfile;
    private CampaignResourceMedium _medium;
    private string _symbolId;
    private string _colorHex;
    private int _mapPriority;
    private int _coveragePercent;
    private CampaignResourceRichness _richness;
    private CampaignResourceConcentration _concentration;
    private IBrush _swatchBrush;

    private CustomResourceEditorItem(
        CampaignResourceDefinition definition,
        int usageCount,
        string? idOverride = null,
        string? nameOverride = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _id = idOverride ?? definition.Id;
        _name = nameOverride ?? definition.Name;
        _category = definition.Category;
        _distributionProfile = definition.DistributionProfile;
        _medium = definition.Medium;
        _symbolId = definition.SymbolId;
        _colorHex = definition.ColorHex;
        _mapPriority = definition.MapPriority;
        _coveragePercent = definition.CoveragePercent;
        _richness = definition.Richness;
        _concentration = definition.Concentration;
        _swatchBrush = CreateSwatch(_colorHex);
        UsageCount = usageCount;

        HasElevationRange = definition.Rules.ElevationMeters is not null;
        ElevationMinimum = definition.Rules.ElevationMeters?.Minimum ?? -500;
        ElevationMaximum = definition.Rules.ElevationMeters?.Maximum ?? 3_000;
        HasGradeRange = definition.Rules.Grade is not null;
        GradeMinimum = definition.Rules.Grade?.Minimum ?? 0;
        GradeMaximum = definition.Rules.Grade?.Maximum ?? 1;
        HasWaterDistanceRange = definition.Rules.WaterDistanceKilometers is not null;
        WaterDistanceMinimum = definition.Rules.WaterDistanceKilometers?.Minimum ?? 0;
        WaterDistanceMaximum = definition.Rules.WaterDistanceKilometers?.Maximum ?? 100;
        HasRegionScaleRange = definition.Rules.RegionScaleKilometers is not null;
        RegionScaleMinimum = definition.Rules.RegionScaleKilometers?.Minimum ?? 10;
        RegionScaleMaximum = definition.Rules.RegionScaleKilometers?.Maximum ?? 80;
        PreferredTerrainTagsText = FormatIds(definition.Rules.PreferredTerrainTags);
        AvoidedTerrainTagsText = FormatIds(definition.Rules.AvoidedTerrainTags);
        ExcludedTerrainSurfacesText = FormatSurfaces(definition.Rules.ExcludedTerrainSurfaces);
        CustomTerrainIncludesText = FormatIds(definition.Rules.CustomTerrainIncludes);
        CustomTerrainExcludesText = FormatIds(definition.Rules.CustomTerrainExcludes);
        FieldWeightsText = FormatWeights(definition.Rules.FieldWeights);
        AssociationWeightsText = FormatWeights(definition.Rules.AssociationWeights);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int UsageCount { get; }

    public bool IsUsed => UsageCount > 0;

    public string UsageText => IsUsed
        ? $"Used on {UsageCount:N0} tile(s). Stable ID and category are locked; erase all occurrences before deleting it."
        : "Unused. Every field may be edited, and the definition may be deleted.";

    public string Id
    {
        get => _id;
        set
        {
            if (SetProperty(ref _id, value))
            {
                OnPropertyChanged(nameof(IdText));
            }
        }
    }

    public string IdText => $"ID: {Id}";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public CampaignResourceCategory Category
    {
        get => _category;
        set
        {
            if (SetProperty(ref _category, value))
            {
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public CampaignResourceDistributionProfile DistributionProfile
    {
        get => _distributionProfile;
        set
        {
            if (SetProperty(ref _distributionProfile, value))
            {
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public CampaignResourceMedium Medium
    {
        get => _medium;
        set
        {
            if (SetProperty(ref _medium, value))
            {
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public string SymbolId
    {
        get => _symbolId;
        set => SetProperty(ref _symbolId, value);
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

    public int MapPriority
    {
        get => _mapPriority;
        set => SetProperty(ref _mapPriority, value);
    }

    public int CoveragePercent
    {
        get => _coveragePercent;
        set
        {
            if (SetProperty(ref _coveragePercent, value))
            {
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public CampaignResourceRichness Richness
    {
        get => _richness;
        set => SetProperty(ref _richness, value);
    }

    public CampaignResourceConcentration Concentration
    {
        get => _concentration;
        set => SetProperty(ref _concentration, value);
    }

    public bool HasElevationRange { get; set; }

    public double ElevationMinimum { get; set; }

    public double ElevationMaximum { get; set; }

    public bool HasGradeRange { get; set; }

    public double GradeMinimum { get; set; }

    public double GradeMaximum { get; set; }

    public bool HasWaterDistanceRange { get; set; }

    public double WaterDistanceMinimum { get; set; }

    public double WaterDistanceMaximum { get; set; }

    public bool HasRegionScaleRange { get; set; }

    public double RegionScaleMinimum { get; set; }

    public double RegionScaleMaximum { get; set; }

    public string PreferredTerrainTagsText { get; set; }

    public string AvoidedTerrainTagsText { get; set; }

    public string ExcludedTerrainSurfacesText { get; set; }

    public string CustomTerrainIncludesText { get; set; }

    public string CustomTerrainExcludesText { get; set; }

    public string FieldWeightsText { get; set; }

    public string AssociationWeightsText { get; set; }

    public IBrush SwatchBrush
    {
        get => _swatchBrush;
        private set => SetProperty(ref _swatchBrush, value);
    }

    public string SummaryText =>
        $"{Category} · {FormatDistribution(DistributionProfile)} · {Medium} · {CoveragePercent:N0}%";

    public static CustomResourceEditorItem FromDefinition(
        CampaignResourceDefinition definition,
        int usageCount) =>
        new(definition, usageCount);

    public static CustomResourceEditorItem CreateDefault(string id) =>
        new(
            new CampaignResourceDefinition(
                id,
                "Custom Resource",
                CampaignResourceCategory.Finite,
                CampaignResourceDistributionProfile.SurfaceDeposit,
                CampaignResourceMedium.Land,
                "resource",
                "#8B6F47",
                mapPriority: 25,
                coveragePercent: 0,
                CampaignResourceRichness.Balanced,
                CampaignResourceConcentration.Balanced),
            usageCount: 0);

    public static CustomResourceEditorItem Duplicate(
        CampaignResourceDefinition source,
        string id,
        string name) =>
        new(source, usageCount: 0, id, name);

    public CampaignResourceDefinition BuildDefinition()
    {
        var preferredTags = ParseFactorIds(PreferredTerrainTagsText, "Preferred factors");
        var avoidedTags = ParseFactorIds(AvoidedTerrainTagsText, "Avoided factors");
        var excludedSurfaces = ParseSurfaces(
            ExcludedTerrainSurfacesText,
            "Excluded tile surfaces");
        var customIncludes = ParseCustomTerrainIds(
            CustomTerrainIncludesText,
            "Custom terrain includes");
        var customExcludes = ParseCustomTerrainIds(
            CustomTerrainExcludesText,
            "Custom terrain excludes");
        var fieldWeights = ParseWeights(FieldWeightsText, "Field weights");
        var associationWeights = ParseWeights(AssociationWeightsText, "Association weights");

        var rules = new CampaignResourceRuleSet(
            Medium,
            HasElevationRange ? new CampaignResourceRange(ElevationMinimum, ElevationMaximum) : null,
            HasGradeRange ? new CampaignResourceRange(GradeMinimum, GradeMaximum) : null,
            HasWaterDistanceRange
                ? new CampaignResourceRange(WaterDistanceMinimum, WaterDistanceMaximum)
                : null,
            HasRegionScaleRange
                ? new CampaignResourceRange(RegionScaleMinimum, RegionScaleMaximum)
                : null,
            preferredTags,
            customIncludes,
            customExcludes,
            fieldWeights,
            associationWeights,
            avoidedTerrainTags: avoidedTags,
            excludedTerrainSurfaces: excludedSurfaces);

        return new CampaignResourceDefinition(
            Id.Trim(),
            Name.Trim(),
            Category,
            DistributionProfile,
            Medium,
            SymbolId.Trim(),
            ColorHex.Trim(),
            MapPriority,
            CoveragePercent,
            Richness,
            Concentration,
            rules);
    }

    private static IReadOnlyList<string> ParseFactorIds(string text, string fieldName)
    {
        var ids = ParseIds(text, fieldName);
        var unsupported = ids.FirstOrDefault(id => !CampaignResourceSupportFieldIds.IsSupported(id));
        if (unsupported is not null)
        {
            throw new ArgumentException(
                $"{fieldName} contains unsupported factor '{unsupported}'. Choose a factor from the supported list.",
                fieldName);
        }

        return ids;
    }

    private static IReadOnlyList<string> ParseCustomTerrainIds(
        string text,
        string fieldName)
    {
        var ids = ParseIds(text, fieldName);
        var invalid = ids.FirstOrDefault(id => !CampaignResourceDefinition.IsValidIdentifier(id));
        if (invalid is not null)
        {
            throw new ArgumentException(
                $"{fieldName} contains invalid portable ID '{invalid}'.",
                fieldName);
        }

        return ids;
    }

    private static IReadOnlyList<CampaignResourceSurfaceType> ParseSurfaces(
        string text,
        string fieldName)
    {
        var surfaces = new List<CampaignResourceSurfaceType>();
        var unique = new HashSet<CampaignResourceSurfaceType>();
        var entries = (text ?? string.Empty).Split(
            [',', ';', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var normalized = entry.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);
            if (!Enum.TryParse<CampaignResourceSurfaceType>(normalized, ignoreCase: true, out var surface) ||
                !Enum.IsDefined(surface) ||
                surface == CampaignResourceSurfaceType.Unassigned)
            {
                throw new ArgumentException(
                    $"{fieldName} contains unknown assigned surface '{entry}'.",
                    fieldName);
            }

            if (!unique.Add(surface))
            {
                throw new ArgumentException(
                    $"{fieldName} contains '{surface}' more than once.",
                    fieldName);
            }

            surfaces.Add(surface);
        }

        surfaces.Sort();
        return surfaces;
    }

    private static IReadOnlyList<string> ParseIds(string text, string fieldName)
    {
        var ids = (text ?? string.Empty)
            .Split([',', ';', '\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(static value => value.Trim())
            .ToArray();
        var duplicate = ids
            .GroupBy(static value => value, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new ArgumentException($"{fieldName} contains '{duplicate}' more than once.", fieldName);
        }

        return ids;
    }

    private static IReadOnlyDictionary<string, double> ParseWeights(string text, string fieldName)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        var entries = (text ?? string.Empty)
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var separatorIndex = entry.IndexOf('=');
            if (separatorIndex < 0)
            {
                separatorIndex = entry.IndexOf(':');
            }

            if (separatorIndex <= 0 || separatorIndex == entry.Length - 1)
            {
                throw new ArgumentException(
                    $"{fieldName} entry '{entry}' must use factor-id=weight, one entry per line.",
                    fieldName);
            }

            var id = entry[..separatorIndex].Trim();
            if (!CampaignResourceSupportFieldIds.IsSupported(id))
            {
                throw new ArgumentException(
                    $"{fieldName} contains unsupported factor '{id}'. Choose a factor from the supported list.",
                    fieldName);
            }

            if (!double.TryParse(
                    entry[(separatorIndex + 1)..].Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var weight))
            {
                throw new ArgumentException(
                    $"{fieldName} weight for '{id}' is not a valid invariant number.",
                    fieldName);
            }

            if (!result.TryAdd(id, weight))
            {
                throw new ArgumentException($"{fieldName} contains '{id}' more than once.", fieldName);
            }
        }

        return result;
    }

    private static string FormatIds(IEnumerable<string> ids) => string.Join(", ", ids);

    private static string FormatSurfaces(IEnumerable<CampaignResourceSurfaceType> surfaces) =>
        string.Join(", ", surfaces);

    private static string FormatWeights(IReadOnlyDictionary<string, double> weights) =>
        string.Join(
            Environment.NewLine,
            weights.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => $"{pair.Key}={pair.Value.ToString("G", CultureInfo.InvariantCulture)}"));

    private static string FormatDistribution(CampaignResourceDistributionProfile profile) => profile switch
    {
        CampaignResourceDistributionProfile.SurfaceDeposit => "Surface deposit",
        _ => profile.ToString(),
    };

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

public sealed record CustomResourcesDialogResult(
    IReadOnlyList<CampaignResourceDefinition> Definitions,
    string? SelectedResourceId);
