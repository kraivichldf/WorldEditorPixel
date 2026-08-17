using System.Collections.ObjectModel;
using Avalonia.Media;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Models;

namespace Kingdom.World.Editor.ViewModels;

public sealed class EditorViewModel : ViewModelBase
{
    private sealed record SeasonDiagnosticProjection(
        CampaignSeasonSupportFields SupportFields,
        string CurrentSourceTerrainFingerprint,
        string CurrentInputFingerprint);

    private static readonly (int X, int Y)[] CardinalOffsets =
        [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private readonly CommandHistory _history = new();
    private CampaignWorld? _world;
    private CampaignResourceMap? _resourceMap;
    private CampaignResourceGenerationSettings? _resourceGenerationSettings;
    private CampaignResourceTerrainQueryV2? _resourceTerrainQuery;
    private CampaignSeasonMap? _seasonMap;
    private IReadOnlyList<string> _seasonPriorityIds = CampaignSeasonGenerationSettings.DefaultPriority;
    private CampaignSeasonSavedGeneration? _seasonSavedGeneration;
    private CampaignSeasonTerrainQueryV2? _seasonTerrainQuery;
    private SeasonDiagnosticProjection? _seasonDiagnosticProjection;
    private CampaignTileTypeOption _selectedCampaignTileTypeOption;
    private CampaignResourceOption? _selectedResourceOption;
    private CampaignSeasonOption? _selectedSeasonOption;
    private CampaignResourceOccurrenceRow? _selectedPinnedResourceOccurrence;
    private double _stampHeight;
    private int _paintAreaRadius;
    private int _resourcePotential = 50;
    private int _resourcePaintAreaRadius;
    private int _seasonPaintAreaRadius;
    private bool _lockManualResourceEdits = true;
    private bool _lockManualSeasonEdits = true;
    private bool _isResourceEraseTool;
    private bool _isResourcesWorkspace;
    private bool _isSeasonsWorkspace;
    private CampaignSeasonPaintTool _seasonPaintTool;
    private string _seasonSearchText = string.Empty;
    private bool _showSeasonLabels = true;
    private bool _blendSeasonBoundaries = true;
    private CampaignResourceCategoryFilter _selectedResourceCategoryFilter;
    private bool _showCampaignGrid = true;
    private bool _showElevationNumbers = true;
    private bool _useGrayscale;
    private bool _isDirty;
    private bool _isBusy;
    private bool _isLegacyImport;
    private string? _projectDirectory;
    private string? _importSourceDirectory;
    private string _untitledName = "Untitled World";
    private string _statusMessage = "Create or open a world to begin.";
    private string _zoomText = "—";
    private CampaignTilePointerInfo? _hover;
    private CampaignTileCoordinate? _selectedCoordinate;
    private int _riverSplitBranchCount = 2;
    private string _selectedRiverSplitDirection = "Auto";
    private CampaignMapGenerationOptions? _lastGenerationOptions;

    public EditorViewModel()
    {
        CampaignTileTypeOptions = new ObservableCollection<CampaignTileTypeOption>
        {
            CreateCampaignType(CampaignTileType.Unassigned, "Unassigned", "No campaign terrain classification", "#59666A"),
            CreateCampaignType(CampaignTileType.Plains, "Plains", "Textured grass ground and open lowland", "#73945D"),
            CreateCampaignType(CampaignTileType.Steppe, "Steppe", "Textured semi-arid grassland between plains and desert", "#A49A58"),
            CreateCampaignType(CampaignTileType.Desert, "Desert", "Textured dry sand and stone lowland", "#C99142"),
            CreateCampaignType(CampaignTileType.Forest, "Forest", "Textured dense wooded territory", "#2F684F"),
            CreateCampaignType(CampaignTileType.Hills, "Hills", "Grass-covered rolling foothills and ridges", "#8B8A62"),
            CreateCampaignType(CampaignTileType.Mountain, "Mountain", "Textured high rocky territory", "#858784"),
            CreateCampaignType(CampaignTileType.Sea, "Sea", "Textured open salt water", "#1E6A8B"),
            CreateCampaignType(CampaignTileType.Lake, "Lake", "Textured enclosed inland water", "#2D8EA3"),
            CreateCampaignType(CampaignTileType.River, "River", "Narrow auto-connected water channel over grass", "#3B9BC1"),
            CreateCampaignType(CampaignTileType.LargeRiver, "Large River", "Broad auto-connected major-river corridor over grass", "#237FA6"),
            CreateCampaignType(CampaignTileType.Beach, "Beach", "Full textured sand shore tile", "#C3A86D"),
            CreateCampaignType(CampaignTileType.Cliff, "Cliff", "Full textured steep rocky shore tile", "#6F665E"),
        };
        _selectedCampaignTileTypeOption = CampaignTileTypeOptions[1];
        _history.Changed += (_, _) => NotifyHistoryChanged();
    }

    public ObservableCollection<CampaignTileTypeOption> CampaignTileTypeOptions { get; }

    public ObservableCollection<CampaignResourceOption> ResourceOptions { get; } = [];

    public ObservableCollection<CampaignSeasonOption> SeasonOptions { get; } = [];

    public bool HasSeasonOptions => SeasonOptions.Count > 0;

    public bool HasNoSeasonOptions => !HasSeasonOptions;

    public ObservableCollection<CampaignResourceOccurrenceRow> PinnedResourceOccurrences { get; } = [];

    public IReadOnlyList<CampaignResourceCategoryFilter> ResourceCategoryFilterOptions { get; } =
        Enum.GetValues<CampaignResourceCategoryFilter>();

    public CampaignResourceCategoryFilter SelectedResourceCategoryFilter
    {
        get => _selectedResourceCategoryFilter;
        set
        {
            var normalized = Enum.IsDefined(value) ? value : CampaignResourceCategoryFilter.All;
            if (SetProperty(ref _selectedResourceCategoryFilter, normalized))
            {
                RefreshResourceOptions();
                StatusMessage = $"Showing {normalized.ToString().ToLowerInvariant()} resources.";
            }
        }
    }

    public CampaignResourceOption? SelectedResourceOption
    {
        get => _selectedResourceOption;
        set
        {
            if (value is not null && !ResourceOptions.Any(option =>
                    string.Equals(option.Id, value.Id, StringComparison.Ordinal)))
            {
                return;
            }

            if (SetProperty(ref _selectedResourceOption, value))
            {
                OnPropertyChanged(nameof(SelectedResourceId));
                OnPropertyChanged(nameof(CanEditResources));
                NotifyResourceStampChanged();
                NotifyResourceHoverChanged();
                if (value is not null)
                {
                    StatusMessage = $"{value.Name} selected for resource painting.";
                }
            }
        }
    }

    public string? SelectedResourceId => SelectedResourceOption?.Id;

    public int ResourcePotential
    {
        get => _resourcePotential;
        set
        {
            var clamped = Math.Clamp(value, CampaignResourceOccurrence.MinimumPotential,
                CampaignResourceOccurrence.MaximumPotential);
            if (SetProperty(ref _resourcePotential, clamped))
            {
                NotifyResourceStampChanged();
                StatusMessage = $"Resource potential set to {clamped:N0} / 100.";
            }
        }
    }

    public int ResourcePaintAreaRadius
    {
        get => _resourcePaintAreaRadius;
        set
        {
            var clamped = Math.Clamp(value, 0, 12);
            if (SetProperty(ref _resourcePaintAreaRadius, clamped))
            {
                NotifyResourceStampChanged();
                StatusMessage = $"Resource paint area set to {ResourcePaintAreaText}.";
            }
        }
    }

    public string ResourcePaintAreaText
    {
        get
        {
            var sideLength = 1 + ResourcePaintAreaRadius * 2;
            return $"{sideLength} × {sideLength} tiles";
        }
    }

    public bool LockManualResourceEdits
    {
        get => _lockManualResourceEdits;
        set
        {
            if (SetProperty(ref _lockManualResourceEdits, value))
            {
                NotifyResourceStampChanged();
                StatusMessage = value
                    ? "Manual resource additions and updates will be locked."
                    : "Manual resource additions and updates will remain unlocked.";
            }
        }
    }

    public bool IsResourceAddUpdateTool => !IsResourceEraseTool;

    public bool IsResourceEraseTool => _isResourceEraseTool;

    public string ResourceStampSummary
    {
        get
        {
            if (SelectedResourceOption is not { } selected)
            {
                return "No resource selected";
            }

            return IsResourceEraseTool
                ? $"{selected.Name} · erase selected · {ResourcePaintAreaText}"
                : $"{selected.Name} · {ResourcePotential:N0} / 100 · " +
                  $"{(LockManualResourceEdits ? "locked" : "unlocked")} · {ResourcePaintAreaText}";
        }
    }

    public CampaignSeasonOption? SelectedSeasonOption
    {
        get => _selectedSeasonOption;
        set
        {
            if (value is not null && !SeasonOptions.Any(option =>
                    string.Equals(option.Id, value.Id, StringComparison.Ordinal)))
            {
                return;
            }

            if (SetProperty(ref _selectedSeasonOption, value))
            {
                OnPropertyChanged(nameof(SelectedSeasonId));
                OnPropertyChanged(nameof(CanEditSeasons));
                NotifySeasonStampChanged();
                NotifySeasonInspectorChanged();
                if (value is not null)
                {
                    StatusMessage = $"{value.Name} selected for season painting.";
                }
            }
        }
    }

    public string? SelectedSeasonId => SelectedSeasonOption?.Id;

    public string SeasonSearchText
    {
        get => _seasonSearchText;
        set
        {
            var normalized = value ?? string.Empty;
            if (SetProperty(ref _seasonSearchText, normalized))
            {
                RefreshSeasonOptions();
            }
        }
    }

    public int SeasonPaintAreaRadius
    {
        get => _seasonPaintAreaRadius;
        set
        {
            var clamped = Math.Clamp(value, 0, 12);
            if (SetProperty(ref _seasonPaintAreaRadius, clamped))
            {
                NotifySeasonStampChanged();
                StatusMessage = $"Season paint area set to {SeasonPaintAreaText}.";
            }
        }
    }

    public string SeasonPaintAreaText
    {
        get
        {
            var sideLength = 1 + SeasonPaintAreaRadius * 2;
            return $"{sideLength} × {sideLength} tiles";
        }
    }

    public bool LockManualSeasonEdits
    {
        get => _lockManualSeasonEdits;
        set
        {
            if (SetProperty(ref _lockManualSeasonEdits, value))
            {
                NotifySeasonStampChanged();
                StatusMessage = value
                    ? "Manual season painting will be locked against later generation."
                    : "Manual season painting will remain unlocked.";
            }
        }
    }

    public CampaignSeasonPaintTool SeasonPaintTool => _seasonPaintTool;

    public bool IsSeasonPaintTool => SeasonPaintTool == CampaignSeasonPaintTool.Paint;

    public bool IsSeasonResetTool => SeasonPaintTool == CampaignSeasonPaintTool.ResetToDefault;

    public bool IsSeasonLockTool => SeasonPaintTool == CampaignSeasonPaintTool.Lock;

    public bool IsSeasonUnlockTool => SeasonPaintTool == CampaignSeasonPaintTool.Unlock;

    public string SeasonStampSummary
    {
        get
        {
            var tool = SeasonPaintTool switch
            {
                CampaignSeasonPaintTool.Paint => SelectedSeasonOption is { } selected
                    ? $"Paint {selected.Name} · {(LockManualSeasonEdits ? "locked" : "unlocked")}"
                    : "No season selected",
                CampaignSeasonPaintTool.ResetToDefault =>
                    $"Reset to {GetDefaultSeasonName()} · unlocked",
                CampaignSeasonPaintTool.Lock => "Lock existing season",
                CampaignSeasonPaintTool.Unlock => "Unlock existing season",
                _ => "Season tool unavailable",
            };
            return $"{tool} · {SeasonPaintAreaText}";
        }
    }

    public bool ShowSeasonLabels
    {
        get => _showSeasonLabels;
        set => SetProperty(ref _showSeasonLabels, value);
    }

    public bool BlendSeasonBoundaries
    {
        get => _blendSeasonBoundaries;
        set => SetProperty(ref _blendSeasonBoundaries, value);
    }

    public bool IsTerrainWorkspace => !IsResourcesWorkspace && !IsSeasonsWorkspace;

    public bool IsResourcesWorkspace => _isResourcesWorkspace;

    public bool IsSeasonsWorkspace => _isSeasonsWorkspace;

    public CampaignTileTypeOption SelectedCampaignTileTypeOption
    {
        get => _selectedCampaignTileTypeOption;
        set
        {
            if (value is not null && SetProperty(ref _selectedCampaignTileTypeOption, value))
            {
                OnPropertyChanged(nameof(SelectedCampaignTileType));
                OnPropertyChanged(nameof(SelectedCustomTerrainId));
                OnPropertyChanged(nameof(IsRiverSelected));
                OnPropertyChanged(nameof(RiverWidthHelpText));
                OnPropertyChanged(nameof(CanAdjustPaintArea));
                OnPropertyChanged(nameof(PaintAreaText));
                OnPropertyChanged(nameof(StampSummary));
                StatusMessage = IsRiverSelected
                    ? $"{value.Name} selected. Drag paints a 1 × 1 route footprint at {StampHeight:N0} m."
                    : $"{value.Name} selected. Drag to stamp {PaintAreaText} at {StampHeight:N0} m.";
            }
        }
    }

    public CampaignTileType SelectedCampaignTileType => SelectedCampaignTileTypeOption.Type;

    public string? SelectedCustomTerrainId => SelectedCampaignTileTypeOption.CustomTerrainId;

    public bool IsRiverSelected => SelectedCampaignTileType.IsRiver();

    public string RiverWidthHelpText => SelectedCampaignTileType == CampaignTileType.LargeRiver
        ? "Large River shows a broad major-river corridor with visible ground on both sides. Its preview width is symbolic, not literal kilometres."
        : "River keeps grass visible around a narrow bank-and-water channel.";

    public double StampHeight
    {
        get => _stampHeight;
        set
        {
            var minimum = World?.Definition.MinimumHeightMeters ?? short.MinValue;
            var maximum = World?.Definition.MaximumHeightMeters ?? short.MaxValue;
            var clamped = Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), minimum, maximum);
            if (SetProperty(ref _stampHeight, clamped))
            {
                OnPropertyChanged(nameof(StampSummary));
                OnPropertyChanged(nameof(StampElevationLabelText));
                StatusMessage = $"Tile elevation set to {clamped:N0} m at its centre.";
            }
        }
    }

    public decimal MinimumStampHeight => World?.Definition.MinimumHeightMeters ?? short.MinValue;

    public decimal MaximumStampHeight => World?.Definition.MaximumHeightMeters ?? short.MaxValue;

    public int PaintAreaRadius
    {
        get => _paintAreaRadius;
        set
        {
            var clamped = Math.Clamp(value, 0, 12);
            if (SetProperty(ref _paintAreaRadius, clamped))
            {
                OnPropertyChanged(nameof(PaintAreaText));
                OnPropertyChanged(nameof(StampSummary));
                StatusMessage = IsRiverSelected
                    ? "River types keep a 1 × 1 route footprint; paint area is available for other terrain types."
                    : $"Paint area set to {PaintAreaText}.";
            }
        }
    }

    public bool CanAdjustPaintArea => !IsRiverSelected;

    public string PaintAreaText
    {
        get
        {
            if (IsRiverSelected)
            {
                return "1 × 1 route";
            }

            var sideLength = 1 + PaintAreaRadius * 2;
            return $"{sideLength} × {sideLength} tiles";
        }
    }

    public string StampSummary =>
        $"{SelectedCampaignTileTypeOption.Name} · {StampHeight:N0} m centre · " +
        (IsRiverSelected ? "1 × 1 route" : PaintAreaText);

    public string StampElevationLabelText => $"{StampHeight:N0} m";

    public bool CanUsePinnedElevationHelper =>
        World is { } world &&
        _selectedCoordinate is { } coordinate &&
        world.Tiles.IsValidCoordinate(coordinate.X, coordinate.Y) &&
        !IsBusy;

    public IReadOnlyList<string> RiverSplitDirectionOptions { get; } =
        ["Auto", "North", "East", "South", "West"];

    public int RiverSplitBranchCount
    {
        get => _riverSplitBranchCount;
        set
        {
            var clamped = Math.Clamp(value, 2, 4);
            if (SetProperty(ref _riverSplitBranchCount, clamped))
            {
                OnPropertyChanged(nameof(RiverSplitActionText));
            }
        }
    }

    public string SelectedRiverSplitDirection
    {
        get => _selectedRiverSplitDirection;
        set => SetProperty(
            ref _selectedRiverSplitDirection,
            RiverSplitDirectionOptions.Contains(value, StringComparer.Ordinal) ? value : "Auto");
    }

    public bool HasPinnedRiverRoute =>
        TryGetPinnedRiver(out _, out var data) && data.Type.IsRiver();

    public bool CanCreatePinnedRiverSplit
    {
        get
        {
            if (IsBusy || !TryGetPinnedRiver(out var coordinate, out var data) ||
                data.Type is not (CampaignTileType.River or CampaignTileType.LargeRiver))
            {
                return false;
            }

            return CountRiverConnections(World!.Tiles.GetRiverConnections(coordinate.X, coordinate.Y)) <= 1;
        }
    }

    public string RiverSplitActionText => $"Create {RiverSplitBranchCount}-branch split";

    public string RiverSplitHelpText
    {
        get
        {
            if (!TryGetPinnedRiver(out var coordinate, out var data) || !data.Type.IsRiver())
            {
                return "Right-click a River or Large River endpoint to create separated downstream branches.";
            }

            if (data.Type == CampaignTileType.RiverJunction)
            {
                return "This tile is already a Y junction. Pin a normal River or Large River endpoint.";
            }

            var connections = World!.Tiles.GetRiverConnections(coordinate.X, coordinate.Y);
            return CountRiverConnections(connections) switch
            {
                0 => "Isolated endpoint: choose a direction. Auto needs one existing incoming neighbour.",
                1 => "Auto continues away from the existing river. Three and four branches cascade multiple Y junctions.",
                _ => "This tile is inside a route. Pin an endpoint with zero or one river neighbour.",
            };
        }
    }

    public string ElevationHelperText
    {
        get
        {
            if (World is null ||
                _selectedCoordinate is not { } coordinate ||
                !World.Tiles.IsValidCoordinate(coordinate.X, coordinate.Y))
            {
                return "Right-click a tile to copy its centre height or blend its N/E/S/W neighbours.";
            }

            var tile = World.Tiles.GetTile(coordinate.X, coordinate.Y);
            var suggestion = CampaignElevationHelper.SuggestNearby(World.Tiles, coordinate);
            return suggestion.SourceNeighborCount == 0
                ? $"Pinned {coordinate.X:N0}, {coordinate.Y:N0} · centre {tile.HeightMeters:N0} m · " +
                  $"rounded helper {suggestion.HeightMeters:N0} m"
                : $"Pinned {coordinate.X:N0}, {coordinate.Y:N0} · centre {tile.HeightMeters:N0} m · " +
                  $"{suggestion.SourceNeighborCount} neighbours suggest {suggestion.HeightMeters:N0} m";
        }
    }

    public bool ShowCampaignGrid
    {
        get => _showCampaignGrid;
        set => SetProperty(ref _showCampaignGrid, value);
    }

    public bool ShowElevationNumbers
    {
        get => _showElevationNumbers;
        set => SetProperty(ref _showElevationNumbers, value);
    }

    public bool UseGrayscale
    {
        get => _useGrayscale;
        set => SetProperty(ref _useGrayscale, value);
    }

    public bool CanEditCampaignTiles => HasWorld && !IsBusy;

    public bool CanEditResources =>
        IsResourcesWorkspace && ResourceMap is not null && SelectedResourceId is not null && !IsBusy;

    public bool CanEditSeasons =>
        IsSeasonsWorkspace && SeasonMap is not null &&
        (SeasonPaintTool != CampaignSeasonPaintTool.Paint || SelectedSeasonId is not null) &&
        !IsBusy;

    public bool CanAdjustCampaignView => HasWorld && !IsBusy;

    public string CanvasTitle => IsResourcesWorkspace
        ? "Campaign resource potential"
        : IsSeasonsWorkspace
            ? "Campaign season authority"
            : "Campaign tile surface";

    public string CanvasHelpTitle => IsResourcesWorkspace
        ? "Cell color = selected resource potential · number = exact value / 100"
        : IsSeasonsWorkspace
            ? "Cell tint = authoritative season · label = abbreviated season name"
            : "Cell = textured type · number = stored centre elevation (m)";

    public string CanvasHelpText => IsResourcesWorkspace
        ? "Potential numbers appear when tiles are large enough · terrain remains visible beneath the heatmap"
        : IsSeasonsWorkspace
            ? "Season labels appear when tiles are large enough · boundary blending is display-only"
            : "Elevation numbers appear when tiles are large enough · middle-drag pans · wheel zooms";

    public string HoverSurfaceLabel => "Surface here";

    public string FooterFormatText => IsResourcesWorkspace
        ? "Resources · potential 1–100 · locks"
        : IsSeasonsWorkspace
            ? "Seasons · one ID per tile · locks"
            : "Type + Int16 centre height · auto slopes";

    public CampaignWorld? World
    {
        get => _world;
        private set
        {
            if (SetProperty(ref _world, value))
            {
                _resourceTerrainQuery = value is null ? null : new CampaignResourceTerrainQueryV2(value);
                _seasonTerrainQuery = value is null ? null : new CampaignSeasonTerrainQueryV2(value);
                OnPropertyChanged(nameof(HasWorld));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(CanEditCampaignTiles));
                OnPropertyChanged(nameof(CanEditResources));
                OnPropertyChanged(nameof(CanEditSeasons));
                OnPropertyChanged(nameof(CanAdjustCampaignView));
                OnPropertyChanged(nameof(CanRegenerateWorld));
                OnPropertyChanged(nameof(CanRegenerateResources));
                OnPropertyChanged(nameof(CanGenerateSeasons));
                OnPropertyChanged(nameof(DocumentStateText));
                OnPropertyChanged(nameof(WorldSummary));
                OnPropertyChanged(nameof(WorldScaleText));
                OnPropertyChanged(nameof(TileSizeText));
                OnPropertyChanged(nameof(HeightRangeText));
                OnPropertyChanged(nameof(DefaultHeightText));
                OnPropertyChanged(nameof(MinimumStampHeight));
                OnPropertyChanged(nameof(MaximumStampHeight));
                OnPropertyChanged(nameof(StampElevationLabelText));
                NotifyElevationHelperChanged();
                NotifyRiverSplitChanged();
            }
        }
    }

    public CampaignResourceMap? ResourceMap
    {
        get => _resourceMap;
        private set
        {
            if (SetProperty(ref _resourceMap, value))
            {
                OnPropertyChanged(nameof(HasResourceMap));
                NotifyResourceStatusChanged();
                OnPropertyChanged(nameof(CanEditResources));
                OnPropertyChanged(nameof(CanRegenerateResources));
            }
        }
    }

    public CampaignResourceGenerationSettings? ResourceGenerationSettings
    {
        get => _resourceGenerationSettings;
        private set => SetProperty(ref _resourceGenerationSettings, value);
    }

    public ICampaignResourceTerrainQuery? ResourceTerrainQuery => _resourceTerrainQuery;

    public CampaignSeasonMap? SeasonMap
    {
        get => _seasonMap;
        private set
        {
            if (SetProperty(ref _seasonMap, value))
            {
                OnPropertyChanged(nameof(HasSeasonMap));
                OnPropertyChanged(nameof(CanEditSeasons));
                OnPropertyChanged(nameof(CanGenerateSeasons));
                NotifySeasonStatusChanged();
            }
        }
    }

    public IReadOnlyList<string> SeasonPriorityIds => _seasonPriorityIds;

    public CampaignSeasonSavedGeneration? SeasonSavedGeneration
    {
        get => _seasonSavedGeneration;
        private set
        {
            if (SetProperty(ref _seasonSavedGeneration, value))
            {
                NotifySeasonInspectorChanged();
            }
        }
    }

    public ICampaignSeasonTerrainQuery? SeasonTerrainQuery => _seasonTerrainQuery;

    public bool HasWorld => World is not null;

    public bool HasResourceMap => ResourceMap is not null;

    public bool HasSeasonMap => SeasonMap is not null;

    public int ResourceOccurrenceCount => ResourceMap?.OccurrenceCount ?? 0;

    public string ResourceStatusText => ResourceMap is null
        ? "No resource layer"
        : ResourceOccurrenceCount == 0
            ? "No resource occurrences"
            : $"{ResourceOccurrenceCount:N0} resource occurrence(s)";

    public int SeasonLockedTileCount => SeasonMap?.LockedTileCount ?? 0;

    public string SeasonStatusText => SeasonMap is null
        ? "No season layer"
        : $"{SeasonMap.Catalog.Definitions.Count:N0} season(s) · " +
          $"{SeasonLockedTileCount:N0} locked tile(s)";

    public bool CanRegenerateWorld => HasWorld && !IsBusy;

    public bool CanRegenerateResources =>
        HasWorld && ResourceMap is not null && _resourceTerrainQuery is not null && !IsBusy;

    public bool CanGenerateSeasons =>
        HasWorld && SeasonMap is not null && _seasonTerrainQuery is not null && !IsBusy;

    public CampaignResourceGenerationSettings ResolveInitialResourceGenerationSettings()
    {
        if (World is not { } world ||
            ResourceMap is not { } resources ||
            _resourceTerrainQuery is not { } terrainQuery)
        {
            throw new InvalidOperationException(
                "A world and resource layer must be open before resource generation settings can be resolved.");
        }

        ValidateResourceDocument(world, resources, ResourceGenerationSettings);
        if (ResourceGenerationSettings is { } savedSettings)
        {
            return savedSettings;
        }

        var seed = LastGenerationOptions is { } terrainRecipe
            ? CampaignResourceSeed.FromTerrainSeed(terrainRecipe.Seed)
            : CampaignResourceSeed.FromCurrentWorld(
                CampaignResourceGenerationSource.Capture(terrainQuery, resources));
        return new CampaignResourceGenerationSettings(seed, seedDerivedFromWorld: true);
    }

    public void AcceptResourceGeneration(CampaignResourceGenerationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (World is not { } world ||
            ResourceMap is not { } currentResources ||
            _resourceTerrainQuery is not { } terrainQuery)
        {
            throw new InvalidOperationException(
                "A world and resource layer must be open before generated resources can be accepted.");
        }

        if (world.Revision != result.SourceTerrainRevision ||
            currentResources.Revision != result.SourceResourceRevision)
        {
            throw new InvalidOperationException(
                "The terrain or resource map changed after this candidate was generated. " +
                "Generate a fresh resource preview before accepting it.");
        }

        if (result.CandidateMap.Definition != world.Definition)
        {
            throw new ArgumentException(
                "The generated resource candidate must use the current value-equal world definition.",
                nameof(result));
        }

        if (!ReferenceEquals(result.CandidateMap.Catalog, currentResources.Catalog) ||
            !HaveSameResourceDefinitions(result.CandidateMap.Catalog, currentResources.Catalog))
        {
            throw new ArgumentException(
                "The generated resource candidate must use the current resource catalog and definitions.",
                nameof(result));
        }

        if (result.CandidateMap.Revision != result.CandidateResourceRevision)
        {
            throw new InvalidOperationException(
                "The generated resource candidate changed after the preview completed. " +
                "Generate a fresh resource preview before accepting it.");
        }

        if (!result.IsCurrent(terrainQuery, currentResources))
        {
            throw new InvalidOperationException(
                "The resource preview is no longer current. Generate a fresh preview before accepting it.");
        }

        result.Scope.EnsureValid(currentResources.Catalog);
        ValidateResourceDocument(world, result.CandidateMap, result.Settings);
        if (result.CandidateMap.OccurrenceCount > CampaignResourceGenerationResult.MaximumCandidateOccurrenceCount)
        {
            throw new CampaignResourceGenerationLimitException(result.CandidateMap.OccurrenceCount);
        }

        ResourceMap = result.CandidateMap;
        ResourceGenerationSettings = result.Settings;
        _history.Clear();
        RefreshResourceOptions();
        RefreshPinnedResourceOccurrences();
        IsDirty = true;
        StatusMessage =
            $"Accepted reviewed resource candidate · {result.CandidateMap.OccurrenceCount:N0} occurrence(s) · " +
            $"seed {result.Settings.ResourceSeed:N0}. Terrain and project identity were kept; undo history was cleared.";
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
    }

    public CampaignSeasonGenerationSettings ResolveInitialSeasonGenerationSettings()
    {
        if (World is not { } world ||
            SeasonMap is not { } seasons ||
            _seasonTerrainQuery is not { } terrainQuery)
        {
            throw new InvalidOperationException(
                "A world and Season Layer must be open before Season generation settings can be resolved.");
        }

        ValidateSeasonDocument(world, seasons, _seasonPriorityIds, SeasonSavedGeneration);
        if (SeasonSavedGeneration is { } saved)
        {
            return saved.Settings;
        }

        var seed = LastGenerationOptions is { } terrainRecipe
            ? CampaignSeasonSeed.FromTerrainSeed(terrainRecipe.Seed)
            : CampaignSeasonSeed.FromCurrentWorld(
                CampaignSeasonGenerationSource.Capture(terrainQuery, seasons));
        return new CampaignSeasonGenerationSettings(
            seed,
            seedDerivedFromTerrain: true,
            priorityIds: _seasonPriorityIds);
    }

    public void AcceptSeasonGeneration(CampaignSeasonGenerationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (World is not { } world ||
            SeasonMap is not { } currentSeasons ||
            _seasonTerrainQuery is not { } terrainQuery)
        {
            throw new InvalidOperationException(
                "A world and Season Layer must be open before generated seasons can be accepted.");
        }

        if (terrainQuery.Revision != result.SourceTerrainRevision ||
            currentSeasons.Revision != result.SourceSeasonRevision)
        {
            throw new InvalidOperationException(
                "Terrain or the Season Layer changed after this candidate was generated. " +
                "Generate a fresh Season preview before accepting it.");
        }

        if (result.CandidateMap.Definition != world.Definition)
        {
            throw new ArgumentException(
                "The generated Season candidate must use the current value-equal world definition.",
                nameof(result));
        }

        if (!ReferenceEquals(result.CandidateMap.Catalog, currentSeasons.Catalog))
        {
            throw new ArgumentException(
                "The generated Season candidate must use the current immutable Season Catalog.",
                nameof(result));
        }

        if (!result.Settings.PriorityIds.SequenceEqual(_seasonPriorityIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Season priority changed after this candidate was generated. " +
                "Generate a fresh Season preview before accepting it.");
        }

        if (result.CandidateMap.Revision != result.CandidateSeasonRevision)
        {
            throw new InvalidOperationException(
                "The generated Season candidate changed after preview completed. " +
                "Generate a fresh Season preview before accepting it.");
        }

        if (!result.IsCurrent(terrainQuery, currentSeasons))
        {
            throw new InvalidOperationException(
                "The Season preview is no longer current. Generate a fresh preview before accepting it.");
        }

        result.Scope.EnsureValid(world.Definition);
        result.Settings.EnsureValid(currentSeasons.Catalog, world.Definition);
        var sourceFingerprint = CampaignSeasonGenerationFingerprint.GetSourceTerrainFingerprint(
            result.SupportFields.Terrain);
        var inputFingerprint = CampaignSeasonGenerationFingerprint.GetInputFingerprint(
            currentSeasons.Catalog,
            result.Settings);
        var saved = new CampaignSeasonSavedGeneration(
            result.Settings,
            sourceFingerprint,
            inputFingerprint);
        ValidateSeasonDocument(
            world,
            result.CandidateMap,
            result.Settings.PriorityIds,
            saved);

        SeasonMap = result.CandidateMap;
        _seasonPriorityIds = Array.AsReadOnly(result.Settings.PriorityIds.ToArray());
        OnPropertyChanged(nameof(SeasonPriorityIds));
        SeasonSavedGeneration = saved;
        _seasonDiagnosticProjection = new SeasonDiagnosticProjection(
            result.SupportFields,
            sourceFingerprint,
            inputFingerprint);
        _history.Clear();
        RefreshSeasonOptions();
        IsDirty = true;
        StatusMessage =
            $"Accepted reviewed Season candidate · {result.ChangedTileCount:N0} changed tile(s) · " +
            $"seed {result.Settings.SeasonSeed:N0}. Terrain, resources, and project identity were kept; undo history was cleared.";
        NotifyInspectorChanged();
        NotifySeasonStatusChanged();
    }

    public async Task<bool> RebuildSeasonDiagnosticsAsync(
        CancellationToken cancellationToken = default)
    {
        if (SeasonSavedGeneration is not { } saved ||
            SeasonMap is not { } seasons ||
            _seasonTerrainQuery is not { } terrainQuery)
        {
            _seasonDiagnosticProjection = null;
            NotifySeasonInspectorChanged();
            return false;
        }

        var source = CampaignSeasonGenerationSource.Capture(
            terrainQuery,
            seasons,
            cancellationToken);
        var projection = await Task.Run(
            () =>
            {
                var support = CampaignSeasonSupportFields.Build(
                    source.Terrain,
                    saved.Settings,
                    cancellationToken);
                return new SeasonDiagnosticProjection(
                    support,
                    CampaignSeasonGenerationFingerprint.GetSourceTerrainFingerprint(source.Terrain),
                    CampaignSeasonGenerationFingerprint.GetInputFingerprint(
                        source.Catalog,
                        saved.Settings));
            },
            cancellationToken);

        if (!ReferenceEquals(SeasonSavedGeneration, saved) ||
            !ReferenceEquals(SeasonMap, seasons) ||
            !ReferenceEquals(_seasonTerrainQuery, terrainQuery) ||
            terrainQuery.Revision != source.TerrainRevision ||
            seasons.Revision != source.SeasonRevision)
        {
            return false;
        }

        _seasonDiagnosticProjection = projection;
        NotifySeasonInspectorChanged();
        return true;
    }

    public CampaignMapGenerationOptions? LastGenerationOptions
    {
        get => _lastGenerationOptions;
        private set => SetProperty(ref _lastGenerationOptions, value);
    }

    public bool ShowEmptyState => !HasWorld;

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(DocumentStateText));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanEditCampaignTiles));
                OnPropertyChanged(nameof(CanEditResources));
                OnPropertyChanged(nameof(CanEditSeasons));
                OnPropertyChanged(nameof(CanAdjustCampaignView));
                OnPropertyChanged(nameof(CanRegenerateWorld));
                OnPropertyChanged(nameof(CanRegenerateResources));
                OnPropertyChanged(nameof(CanGenerateSeasons));
                NotifyElevationHelperChanged();
                NotifyRiverSplitChanged();
                NotifyPinnedResourceActionChanged();
                NotifyHistoryChanged();
            }
        }
    }

    public string? ProjectDirectory
    {
        get => _projectDirectory;
        private set
        {
            if (SetProperty(ref _projectDirectory, value))
            {
                OnPropertyChanged(nameof(WorldName));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(ProjectPathText));
            }
        }
    }

    public string WorldName => ProjectDirectory is null
        ? _untitledName
        : Path.GetFileName(Path.TrimEndingDirectorySeparator(ProjectDirectory));

    public string WindowTitle => $"{WorldName}{(IsDirty ? " *" : string.Empty)} — Kingdom World Editor";

    public string ProjectPathText => ProjectDirectory ?? (_importSourceDirectory is null
        ? "Not saved yet"
        : $"Imported from {_importSourceDirectory}\nSave to a new folder to preserve the original.");

    public bool IsLegacyImportPending => _isLegacyImport && ProjectDirectory is null;

    public string? ImportSourceDirectory => _importSourceDirectory;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ZoomText
    {
        get => _zoomText;
        private set => SetProperty(ref _zoomText, value);
    }

    public string DocumentStateText => !HasWorld
        ? "No world"
        : _isLegacyImport && ProjectDirectory is null
            ? "Converted · unsaved"
            : IsDirty ? "Modified" : "Saved";

    public string WorldSummary => World is null
        ? "—"
        : $"{World.Definition.TilesX:N0} × {World.Definition.TilesY:N0} · {World.Definition.TileCount:N0} tiles";

    public string WorldScaleText => World is null
        ? "—"
        : $"{FormatKilometers(World.Definition.WorldWidthMeters)} × " +
          $"{FormatKilometers(World.Definition.WorldHeightMeters)}";

    public string TileSizeText => World is null
        ? "—"
        : $"{FormatKilometers(World.Definition.CampaignTileSizeMeters)} × " +
          $"{FormatKilometers(World.Definition.CampaignTileSizeMeters)}";

    public string HeightRangeText => World is null
        ? "—"
        : $"{World.Definition.MinimumHeightMeters:N0} to {World.Definition.MaximumHeightMeters:N0} m";

    public string DefaultHeightText => World is null
        ? "—"
        : $"{World.Definition.DefaultTileHeightMeters:N0} m";

    public bool CanUndo => !IsBusy && _history.CanUndo;

    public bool CanRedo => !IsBusy && _history.CanRedo;

    public string UndoMenuLabel => _history.NextUndoDescription is { } description ? $"Undo {description}" : "Undo";

    public string RedoMenuLabel => _history.NextRedoDescription is { } description ? $"Redo {description}" : "Redo";

    public string HoverTileXText => _hover?.Coordinate.X.ToString("N0") ?? "—";

    public string HoverTileYText => _hover?.Coordinate.Y.ToString("N0") ?? "—";

    public string HoverTileTypeText => GetTileTypeText(_hover?.Coordinate);

    public string HoverStoredHeightText => GetStoredHeightText(_hover?.Coordinate);

    public string HoverDerivedHeightText => GetDerivedHeightText(_hover);

    public string HoverWorldPositionText => GetWorldPositionText(_hover);

    public byte? HoverSelectedResourcePotential
    {
        get
        {
            if (ResourceMap is not { } resources ||
                SelectedResourceId is not { } resourceId ||
                _hover is not { } hover ||
                !resources.IsValidCoordinate(hover.Coordinate.X, hover.Coordinate.Y))
            {
                return null;
            }

            return resources.TryGetOccurrence(
                hover.Coordinate.X,
                hover.Coordinate.Y,
                resourceId,
                out var occurrence)
                ? occurrence.Potential
                : null;
        }
    }

    public string HoverSelectedResourceText
    {
        get
        {
            if (SelectedResourceOption is not { } selected || _hover is null || ResourceMap is null)
            {
                return "—";
            }

            if (!ResourceMap.TryGetOccurrence(
                    _hover.Value.Coordinate.X,
                    _hover.Value.Coordinate.Y,
                    selected.Id,
                    out var occurrence))
            {
                return $"{selected.Name} · none";
            }

            return $"{selected.Name} · {occurrence.Potential:N0} / 100 · " +
                   (occurrence.Locked ? "locked" : "unlocked");
        }
    }

    public string HoverSeasonText
    {
        get
        {
            if (SeasonMap is not { } seasons || _hover is not { } hover)
            {
                return "—";
            }

            var tile = seasons.GetTile(hover.Coordinate.X, hover.Coordinate.Y);
            var definition = seasons.Catalog.Get(tile.SeasonId);
            return $"{definition.Name} · {(tile.Locked ? "locked" : "unlocked")}";
        }
    }

    public CampaignTileCoordinate? PinnedCoordinate => _selectedCoordinate;

    public CampaignResourceOccurrenceRow? SelectedPinnedResourceOccurrence
    {
        get => _selectedPinnedResourceOccurrence;
        set
        {
            if (value is not null && !PinnedResourceOccurrences.Any(row =>
                    string.Equals(row.ResourceId, value.ResourceId, StringComparison.Ordinal)))
            {
                return;
            }

            if (SetProperty(ref _selectedPinnedResourceOccurrence, value))
            {
                NotifyPinnedResourceActionChanged();
            }
        }
    }

    public bool HasPinnedResourceOccurrences => PinnedResourceOccurrences.Count > 0;

    public bool HasNoPinnedResourceOccurrences => !HasPinnedResourceOccurrences;

    public bool CanAdoptPinnedResource =>
        !IsBusy && SelectedPinnedResourceOccurrence is not null;

    public bool CanLockPinnedResource =>
        CanEditSelectedPinnedOccurrence && SelectedPinnedResourceOccurrence is { IsLocked: false };

    public bool CanUnlockPinnedResource =>
        CanEditSelectedPinnedOccurrence && SelectedPinnedResourceOccurrence is { IsLocked: true };

    public bool CanErasePinnedResource => CanEditSelectedPinnedOccurrence;

    public string SelectedPinnedResourceWarningText =>
        SelectedPinnedResourceOccurrence?.HardWarningText ?? "Select a pinned resource occurrence.";

    public string SelectedPinnedResourceUnevaluatedText =>
        SelectedPinnedResourceOccurrence?.UnevaluatedFactorsText ?? string.Empty;

    public string SelectionText => GetSelectionText();

    public bool HasPinnedSeason =>
        SeasonMap is { } seasons && _selectedCoordinate is { } coordinate &&
        seasons.IsValidCoordinate(coordinate.X, coordinate.Y);

    public string PinnedSeasonIdentityText
    {
        get
        {
            if (!TryGetPinnedSeason(out _, out var tile, out var definition))
            {
                return "Right-click a tile to inspect its authoritative season.";
            }

            var source = SeasonMap!.Catalog.IsBuiltIn(definition.Id) ? "Built-in" : "Custom";
            return $"{definition.Name} · ID {definition.Id} · {source} · " +
                   $"fallback {definition.Fallback} · {(tile.Locked ? "locked" : "unlocked")}";
        }
    }

    public string PinnedSeasonTerrainText
    {
        get
        {
            if (World is not { } world ||
                _selectedCoordinate is not { } coordinate ||
                _seasonTerrainQuery is not { } terrainQuery)
            {
                return "Terrain support: —";
            }

            var terrain = terrainQuery.GetSample(coordinate.X, coordinate.Y);
            var customName = terrain.CustomTerrainId is { } customId &&
                             world.Tiles.TryGetCustomTerrainDefinition(customId, out var custom)
                ? $" ({custom.Name})"
                : string.Empty;
            return $"Terrain {terrain.TerrainType}{customName} · {terrain.ElevationMeters:N0} m · " +
                   $"water {terrain.WaterFeatures}";
        }
    }

    public string PinnedSeasonRuleText
    {
        get
        {
            if (!TryGetPinnedSeason(out _, out _, out var definition))
            {
                return "Rule: —";
            }

            var priorityIndex = _seasonPriorityIds
                .Select((id, index) => (id, index))
                .FirstOrDefault(pair => string.Equals(pair.id, definition.Id, StringComparison.Ordinal));
            var priority = _seasonPriorityIds.Contains(definition.Id, StringComparer.Ordinal)
                ? $"priority {priorityIndex.index + 1:N0} of {_seasonPriorityIds.Count:N0}"
                : "manual-only";
            return $"{priority} · {GetSeasonRuleSummary(definition.Rule)}";
        }
    }

    public string PinnedSeasonGenerationText
    {
        get
        {
            if (SeasonSavedGeneration is not { } saved)
            {
                return "No accepted generation recipe. Climate support, winning rule, overlaps, and staleness are unavailable for this manual/compatibility layer.";
            }

            if (!TryGetPinnedSeason(out var coordinate, out var authoritativeTile, out var authoritativeDefinition))
            {
                return "Right-click a tile to inspect its generated climate support and rule result.";
            }

            if (_seasonDiagnosticProjection is not { } projection ||
                SeasonMap is not { } seasons ||
                _seasonTerrainQuery is not { } terrainQuery)
            {
                return "An accepted generation recipe is saved, but its derived diagnostic cache is not ready. Reopen Generate seasons or reload the project to rebuild it; tile authority remains exact.";
            }

            CampaignSeasonGenerationDiagnostic diagnostic;
            try
            {
                diagnostic = CampaignSeasonGenerationDiagnostics.Evaluate(
                    projection.SupportFields,
                    seasons.Catalog,
                    saved.Settings,
                    coordinate.X,
                    coordinate.Y);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return $"Generation diagnostics are unavailable: {exception.Message}";
            }

            var winner = seasons.Catalog.Get(diagnostic.WinningSeasonId);
            var shadowed = diagnostic.ShadowedSeasonIds.Count == 0
                ? "none"
                : string.Join(
                    ", ",
                    diagnostic.ShadowedSeasonIds.Select(id => seasons.Catalog.Get(id).Name));
            var authoritativePriorityIndex = saved.Settings.PriorityIds
                .Select((id, index) => (id, index))
                .FirstOrDefault(pair => string.Equals(
                    pair.id,
                    authoritativeTile.SeasonId,
                    StringComparison.Ordinal));
            var authoritativeEnabled = saved.Settings.PriorityIds.Contains(
                authoritativeTile.SeasonId,
                StringComparer.Ordinal);
            var higherPriorityOverlaps = authoritativeEnabled
                ? diagnostic.MatchingSeasonIds
                    .Where(id => GetSeasonPriorityIndex(saved.Settings.PriorityIds, id) < authoritativePriorityIndex.index)
                    .Select(id => seasons.Catalog.Get(id).Name)
                    .ToArray()
                : diagnostic.MatchingSeasonIds
                    .Select(id => seasons.Catalog.Get(id).Name)
                    .ToArray();
            var overlapText = higherPriorityOverlaps.Length == 0
                ? "none"
                : string.Join(", ", higherPriorityOverlaps);
            var inputFingerprint = CampaignSeasonGenerationFingerprint.GetInputFingerprint(
                seasons.Catalog,
                saved.Settings);
            var sourceCurrent =
                terrainQuery.Revision == projection.SupportFields.Terrain.Revision &&
                string.Equals(
                    projection.CurrentSourceTerrainFingerprint,
                    saved.SourceTerrainFingerprint,
                    StringComparison.Ordinal);
            var inputsCurrent = string.Equals(
                inputFingerprint,
                saved.InputFingerprint,
                StringComparison.Ordinal);
            var authorityDifference = string.Equals(
                authoritativeTile.SeasonId,
                diagnostic.WinningSeasonId,
                StringComparison.Ordinal)
                ? "authority matches winner"
                : $"authority is {authoritativeDefinition.Name} ({(authoritativeTile.Locked ? "locked" : "manual/unlocked")})";
            var support = diagnostic.Support;
            return
                $"Climate · latitude {support.LatitudeDegrees:0.##}° · temperature {support.TemperatureCelsius:0.##} °C · " +
                $"moisture {support.Moisture:0.###} · intensity {support.SeasonalIntensity:+0.###;-0.###;0} · " +
                $"tendency {support.SeasonalTendency:+0.###;-0.###;0} · rain shadow {support.RainShadow:0.###}. " +
                $"Water distance · Sea {FormatSeasonDistance(support.SeaDistanceKilometers)} · " +
                $"Lake {FormatSeasonDistance(support.LakeDistanceKilometers)} · " +
                $"River {FormatSeasonDistance(support.RiverDistanceKilometers)}. " +
                $"Generator winner {winner.Name}; shadowed matches {shadowed}; higher-priority overlaps for current authority {overlapText}; {authorityDifference}. " +
                $"Accepted recipe source {(sourceCurrent ? "current" : "stale")} · inputs {(inputsCurrent ? "current" : "stale")}.";
        }
    }

    public bool CanLockPinnedSeason =>
        !IsBusy && TryGetPinnedSeason(out _, out var tile, out _) && !tile.Locked;

    public bool CanUnlockPinnedSeason =>
        !IsBusy && TryGetPinnedSeason(out _, out var tile, out _) && tile.Locked;

    public void SwitchToTerrainWorkspace() => SetWorkspace(EditorWorkspace.Terrain);

    public void SwitchToResourcesWorkspace() => SetWorkspace(EditorWorkspace.Resources);

    public void SwitchToSeasonsWorkspace() => SetWorkspace(EditorWorkspace.Seasons);

    public void SelectResourceAddUpdateTool() => SetResourceEraseTool(erase: false);

    public void SelectResourceEraseTool() => SetResourceEraseTool(erase: true);

    public void SelectSeasonPaintTool() => SetSeasonPaintTool(CampaignSeasonPaintTool.Paint);

    public void SelectSeasonResetTool() => SetSeasonPaintTool(CampaignSeasonPaintTool.ResetToDefault);

    public void SelectSeasonLockTool() => SetSeasonPaintTool(CampaignSeasonPaintTool.Lock);

    public void SelectSeasonUnlockTool() => SetSeasonPaintTool(CampaignSeasonPaintTool.Unlock);

    public bool LockPinnedSeason() => SetPinnedSeasonLock(locked: true);

    public bool UnlockPinnedSeason() => SetPinnedSeasonLock(locked: false);

    public bool AdoptSelectedPinnedResource()
    {
        if (!CanAdoptPinnedResource || SelectedPinnedResourceOccurrence is not { } selected)
        {
            return false;
        }

        var option = ResourceOptions.FirstOrDefault(value =>
            string.Equals(value.Id, selected.ResourceId, StringComparison.Ordinal));
        if (option is null)
        {
            SelectedResourceCategoryFilter = CampaignResourceCategoryFilter.All;
            option = ResourceOptions.FirstOrDefault(value =>
                string.Equals(value.Id, selected.ResourceId, StringComparison.Ordinal));
        }

        if (option is null)
        {
            return false;
        }

        SelectedResourceOption = option;
        ResourcePotential = selected.Potential;
        LockManualResourceEdits = selected.IsLocked;
        StatusMessage = $"Adopted {selected.Name} at {selected.Potential:N0} / 100 " +
                        $"with {(selected.IsLocked ? "locked" : "unlocked")} authoring state.";
        return true;
    }

    public bool LockSelectedPinnedResource() => SetSelectedPinnedResourceLock(locked: true);

    public bool UnlockSelectedPinnedResource() => SetSelectedPinnedResourceLock(locked: false);

    public bool EraseSelectedPinnedResource()
    {
        if (!TryGetSelectedPinnedOccurrence(out var coordinate, out var occurrence, out var definition))
        {
            return false;
        }

        var command = new CampaignResourceEditCommand(
            ResourceMap!,
            $"Erase {definition.Name}",
            [new CampaignResourceChange(
                coordinate.X,
                coordinate.Y,
                occurrence.ResourceId,
                occurrence,
                After: null)]);
        _history.Execute(command);
        CompleteDirectResourceEdit(
            command,
            $"Erased {definition.Name} from pinned tile {coordinate.X:N0}, {coordinate.Y:N0}.");
        return true;
    }

    public void CreateWorld(CampaignWorldDefinition definition) =>
        CreateWorld(new CampaignWorld(definition), generationResult: null);

    public void CreateWorld(
        CampaignWorld world,
        CampaignMapGenerationResult? generationResult)
    {
        ArgumentNullException.ThrowIfNull(world);
        var seasons = new CampaignSeasonMap(world.Definition);
        CreateWorld(
            world,
            generationResult,
            seasons,
            CampaignSeasonGenerationSettings.DefaultPriority,
            seasonSavedGeneration: null,
            seasonSupportFields: null);
    }

    public void CreateWorld(
        CampaignWorld world,
        CampaignMapGenerationResult? generationResult,
        CampaignSeasonMap seasons,
        IReadOnlyList<string> seasonPriorityIds,
        CampaignSeasonSavedGeneration? seasonSavedGeneration,
        CampaignSeasonSupportFields? seasonSupportFields)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(seasons);
        ArgumentNullException.ThrowIfNull(seasonPriorityIds);
        ValidateSeasonDocument(world, seasons, seasonPriorityIds, seasonSavedGeneration);
        if (seasonSupportFields is not null &&
            seasonSupportFields.Terrain.Definition != world.Definition)
        {
            throw new ArgumentException(
                "Season support diagnostics must use the new world's value-equal definition.",
                nameof(seasonSupportFields));
        }

        var resources = new CampaignResourceMap(world.Definition);
        World = world;
        ResourceMap = resources;
        ResourceGenerationSettings = null;
        InstallSeasonDocument(
            seasons,
            seasonPriorityIds,
            seasonSavedGeneration);
        _seasonDiagnosticProjection = seasonSavedGeneration is not null && seasonSupportFields is not null
            ? new SeasonDiagnosticProjection(
                seasonSupportFields,
                seasonSavedGeneration.SourceTerrainFingerprint,
                seasonSavedGeneration.InputFingerprint)
            : null;
        _untitledName = "Untitled World";
        ProjectDirectory = null;
        _importSourceDirectory = null;
        _isLegacyImport = false;
        LastGenerationOptions = CreateGenerationOptions(generationResult);
        _history.Clear();
        _hover = null;
        _selectedCoordinate = null;
        OnPropertyChanged(nameof(PinnedCoordinate));
        RefreshCustomTerrainTypes();
        RefreshResourceOptions();
        RefreshSeasonOptions();
        RefreshPinnedResourceOccurrences();
        StampHeight = world.Definition.DefaultTileHeightMeters;
        IsDirty = true;
        StatusMessage = generationResult is { Preset: not CampaignMapGenerationPreset.Blank } generated
            ? $"Generated {GetGenerationPresetName(generated.Preset)} · seed {generated.Seed:N0} · " +
              GetGenerationCoastStatus(generated) +
              $"{(generated.RequestedLandMix is null ? $"{generated.MountainDensity} mountain systems" : "custom inland terrain mix")} · " +
              $"{generated.LandTileCount:N0} land, {generated.SeaTileCount:N0} Sea, " +
              $"{generated.LakeTileCount:N0} Lake, {GetGeneratedRiverStatus(generated)}. " +
              "Every tile is editable."
            : $"Created blank {WorldSummary}. Stamp a type and centre height into complete campaign tiles. " +
              $"Every tile starts as {seasons.Catalog.Get(seasons.DefaultSeasonId).Name}.";
        NotifyDocumentIdentityChanged();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
        NotifySeasonStatusChanged();
    }

    public void RegenerateWorld(
        CampaignWorld world,
        CampaignMapGenerationResult generationResult,
        CampaignResourceWorldRegenerationResult? resourceRegenerationResult = null,
        CampaignSeasonWorldRegenerationResult? seasonRegenerationResult = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(generationResult);
        if (World is null)
        {
            throw new InvalidOperationException("A world must be open before it can be regenerated.");
        }

        if (generationResult.Preset == CampaignMapGenerationPreset.Blank)
        {
            throw new ArgumentException("Regeneration requires a generated world preset.", nameof(generationResult));
        }

        var currentResources = ResourceMap ?? new CampaignResourceMap(World.Definition);
        var currentSeasons = SeasonMap ?? new CampaignSeasonMap(World.Definition);
        ValidateResourceDocument(World, currentResources, ResourceGenerationSettings);
        ValidateSeasonDocument(World, currentSeasons, _seasonPriorityIds, SeasonSavedGeneration);
        var sameLattice = HasSameCampaignLattice(World.Definition, world.Definition);
        var seasonsCanResetWithoutDataLoss =
            currentSeasons.LockedTileCount == 0 &&
            currentSeasons.GetUsageCount(currentSeasons.DefaultSeasonId) == currentSeasons.TileCount &&
            SeasonSavedGeneration is null;
        if (!sameLattice && !seasonsCanResetWithoutDataLoss && seasonRegenerationResult is null)
        {
            throw new InvalidOperationException(
                "Changing the campaign grid would discard authoritative season assignments, locks, or a saved generation recipe. " +
                "Generate a reviewed season remap in the terrain-and-Season preview before accepting the new grid.");
        }

        if (!sameLattice &&
            (currentResources.OccurrenceCount > 0 || ResourceGenerationSettings is not null) &&
            resourceRegenerationResult is null)
        {
            throw new InvalidOperationException(
                "A changed campaign grid with resources or saved resource settings requires the reviewed " +
                "resource-impact candidate from Regenerate world. Generate a fresh preview before accepting it.");
        }

        CampaignResourceMap reboundResources;
        CampaignResourceGenerationSettings? nextResourceSettings;
        if (resourceRegenerationResult is null)
        {
            reboundResources = RebindResourceMap(
                world.Definition,
                currentResources,
                preserveOccurrences: sameLattice);
            nextResourceSettings = ResourceGenerationSettings;
        }
        else
        {
            if (!resourceRegenerationResult.IsCurrent(World, currentResources, world))
            {
                throw new InvalidOperationException(
                    "The terrain or resource document changed after this world preview was generated. " +
                    "Generate a fresh preview before accepting it.");
            }

            if (resourceRegenerationResult.Report.SameLattice != sameLattice)
            {
                throw new ArgumentException(
                    "The resource-impact report does not match the candidate campaign lattice.",
                    nameof(resourceRegenerationResult));
            }

            if (resourceRegenerationResult.CandidateMap.Definition != world.Definition)
            {
                throw new ArgumentException(
                    "The reviewed resource candidate must use the replacement world's value-equal definition.",
                    nameof(resourceRegenerationResult));
            }

            if (!ReferenceEquals(resourceRegenerationResult.CandidateMap.Catalog, currentResources.Catalog) ||
                !HaveSameResourceDefinitions(
                    resourceRegenerationResult.CandidateMap.Catalog,
                    currentResources.Catalog))
            {
                throw new ArgumentException(
                    "The reviewed resource candidate must retain the current resource catalog and definitions.",
                    nameof(resourceRegenerationResult));
            }

            if (!ReferenceEquals(
                    resourceRegenerationResult.Settings,
                    ResourceGenerationSettings))
            {
                throw new InvalidOperationException(
                    "Resource generation settings changed after this world preview was generated. " +
                    "Generate a fresh preview before accepting it.");
            }

            reboundResources = resourceRegenerationResult.CandidateMap;
            nextResourceSettings = resourceRegenerationResult.Settings;
        }

        nextResourceSettings?.EnsureValid(reboundResources.Catalog);
        ValidateResourceDocument(world, reboundResources, nextResourceSettings);
        CampaignSeasonMap reboundSeasons;
        CampaignSeasonSavedGeneration? nextSeasonSavedGeneration;
        CampaignSeasonSupportFields? nextSeasonSupportFields;
        if (seasonRegenerationResult is null)
        {
            reboundSeasons = sameLattice
                ? RebindSeasonMap(world.Definition, currentSeasons)
                : new CampaignSeasonMap(
                    world.Definition,
                    currentSeasons.Catalog,
                    currentSeasons.DefaultSeasonId);
            nextSeasonSavedGeneration = SeasonSavedGeneration;
            nextSeasonSupportFields = null;
        }
        else
        {
            if (!seasonRegenerationResult.IsCurrent(World, currentSeasons, world))
            {
                throw new InvalidOperationException(
                    "The terrain or Season Layer changed after this world preview was generated. " +
                    "Generate a fresh preview before accepting it.");
            }

            if (seasonRegenerationResult.Report.SameLattice != sameLattice)
            {
                throw new ArgumentException(
                    "The Season impact report does not match the candidate campaign lattice.",
                    nameof(seasonRegenerationResult));
            }

            if (!seasonRegenerationResult.Report.CanAccept)
            {
                throw new InvalidOperationException(
                    "The reviewed Season candidate still has unresolved equal-overlap locks or unpermitted locked drops.");
            }

            if (seasonRegenerationResult.CandidateMap.Definition != world.Definition)
            {
                throw new ArgumentException(
                    "The reviewed Season candidate must use the replacement world's value-equal definition.",
                    nameof(seasonRegenerationResult));
            }

            if (!ReferenceEquals(seasonRegenerationResult.CandidateMap.Catalog, currentSeasons.Catalog) ||
                !string.Equals(
                    seasonRegenerationResult.CandidateMap.DefaultSeasonId,
                    currentSeasons.DefaultSeasonId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The reviewed Season candidate must retain the current immutable catalog and default identity.",
                    nameof(seasonRegenerationResult));
            }

            if (!seasonRegenerationResult.SourcePriorityIds.SequenceEqual(
                    _seasonPriorityIds,
                    StringComparer.Ordinal) ||
                !ReferenceEquals(
                    seasonRegenerationResult.SourceSavedGeneration,
                    SeasonSavedGeneration))
            {
                throw new InvalidOperationException(
                    "Season priority or saved generation settings changed after this world preview was generated. " +
                    "Generate a fresh preview before accepting it.");
            }

            reboundSeasons = seasonRegenerationResult.CandidateMap;
            nextSeasonSavedGeneration = seasonRegenerationResult.SavedGeneration;
            nextSeasonSupportFields = seasonRegenerationResult.SupportFields;
        }

        ValidateSeasonDocument(
            world,
            reboundSeasons,
            _seasonPriorityIds,
            nextSeasonSavedGeneration);
        var nextGenerationOptions = CreateGenerationOptions(generationResult);

        World = world;
        ResourceMap = reboundResources;
        ResourceGenerationSettings = nextResourceSettings;
        InstallSeasonDocument(reboundSeasons, _seasonPriorityIds, nextSeasonSavedGeneration);
        _seasonDiagnosticProjection = nextSeasonSavedGeneration is not null &&
            nextSeasonSupportFields is not null
            ? new SeasonDiagnosticProjection(
                nextSeasonSupportFields,
                nextSeasonSavedGeneration.SourceTerrainFingerprint,
                nextSeasonSavedGeneration.InputFingerprint)
            : null;
        LastGenerationOptions = nextGenerationOptions;
        _history.Clear();
        _hover = null;
        _selectedCoordinate = null;
        OnPropertyChanged(nameof(PinnedCoordinate));
        RefreshCustomTerrainTypes();
        RefreshResourceOptions();
        RefreshSeasonOptions();
        RefreshPinnedResourceOccurrences();
        StampHeight = Math.Clamp(
            StampHeight,
            world.Definition.MinimumHeightMeters,
            world.Definition.MaximumHeightMeters);
        IsDirty = true;
        var resourceStatus = resourceRegenerationResult is null
            ? sameLattice
                ? $"{reboundResources.OccurrenceCount:N0} resource occurrence(s) were preserved"
                : "the empty resource layer was rebound to the new grid"
            : GetWorldRegenerationResourceStatus(resourceRegenerationResult.Report);
        var seasonStatus = seasonRegenerationResult is null
            ? sameLattice
                ? $"{reboundSeasons.TileCount:N0} season tile(s) were preserved exactly"
                : $"the uniform unlocked {GetDefaultSeasonName()} layer was rebound to the new grid"
            : GetWorldRegenerationSeasonStatus(seasonRegenerationResult.Report);
        StatusMessage =
            $"Regenerated {GetGenerationPresetName(generationResult.Preset)} from the reviewed preview · " +
            $"seed {generationResult.Seed:N0}. World definition and tiles were replaced; " +
            $"{resourceStatus}; " +
            $"{seasonStatus}; " +
            "current project identity was kept and undo history was cleared.";
        NotifyDocumentIdentityChanged();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
        NotifySeasonStatusChanged();
    }

    public void OpenWorld(
        CampaignWorld world,
        string? projectDirectory,
        bool wasConvertedFromLegacy,
        string sourceProjectDirectory,
        int normalizedLegacyCoastalTileCount = 0)
    {
        ArgumentNullException.ThrowIfNull(world);
        OpenWorld(
            world,
            new CampaignResourceMap(world.Definition),
            resourceGenerationSettings: null,
            projectDirectory,
            wasConvertedFromLegacy,
            sourceProjectDirectory,
            normalizedLegacyCoastalTileCount);
    }

    public void OpenWorld(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? resourceGenerationSettings,
        string? projectDirectory,
        bool wasConvertedFromLegacy,
        string sourceProjectDirectory,
        int normalizedLegacyCoastalTileCount = 0)
    {
        ArgumentNullException.ThrowIfNull(world);
        OpenWorld(
            world,
            resourceMap,
            resourceGenerationSettings,
            new CampaignSeasonMap(world.Definition),
            CampaignSeasonGenerationSettings.DefaultPriority,
            seasonSavedGeneration: null,
            projectDirectory,
            wasConvertedFromLegacy,
            sourceProjectDirectory,
            normalizedLegacyCoastalTileCount,
            seasonsWereImplicitCompatibility: false);
    }

    public void OpenWorld(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? resourceGenerationSettings,
        CampaignSeasonMap seasonMap,
        IEnumerable<string> seasonPriorityIds,
        CampaignSeasonSavedGeneration? seasonSavedGeneration,
        string? projectDirectory,
        bool wasConvertedFromLegacy,
        string sourceProjectDirectory,
        int normalizedLegacyCoastalTileCount = 0,
        bool seasonsWereImplicitCompatibility = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(resourceMap);
        ValidateResourceDocument(world, resourceMap, resourceGenerationSettings);
        ValidateSeasonDocument(world, seasonMap, seasonPriorityIds, seasonSavedGeneration);

        World = world;
        ResourceMap = resourceMap;
        ResourceGenerationSettings = resourceGenerationSettings;
        InstallSeasonDocument(seasonMap, seasonPriorityIds, seasonSavedGeneration);
        _isLegacyImport = wasConvertedFromLegacy;
        _importSourceDirectory = wasConvertedFromLegacy ? sourceProjectDirectory : null;
        _untitledName = wasConvertedFromLegacy
            ? $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(sourceProjectDirectory))} (converted)"
            : "Untitled World";
        ProjectDirectory = projectDirectory;
        LastGenerationOptions = null;
        _history.Clear();
        _hover = null;
        _selectedCoordinate = null;
        OnPropertyChanged(nameof(PinnedCoordinate));
        RefreshCustomTerrainTypes();
        RefreshResourceOptions();
        RefreshSeasonOptions();
        RefreshPinnedResourceOccurrences();
        StampHeight = world.Definition.DefaultTileHeightMeters;
        IsDirty = wasConvertedFromLegacy || normalizedLegacyCoastalTileCount > 0;
        StatusMessage = wasConvertedFromLegacy
            ? "Legacy terrain imported into tile-centre heights. Save to a new folder; the source remains unchanged."
            : normalizedLegacyCoastalTileCount > 0
                ? $"Opened {WorldName}; converted {normalizedLegacyCoastalTileCount:N0} legacy Coastal tile(s) to Plains. " +
                  "Automatic 10% water edges now preserve the underlying land. Save to update the project."
            : seasonsWereImplicitCompatibility
                ? $"Opened {WorldName} with an implicit unlocked Spring season layer. " +
                  "The first save will add season project files without changing terrain or resources."
            : $"Opened {WorldName} with {world.Tiles.MaterializedTileCount:N0} stored tile overrides and " +
              $"{resourceMap.OccurrenceCount:N0} resource occurrence(s), plus " +
              $"{seasonMap.LockedTileCount:N0} locked season tile(s).";
        NotifyDocumentIdentityChanged();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
        NotifySeasonStatusChanged();
    }

    public void MarkSaved(string projectDirectory)
    {
        ProjectDirectory = projectDirectory;
        _importSourceDirectory = null;
        _isLegacyImport = false;
        IsDirty = false;
        StatusMessage = $"Saved {WorldName}.";
        NotifyDocumentIdentityChanged();
    }

    public bool UpdateCustomTerrainTypes(
        IReadOnlyList<CampaignCustomTerrainDefinition> definitions)
    {
        if (World is null || IsBusy)
        {
            return false;
        }

        if (!World.Tiles.SetCustomTerrainDefinitions(definitions))
        {
            return false;
        }

        RefreshCustomTerrainTypes();
        IsDirty = true;
        StatusMessage = World.Tiles.CustomTerrainDefinitions.Count == 0
            ? "Removed all custom land tile types. Painted custom tiles must be repainted first."
            : $"Updated {World.Tiles.CustomTerrainDefinitions.Count:N0} custom land tile type(s). " +
              "They remain safe base terrain for water and river rules.";
        RefreshPinnedResourceOccurrences();
        NotifyInspectorChanged();
        return true;
    }

    public bool UpdateCustomResources(
        IReadOnlyList<CampaignResourceDefinition> definitions,
        string? selectedResourceId = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (World is not { } world || ResourceMap is not { } currentResources || IsBusy)
        {
            return false;
        }

        var replacementCatalog = new CampaignResourceCatalog(definitions);
        if (selectedResourceId is not null && !replacementCatalog.Contains(selectedResourceId))
        {
            throw new ArgumentException(
                $"Selected resource '{selectedResourceId}' is not present in the replacement catalog.",
                nameof(selectedResourceId));
        }

        var usageCounts = currentResources.GetUsageCounts(
            currentResources.Catalog.CustomDefinitions.Select(static definition => definition.Id));
        foreach (var currentDefinition in currentResources.Catalog.CustomDefinitions)
        {
            var usageCount = usageCounts[currentDefinition.Id];
            if (usageCount == 0)
            {
                continue;
            }

            if (!replacementCatalog.TryGet(currentDefinition.Id, out var replacementDefinition))
            {
                throw new InvalidOperationException(
                    $"Custom resource '{currentDefinition.Name}' ({currentDefinition.Id}) is used on " +
                    $"{usageCount:N0} tile(s). Erase those occurrences before changing its stable ID or deleting it.");
            }

            if (replacementDefinition.Category != currentDefinition.Category)
            {
                throw new InvalidOperationException(
                    $"Custom resource '{currentDefinition.Name}' ({currentDefinition.Id}) is used on " +
                    $"{usageCount:N0} tile(s). Its Renewable/Finite category is locked while it is used.");
            }
        }

        if (HaveEquivalentResourceDefinitions(currentResources.Catalog, replacementCatalog))
        {
            SelectResourceOption(selectedResourceId);
            StatusMessage = "Custom resource definitions are unchanged.";
            return false;
        }

        var replacementSettings = RebindResourceGenerationSettings(
            ResourceGenerationSettings,
            replacementCatalog);
        var replacementMap = new CampaignResourceMap(world.Definition, replacementCatalog);
        replacementMap.Apply(currentResources.GetMaterializedOccurrences().Select(static entry =>
            CampaignResourceMutation.Upsert(entry.X, entry.Y, entry.Occurrence)));
        ValidateResourceDocument(world, replacementMap, replacementSettings);

        ResourceMap = replacementMap;
        ResourceGenerationSettings = replacementSettings;
        _history.Clear();
        RefreshResourceOptions();
        SelectResourceOption(selectedResourceId);
        RefreshPinnedResourceOccurrences();
        IsDirty = true;
        StatusMessage = replacementCatalog.CustomDefinitions.Count == 0
            ? "Removed all unused custom resources. Existing built-in occurrences were preserved; undo history was cleared."
            : $"Updated {replacementCatalog.CustomDefinitions.Count:N0} custom resource definition(s). " +
              $"Preserved {replacementMap.OccurrenceCount:N0} occurrence(s); undo history was cleared.";
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
        return true;
    }

    public bool UpdateSeasons(
        IReadOnlyList<CampaignSeasonDefinition> builtInDefinitions,
        IReadOnlyList<CampaignSeasonDefinition> customDefinitions,
        IReadOnlyList<string> priorityIds,
        IReadOnlyDictionary<string, string> deletedSeasonReplacements,
        string? selectedSeasonId = null)
    {
        ArgumentNullException.ThrowIfNull(builtInDefinitions);
        ArgumentNullException.ThrowIfNull(customDefinitions);
        ArgumentNullException.ThrowIfNull(priorityIds);
        ArgumentNullException.ThrowIfNull(deletedSeasonReplacements);
        if (World is not { } world || SeasonMap is not { } currentSeasons || IsBusy)
        {
            return false;
        }

        var replacementCatalog = new CampaignSeasonCatalog(customDefinitions, builtInDefinitions);
        new CampaignSeasonGenerationSettings(0, priorityIds: priorityIds)
            .EnsureValid(replacementCatalog, world.Definition);
        if (selectedSeasonId is not null && !replacementCatalog.Contains(selectedSeasonId))
        {
            throw new ArgumentException(
                $"Selected season '{selectedSeasonId}' is not present in the replacement catalog.",
                nameof(selectedSeasonId));
        }

        foreach (var (removedId, replacementId) in deletedSeasonReplacements)
        {
            if (replacementCatalog.Contains(removedId))
            {
                throw new ArgumentException(
                    $"Replacement mapping references season '{removedId}', which was not removed.",
                    nameof(deletedSeasonReplacements));
            }

            if (!currentSeasons.Catalog.Contains(removedId) || !replacementCatalog.Contains(replacementId))
            {
                throw new ArgumentException(
                    $"Season replacement '{removedId}' → '{replacementId}' is not valid for the current and replacement catalogs.",
                    nameof(deletedSeasonReplacements));
            }
        }

        var usageCounts = currentSeasons.GetUsageCounts(
            currentSeasons.Catalog.Definitions.Select(static definition => definition.Id));
        foreach (var currentDefinition in currentSeasons.Catalog.Definitions)
        {
            if (replacementCatalog.Contains(currentDefinition.Id) || usageCounts[currentDefinition.Id] == 0)
            {
                continue;
            }

            if (!deletedSeasonReplacements.ContainsKey(currentDefinition.Id))
            {
                throw new InvalidOperationException(
                    $"Season '{currentDefinition.Name}' ({currentDefinition.Id}) is used by " +
                    $"{usageCounts[currentDefinition.Id]:N0} tile(s). Choose a replacement before deleting or changing its stable ID.");
            }
        }

        var priority = priorityIds.ToArray();
        var equivalentDefinitions = HaveEquivalentSeasonDefinitions(
            currentSeasons.Catalog,
            replacementCatalog);
        var equivalentPriority = _seasonPriorityIds.SequenceEqual(priority, StringComparer.Ordinal);
        if (equivalentDefinitions && equivalentPriority && deletedSeasonReplacements.Count == 0)
        {
            SelectSeasonOption(selectedSeasonId);
            StatusMessage = "Season definitions and priority are unchanged.";
            return false;
        }

        var nextDefaultSeasonId = replacementCatalog.Contains(currentSeasons.DefaultSeasonId)
            ? currentSeasons.DefaultSeasonId
            : deletedSeasonReplacements.TryGetValue(currentSeasons.DefaultSeasonId, out var defaultReplacement)
                ? defaultReplacement
                : throw new InvalidOperationException(
                    $"The project default season '{currentSeasons.DefaultSeasonId}' was removed without a replacement.");
        var replacementMap = new CampaignSeasonMap(
            world.Definition,
            replacementCatalog,
            nextDefaultSeasonId);
        replacementMap.Apply(currentSeasons.GetAllTiles().Select(entry =>
        {
            var seasonId = replacementCatalog.Contains(entry.Tile.SeasonId)
                ? entry.Tile.SeasonId
                : deletedSeasonReplacements[entry.Tile.SeasonId];
            return new CampaignSeasonMutation(
                entry.X,
                entry.Y,
                new CampaignSeasonTile(seasonId, entry.Tile.Locked));
        }));

        var nextSavedGeneration = equivalentPriority ? SeasonSavedGeneration : null;
        ValidateSeasonDocument(world, replacementMap, priority, nextSavedGeneration);
        InstallSeasonDocument(replacementMap, priority, nextSavedGeneration);
        _history.Clear();
        RefreshSeasonOptions();
        SelectSeasonOption(selectedSeasonId);
        IsDirty = true;
        StatusMessage =
            $"Updated {replacementCatalog.Definitions.Count:N0} season definition(s); " +
            $"preserved {replacementMap.TileCount:N0} tile assignments and cleared undo history.";
        NotifyInspectorChanged();
        NotifySeasonStatusChanged();
        return true;
    }

    public void RecordTileStroke(CampaignTileStampCommand command, int blockedRiverTiles = 0)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.IsEmpty)
        {
            if (blockedRiverTiles > 0)
            {
                StatusMessage = $"{blockedRiverTiles:N0} river crossing tiles blocked; no tiles changed.";
            }

            return;
        }

        _history.RecordExecuted(command);
        IsDirty = true;
        StatusMessage = blockedRiverTiles > 0
            ? $"{command.Description}: {command.Changes.Count:N0} tiles changed; " +
              $"{blockedRiverTiles:N0} river crossing tiles blocked."
            : $"{command.Description}: {command.Changes.Count:N0} complete tiles changed.";
        RefreshPinnedResourceOccurrences();
        NotifyInspectorChanged();
    }

    public void RecordResourceStroke(CampaignResourceEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (ResourceMap is null)
        {
            throw new InvalidOperationException("A resource layer must be open before recording a resource stroke.");
        }

        if (command.IsEmpty)
        {
            StatusMessage = "Resource stroke made no authoritative changes.";
            RefreshPinnedResourceOccurrences();
            NotifyResourceStatusChanged();
            return;
        }

        _history.RecordExecuted(command);
        IsDirty = true;
        StatusMessage = $"{command.Description}: {command.Changes.Count:N0} resource occurrence(s) changed.";
        RefreshPinnedResourceOccurrences();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
    }

    public void RecordSeasonStroke(CampaignSeasonEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (SeasonMap is null)
        {
            throw new InvalidOperationException("A season layer must be open before recording a season stroke.");
        }

        if (command.IsEmpty)
        {
            StatusMessage = "Season stroke made no authoritative changes.";
            NotifySeasonInspectorChanged();
            NotifySeasonStatusChanged();
            return;
        }

        _history.RecordExecuted(command);
        IsDirty = true;
        StatusMessage = $"{command.Description}: {command.Changes.Count:N0} season tile(s) changed.";
        NotifyInspectorChanged();
        NotifySeasonStatusChanged();
    }

    public bool Undo()
    {
        if (IsBusy)
        {
            return false;
        }

        var description = _history.NextUndoDescription;
        if (!_history.Undo())
        {
            return false;
        }

        IsDirty = true;
        StatusMessage = $"Undid {description ?? "tile edit"}.";
        RefreshPinnedResourceOccurrences();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
        NotifySeasonStatusChanged();
        return true;
    }

    public bool Redo()
    {
        if (IsBusy)
        {
            return false;
        }

        var description = _history.NextRedoDescription;
        if (!_history.Redo())
        {
            return false;
        }

        IsDirty = true;
        StatusMessage = $"Redid {description ?? "tile edit"}.";
        RefreshPinnedResourceOccurrences();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
        NotifySeasonStatusChanged();
        return true;
    }

    public void UpdateHover(CampaignTilePointerInfo? hover)
    {
        if (_hover == hover)
        {
            return;
        }

        _hover = hover;
        NotifyInspectorChanged();
    }

    public void SelectCoordinate(CampaignTileCoordinate coordinate)
    {
        _selectedCoordinate = coordinate;
        OnPropertyChanged(nameof(PinnedCoordinate));
        OnPropertyChanged(nameof(SelectionText));
        RefreshPinnedResourceOccurrences();
        NotifyElevationHelperChanged();
        NotifyRiverSplitChanged();
        NotifySeasonInspectorChanged();
    }

    public bool CreatePinnedRiverSplit()
    {
        if (!CanCreatePinnedRiverSplit ||
            _selectedCoordinate is not { } coordinate ||
            World is null)
        {
            StatusMessage = RiverSplitHelpText;
            return false;
        }

        RiverSplitDirection? direction = SelectedRiverSplitDirection == "Auto"
            ? null
            : Enum.Parse<RiverSplitDirection>(SelectedRiverSplitDirection, ignoreCase: false);
        if (!CampaignRiverSplitBuilder.TryCreate(
                World.Tiles,
                coordinate,
                RiverSplitBranchCount,
                direction,
                out var command,
                out var failureReason))
        {
            StatusMessage = failureReason ?? "The river split could not be created.";
            return false;
        }

        _history.RecordExecuted(command!);
        IsDirty = true;
        StatusMessage =
            $"Created {RiverSplitBranchCount} separated branches from pinned tile " +
            $"{coordinate.X:N0}, {coordinate.Y:N0}; no four-way crossing was added.";
        RefreshPinnedResourceOccurrences();
        NotifyInspectorChanged();
        return true;
    }

    public bool UsePinnedCenterHeight()
    {
        if (World is null ||
            _selectedCoordinate is not { } coordinate ||
            !World.Tiles.IsValidCoordinate(coordinate.X, coordinate.Y) ||
            IsBusy)
        {
            return false;
        }

        var height = World.Tiles.GetTile(coordinate.X, coordinate.Y).HeightMeters;
        StampHeight = height;
        StatusMessage = $"Copied {height:N0} m from pinned tile {coordinate.X:N0}, {coordinate.Y:N0}.";
        return true;
    }

    public bool UsePinnedNearbyHeight()
    {
        if (World is null ||
            _selectedCoordinate is not { } coordinate ||
            !World.Tiles.IsValidCoordinate(coordinate.X, coordinate.Y) ||
            IsBusy)
        {
            return false;
        }

        var suggestion = CampaignElevationHelper.SuggestNearby(World.Tiles, coordinate);
        StampHeight = suggestion.HeightMeters;
        StatusMessage = suggestion.SourceNeighborCount == 0
            ? $"Set {suggestion.HeightMeters:N0} m from the rounded pinned height; this tile has no neighbours."
            : $"Set {suggestion.HeightMeters:N0} m from {suggestion.SourceNeighborCount} N/E/S/W neighbours " +
              $"around pinned tile {coordinate.X:N0}, {coordinate.Y:N0}.";
        return true;
    }

    public void SetZoom(double pixelsPerTile)
    {
        ZoomText = pixelsPerTile >= 1
            ? $"{pixelsPerTile:N2} px/tile"
            : $"1 px/{1 / pixelsPerTile:N1} tiles";
    }

    private string GetTileTypeText(CampaignTileCoordinate? coordinate)
    {
        if (World is null || coordinate is null)
        {
            return "—";
        }

        var value = coordinate.Value;
        var tile = World.Tiles.GetTile(value.X, value.Y);
        return GetCampaignTileTypeName(tile) + GetAutomaticCoastSuffix(value, tile.Type);
    }

    private string GetStoredHeightText(CampaignTileCoordinate? coordinate)
    {
        if (World is null || coordinate is null)
        {
            return "—";
        }

        return $"{World.Tiles.GetTile(coordinate.Value.X, coordinate.Value.Y).HeightMeters:N0} m";
    }

    private string GetDerivedHeightText(CampaignTilePointerInfo? hover)
    {
        if (World is null || hover is null)
        {
            return "—";
        }

        var height = World.Tiles.GetDerivedHeight(hover.Value.TileSpaceX, hover.Value.TileSpaceY);
        return $"{height:N1} m";
    }

    private string GetWorldPositionText(CampaignTilePointerInfo? hover)
    {
        if (World is null || hover is null)
        {
            return "—";
        }

        var metersX = hover.Value.TileSpaceX * World.Definition.CampaignTileSizeMeters;
        var metersY = hover.Value.TileSpaceY * World.Definition.CampaignTileSizeMeters;
        return $"{FormatKilometers(metersX)}, {FormatKilometers(metersY)}";
    }

    private string GetSelectionText()
    {
        if (World is null || _selectedCoordinate is not { } coordinate)
        {
            return "No pinned tile";
        }

        var tile = World.Tiles.GetTile(coordinate.X, coordinate.Y);
        return $"Tile {coordinate.X:N0}, {coordinate.Y:N0} · " +
               $"{GetCampaignTileTypeName(tile)}{GetAutomaticCoastSuffix(coordinate, tile.Type)} · " +
               $"{tile.HeightMeters:N0} m at centre";
    }

    private void SetWorkspace(EditorWorkspace workspace)
    {
        var resources = workspace == EditorWorkspace.Resources;
        var seasons = workspace == EditorWorkspace.Seasons;
        if (_isResourcesWorkspace == resources && _isSeasonsWorkspace == seasons)
        {
            return;
        }

        _isResourcesWorkspace = resources;
        _isSeasonsWorkspace = seasons;
        OnPropertyChanged(nameof(IsResourcesWorkspace));
        OnPropertyChanged(nameof(IsSeasonsWorkspace));
        OnPropertyChanged(nameof(IsTerrainWorkspace));
        OnPropertyChanged(nameof(CanEditResources));
        OnPropertyChanged(nameof(CanEditSeasons));
        NotifyWorkspacePresentationChanged();
        StatusMessage = workspace switch
        {
            EditorWorkspace.Resources =>
                "Resources workspace active. Paint or inspect the selected resource without changing terrain.",
            EditorWorkspace.Seasons =>
                "Seasons workspace active. Paint, reset, lock, or unlock whole season tiles without changing terrain or resources.",
            _ => "Terrain workspace active. Stamp complete terrain tiles and centre heights.",
        };
    }

    private void SetSeasonPaintTool(CampaignSeasonPaintTool tool)
    {
        if (!Enum.IsDefined(tool) || _seasonPaintTool == tool)
        {
            return;
        }

        _seasonPaintTool = tool;
        OnPropertyChanged(nameof(SeasonPaintTool));
        OnPropertyChanged(nameof(IsSeasonPaintTool));
        OnPropertyChanged(nameof(IsSeasonResetTool));
        OnPropertyChanged(nameof(IsSeasonLockTool));
        OnPropertyChanged(nameof(IsSeasonUnlockTool));
        OnPropertyChanged(nameof(CanEditSeasons));
        NotifySeasonStampChanged();
        StatusMessage = tool switch
        {
            CampaignSeasonPaintTool.Paint => "Paint selected season tool active.",
            CampaignSeasonPaintTool.ResetToDefault =>
                $"Reset to {GetDefaultSeasonName()} tool active; reset tiles become unlocked.",
            CampaignSeasonPaintTool.Lock => "Lock existing season tool active.",
            CampaignSeasonPaintTool.Unlock => "Unlock existing season tool active.",
            _ => "Season tool changed.",
        };
    }

    private void SetResourceEraseTool(bool erase)
    {
        if (_isResourceEraseTool == erase)
        {
            return;
        }

        _isResourceEraseTool = erase;
        OnPropertyChanged(nameof(IsResourceEraseTool));
        OnPropertyChanged(nameof(IsResourceAddUpdateTool));
        NotifyResourceStampChanged();
        StatusMessage = erase
            ? "Erase selected resource tool active. Other resources and terrain remain unchanged."
            : "Add / update resource tool active.";
    }

    private void RefreshResourceOptions()
    {
        var previousId = SelectedResourceId;
        ResourceOptions.Clear();
        if (ResourceMap is { } resources)
        {
            foreach (var definition in resources.Catalog.Definitions.Where(IsResourceVisible))
            {
                ResourceOptions.Add(new CampaignResourceOption(
                    definition.Id,
                    definition.Name,
                    definition.Category,
                    new SolidColorBrush(Color.Parse(definition.ColorHex)),
                    resources.Catalog.IsBuiltIn(definition.Id) is false));
            }
        }

        var replacement = previousId is null
            ? null
            : ResourceOptions.FirstOrDefault(option =>
                string.Equals(option.Id, previousId, StringComparison.Ordinal));
        SelectedResourceOption = replacement ?? ResourceOptions.FirstOrDefault();
        NotifyResourceStampChanged();
    }

    private void RefreshSeasonOptions()
    {
        var previousId = SelectedSeasonId;
        SeasonOptions.Clear();
        if (SeasonMap is { } seasons)
        {
            var search = SeasonSearchText.Trim();
            foreach (var definition in seasons.Catalog.Definitions)
            {
                if (search.Length > 0 &&
                    !definition.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                    !definition.Id.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SeasonOptions.Add(new CampaignSeasonOption(
                    definition.Id,
                    definition.Name,
                    definition.Fallback,
                    new SolidColorBrush(Color.Parse(definition.ColorHex)),
                    IsCustom: !seasons.Catalog.IsBuiltIn(definition.Id),
                    IsGenerationEnabled: _seasonPriorityIds.Contains(
                        definition.Id,
                        StringComparer.Ordinal)));
            }
        }

        var replacement = previousId is null
            ? null
            : SeasonOptions.FirstOrDefault(option =>
                string.Equals(option.Id, previousId, StringComparison.Ordinal));
        SelectedSeasonOption = replacement ?? SeasonOptions.FirstOrDefault();
        OnPropertyChanged(nameof(HasSeasonOptions));
        OnPropertyChanged(nameof(HasNoSeasonOptions));
        NotifySeasonStampChanged();
    }

    private bool IsResourceVisible(CampaignResourceDefinition definition) =>
        SelectedResourceCategoryFilter switch
        {
            CampaignResourceCategoryFilter.All => true,
            CampaignResourceCategoryFilter.Renewable =>
                definition.Category == CampaignResourceCategory.Renewable,
            CampaignResourceCategoryFilter.Finite =>
                definition.Category == CampaignResourceCategory.Finite,
            _ => false,
        };

    private void RefreshPinnedResourceOccurrences()
    {
        var previousId = SelectedPinnedResourceOccurrence?.ResourceId;
        PinnedResourceOccurrences.Clear();
        if (World is { } world &&
            ResourceMap is { } resources &&
            _resourceTerrainQuery is { } terrainQuery &&
            _selectedCoordinate is { } coordinate &&
            world.Tiles.IsValidCoordinate(coordinate.X, coordinate.Y))
        {
            var terrain = terrainQuery.GetSample(coordinate.X, coordinate.Y);
            foreach (var occurrence in resources.GetOccurrences(coordinate.X, coordinate.Y))
            {
                var definition = resources.Catalog.Get(occurrence.ResourceId);
                var diagnostic = CampaignResourceDiagnosticEvaluator.Evaluate(definition, terrain);
                var hardWarningText = diagnostic.HasWarnings
                    ? string.Join(" ", diagnostic.Issues.Select(static issue => issue.Message))
                    : "No implemented hard-rule warning. Generator suitability is not implied.";
                var unevaluatedText = diagnostic.HasUnevaluatedFactors
                    ? "Not evaluated: " + string.Join(
                        ", ",
                        diagnostic.UnevaluatedFactors.Select(GetUnevaluatedFactorName)) + "."
                    : "All declared factors were evaluated.";
                PinnedResourceOccurrences.Add(new CampaignResourceOccurrenceRow(
                    occurrence.ResourceId,
                    definition.Name,
                    definition.Category,
                    occurrence.Potential,
                    occurrence.Locked,
                    diagnostic.HasWarnings,
                    hardWarningText,
                    unevaluatedText));
            }
        }

        var replacement = previousId is null
            ? null
            : PinnedResourceOccurrences.FirstOrDefault(row =>
                string.Equals(row.ResourceId, previousId, StringComparison.Ordinal));
        SelectedPinnedResourceOccurrence = replacement ?? PinnedResourceOccurrences.FirstOrDefault();
        OnPropertyChanged(nameof(HasPinnedResourceOccurrences));
        OnPropertyChanged(nameof(HasNoPinnedResourceOccurrences));
        NotifyPinnedResourceActionChanged();
    }

    private bool SetSelectedPinnedResourceLock(bool locked)
    {
        if (!TryGetSelectedPinnedOccurrence(out var coordinate, out var occurrence, out var definition) ||
            occurrence.Locked == locked)
        {
            return false;
        }

        var command = new CampaignResourceEditCommand(
            ResourceMap!,
            locked ? $"Lock {definition.Name}" : $"Unlock {definition.Name}",
            [new CampaignResourceChange(
                coordinate.X,
                coordinate.Y,
                occurrence.ResourceId,
                occurrence,
                occurrence with { Locked = locked })]);
        _history.Execute(command);
        CompleteDirectResourceEdit(
            command,
            $"{(locked ? "Locked" : "Unlocked")} {definition.Name} at pinned tile " +
            $"{coordinate.X:N0}, {coordinate.Y:N0}.");
        return true;
    }

    private bool TryGetSelectedPinnedOccurrence(
        out CampaignTileCoordinate coordinate,
        out CampaignResourceOccurrence occurrence,
        out CampaignResourceDefinition definition)
    {
        if (CanEditSelectedPinnedOccurrence &&
            _selectedCoordinate is { } selected &&
            SelectedPinnedResourceOccurrence is { } row &&
            ResourceMap!.TryGetOccurrence(selected.X, selected.Y, row.ResourceId, out occurrence))
        {
            coordinate = selected;
            definition = ResourceMap.Catalog.Get(row.ResourceId);
            return true;
        }

        coordinate = default;
        occurrence = default;
        definition = null!;
        return false;
    }

    private bool CanEditSelectedPinnedOccurrence =>
        !IsBusy &&
        ResourceMap is { } resources &&
        _selectedCoordinate is { } coordinate &&
        resources.IsValidCoordinate(coordinate.X, coordinate.Y) &&
        SelectedPinnedResourceOccurrence is { } row &&
        resources.TryGetOccurrence(coordinate.X, coordinate.Y, row.ResourceId, out _);

    private void CompleteDirectResourceEdit(
        CampaignResourceEditCommand command,
        string statusMessage)
    {
        if (command.IsEmpty)
        {
            return;
        }

        IsDirty = true;
        StatusMessage = statusMessage;
        RefreshPinnedResourceOccurrences();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
    }

    private bool SetPinnedSeasonLock(bool locked)
    {
        if (!TryGetPinnedSeason(out var coordinate, out var tile, out var definition) ||
            tile.Locked == locked)
        {
            return false;
        }

        var command = new CampaignSeasonEditCommand(
            SeasonMap!,
            locked ? $"Lock {definition.Name} season" : $"Unlock {definition.Name} season",
            [new CampaignSeasonChange(
                coordinate.X,
                coordinate.Y,
                tile,
                tile with { Locked = locked })]);
        _history.Execute(command);
        IsDirty = true;
        StatusMessage = $"{(locked ? "Locked" : "Unlocked")} {definition.Name} at pinned tile " +
                        $"{coordinate.X:N0}, {coordinate.Y:N0}.";
        NotifyInspectorChanged();
        NotifySeasonStatusChanged();
        return true;
    }

    private bool TryGetPinnedSeason(
        out CampaignTileCoordinate coordinate,
        out CampaignSeasonTile tile,
        out CampaignSeasonDefinition definition)
    {
        if (SeasonMap is { } seasons &&
            _selectedCoordinate is { } selected &&
            seasons.IsValidCoordinate(selected.X, selected.Y))
        {
            coordinate = selected;
            tile = seasons.GetTile(selected.X, selected.Y);
            definition = seasons.Catalog.Get(tile.SeasonId);
            return true;
        }

        coordinate = default;
        tile = default;
        definition = null!;
        return false;
    }

    private static string GetUnevaluatedFactorName(CampaignResourceUnevaluatedFactor factor) =>
        factor switch
        {
            CampaignResourceUnevaluatedFactor.ClimateProfile => "climate profile",
            CampaignResourceUnevaluatedFactor.GeologyProfile => "geology profile",
            CampaignResourceUnevaluatedFactor.PreferredTerrainTags => "preferred terrain tags",
            CampaignResourceUnevaluatedFactor.AvoidedTerrainTags => "avoided terrain tags",
            CampaignResourceUnevaluatedFactor.FieldWeights => "field weights",
            CampaignResourceUnevaluatedFactor.AssociationWeights => "association weights",
            CampaignResourceUnevaluatedFactor.DistributionShape => "distribution shape",
            CampaignResourceUnevaluatedFactor.RegionScale => "region scale",
            CampaignResourceUnevaluatedFactor.FinalGeneratorSuitability => "final generator suitability",
            _ => factor.ToString(),
        };

    private static void ValidateResourceDocument(
        CampaignWorld world,
        CampaignResourceMap resources,
        CampaignResourceGenerationSettings? settings)
    {
        if (resources.Definition != world.Definition)
        {
            throw new ArgumentException(
                "The resource map definition must be value-equal to the terrain world definition.",
                nameof(resources));
        }

        resources.EnsureValid();
        settings?.EnsureValid(resources.Catalog);
    }

    private static void ValidateSeasonDocument(
        CampaignWorld world,
        CampaignSeasonMap seasons,
        IEnumerable<string> priorityIds,
        CampaignSeasonSavedGeneration? savedGeneration)
    {
        ArgumentNullException.ThrowIfNull(seasons);
        ArgumentNullException.ThrowIfNull(priorityIds);
        if (seasons.Definition != world.Definition)
        {
            throw new ArgumentException(
                "The season map definition must be value-equal to the terrain world definition.",
                nameof(seasons));
        }

        seasons.EnsureValid();
        var priority = priorityIds.ToArray();
        new CampaignSeasonGenerationSettings(0, priorityIds: priority)
            .EnsureValid(seasons.Catalog, seasons.Definition);
        if (savedGeneration is not null)
        {
            savedGeneration.Settings.EnsureValid(seasons.Catalog, seasons.Definition);
            if (!priority.SequenceEqual(
                    savedGeneration.Settings.PriorityIds,
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Saved season generation settings must use the active season priority.",
                    nameof(savedGeneration));
            }
        }
    }

    private void InstallSeasonDocument(
        CampaignSeasonMap seasonMap,
        IEnumerable<string> priorityIds,
        CampaignSeasonSavedGeneration? savedGeneration)
    {
        ArgumentNullException.ThrowIfNull(seasonMap);
        ArgumentNullException.ThrowIfNull(priorityIds);
        var priority = Array.AsReadOnly(priorityIds.ToArray());
        new CampaignSeasonGenerationSettings(0, priorityIds: priority)
            .EnsureValid(seasonMap.Catalog, seasonMap.Definition);
        SeasonMap = seasonMap;
        _seasonPriorityIds = priority;
        OnPropertyChanged(nameof(SeasonPriorityIds));
        if (savedGeneration is null ||
            !ReferenceEquals(_seasonDiagnosticProjection?.SupportFields.Settings, savedGeneration.Settings))
        {
            _seasonDiagnosticProjection = null;
        }

        SeasonSavedGeneration = savedGeneration;
    }

    private static bool HaveSameResourceDefinitions(
        CampaignResourceCatalog left,
        CampaignResourceCatalog right) =>
        left.Definitions.Count == right.Definitions.Count &&
        left.Definitions.Zip(right.Definitions).All(static pair =>
            ReferenceEquals(pair.First, pair.Second));

    private static bool HaveEquivalentResourceDefinitions(
        CampaignResourceCatalog left,
        CampaignResourceCatalog right) =>
        left.Definitions.Count == right.Definitions.Count &&
        left.Definitions.Zip(right.Definitions).All(static pair =>
            HaveEquivalentResourceDefinition(pair.First, pair.Second));

    private static bool HaveEquivalentResourceDefinition(
        CampaignResourceDefinition left,
        CampaignResourceDefinition right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        left.Category == right.Category &&
        left.DistributionProfile == right.DistributionProfile &&
        left.Medium == right.Medium &&
        string.Equals(left.SymbolId, right.SymbolId, StringComparison.Ordinal) &&
        string.Equals(left.ColorHex, right.ColorHex, StringComparison.Ordinal) &&
        left.MapPriority == right.MapPriority &&
        left.CoveragePercent == right.CoveragePercent &&
        left.Richness == right.Richness &&
        left.Concentration == right.Concentration &&
        left.Rules.ElevationMeters == right.Rules.ElevationMeters &&
        left.Rules.Grade == right.Rules.Grade &&
        left.Rules.WaterDistanceKilometers == right.Rules.WaterDistanceKilometers &&
        left.Rules.RegionScaleKilometers == right.Rules.RegionScaleKilometers &&
        left.Rules.PreferredTerrainTags.SequenceEqual(
            right.Rules.PreferredTerrainTags,
            StringComparer.Ordinal) &&
        left.Rules.AvoidedTerrainTags.SequenceEqual(
            right.Rules.AvoidedTerrainTags,
            StringComparer.Ordinal) &&
        left.Rules.ExcludedTerrainSurfaces.SequenceEqual(
            right.Rules.ExcludedTerrainSurfaces) &&
        left.Rules.CustomTerrainIncludes.SequenceEqual(
            right.Rules.CustomTerrainIncludes,
            StringComparer.Ordinal) &&
        left.Rules.CustomTerrainExcludes.SequenceEqual(
            right.Rules.CustomTerrainExcludes,
            StringComparer.Ordinal) &&
        HaveEquivalentWeights(left.Rules.FieldWeights, right.Rules.FieldWeights) &&
        HaveEquivalentWeights(left.Rules.AssociationWeights, right.Rules.AssociationWeights);

    private static bool HaveEquivalentSeasonDefinitions(
        CampaignSeasonCatalog left,
        CampaignSeasonCatalog right) =>
        left.Definitions.Count == right.Definitions.Count &&
        left.Definitions.Zip(right.Definitions).All(static pair =>
            HaveEquivalentSeasonDefinition(pair.First, pair.Second));

    private static bool HaveEquivalentSeasonDefinition(
        CampaignSeasonDefinition left,
        CampaignSeasonDefinition right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        left.Fallback == right.Fallback &&
        string.Equals(left.ColorHex, right.ColorHex, StringComparison.Ordinal) &&
        left.TintStrengthPercent == right.TintStrengthPercent &&
        left.EffectIntensityPercent == right.EffectIntensityPercent &&
        left.Rule.LatitudeDegrees == right.Rule.LatitudeDegrees &&
        left.Rule.ElevationMeters == right.Rule.ElevationMeters &&
        left.Rule.TemperatureCelsius == right.Rule.TemperatureCelsius &&
        left.Rule.Moisture == right.Rule.Moisture &&
        left.Rule.SeasonalIntensity == right.Rule.SeasonalIntensity &&
        left.Rule.SeasonalTendency == right.Rule.SeasonalTendency &&
        left.Rule.SeaDistanceKilometers == right.Rule.SeaDistanceKilometers &&
        left.Rule.LakeDistanceKilometers == right.Rule.LakeDistanceKilometers &&
        left.Rule.RiverDistanceKilometers == right.Rule.RiverDistanceKilometers &&
        left.Rule.TerrainIncludes.SequenceEqual(right.Rule.TerrainIncludes) &&
        left.Rule.TerrainExcludes.SequenceEqual(right.Rule.TerrainExcludes) &&
        left.Rule.CustomTerrainIncludes.SequenceEqual(
            right.Rule.CustomTerrainIncludes,
            StringComparer.Ordinal) &&
        left.Rule.CustomTerrainExcludes.SequenceEqual(
            right.Rule.CustomTerrainExcludes,
            StringComparer.Ordinal);

    private static bool HaveEquivalentWeights(
        IReadOnlyDictionary<string, double> left,
        IReadOnlyDictionary<string, double> right) =>
        left.Count == right.Count &&
        left.All(pair => right.TryGetValue(pair.Key, out var value) && value.Equals(pair.Value));

    private static CampaignResourceGenerationSettings? RebindResourceGenerationSettings(
        CampaignResourceGenerationSettings? settings,
        CampaignResourceCatalog catalog)
    {
        if (settings is null)
        {
            return null;
        }

        var retainedOverrides = settings.Overrides
            .Where(value => catalog.Contains(value.ResourceId))
            .ToArray();
        if (retainedOverrides.Length == settings.Overrides.Count)
        {
            settings.EnsureValid(catalog);
            return settings;
        }

        var replacement = new CampaignResourceGenerationSettings(
            settings.ResourceSeed,
            settings.SeedDerivedFromWorld,
            settings.Abundance,
            settings.Climate,
            settings.Geology,
            retainedOverrides,
            settings.SchemaVersion);
        replacement.EnsureValid(catalog);
        return replacement;
    }

    private void SelectResourceOption(string? resourceId)
    {
        if (resourceId is null || ResourceMap is not { } resources || !resources.Catalog.Contains(resourceId))
        {
            return;
        }

        var definition = resources.Catalog.Get(resourceId);
        if (!IsResourceVisible(definition))
        {
            SelectedResourceCategoryFilter = CampaignResourceCategoryFilter.All;
        }

        SelectedResourceOption = ResourceOptions.FirstOrDefault(option =>
            string.Equals(option.Id, resourceId, StringComparison.Ordinal));
    }

    private void SelectSeasonOption(string? seasonId)
    {
        if (seasonId is null || SeasonMap is not { } seasons || !seasons.Catalog.Contains(seasonId))
        {
            return;
        }

        if (!SeasonOptions.Any(option => string.Equals(option.Id, seasonId, StringComparison.Ordinal)))
        {
            SeasonSearchText = string.Empty;
        }

        SelectedSeasonOption = SeasonOptions.FirstOrDefault(option =>
            string.Equals(option.Id, seasonId, StringComparison.Ordinal));
    }

    private static CampaignResourceMap RebindResourceMap(
        CampaignWorldDefinition definition,
        CampaignResourceMap source,
        bool preserveOccurrences)
    {
        var rebound = new CampaignResourceMap(definition, source.Catalog);
        if (preserveOccurrences)
        {
            rebound.Apply(source.GetMaterializedOccurrences().Select(static entry =>
                CampaignResourceMutation.Upsert(entry.X, entry.Y, entry.Occurrence)));
        }

        rebound.EnsureValid();
        return rebound;
    }

    private static CampaignSeasonMap RebindSeasonMap(
        CampaignWorldDefinition definition,
        CampaignSeasonMap source)
    {
        var rebound = new CampaignSeasonMap(
            definition,
            source.Catalog,
            source.DefaultSeasonId);
        var entries = source.GetAllTiles();
        rebound.Apply(entries.Select(static entry =>
            new CampaignSeasonMutation(entry.X, entry.Y, entry.Tile)));
        rebound.EnsureValid();
        return rebound;
    }

    private static bool HasSameCampaignLattice(
        CampaignWorldDefinition left,
        CampaignWorldDefinition right) =>
        left.WorldWidthMeters == right.WorldWidthMeters &&
        left.WorldHeightMeters == right.WorldHeightMeters &&
        left.CampaignTileSizeMeters == right.CampaignTileSizeMeters;

    private static string GetWorldRegenerationResourceStatus(
        CampaignResourceWorldRegenerationReport report) => report.Mode switch
        {
            CampaignResourceLatticeRemapMode.PreserveSameLattice =>
                $"{report.FinalOccurrenceCount:N0} resource occurrence(s) were preserved exactly",
            CampaignResourceLatticeRemapMode.RemapAllOccurrences =>
                $"resources were remapped by physical position to {report.FinalOccurrenceCount:N0} occurrence(s) " +
                $"({report.MovedSourceOccurrenceCount:N0} moved, {report.MergedOccurrenceCount:N0} merged, " +
                $"{report.DroppedOccurrenceCount:N0} dropped)",
            CampaignResourceLatticeRemapMode.RemapLocksAndRegenerateUnlocked =>
                $"{report.LockedRetainedOccurrenceCount:N0} locked resource target(s) were remapped and " +
                $"{report.RegeneratedUnlockedOccurrenceCount:N0} unlocked occurrence(s) were regenerated " +
                $"({report.MergedOccurrenceCount:N0} merged, {report.DroppedOccurrenceCount:N0} dropped)",
            _ => throw new ArgumentOutOfRangeException(nameof(report)),
        };

    private static string GetWorldRegenerationSeasonStatus(
        CampaignSeasonWorldRegenerationReport report) => report.Mode switch
        {
            CampaignSeasonLatticeRemapMode.PreserveSameLattice =>
                $"{report.FinalLockedTileCount:N0} locked Season tile(s) and every unlocked assignment were preserved exactly",
            CampaignSeasonLatticeRemapMode.RemapLocksAndRegenerateUnlocked =>
                $"{report.FinalLockedTileCount:N0} locked Season target(s) were remapped and unlocked tiles were regenerated " +
                $"({report.MovedLockedTileCount:N0} moved, {report.MergedLockedTileCount:N0} merged, " +
                $"{report.DisplacedLockedTileCount:N0} displaced, {report.LockedDrops.Count:N0} reviewed drops)",
            _ => throw new ArgumentOutOfRangeException(nameof(report)),
        };

    private string GetAutomaticCoastSuffix(
        CampaignTileCoordinate coordinate,
        CampaignTileType type)
    {
        if (World is null || type.IsWater())
        {
            return string.Empty;
        }

        var touchesSea = false;
        var touchesLake = false;
        foreach (var (offsetX, offsetY) in CardinalOffsets)
        {
            var neighborX = coordinate.X + offsetX;
            var neighborY = coordinate.Y + offsetY;
            if (!World.Tiles.IsValidCoordinate(neighborX, neighborY))
            {
                continue;
            }

            var neighborType = World.Tiles.GetTile(neighborX, neighborY).Type;
            touchesSea |= neighborType is CampaignTileType.Water or CampaignTileType.Sea;
            touchesLake |= neighborType == CampaignTileType.Lake;
        }

        return (touchesSea, touchesLake) switch
        {
            (true, true) => " · automatic Sea/Lake coast",
            (true, false) => " · automatic Sea coast",
            (false, true) => " · automatic Lake coast",
            _ => string.Empty,
        };
    }

    private void RefreshCustomTerrainTypes()
    {
        var previous = _selectedCampaignTileTypeOption;
        for (var index = CampaignTileTypeOptions.Count - 1; index >= 0; index--)
        {
            if (CampaignTileTypeOptions[index].CustomTerrainId is not null)
            {
                CampaignTileTypeOptions.RemoveAt(index);
            }
        }

        if (World is not null)
        {
            foreach (var definition in World.Tiles.CustomTerrainDefinitions)
            {
                CampaignTileTypeOptions.Add(CreateCustomCampaignType(definition));
            }
        }

        var replacement = previous.CustomTerrainId is { } customTerrainId
            ? CampaignTileTypeOptions.FirstOrDefault(option =>
                string.Equals(option.CustomTerrainId, customTerrainId, StringComparison.Ordinal))
            : CampaignTileTypeOptions.FirstOrDefault(option =>
                option.CustomTerrainId is null && option.Type == previous.Type);
        if (replacement is not null)
        {
            SelectedCampaignTileTypeOption = replacement;
        }
        else if (CampaignTileTypeOptions.Count > 1)
        {
            SelectedCampaignTileTypeOption = CampaignTileTypeOptions[1];
        }
    }

    private string GetCampaignTileTypeName(CampaignTileData tile)
    {
        if (World?.Tiles.TryGetCustomTerrainDefinition(tile.CustomTerrainId, out var definition) == true)
        {
            return definition.Name;
        }

        return GetCampaignTileTypeName(tile.Type);
    }

    private string GetCampaignTileTypeName(CampaignTileType type) =>
        type switch
        {
            CampaignTileType.Water => "Sea",
            CampaignTileType.RiverJunction => "River Junction",
            _ => CampaignTileTypeOptions.FirstOrDefault(option => option.Type == type)?.Name ?? type.ToString(),
        };

    private static string GetGenerationPresetName(CampaignMapGenerationPreset preset) => preset switch
    {
        CampaignMapGenerationPreset.EastCoast => "East coast",
        CampaignMapGenerationPreset.WestCoast => "West coast",
        CampaignMapGenerationPreset.NorthCoast => "North coast",
        CampaignMapGenerationPreset.SouthCoast => "South coast",
        CampaignMapGenerationPreset.InlandSea => "Sea in center",
        CampaignMapGenerationPreset.LandOnly => "Land only",
        _ => preset.ToString(),
    };

    private static string GetGenerationCoastStatus(CampaignMapGenerationResult result) =>
        result.Preset is CampaignMapGenerationPreset.EastCoast or
            CampaignMapGenerationPreset.WestCoast or
            CampaignMapGenerationPreset.NorthCoast or
            CampaignMapGenerationPreset.SouthCoast
            ? $"{GetCoastlineStyleName(result.CoastlineStyle)} · "
            : string.Empty;

    private static string GetCoastlineStyleName(CampaignMapCoastlineStyle style) => style switch
    {
        CampaignMapCoastlineStyle.Smooth => "Smooth shelf",
        CampaignMapCoastlineStyle.FlowingCapes => "Flowing bays and capes",
        CampaignMapCoastlineStyle.Natural => "Natural mixed coast",
        CampaignMapCoastlineStyle.Rugged => "Rugged coast",
        _ => style.ToString(),
    };

    private static string GetGeneratedRiverStatus(CampaignMapGenerationResult result)
    {
        var details = new List<string>(2);
        if (result.LargeRiverTileCount > 0)
        {
            details.Add($"{result.LargeRiverTileCount:N0} Large River");
        }

        if (result.RiverJunctionTileCount > 0)
        {
            details.Add($"{result.RiverJunctionTileCount:N0} confluences");
        }

        return details.Count == 0
            ? $"{result.RiverTileCount:N0} River tiles"
            : $"{result.RiverTileCount:N0} River tiles ({string.Join(", ", details)})";
    }

    private string GetDefaultSeasonName() =>
        SeasonMap is { } seasons
            ? seasons.Catalog.Get(seasons.DefaultSeasonId).Name
            : "default season";

    private static string GetSeasonRuleSummary(CampaignSeasonRule rule)
    {
        var parts = new List<string>();
        AddSeasonRange(parts, "latitude", rule.LatitudeDegrees, "°");
        AddSeasonRange(parts, "elevation", rule.ElevationMeters, " m");
        AddSeasonRange(parts, "temperature", rule.TemperatureCelsius, " °C");
        AddSeasonRange(parts, "moisture", rule.Moisture, string.Empty);
        AddSeasonRange(parts, "intensity", rule.SeasonalIntensity, string.Empty);
        AddSeasonRange(parts, "tendency", rule.SeasonalTendency, string.Empty);
        AddSeasonRange(parts, "Sea distance", rule.SeaDistanceKilometers, " km");
        AddSeasonRange(parts, "Lake distance", rule.LakeDistanceKilometers, " km");
        AddSeasonRange(parts, "River distance", rule.RiverDistanceKilometers, " km");
        if (rule.TerrainIncludes.Count > 0 || rule.CustomTerrainIncludes.Count > 0)
        {
            parts.Add("terrain include list");
        }

        if (rule.TerrainExcludes.Count > 0 || rule.CustomTerrainExcludes.Count > 0)
        {
            parts.Add("terrain exclude list");
        }

        return parts.Count == 0 ? "catch-all rule" : string.Join(" · ", parts);
    }

    private static int GetSeasonPriorityIndex(
        IReadOnlyList<string> priorityIds,
        string seasonId)
    {
        for (var index = 0; index < priorityIds.Count; index++)
        {
            if (string.Equals(priorityIds[index], seasonId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static string FormatSeasonDistance(double distanceKilometers) =>
        double.IsPositiveInfinity(distanceKilometers)
            ? "∞"
            : $"{distanceKilometers:0.##} km";

    private static void AddSeasonRange(
        ICollection<string> parts,
        string name,
        CampaignSeasonRange? range,
        string suffix)
    {
        if (range is { } value)
        {
            parts.Add($"{name} {value.Minimum:0.##}–{value.Maximum:0.##}{suffix}");
        }
    }

    private static CampaignMapGenerationOptions? CreateGenerationOptions(
        CampaignMapGenerationResult? result) =>
        result is null or { Preset: CampaignMapGenerationPreset.Blank }
            ? null
            : new CampaignMapGenerationOptions(
                result.Preset,
                result.Seed,
                result.TerrainStyle,
                result.Hydrology,
                result.MountainDensity,
                result.RequestedLandMix,
                result.TidalInlets,
                CustomTerrainDefinitions: null,
                CoastlineStyle: result.CoastlineStyle);

    private static CampaignTileTypeOption CreateCampaignType(
        CampaignTileType type,
        string name,
        string description,
        string color) =>
        new(type, name, description, new SolidColorBrush(Color.Parse(color)));

    private static CampaignTileTypeOption CreateCustomCampaignType(
        CampaignCustomTerrainDefinition definition) =>
        new(
            definition.BaseType,
            definition.Name,
            $"Custom {GetBaseTerrainName(definition.BaseType)} terrain · {definition.GenerationSharePercent}% inland mix",
            new SolidColorBrush(Color.Parse(definition.ColorHex)),
            definition.Id);

    private static string GetBaseTerrainName(CampaignTileType type) => type switch
    {
        CampaignTileType.Plains => "Plains",
        CampaignTileType.Steppe => "Steppe",
        CampaignTileType.Desert => "Desert",
        CampaignTileType.Forest => "Forest",
        CampaignTileType.Hills => "Hills",
        CampaignTileType.Mountain => "Mountain",
        _ => type.ToString(),
    };

    private static string FormatKilometers(long meters) =>
        $"{WorldUnits.MetersToKilometers(meters):#,0.###} km";

    private static string FormatKilometers(double meters) =>
        $"{meters / WorldUnits.MetersPerKilometer:#,0.###} km";

    private static CampaignWorldDefinition PreviewDefinition { get; } = CampaignWorldDefinition.Create(
        worldWidthMeters: 5_000,
        worldHeightMeters: 5_000,
        campaignTileSizeMeters: 5_000,
        seaLevelMeters: 0,
        minimumHeightMeters: -1_000,
        maximumHeightMeters: 6_000);

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoMenuLabel));
        OnPropertyChanged(nameof(RedoMenuLabel));
    }

    private void NotifyDocumentIdentityChanged()
    {
        OnPropertyChanged(nameof(WorldName));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ProjectPathText));
        OnPropertyChanged(nameof(DocumentStateText));
        OnPropertyChanged(nameof(IsLegacyImportPending));
        OnPropertyChanged(nameof(ImportSourceDirectory));
    }

    private void NotifyInspectorChanged()
    {
        OnPropertyChanged(nameof(HoverTileXText));
        OnPropertyChanged(nameof(HoverTileYText));
        OnPropertyChanged(nameof(HoverTileTypeText));
        OnPropertyChanged(nameof(HoverStoredHeightText));
        OnPropertyChanged(nameof(HoverDerivedHeightText));
        OnPropertyChanged(nameof(HoverWorldPositionText));
        OnPropertyChanged(nameof(SelectionText));
        NotifyResourceHoverChanged();
        NotifySeasonInspectorChanged();
        NotifyElevationHelperChanged();
        NotifyRiverSplitChanged();
    }

    private void NotifyResourceHoverChanged()
    {
        OnPropertyChanged(nameof(HoverSelectedResourcePotential));
        OnPropertyChanged(nameof(HoverSelectedResourceText));
    }

    private void NotifyResourceStampChanged()
    {
        OnPropertyChanged(nameof(ResourcePaintAreaText));
        OnPropertyChanged(nameof(ResourceStampSummary));
    }

    private void NotifyResourceStatusChanged()
    {
        OnPropertyChanged(nameof(ResourceOccurrenceCount));
        OnPropertyChanged(nameof(ResourceStatusText));
        NotifyResourceHoverChanged();
    }

    private void NotifySeasonStampChanged()
    {
        OnPropertyChanged(nameof(SeasonPaintAreaText));
        OnPropertyChanged(nameof(SeasonStampSummary));
    }

    private void NotifySeasonStatusChanged()
    {
        OnPropertyChanged(nameof(SeasonLockedTileCount));
        OnPropertyChanged(nameof(SeasonStatusText));
        NotifySeasonInspectorChanged();
    }

    private void NotifySeasonInspectorChanged()
    {
        OnPropertyChanged(nameof(HoverSeasonText));
        OnPropertyChanged(nameof(HasPinnedSeason));
        OnPropertyChanged(nameof(PinnedSeasonIdentityText));
        OnPropertyChanged(nameof(PinnedSeasonTerrainText));
        OnPropertyChanged(nameof(PinnedSeasonRuleText));
        OnPropertyChanged(nameof(PinnedSeasonGenerationText));
        OnPropertyChanged(nameof(CanLockPinnedSeason));
        OnPropertyChanged(nameof(CanUnlockPinnedSeason));
    }

    private void NotifyPinnedResourceActionChanged()
    {
        OnPropertyChanged(nameof(CanAdoptPinnedResource));
        OnPropertyChanged(nameof(CanLockPinnedResource));
        OnPropertyChanged(nameof(CanUnlockPinnedResource));
        OnPropertyChanged(nameof(CanErasePinnedResource));
        OnPropertyChanged(nameof(SelectedPinnedResourceWarningText));
        OnPropertyChanged(nameof(SelectedPinnedResourceUnevaluatedText));
    }

    private void NotifyWorkspacePresentationChanged()
    {
        OnPropertyChanged(nameof(CanvasTitle));
        OnPropertyChanged(nameof(CanvasHelpTitle));
        OnPropertyChanged(nameof(CanvasHelpText));
        OnPropertyChanged(nameof(HoverSurfaceLabel));
        OnPropertyChanged(nameof(FooterFormatText));
    }

    private void NotifyElevationHelperChanged()
    {
        OnPropertyChanged(nameof(CanUsePinnedElevationHelper));
        OnPropertyChanged(nameof(ElevationHelperText));
    }

    private void NotifyRiverSplitChanged()
    {
        OnPropertyChanged(nameof(HasPinnedRiverRoute));
        OnPropertyChanged(nameof(CanCreatePinnedRiverSplit));
        OnPropertyChanged(nameof(RiverSplitHelpText));
    }

    private bool TryGetPinnedRiver(
        out CampaignTileCoordinate coordinate,
        out CampaignTileData data)
    {
        if (World is { } world &&
            _selectedCoordinate is { } selected &&
            world.Tiles.IsValidCoordinate(selected.X, selected.Y))
        {
            coordinate = selected;
            data = world.Tiles.GetTile(selected.X, selected.Y);
            return true;
        }

        coordinate = default;
        data = default;
        return false;
    }

    private static int CountRiverConnections(RiverConnections connections)
    {
        var value = (byte)connections;
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

}
