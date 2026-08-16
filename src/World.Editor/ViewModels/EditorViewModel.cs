using System.Collections.ObjectModel;
using Avalonia.Media;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Generation;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Models;

namespace Kingdom.World.Editor.ViewModels;

public sealed class EditorViewModel : ViewModelBase
{
    private static readonly (int X, int Y)[] CardinalOffsets =
        [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private readonly CommandHistory _history = new();
    private CampaignWorld? _world;
    private CampaignResourceMap? _resourceMap;
    private CampaignResourceGenerationSettings? _resourceGenerationSettings;
    private CampaignResourceTerrainQueryV2? _resourceTerrainQuery;
    private CampaignTileTypeOption _selectedCampaignTileTypeOption;
    private CampaignResourceOption? _selectedResourceOption;
    private CampaignResourceOccurrenceRow? _selectedPinnedResourceOccurrence;
    private double _stampHeight;
    private int _paintAreaRadius;
    private int _resourcePotential = 50;
    private int _resourcePaintAreaRadius;
    private bool _lockManualResourceEdits = true;
    private bool _isResourceEraseTool;
    private bool _isResourcesWorkspace;
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

    public bool IsTerrainWorkspace => !IsResourcesWorkspace;

    public bool IsResourcesWorkspace => _isResourcesWorkspace;

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

    public bool CanAdjustCampaignView => HasWorld && !IsBusy;

    public string CanvasTitle => IsResourcesWorkspace
        ? "Campaign resource potential"
        : "Campaign tile surface";

    public string CanvasHelpTitle => IsResourcesWorkspace
        ? "Cell color = selected resource potential · number = exact value / 100"
        : "Cell = textured type · number = stored centre elevation (m)";

    public string CanvasHelpText => IsResourcesWorkspace
        ? "Potential numbers appear when tiles are large enough · terrain remains visible beneath the heatmap"
        : "Elevation numbers appear when tiles are large enough · middle-drag pans · wheel zooms";

    public string HoverSurfaceLabel => "Surface here";

    public string FooterFormatText => IsResourcesWorkspace
        ? "Resources · potential 1–100 · locks"
        : "Type + Int16 centre height · auto slopes";

    public CampaignWorld? World
    {
        get => _world;
        private set
        {
            if (SetProperty(ref _world, value))
            {
                _resourceTerrainQuery = value is null ? null : new CampaignResourceTerrainQueryV2(value);
                OnPropertyChanged(nameof(HasWorld));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(CanEditCampaignTiles));
                OnPropertyChanged(nameof(CanEditResources));
                OnPropertyChanged(nameof(CanAdjustCampaignView));
                OnPropertyChanged(nameof(CanRegenerateWorld));
                OnPropertyChanged(nameof(CanRegenerateResources));
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

    public bool HasWorld => World is not null;

    public bool HasResourceMap => ResourceMap is not null;

    public int ResourceOccurrenceCount => ResourceMap?.OccurrenceCount ?? 0;

    public string ResourceStatusText => ResourceMap is null
        ? "No resource layer"
        : ResourceOccurrenceCount == 0
            ? "No resource occurrences"
            : $"{ResourceOccurrenceCount:N0} resource occurrence(s)";

    public bool CanRegenerateWorld => HasWorld && !IsBusy;

    public bool CanRegenerateResources =>
        HasWorld && ResourceMap is not null && _resourceTerrainQuery is not null && !IsBusy;

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
                OnPropertyChanged(nameof(CanAdjustCampaignView));
                OnPropertyChanged(nameof(CanRegenerateWorld));
                OnPropertyChanged(nameof(CanRegenerateResources));
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

    public void SwitchToTerrainWorkspace() => SetResourcesWorkspace(resources: false);

    public void SwitchToResourcesWorkspace() => SetResourcesWorkspace(resources: true);

    public void SelectResourceAddUpdateTool() => SetResourceEraseTool(erase: false);

    public void SelectResourceEraseTool() => SetResourceEraseTool(erase: true);

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
        var resources = new CampaignResourceMap(world.Definition);
        World = world;
        ResourceMap = resources;
        ResourceGenerationSettings = null;
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
            : $"Created blank {WorldSummary}. Stamp a type and centre height into complete campaign tiles.";
        NotifyDocumentIdentityChanged();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
    }

    public void RegenerateWorld(
        CampaignWorld world,
        CampaignMapGenerationResult generationResult,
        CampaignResourceWorldRegenerationResult? resourceRegenerationResult = null)
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
        ValidateResourceDocument(World, currentResources, ResourceGenerationSettings);
        var sameLattice = HasSameCampaignLattice(World.Definition, world.Definition);
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
        var nextGenerationOptions = CreateGenerationOptions(generationResult);

        World = world;
        ResourceMap = reboundResources;
        ResourceGenerationSettings = nextResourceSettings;
        LastGenerationOptions = nextGenerationOptions;
        _history.Clear();
        _hover = null;
        _selectedCoordinate = null;
        OnPropertyChanged(nameof(PinnedCoordinate));
        RefreshCustomTerrainTypes();
        RefreshResourceOptions();
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
        StatusMessage =
            $"Regenerated {GetGenerationPresetName(generationResult.Preset)} from the reviewed preview · " +
            $"seed {generationResult.Seed:N0}. World definition and tiles were replaced; " +
            $"{resourceStatus}; " +
            "current project identity was kept and undo history was cleared.";
        NotifyDocumentIdentityChanged();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
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
        ArgumentNullException.ThrowIfNull(resourceMap);
        ValidateResourceDocument(world, resourceMap, resourceGenerationSettings);

        World = world;
        ResourceMap = resourceMap;
        ResourceGenerationSettings = resourceGenerationSettings;
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
        RefreshPinnedResourceOccurrences();
        StampHeight = world.Definition.DefaultTileHeightMeters;
        IsDirty = wasConvertedFromLegacy || normalizedLegacyCoastalTileCount > 0;
        StatusMessage = wasConvertedFromLegacy
            ? "Legacy terrain imported into tile-centre heights. Save to a new folder; the source remains unchanged."
            : normalizedLegacyCoastalTileCount > 0
                ? $"Opened {WorldName}; converted {normalizedLegacyCoastalTileCount:N0} legacy Coastal tile(s) to Plains. " +
                  "Automatic 10% water edges now preserve the underlying land. Save to update the project."
            : $"Opened {WorldName} with {world.Tiles.MaterializedTileCount:N0} stored tile overrides and " +
              $"{resourceMap.OccurrenceCount:N0} resource occurrence(s).";
        NotifyDocumentIdentityChanged();
        NotifyInspectorChanged();
        NotifyResourceStatusChanged();
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

    private void SetResourcesWorkspace(bool resources)
    {
        if (!SetProperty(ref _isResourcesWorkspace, resources, nameof(IsResourcesWorkspace)))
        {
            return;
        }

        OnPropertyChanged(nameof(IsTerrainWorkspace));
        OnPropertyChanged(nameof(CanEditResources));
        NotifyWorkspacePresentationChanged();
        StatusMessage = resources
            ? "Resources workspace active. Paint or inspect the selected resource without changing terrain."
            : "Terrain workspace active. Stamp complete terrain tiles and centre heights.";
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
