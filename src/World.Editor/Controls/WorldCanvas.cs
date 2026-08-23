using System.Buffers;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;
using Kingdom.World.Core.Commands;
using Kingdom.World.Core.Models;
using Kingdom.World.Editor.Models;
using Kingdom.World.Editor.ViewModels;

namespace Kingdom.World.Editor.Controls;

public sealed class WorldCanvas : Control
{
    public static readonly StyledProperty<CampaignWorld?> WorldProperty =
        AvaloniaProperty.Register<WorldCanvas, CampaignWorld?>(nameof(World));

    public static readonly StyledProperty<CampaignTileType> SelectedCampaignTileTypeProperty =
        AvaloniaProperty.Register<WorldCanvas, CampaignTileType>(
            nameof(SelectedCampaignTileType),
            CampaignTileType.Plains);

    public static readonly StyledProperty<string?> SelectedCustomTerrainIdProperty =
        AvaloniaProperty.Register<WorldCanvas, string?>(nameof(SelectedCustomTerrainId));

    public static readonly StyledProperty<double> StampHeightProperty =
        AvaloniaProperty.Register<WorldCanvas, double>(nameof(StampHeight));

    public static readonly StyledProperty<int> PaintAreaRadiusProperty =
        AvaloniaProperty.Register<WorldCanvas, int>(nameof(PaintAreaRadius));

    public static readonly StyledProperty<bool> ShowCampaignGridProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(ShowCampaignGrid), true);

    public static readonly StyledProperty<bool> ShowElevationNumbersProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(ShowElevationNumbers), true);

    public static readonly StyledProperty<bool> UseGrayscaleProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(UseGrayscale));

    public static readonly StyledProperty<bool> AllowTileEditingProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(AllowTileEditing), true);

    public static readonly StyledProperty<CampaignResourceMap?> ResourceMapProperty =
        AvaloniaProperty.Register<WorldCanvas, CampaignResourceMap?>(nameof(ResourceMap));

    public static readonly StyledProperty<bool> IsResourceWorkspaceProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(IsResourceWorkspace));

    public static readonly StyledProperty<string?> SelectedResourceIdProperty =
        AvaloniaProperty.Register<WorldCanvas, string?>(nameof(SelectedResourceId));

    public static readonly StyledProperty<int> ResourcePotentialProperty =
        AvaloniaProperty.Register<WorldCanvas, int>(nameof(ResourcePotential), 50);

    public static readonly StyledProperty<bool> LockManualResourceEditsProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(LockManualResourceEdits), true);

    public static readonly StyledProperty<bool> EraseSelectedResourceProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(EraseSelectedResource));

    public static readonly StyledProperty<int> ResourcePaintAreaRadiusProperty =
        AvaloniaProperty.Register<WorldCanvas, int>(nameof(ResourcePaintAreaRadius));

    public static readonly StyledProperty<CampaignSeasonMap?> SeasonMapProperty =
        AvaloniaProperty.Register<WorldCanvas, CampaignSeasonMap?>(nameof(SeasonMap));

    public static readonly StyledProperty<bool> IsSeasonWorkspaceProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(IsSeasonWorkspace));

    public static readonly StyledProperty<string?> SelectedSeasonIdProperty =
        AvaloniaProperty.Register<WorldCanvas, string?>(nameof(SelectedSeasonId));

    public static readonly StyledProperty<CampaignSeasonPaintTool> SeasonPaintToolProperty =
        AvaloniaProperty.Register<WorldCanvas, CampaignSeasonPaintTool>(nameof(SeasonPaintTool));

    public static readonly StyledProperty<bool> LockManualSeasonEditsProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(LockManualSeasonEdits), true);

    public static readonly StyledProperty<int> SeasonPaintAreaRadiusProperty =
        AvaloniaProperty.Register<WorldCanvas, int>(nameof(SeasonPaintAreaRadius));

    public static readonly StyledProperty<bool> ShowSeasonLabelsProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(ShowSeasonLabels), true);

    public static readonly StyledProperty<bool> BlendSeasonBoundariesProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(BlendSeasonBoundaries), true);

    public static readonly StyledProperty<bool> AllowAreaSelectionProperty =
        AvaloniaProperty.Register<WorldCanvas, bool>(nameof(AllowAreaSelection));

    public static readonly StyledProperty<CampaignTileArea?> SelectedAreaProperty =
        AvaloniaProperty.Register<WorldCanvas, CampaignTileArea?>(nameof(SelectedArea));

    private const double MinimumZoom = 0.000001;
    private const double MaximumZoom = 256;
    private const int MaximumRasterWidth = 1100;
    private const int MaximumRasterHeight = 800;
    private const int ParallelRasterPixelThreshold = 128 * 1024;
    private const double MinimumElevationLabelZoom = 28;
    private const double MinimumResourcePotentialLabelZoom = 28;
    private const double MinimumSeasonLabelZoom = 28;

    private static readonly IBrush CanvasBackground =
        new ImmutableSolidColorBrush(Color.Parse("#0D1317").ToUInt32());
    private static readonly IBrush ResourceTerrainMuteBrush =
        new ImmutableSolidColorBrush(Color.FromArgb(132, 17, 22, 24).ToUInt32());
    private static readonly IPen WorldBorderPen = new ImmutablePen(
        Color.FromArgb(210, 170, 190, 190).ToUInt32(),
        1.2);
    private static readonly IPen TileCursorPen = new ImmutablePen(
        Color.Parse("#72D4DC").ToUInt32(),
        2);
    private static readonly IPen KeyboardCursorPen = new ImmutablePen(
        Color.Parse("#FFF27A").ToUInt32(),
        2.4);
    private static readonly IPen BlockedTileCursorPen = new ImmutablePen(
        Color.Parse("#FF6B6B").ToUInt32(),
        2.2);
    private static readonly IPen SelectionPen = new ImmutablePen(
        Color.Parse("#E3B557").ToUInt32(),
        2.2);
    private static readonly IBrush AreaSelectionBrush =
        new ImmutableSolidColorBrush(Color.FromArgb(48, 227, 181, 87).ToUInt32());
    private static readonly IBrush ElevationLabelOutlineBrush =
        new ImmutableSolidColorBrush(Color.FromArgb(235, 0, 0, 0).ToUInt32());
    private static readonly IBrush ElevationLabelTextBrush =
        new ImmutableSolidColorBrush(Colors.White.ToUInt32());
    private static readonly Typeface ElevationLabelTypeface = new(
        "Tahoma",
        FontStyle.Normal,
        FontWeight.Bold,
        FontStretch.Normal);
    private static readonly IPen MinorGridPen = new ImmutablePen(
        Color.FromArgb(62, 225, 235, 232).ToUInt32(),
        0.7);
    private static readonly IPen MajorGridPen = new ImmutablePen(
        Color.FromArgb(120, 225, 235, 232).ToUInt32(),
        1.2);
    private static readonly (int X, int Y, RiverConnections Connection)[] RiverRenderNeighbors =
    [
        (0, -1, RiverConnections.North),
        (1, 0, RiverConnections.East),
        (0, 1, RiverConnections.South),
        (-1, 0, RiverConnections.West),
    ];
    private static readonly IReadOnlyDictionary<CampaignTileType, IBrush> PreviewBrushes =
        new Dictionary<CampaignTileType, IBrush>
        {
            [CampaignTileType.Unassigned] = new ImmutableSolidColorBrush(Color.FromArgb(92, 89, 102, 106).ToUInt32()),
            [CampaignTileType.Water] = new ImmutableSolidColorBrush(Color.FromArgb(92, 36, 125, 154).ToUInt32()),
            [CampaignTileType.Plains] = new ImmutableSolidColorBrush(Color.FromArgb(92, 115, 148, 93).ToUInt32()),
            [CampaignTileType.Steppe] = new ImmutableSolidColorBrush(Color.FromArgb(92, 164, 154, 88).ToUInt32()),
            [CampaignTileType.Desert] = new ImmutableSolidColorBrush(Color.FromArgb(92, 201, 145, 66).ToUInt32()),
            [CampaignTileType.Forest] = new ImmutableSolidColorBrush(Color.FromArgb(92, 47, 104, 79).ToUInt32()),
            [CampaignTileType.Hills] = new ImmutableSolidColorBrush(Color.FromArgb(92, 139, 138, 98).ToUInt32()),
            [CampaignTileType.Mountain] = new ImmutableSolidColorBrush(Color.FromArgb(92, 133, 135, 132).ToUInt32()),
            [CampaignTileType.Sea] = new ImmutableSolidColorBrush(Color.FromArgb(92, 30, 106, 139).ToUInt32()),
            [CampaignTileType.Lake] = new ImmutableSolidColorBrush(Color.FromArgb(92, 45, 142, 163).ToUInt32()),
            [CampaignTileType.River] = new ImmutableSolidColorBrush(Color.FromArgb(72, 88, 132, 78).ToUInt32()),
            [CampaignTileType.LargeRiver] = new ImmutableSolidColorBrush(Color.FromArgb(72, 78, 127, 79).ToUInt32()),
            [CampaignTileType.Beach] = new ImmutableSolidColorBrush(Color.FromArgb(92, 195, 168, 109).ToUInt32()),
            [CampaignTileType.Cliff] = new ImmutableSolidColorBrush(Color.FromArgb(92, 111, 102, 94).ToUInt32()),
        };

    private WriteableBitmap? _surfaceBitmap;
    private RasterKey? _rasterKey;
    private WriteableBitmap? _resourceBitmap;
    private ResourceRasterKey? _resourceRasterKey;
    private ResourceRasterSnapshot? _resourceRasterSnapshot;
    private WriteableBitmap? _seasonBitmap;
    private SeasonRasterKey? _seasonRasterKey;
    private SeasonRasterSnapshot? _seasonRasterSnapshot;
    private double _zoom = 1;
    private double _originX;
    private double _originY;
    private bool _fitRequested = true;
    private bool _isPanning;
    private bool _isSelectingArea;
    private Point _lastPointerPosition;
    private CampaignTilePointerInfo? _hover;
    private CampaignTileCoordinate? _selectedCoordinate;
    private CampaignTileCoordinate? _keyboardCoordinate;
    private CampaignTileCoordinate? _areaSelectionStart;
    private CampaignTileArea? _areaSelectionBeforeDrag;
    private CampaignTileCoordinate? _lastStampCoordinate;
    private CampaignTileStampBuilder? _stroke;
    private CampaignResourceStrokeBuilder? _resourceStroke;
    private CampaignSeasonStrokeBuilder? _seasonStroke;
    private string? _strokeResourceId;
    private byte _strokeResourcePotential;
    private bool _strokeLocksResource;
    private bool _strokeErasesResource;
    private int _strokeResourcePaintAreaRadius;
    private string? _strokeSeasonId;
    private CampaignSeasonPaintTool _strokeSeasonTool;
    private bool _strokeLocksSeason;
    private int _strokeSeasonPaintAreaRadius;
    private readonly HashSet<CampaignTileCoordinate> _blockedRiverCoordinates = [];
    private readonly Dictionary<ElevationLabelKey, ElevationLabelText> _elevationLabelCache = [];
    private readonly Dictionary<ResourcePotentialLabelKey, ElevationLabelText>
        _resourcePotentialLabelCache = [];
    private readonly Dictionary<SeasonLabelKey, ElevationLabelText> _seasonLabelCache = [];
    private RiverRenderStyleCache? _riverRenderStyleCache;
    private RiverRenderStyleCache? _largeRiverRenderStyleCache;
    private RiverRenderStyleCache? _previewRiverRenderStyleCache;
    private RiverRenderStyleCache? _previewLargeRiverRenderStyleCache;
    private ResourcePreviewBrushCache? _resourcePreviewBrushCache;

    static WorldCanvas()
    {
        AffectsRender<WorldCanvas>(
            WorldProperty,
            SelectedCampaignTileTypeProperty,
            SelectedCustomTerrainIdProperty,
            StampHeightProperty,
            PaintAreaRadiusProperty,
            ShowCampaignGridProperty,
            ShowElevationNumbersProperty,
            UseGrayscaleProperty,
            AllowTileEditingProperty,
            ResourceMapProperty,
            IsResourceWorkspaceProperty,
            SelectedResourceIdProperty,
            ResourcePotentialProperty,
            LockManualResourceEditsProperty,
            EraseSelectedResourceProperty,
            ResourcePaintAreaRadiusProperty,
            SeasonMapProperty,
            IsSeasonWorkspaceProperty,
            SelectedSeasonIdProperty,
            SeasonPaintToolProperty,
            LockManualSeasonEditsProperty,
            SeasonPaintAreaRadiusProperty,
            ShowSeasonLabelsProperty,
            BlendSeasonBoundariesProperty,
            AllowAreaSelectionProperty,
            SelectedAreaProperty);
    }

    public WorldCanvas()
    {
        Focusable = true;
        IsTabStop = true;
        ClipToBounds = true;
        AutomationProperties.SetName(this, "Campaign tile editing canvas");
        AutomationProperties.SetHelpText(
            this,
            "Arrow keys move the tile cursor. Enter applies the active tool. Space pins the current tile for inspection.");
        GotFocus += (_, _) =>
        {
            if (World is { } world)
            {
                EnsureKeyboardCoordinate(world, raiseHover: true);
            }

            InvalidateVisual();
        };
        LostFocus += (_, _) => InvalidateVisual();
    }

    public event EventHandler<CampaignTilePointerEventArgs>? TileHovered;

    public event EventHandler<CampaignTilePointerEventArgs>? TileSelected;

    public event EventHandler<CampaignTileAreaSelectedEventArgs>? TileAreaSelected;

    public event EventHandler<CampaignTileStrokeEventArgs>? StrokeCompleted;

    public event EventHandler<CampaignResourceStrokeEventArgs>? ResourceStrokeCompleted;

    public event EventHandler<CampaignSeasonStrokeEventArgs>? SeasonStrokeCompleted;

    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    public event EventHandler<WorldCanvasViewportChangedEventArgs>? ViewportChanged;

    public bool HasActiveStroke =>
        _stroke is not null || _resourceStroke is not null || _seasonStroke is not null;

    internal CampaignTileCoordinate? KeyboardCoordinate => _keyboardCoordinate;

    public CampaignWorld? World
    {
        get => GetValue(WorldProperty);
        set => SetValue(WorldProperty, value);
    }

    public CampaignTileType SelectedCampaignTileType
    {
        get => GetValue(SelectedCampaignTileTypeProperty);
        set => SetValue(SelectedCampaignTileTypeProperty, value);
    }

    public string? SelectedCustomTerrainId
    {
        get => GetValue(SelectedCustomTerrainIdProperty);
        set => SetValue(SelectedCustomTerrainIdProperty, value);
    }

    public double StampHeight
    {
        get => GetValue(StampHeightProperty);
        set => SetValue(StampHeightProperty, value);
    }

    public int PaintAreaRadius
    {
        get => GetValue(PaintAreaRadiusProperty);
        set => SetValue(PaintAreaRadiusProperty, value);
    }

    public bool ShowCampaignGrid
    {
        get => GetValue(ShowCampaignGridProperty);
        set => SetValue(ShowCampaignGridProperty, value);
    }

    public bool ShowElevationNumbers
    {
        get => GetValue(ShowElevationNumbersProperty);
        set => SetValue(ShowElevationNumbersProperty, value);
    }

    public bool UseGrayscale
    {
        get => GetValue(UseGrayscaleProperty);
        set => SetValue(UseGrayscaleProperty, value);
    }

    public bool AllowTileEditing
    {
        get => GetValue(AllowTileEditingProperty);
        set => SetValue(AllowTileEditingProperty, value);
    }

    public CampaignResourceMap? ResourceMap
    {
        get => GetValue(ResourceMapProperty);
        set => SetValue(ResourceMapProperty, value);
    }

    public bool IsResourceWorkspace
    {
        get => GetValue(IsResourceWorkspaceProperty);
        set => SetValue(IsResourceWorkspaceProperty, value);
    }

    public string? SelectedResourceId
    {
        get => GetValue(SelectedResourceIdProperty);
        set => SetValue(SelectedResourceIdProperty, value);
    }

    public int ResourcePotential
    {
        get => GetValue(ResourcePotentialProperty);
        set => SetValue(ResourcePotentialProperty, value);
    }

    public bool LockManualResourceEdits
    {
        get => GetValue(LockManualResourceEditsProperty);
        set => SetValue(LockManualResourceEditsProperty, value);
    }

    public bool EraseSelectedResource
    {
        get => GetValue(EraseSelectedResourceProperty);
        set => SetValue(EraseSelectedResourceProperty, value);
    }

    public int ResourcePaintAreaRadius
    {
        get => GetValue(ResourcePaintAreaRadiusProperty);
        set => SetValue(ResourcePaintAreaRadiusProperty, value);
    }

    public CampaignSeasonMap? SeasonMap
    {
        get => GetValue(SeasonMapProperty);
        set => SetValue(SeasonMapProperty, value);
    }

    public bool IsSeasonWorkspace
    {
        get => GetValue(IsSeasonWorkspaceProperty);
        set => SetValue(IsSeasonWorkspaceProperty, value);
    }

    public string? SelectedSeasonId
    {
        get => GetValue(SelectedSeasonIdProperty);
        set => SetValue(SelectedSeasonIdProperty, value);
    }

    public CampaignSeasonPaintTool SeasonPaintTool
    {
        get => GetValue(SeasonPaintToolProperty);
        set => SetValue(SeasonPaintToolProperty, value);
    }

    public bool LockManualSeasonEdits
    {
        get => GetValue(LockManualSeasonEditsProperty);
        set => SetValue(LockManualSeasonEditsProperty, value);
    }

    public int SeasonPaintAreaRadius
    {
        get => GetValue(SeasonPaintAreaRadiusProperty);
        set => SetValue(SeasonPaintAreaRadiusProperty, value);
    }

    public bool ShowSeasonLabels
    {
        get => GetValue(ShowSeasonLabelsProperty);
        set => SetValue(ShowSeasonLabelsProperty, value);
    }

    public bool BlendSeasonBoundaries
    {
        get => GetValue(BlendSeasonBoundariesProperty);
        set => SetValue(BlendSeasonBoundariesProperty, value);
    }

    public bool AllowAreaSelection
    {
        get => GetValue(AllowAreaSelectionProperty);
        set => SetValue(AllowAreaSelectionProperty, value);
    }

    public CampaignTileArea? SelectedArea
    {
        get => GetValue(SelectedAreaProperty);
        set => SetValue(SelectedAreaProperty, value);
    }

    public void ZoomToFit()
    {
        _fitRequested = true;
        ApplyFitIfPossible();
        InvalidateVisual();
    }

    public WorldCanvasViewport CaptureViewport() =>
        new(_zoom, _originX, _originY);

    public void ApplyViewport(WorldCanvasViewport viewport, bool raiseEvent = false)
    {
        ValidateViewport(viewport);

        _zoom = Math.Clamp(viewport.Zoom, MinimumZoom, MaximumZoom);
        _originX = viewport.OriginX;
        _originY = viewport.OriginY;
        _fitRequested = false;
        MarkSurfaceBitmapDirty();
        MarkResourceBitmapDirty();
        MarkSeasonBitmapDirty();
        if (raiseEvent)
        {
            RaiseViewportChanged();
        }

        InvalidateVisual();
    }

    public void NotifyWorldChanged()
    {
        MarkSurfaceBitmapDirty();
        MarkResourceBitmapDirty();
        MarkSeasonBitmapDirty();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(CanvasBackground, new Rect(Bounds.Size));
        var world = World;
        if (world is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        ApplyFitIfPossible();
        if (!_isPanning || _surfaceBitmap is null || _rasterKey is null)
        {
            EnsureSurfaceBitmap(world);
        }

        if (_surfaceBitmap is not null)
        {
            var destination = new Rect(Bounds.Size);
            if (_isPanning && _rasterKey is { } cached)
            {
                var zoomRatio = _zoom / cached.Zoom;
                destination = new Rect(
                    (cached.OriginX - _originX) * _zoom,
                    (cached.OriginY - _originY) * _zoom,
                    cached.ViewportWidth * zoomRatio,
                    cached.ViewportHeight * zoomRatio);
            }

            context.DrawImage(
                _surfaceBitmap,
                new Rect(_surfaceBitmap.Size),
                destination);
        }

        DrawRivers(context, world);

        if (IsResourceWorkspace)
        {
            DrawResourceTerrainMute(context, world);
            DrawResourceHeatmap(context, world);
        }
        else if (IsSeasonWorkspace)
        {
            DrawSeasonOverlay(context, world);
        }

        DrawWorldBorder(context, world);
        if (ShowCampaignGrid)
        {
            DrawCampaignGrid(context, world);
        }

        if (IsResourceWorkspace)
        {
            DrawResourcePotentialNumbers(context, world);
        }
        else if (IsSeasonWorkspace && ShowSeasonLabels)
        {
            DrawSeasonLabels(context, world);
        }
        else if (ShowElevationNumbers)
        {
            DrawElevationNumbers(context, world);
        }

        if (AllowTileEditing)
        {
            DrawStampCursor(context);
        }

        DrawKeyboardCursor(context);
        DrawSelection(context);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var previous = Bounds.Size;
        var arranged = base.ArrangeOverride(finalSize);
        if (previous != finalSize)
        {
            MarkSurfaceBitmapDirty();
            MarkResourceBitmapDirty();
            MarkSeasonBitmapDirty();
            _fitRequested = true;
        }

        return arranged;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WorldProperty)
        {
            _fitRequested = true;
            _hover = null;
            _selectedCoordinate = null;
            _keyboardCoordinate = null;
            SelectedArea = null;
            _isSelectingArea = false;
            _areaSelectionStart = null;
            _areaSelectionBeforeDrag = null;
            _blockedRiverCoordinates.Clear();
            MarkSurfaceBitmapDirty();
            MarkResourceBitmapDirty();
            MarkSeasonBitmapDirty();
        }
        else if (change.Property == UseGrayscaleProperty)
        {
            MarkSurfaceBitmapDirty();
        }
        else if (change.Property == ResourceMapProperty ||
                 change.Property == SelectedResourceIdProperty)
        {
            MarkResourceBitmapDirty();
        }
        else if (change.Property == SeasonMapProperty ||
                 change.Property == BlendSeasonBoundariesProperty)
        {
            MarkSeasonBitmapDirty();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var current = e.GetCurrentPoint(this);
        _lastPointerPosition = current.Position;

        if (current.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        var pointer = ToPointerInfo(current.Position);
        if (current.Properties.IsRightButtonPressed && pointer is { } selection)
        {
            SelectCoordinate(selection);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (current.Properties.IsLeftButtonPressed &&
            AllowAreaSelection &&
            World is { } selectionWorld &&
            pointer is { } areaPointer)
        {
            _isSelectingArea = true;
            _areaSelectionStart = areaPointer.Coordinate;
            _areaSelectionBeforeDrag = SelectedArea;
            SelectedArea = CreateAreaSelection(
                selectionWorld.Definition,
                areaPointer.Coordinate,
                areaPointer.Coordinate);
            e.Pointer.Capture(this);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!current.Properties.IsLeftButtonPressed || !AllowTileEditing || World is null || pointer is null)
        {
            return;
        }

        _lastStampCoordinate = null;
        _blockedRiverCoordinates.Clear();
        _keyboardCoordinate = pointer.Value.Coordinate;
        if (IsSeasonWorkspace)
        {
            if (!BeginSeasonStroke(World, out _))
            {
                return;
            }

            ApplySeasonAt(pointer.Value.Coordinate);
        }
        else if (IsResourceWorkspace)
        {
            if (!BeginResourceStroke(World, out _, out _))
            {
                return;
            }

            ApplyResourceAt(pointer.Value.Coordinate);
        }
        else
        {
            BeginTerrainStroke(World);
            ApplyTileAt(pointer.Value.Coordinate);
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var current = e.GetCurrentPoint(this);
        var position = current.Position;
        if (_isPanning)
        {
            var delta = position - _lastPointerPosition;
            _originX -= delta.X / _zoom;
            _originY -= delta.Y / _zoom;
            _lastPointerPosition = position;
            RaiseViewportChanged();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var pointer = ToPointerInfo(position);
        if (_hover != pointer)
        {
            _hover = pointer;
            if (pointer is { } currentPointer)
            {
                _keyboardCoordinate = currentPointer.Coordinate;
            }

            TileHovered?.Invoke(this, new CampaignTilePointerEventArgs(pointer));
        }

        if (_isSelectingArea &&
            current.Properties.IsLeftButtonPressed &&
            World is { } selectionWorld &&
            _areaSelectionStart is { } start &&
            pointer is { } areaPointer)
        {
            SelectedArea = CreateAreaSelection(
                selectionWorld.Definition,
                start,
                areaPointer.Coordinate);
            _lastPointerPosition = position;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if ((_stroke is not null || _resourceStroke is not null || _seasonStroke is not null) &&
            current.Properties.IsLeftButtonPressed &&
            pointer is { } stamp)
        {
            if (_seasonStroke is not null)
            {
                ApplySeasonAt(stamp.Coordinate);
            }
            else if (_resourceStroke is not null)
            {
                ApplyResourceAt(stamp.Coordinate);
            }
            else
            {
                ApplyTileAt(stamp.Coordinate);
            }

            e.Handled = true;
        }

        _lastPointerPosition = position;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            MarkSurfaceBitmapDirty();
            MarkResourceBitmapDirty();
            MarkSeasonBitmapDirty();
            RaiseViewportChanged();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_isSelectingArea)
        {
            _isSelectingArea = false;
            _areaSelectionStart = null;
            _areaSelectionBeforeDrag = null;
            e.Pointer.Capture(null);
            if (SelectedArea is { } selectedArea)
            {
                TileAreaSelected?.Invoke(this, new CampaignTileAreaSelectedEventArgs(selectedArea));
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if ((_stroke is null && _resourceStroke is null && _seasonStroke is null) || World is null)
        {
            return;
        }

        if (_seasonStroke is not null)
        {
            var command = _seasonStroke.Complete(BuildSeasonStrokeDescription());
            _seasonStroke = null;
            ResetSeasonStrokeSettings();
            SeasonStrokeCompleted?.Invoke(this, new CampaignSeasonStrokeEventArgs(command));
        }
        else if (_resourceStroke is not null)
        {
            var command = _resourceStroke.Complete(BuildResourceStrokeDescription());
            _resourceStroke = null;
            ResetResourceStrokeSettings();
            ResourceStrokeCompleted?.Invoke(this, new CampaignResourceStrokeEventArgs(command));
        }
        else if (_stroke is not null)
        {
            var height = GetStampHeight(World);
            var command = _stroke.Complete(BuildStrokeDescription(height));
            var blockedRiverTileCount = _blockedRiverCoordinates.Count;
            _stroke = null;
            StrokeCompleted?.Invoke(this, new CampaignTileStrokeEventArgs(command, blockedRiverTileCount));
        }

        _lastStampCoordinate = null;
        _blockedRiverCoordinates.Clear();
        e.Pointer.Capture(null);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (World is null || e.Delta.Y == 0)
        {
            return;
        }

        var position = e.GetPosition(this);
        var tileX = _originX + position.X / _zoom;
        var tileY = _originY + position.Y / _zoom;
        var factor = Math.Pow(1.18, e.Delta.Y);
        var nextZoom = Math.Clamp(_zoom * factor, MinimumZoom, MaximumZoom);
        if (Math.Abs(nextZoom - _zoom) < double.Epsilon)
        {
            return;
        }

        _zoom = nextZoom;
        _originX = tileX - position.X / _zoom;
        _originY = tileY - position.Y / _zoom;
        MarkSurfaceBitmapDirty();
        MarkResourceBitmapDirty();
        MarkSeasonBitmapDirty();
        RaiseZoomChanged();
        RaiseViewportChanged();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_stroke is null && _resourceStroke is null && _seasonStroke is null)
        {
            _hover = null;
            TileHovered?.Invoke(this, new CampaignTilePointerEventArgs(null));
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape && (HasActiveStroke || _isSelectingArea))
        {
            CancelActiveInteraction();
            e.Handled = true;
            return;
        }

        if (World is not { } world)
        {
            return;
        }

        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var handled = e.Key switch
        {
            Key.Left => MoveKeyboardCursor(world, -1, 0),
            Key.Right => MoveKeyboardCursor(world, 1, 0),
            Key.Up => MoveKeyboardCursor(world, 0, -1),
            Key.Down => MoveKeyboardCursor(world, 0, 1),
            Key.Enter => PaintAtKeyboardCursor(world),
            Key.Space => PinKeyboardCursor(world),
            _ => false,
        };
        if (handled)
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        CancelActiveInteraction();
    }

    public bool CancelActiveInteraction()
    {
        var cancelledStroke = HasActiveStroke;
        var cancelledAreaSelection = _isSelectingArea;
        _stroke?.Cancel();
        _resourceStroke?.Cancel();
        _seasonStroke?.Cancel();
        _stroke = null;
        _resourceStroke = null;
        _seasonStroke = null;
        ResetResourceStrokeSettings();
        ResetSeasonStrokeSettings();
        _lastStampCoordinate = null;
        _blockedRiverCoordinates.Clear();
        if (cancelledAreaSelection)
        {
            SelectedArea = _areaSelectionBeforeDrag;
        }

        _isSelectingArea = false;
        _areaSelectionStart = null;
        _areaSelectionBeforeDrag = null;

        var cancelledPan = _isPanning;
        _isPanning = false;
        if (!cancelledStroke && !cancelledPan && !cancelledAreaSelection)
        {
            return false;
        }

        MarkSurfaceBitmapDirty();
        MarkResourceBitmapDirty();
        MarkSeasonBitmapDirty();
        InvalidateVisual();
        return true;
    }

    private void BeginTerrainStroke(CampaignWorld world)
    {
        _stroke = new CampaignTileStampBuilder(world.Tiles);
    }

    private bool BeginResourceStroke(
        CampaignWorld world,
        out CampaignResourceMap resources,
        out CampaignResourceDefinition definition)
    {
        if (!TryGetActiveResource(world, out resources, out definition))
        {
            return false;
        }

        _resourceStroke = new CampaignResourceStrokeBuilder(resources);
        _strokeResourceId = definition.Id;
        _strokeResourcePotential = (byte)Math.Clamp(
            ResourcePotential,
            CampaignResourceOccurrence.MinimumPotential,
            CampaignResourceOccurrence.MaximumPotential);
        _strokeLocksResource = LockManualResourceEdits;
        _strokeErasesResource = EraseSelectedResource;
        _strokeResourcePaintAreaRadius = EffectiveResourcePaintAreaRadius;
        return true;
    }

    private bool BeginSeasonStroke(CampaignWorld world, out CampaignSeasonMap seasons)
    {
        if (!TryGetActiveSeason(world, out seasons))
        {
            return false;
        }

        _seasonStroke = new CampaignSeasonStrokeBuilder(seasons);
        _strokeSeasonId = SelectedSeasonId;
        _strokeSeasonTool = SeasonPaintTool;
        _strokeLocksSeason = LockManualSeasonEdits;
        _strokeSeasonPaintAreaRadius = EffectiveSeasonPaintAreaRadius;
        return true;
    }

    private bool MoveKeyboardCursor(CampaignWorld world, int deltaX, int deltaY)
    {
        var current = EnsureKeyboardCoordinate(world, raiseHover: false);
        var next = new CampaignTileCoordinate(
            Math.Clamp(current.X + deltaX, 0, world.Definition.TilesX - 1),
            Math.Clamp(current.Y + deltaY, 0, world.Definition.TilesY - 1));
        SetKeyboardCoordinate(world, next, raiseHover: true);
        EnsureKeyboardCoordinateVisible(world, next);
        InvalidateVisual();
        return true;
    }

    private bool PaintAtKeyboardCursor(CampaignWorld world)
    {
        if (!AllowTileEditing)
        {
            return false;
        }

        var coordinate = EnsureKeyboardCoordinate(world, raiseHover: true);
        _lastStampCoordinate = null;
        _blockedRiverCoordinates.Clear();
        if (IsSeasonWorkspace)
        {
            if (!BeginSeasonStroke(world, out _))
            {
                return false;
            }

            ApplySeasonAt(coordinate);
            var command = _seasonStroke!.Complete(BuildSeasonStrokeDescription());
            _seasonStroke = null;
            ResetSeasonStrokeSettings();
            SeasonStrokeCompleted?.Invoke(this, new CampaignSeasonStrokeEventArgs(command));
        }
        else if (IsResourceWorkspace)
        {
            if (!BeginResourceStroke(world, out _, out _))
            {
                return false;
            }

            ApplyResourceAt(coordinate);
            var command = _resourceStroke!.Complete(BuildResourceStrokeDescription());
            _resourceStroke = null;
            ResetResourceStrokeSettings();
            ResourceStrokeCompleted?.Invoke(this, new CampaignResourceStrokeEventArgs(command));
        }
        else
        {
            BeginTerrainStroke(world);
            ApplyTileAt(coordinate);
            var height = GetStampHeight(world);
            var command = _stroke!.Complete(BuildStrokeDescription(height));
            var blockedRiverTileCount = _blockedRiverCoordinates.Count;
            _stroke = null;
            StrokeCompleted?.Invoke(this, new CampaignTileStrokeEventArgs(command, blockedRiverTileCount));
        }

        _lastStampCoordinate = null;
        _blockedRiverCoordinates.Clear();
        InvalidateVisual();
        return true;
    }

    private bool PinKeyboardCursor(CampaignWorld world)
    {
        var coordinate = EnsureKeyboardCoordinate(world, raiseHover: true);
        SelectCoordinate(CreatePointerInfo(coordinate));
        InvalidateVisual();
        return true;
    }

    private CampaignTileCoordinate EnsureKeyboardCoordinate(CampaignWorld world, bool raiseHover)
    {
        if (_keyboardCoordinate is { } existing &&
            world.Tiles.IsValidCoordinate(existing.X, existing.Y))
        {
            if (raiseHover)
            {
                SetKeyboardCoordinate(world, existing, raiseHover: true);
            }

            return existing;
        }

        var initial = _selectedCoordinate is { } selected &&
                      world.Tiles.IsValidCoordinate(selected.X, selected.Y)
            ? selected
            : _hover is { } hover &&
              world.Tiles.IsValidCoordinate(hover.Coordinate.X, hover.Coordinate.Y)
                ? hover.Coordinate
                : new CampaignTileCoordinate(
                    world.Definition.TilesX / 2,
                    world.Definition.TilesY / 2);
        SetKeyboardCoordinate(world, initial, raiseHover);
        return initial;
    }

    private void SetKeyboardCoordinate(
        CampaignWorld world,
        CampaignTileCoordinate coordinate,
        bool raiseHover)
    {
        if (!world.Tiles.IsValidCoordinate(coordinate.X, coordinate.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate));
        }

        _keyboardCoordinate = coordinate;
        if (!raiseHover)
        {
            return;
        }

        var info = CreatePointerInfo(coordinate);
        _hover = info;
        TileHovered?.Invoke(this, new CampaignTilePointerEventArgs(info));
    }

    private void EnsureKeyboardCoordinateVisible(
        CampaignWorld world,
        CampaignTileCoordinate coordinate)
    {
        ApplyFitIfPossible();
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var visibleWidth = Bounds.Width / _zoom;
        var visibleHeight = Bounds.Height / _zoom;
        var nextOriginX = _originX;
        var nextOriginY = _originY;
        if (coordinate.X < nextOriginX)
        {
            nextOriginX = coordinate.X;
        }
        else if (coordinate.X + 1 > nextOriginX + visibleWidth)
        {
            nextOriginX = coordinate.X + 1 - visibleWidth;
        }

        if (coordinate.Y < nextOriginY)
        {
            nextOriginY = coordinate.Y;
        }
        else if (coordinate.Y + 1 > nextOriginY + visibleHeight)
        {
            nextOriginY = coordinate.Y + 1 - visibleHeight;
        }

        if (Math.Abs(nextOriginX - _originX) < double.Epsilon &&
            Math.Abs(nextOriginY - _originY) < double.Epsilon)
        {
            return;
        }

        _originX = nextOriginX;
        _originY = nextOriginY;
        MarkSurfaceBitmapDirty();
        MarkResourceBitmapDirty();
        MarkSeasonBitmapDirty();
        RaiseViewportChanged();
    }

    private static CampaignTilePointerInfo CreatePointerInfo(CampaignTileCoordinate coordinate) =>
        new(coordinate, coordinate.X + 0.5, coordinate.Y + 0.5);

    private void SelectCoordinate(CampaignTilePointerInfo selection)
    {
        _selectedCoordinate = selection.Coordinate;
        _keyboardCoordinate = selection.Coordinate;
        TileSelected?.Invoke(this, new CampaignTilePointerEventArgs(selection));
    }

    private void ApplyTileAt(CampaignTileCoordinate coordinate)
    {
        if (_stroke is null || World is null)
        {
            return;
        }

        var data = new CampaignTileData(
            SelectedCampaignTileType,
            GetStampHeight(World),
            SelectedCustomTerrainId);
        if (_lastStampCoordinate is not { } previous)
        {
            ApplyPaintArea(coordinate, data);
        }
        else if (SelectedCampaignTileType.IsRiver())
        {
            ApplyFourConnectedRiverPath(previous, coordinate, data);
        }
        else
        {
            var dx = coordinate.X - previous.X;
            var dy = coordinate.Y - previous.Y;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps == 0)
            {
                ApplyPaintArea(coordinate, data);
            }
            else
            {
                for (var step = 1; step <= steps; step++)
                {
                    var x = previous.X + (int)Math.Round(
                        (double)dx * step / steps,
                        MidpointRounding.AwayFromZero);
                    var y = previous.Y + (int)Math.Round(
                        (double)dy * step / steps,
                        MidpointRounding.AwayFromZero);
                    ApplyPaintArea(new CampaignTileCoordinate(x, y), data);
                }
            }
        }

        _lastStampCoordinate = coordinate;
        _keyboardCoordinate = coordinate;
        InvalidateVisual();
    }

    private void ApplyResourceAt(CampaignTileCoordinate coordinate)
    {
        if (_resourceStroke is null || World is null || _strokeResourceId is null)
        {
            return;
        }

        if (_lastStampCoordinate is not { } previous)
        {
            ApplyResourcePaintArea(coordinate);
        }
        else
        {
            var dx = coordinate.X - previous.X;
            var dy = coordinate.Y - previous.Y;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps == 0)
            {
                ApplyResourcePaintArea(coordinate);
            }
            else
            {
                for (var step = 1; step <= steps; step++)
                {
                    var x = previous.X + (int)Math.Round(
                        (double)dx * step / steps,
                        MidpointRounding.AwayFromZero);
                    var y = previous.Y + (int)Math.Round(
                        (double)dy * step / steps,
                        MidpointRounding.AwayFromZero);
                    ApplyResourcePaintArea(new CampaignTileCoordinate(x, y));
                }
            }
        }

        _lastStampCoordinate = coordinate;
        _keyboardCoordinate = coordinate;
        InvalidateVisual();
    }

    private void ApplyResourcePaintArea(CampaignTileCoordinate center)
    {
        if (_resourceStroke is null || World is null || _strokeResourceId is null)
        {
            return;
        }

        var area = CampaignTileArea.Centered(
            World.Definition,
            center,
            _strokeResourcePaintAreaRadius);
        foreach (var coordinate in area.EnumerateCoordinates())
        {
            if (_strokeErasesResource)
            {
                _resourceStroke.Remove(coordinate, _strokeResourceId);
            }
            else
            {
                _resourceStroke.Upsert(
                    coordinate,
                    new CampaignResourceOccurrence(
                        _strokeResourceId,
                        _strokeResourcePotential,
                        _strokeLocksResource));
            }
        }
    }

    private void ApplySeasonAt(CampaignTileCoordinate coordinate)
    {
        if (_seasonStroke is null || World is null)
        {
            return;
        }

        if (_lastStampCoordinate is not { } previous)
        {
            ApplySeasonPaintArea(coordinate);
        }
        else
        {
            var dx = coordinate.X - previous.X;
            var dy = coordinate.Y - previous.Y;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps == 0)
            {
                ApplySeasonPaintArea(coordinate);
            }
            else
            {
                for (var step = 1; step <= steps; step++)
                {
                    var x = previous.X + (int)Math.Round(
                        (double)dx * step / steps,
                        MidpointRounding.AwayFromZero);
                    var y = previous.Y + (int)Math.Round(
                        (double)dy * step / steps,
                        MidpointRounding.AwayFromZero);
                    ApplySeasonPaintArea(new CampaignTileCoordinate(x, y));
                }
            }
        }

        _lastStampCoordinate = coordinate;
        _keyboardCoordinate = coordinate;
        InvalidateVisual();
    }

    private void ApplySeasonPaintArea(CampaignTileCoordinate center)
    {
        if (_seasonStroke is null ||
            World is null ||
            SeasonMap is not { } seasons ||
            seasons.Definition != World.Definition)
        {
            return;
        }

        ApplySeasonToolToArea(
            _seasonStroke,
            seasons,
            center,
            _strokeSeasonPaintAreaRadius,
            _strokeSeasonTool,
            _strokeSeasonId,
            _strokeLocksSeason);
    }

    internal static void ApplySeasonToolToArea(
        CampaignSeasonStrokeBuilder stroke,
        CampaignSeasonMap seasons,
        CampaignTileCoordinate center,
        int paintAreaRadius,
        CampaignSeasonPaintTool tool,
        string? selectedSeasonId,
        bool lockPaintedTiles)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        ArgumentNullException.ThrowIfNull(seasons);
        if (paintAreaRadius is < 0 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paintAreaRadius),
                paintAreaRadius,
                "Season paint area radius must be from 0 through 12 tiles.");
        }

        if (!Enum.IsDefined(tool))
        {
            throw new ArgumentOutOfRangeException(nameof(tool), tool, "Unknown season paint tool.");
        }

        if (!seasons.Catalog.Contains(selectedSeasonId))
        {
            throw new ArgumentException(
                "Season editing requires a selected ID from the active Season Catalog.",
                nameof(selectedSeasonId));
        }

        var area = CampaignTileArea.Centered(
            seasons.Definition,
            center,
            paintAreaRadius);
        foreach (var coordinate in area.EnumerateCoordinates())
        {
            switch (tool)
            {
                case CampaignSeasonPaintTool.Paint:
                    stroke.Upsert(coordinate, selectedSeasonId!, lockPaintedTiles);
                    break;
                case CampaignSeasonPaintTool.Erase:
                    stroke.Remove(coordinate, selectedSeasonId!);
                    break;
                case CampaignSeasonPaintTool.Lock:
                    stroke.SetLocked(coordinate, selectedSeasonId!, locked: true);
                    break;
                case CampaignSeasonPaintTool.Unlock:
                    stroke.SetLocked(coordinate, selectedSeasonId!, locked: false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tool), tool, "Unknown season paint tool.");
            }
        }
    }

    private void ApplyFourConnectedRiverPath(
        CampaignTileCoordinate start,
        CampaignTileCoordinate target,
        CampaignTileData data)
    {
        var x = start.X;
        var y = start.Y;
        if (x == target.X && y == target.Y)
        {
            TryApplyTile(target, data);
            return;
        }

        while (x != target.X || y != target.Y)
        {
            var remainingX = target.X - x;
            var remainingY = target.Y - y;
            if (remainingX != 0 && Math.Abs(remainingX) >= Math.Abs(remainingY))
            {
                x += Math.Sign(remainingX);
            }
            else
            {
                y += Math.Sign(remainingY);
            }

            TryApplyTile(new CampaignTileCoordinate(x, y), data);
        }
    }

    private void ApplyPaintArea(CampaignTileCoordinate center, CampaignTileData data)
    {
        if (World is not { } world)
        {
            return;
        }

        foreach (var coordinate in GetPaintArea(world, center).EnumerateCoordinates())
        {
            TryApplyTile(coordinate, data);
        }
    }

    private void TryApplyTile(CampaignTileCoordinate coordinate, CampaignTileData data)
    {
        if (_stroke is null || _stroke.TryApplyTile(coordinate, data, out _))
        {
            return;
        }

        _blockedRiverCoordinates.Add(coordinate);
    }

    private short GetStampHeight(CampaignWorld world) =>
        (short)Math.Clamp(
            Math.Round(StampHeight, MidpointRounding.AwayFromZero),
            world.Definition.MinimumHeightMeters,
            world.Definition.MaximumHeightMeters);

    private int EffectivePaintAreaRadius =>
        SelectedCampaignTileType.IsRiver()
            ? 0
            : Math.Clamp(PaintAreaRadius, 0, 12);

    private int PaintAreaSideLength => 1 + EffectivePaintAreaRadius * 2;

    private int EffectiveResourcePaintAreaRadius =>
        Math.Clamp(ResourcePaintAreaRadius, 0, 12);

    private int EffectiveSeasonPaintAreaRadius =>
        Math.Clamp(SeasonPaintAreaRadius, 0, 12);

    private CampaignTileArea GetPaintArea(CampaignWorld world, CampaignTileCoordinate center) =>
        CampaignTileArea.Centered(world.Definition, center, EffectivePaintAreaRadius);

    private string BuildStrokeDescription(short height) =>
        EffectivePaintAreaRadius == 0
            ? $"Stamp {GetSelectedTerrainName()} at {height:N0} m"
            : $"Stamp {GetSelectedTerrainName()} {PaintAreaSideLength} × {PaintAreaSideLength} tiles at {height:N0} m";

    private string BuildResourceStrokeDescription()
    {
        var resourceId = _strokeResourceId ?? SelectedResourceId ?? "resource";
        var resourceName = ResourceMap?.Catalog.TryGet(resourceId, out var definition) == true
            ? definition.Name
            : resourceId;
        var sideLength = 1 + _strokeResourcePaintAreaRadius * 2;
        var area = sideLength == 1 ? "" : $" {sideLength} × {sideLength} tiles";
        if (_strokeErasesResource)
        {
            return $"Erase {resourceName}{area}";
        }

        var lockText = _strokeLocksResource ? ", locked" : "";
        return $"Paint {resourceName}{area} at {_strokeResourcePotential} potential{lockText}";
    }

    private string BuildSeasonStrokeDescription()
    {
        var sideLength = 1 + _strokeSeasonPaintAreaRadius * 2;
        var area = sideLength == 1 ? string.Empty : $" {sideLength} × {sideLength} tiles";
        return _strokeSeasonTool switch
        {
            CampaignSeasonPaintTool.Paint =>
                $"Paint {GetSelectedSeasonName()}{area}{(_strokeLocksSeason ? ", locked" : ", unlocked")}",
            CampaignSeasonPaintTool.Erase => $"Erase selected Season Occurrence{area}",
            CampaignSeasonPaintTool.Lock => $"Lock seasons{area}",
            CampaignSeasonPaintTool.Unlock => $"Unlock seasons{area}",
            _ => $"Edit seasons{area}",
        };
    }

    private string GetSelectedTerrainName() =>
        World?.Tiles.TryGetCustomTerrainDefinition(SelectedCustomTerrainId, out var definition) == true
            ? definition.Name
            : SelectedCampaignTileType.ToString();

    private bool TryGetActiveResource(
        CampaignWorld world,
        out CampaignResourceMap resources,
        out CampaignResourceDefinition definition)
    {
        var candidate = ResourceMap;
        if (candidate is not null &&
            candidate.Definition == world.Definition &&
            candidate.Catalog.TryGet(SelectedResourceId, out var found))
        {
            resources = candidate;
            definition = found;
            return true;
        }

        resources = null!;
        definition = null!;
        return false;
    }

    private bool TryGetActiveSeason(CampaignWorld world, out CampaignSeasonMap seasons)
    {
        var candidate = SeasonMap;
        if (candidate is not null &&
            candidate.Definition == world.Definition &&
            (SeasonPaintTool != CampaignSeasonPaintTool.Paint ||
             candidate.Catalog.Contains(SelectedSeasonId)))
        {
            seasons = candidate;
            return true;
        }

        seasons = null!;
        return false;
    }

    private string GetSelectedSeasonName()
    {
        var seasonId = _strokeSeasonId ?? SelectedSeasonId;
        return SeasonMap?.Catalog.TryGet(seasonId, out var definition) == true
            ? definition.Name
            : seasonId ?? "season";
    }

    private void ResetResourceStrokeSettings()
    {
        _strokeResourceId = null;
        _strokeResourcePotential = default;
        _strokeLocksResource = false;
        _strokeErasesResource = false;
        _strokeResourcePaintAreaRadius = 0;
    }

    private void ResetSeasonStrokeSettings()
    {
        _strokeSeasonId = null;
        _strokeSeasonTool = default;
        _strokeLocksSeason = false;
        _strokeSeasonPaintAreaRadius = 0;
    }

    private CampaignTilePointerInfo? ToPointerInfo(Point position)
    {
        var world = World;
        if (world is null)
        {
            return null;
        }

        var tileSpaceX = _originX + position.X / _zoom;
        var tileSpaceY = _originY + position.Y / _zoom;
        if (tileSpaceX < 0 || tileSpaceY < 0 ||
            tileSpaceX > world.Definition.TilesX || tileSpaceY > world.Definition.TilesY)
        {
            return null;
        }

        var tileX = Math.Min((int)Math.Floor(tileSpaceX), world.Definition.TilesX - 1);
        var tileY = Math.Min((int)Math.Floor(tileSpaceY), world.Definition.TilesY - 1);
        return new CampaignTilePointerInfo(
            new CampaignTileCoordinate(tileX, tileY),
            tileSpaceX,
            tileSpaceY);
    }

    private void ApplyFitIfPossible()
    {
        var world = World;
        if (!_fitRequested || world is null || Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }

        var worldWidth = world.Definition.TilesX;
        var worldHeight = world.Definition.TilesY;
        var availableWidth = Math.Max(1, Bounds.Width - 48);
        var availableHeight = Math.Max(1, Bounds.Height - 48);
        _zoom = Math.Clamp(
            Math.Min(availableWidth / worldWidth, availableHeight / worldHeight),
            MinimumZoom,
            MaximumZoom);
        _originX = worldWidth / 2.0 - Bounds.Width / (2 * _zoom);
        _originY = worldHeight / 2.0 - Bounds.Height / (2 * _zoom);
        _fitRequested = false;
        MarkSurfaceBitmapDirty();
        MarkResourceBitmapDirty();
        RaiseZoomChanged();
        RaiseViewportChanged();
    }

    private void EnsureSurfaceBitmap(CampaignWorld world)
    {
        var boundsWidth = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
        var boundsHeight = Math.Max(1, (int)Math.Ceiling(Bounds.Height));
        var scale = Math.Max(
            1,
            Math.Max((double)boundsWidth / MaximumRasterWidth, (double)boundsHeight / MaximumRasterHeight));
        var rasterWidth = Math.Max(1, (int)Math.Ceiling(boundsWidth / scale));
        var rasterHeight = Math.Max(1, (int)Math.Ceiling(boundsHeight / scale));
        var key = new RasterKey(
            world.Revision,
            rasterWidth,
            rasterHeight,
            Bounds.Width,
            Bounds.Height,
            _originX,
            _originY,
            _zoom,
            UseGrayscale);
        if (_surfaceBitmap is not null && _rasterKey == key)
        {
            return;
        }

        var pixelSize = new PixelSize(rasterWidth, rasterHeight);
        if (_surfaceBitmap is null || _surfaceBitmap.PixelSize != pixelSize)
        {
            _surfaceBitmap?.Dispose();
            _surfaceBitmap = new WriteableBitmap(
                pixelSize,
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }

        using var snapshot = SurfaceRasterSnapshot.Create(
            world,
            Bounds.Width,
            Bounds.Height,
            _originX,
            _originY,
            _zoom);
        FillSurfaceBitmap(
            _surfaceBitmap,
            snapshot,
            Bounds.Width,
            Bounds.Height,
            _originX,
            _originY,
            _zoom,
            UseGrayscale);
        _rasterKey = key;
    }

    private void DrawResourceTerrainMute(DrawingContext context, CampaignWorld world)
    {
        var worldRectangle = new Rect(
            -_originX * _zoom,
            -_originY * _zoom,
            world.Definition.TilesX * _zoom,
            world.Definition.TilesY * _zoom);
        context.FillRectangle(ResourceTerrainMuteBrush, worldRectangle);
    }

    private void DrawResourceHeatmap(DrawingContext context, CampaignWorld world)
    {
        if (!TryGetActiveResource(world, out var resources, out var definition))
        {
            return;
        }

        if (!_isPanning || _resourceBitmap is null || _resourceRasterKey is null)
        {
            EnsureResourceBitmap(resources, definition);
        }

        if (_resourceBitmap is null || _resourceRasterKey is not { } cached)
        {
            return;
        }

        var destination = new Rect(Bounds.Size);
        if (_isPanning)
        {
            var zoomRatio = _zoom / cached.Zoom;
            destination = new Rect(
                (cached.OriginX - _originX) * _zoom,
                (cached.OriginY - _originY) * _zoom,
                cached.ViewportWidth * zoomRatio,
                cached.ViewportHeight * zoomRatio);
        }

        context.DrawImage(
            _resourceBitmap,
            new Rect(_resourceBitmap.Size),
            destination);
    }

    private void DrawSeasonOverlay(DrawingContext context, CampaignWorld world)
    {
        var seasons = SeasonMap;
        if (seasons is null || seasons.Definition != world.Definition)
        {
            return;
        }

        if (!_isPanning || _seasonBitmap is null || _seasonRasterKey is null)
        {
            EnsureSeasonBitmap(seasons);
        }

        if (_seasonBitmap is null || _seasonRasterKey is not { } cached)
        {
            return;
        }

        var destination = new Rect(Bounds.Size);
        if (_isPanning)
        {
            var zoomRatio = _zoom / cached.Zoom;
            destination = new Rect(
                (cached.OriginX - _originX) * _zoom,
                (cached.OriginY - _originY) * _zoom,
                cached.ViewportWidth * zoomRatio,
                cached.ViewportHeight * zoomRatio);
        }

        context.DrawImage(
            _seasonBitmap,
            new Rect(_seasonBitmap.Size),
            destination);
    }

    private void EnsureSeasonBitmap(CampaignSeasonMap seasons)
    {
        var boundsWidth = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
        var boundsHeight = Math.Max(1, (int)Math.Ceiling(Bounds.Height));
        var scale = Math.Max(
            1,
            Math.Max((double)boundsWidth / MaximumRasterWidth, (double)boundsHeight / MaximumRasterHeight));
        var rasterWidth = Math.Max(1, (int)Math.Ceiling(boundsWidth / scale));
        var rasterHeight = Math.Max(1, (int)Math.Ceiling(boundsHeight / scale));
        var key = new SeasonRasterKey(
            seasons.Revision,
            SelectedSeasonId ?? string.Empty,
            rasterWidth,
            rasterHeight,
            Bounds.Width,
            Bounds.Height,
            _originX,
            _originY,
            _zoom,
            BlendSeasonBoundaries);
        if (_seasonBitmap is not null &&
            _seasonRasterSnapshot is not null &&
            _seasonRasterKey == key)
        {
            return;
        }

        var pixelSize = new PixelSize(rasterWidth, rasterHeight);
        if (_seasonBitmap is null || _seasonBitmap.PixelSize != pixelSize)
        {
            _seasonBitmap?.Dispose();
            _seasonBitmap = new WriteableBitmap(
                pixelSize,
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
        }

        _seasonRasterSnapshot?.Dispose();
        _seasonRasterSnapshot = SeasonRasterSnapshot.Create(
            seasons,
            SelectedSeasonId,
            Bounds.Width,
            Bounds.Height,
            _originX,
            _originY,
            _zoom);
        FillSeasonBitmap(
            _seasonBitmap,
            _seasonRasterSnapshot,
            Bounds.Width,
            Bounds.Height,
            _originX,
            _originY,
            _zoom,
            BlendSeasonBoundaries);
        _seasonRasterKey = key;
    }

    private static unsafe void FillSeasonBitmap(
        WriteableBitmap bitmap,
        SeasonRasterSnapshot snapshot,
        double viewportWidth,
        double viewportHeight,
        double originX,
        double originY,
        double zoom,
        bool blendBoundaries)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        var sourceRowBytes = checked(width * 4);
        var pixels = ArrayPool<byte>.Shared.Rent(checked(sourceRowBytes * height));
        try
        {
            Array.Clear(pixels, 0, sourceRowBytes * height);
            if ((long)width * height >= ParallelRasterPixelThreshold &&
                Environment.ProcessorCount > 1)
            {
                Parallel.For(0, height, pixelY => FillSeasonRow(
                    pixels,
                    pixelY,
                    width,
                    height,
                    snapshot,
                    viewportWidth,
                    viewportHeight,
                    originX,
                    originY,
                    zoom,
                    blendBoundaries));
            }
            else
            {
                for (var pixelY = 0; pixelY < height; pixelY++)
                {
                    FillSeasonRow(
                        pixels,
                        pixelY,
                        width,
                        height,
                        snapshot,
                        viewportWidth,
                        viewportHeight,
                        originX,
                        originY,
                        zoom,
                        blendBoundaries);
                }
            }

            using var framebuffer = bitmap.Lock();
            fixed (byte* sourceAddress = pixels)
            {
                var destinationAddress = (byte*)framebuffer.Address;
                for (var pixelY = 0; pixelY < height; pixelY++)
                {
                    Buffer.MemoryCopy(
                        sourceAddress + pixelY * sourceRowBytes,
                        destinationAddress + pixelY * framebuffer.RowBytes,
                        framebuffer.RowBytes,
                        sourceRowBytes);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }

    private static void FillSeasonRow(
        byte[] pixels,
        int pixelY,
        int width,
        int height,
        SeasonRasterSnapshot snapshot,
        double viewportWidth,
        double viewportHeight,
        double originX,
        double originY,
        double zoom,
        bool blendBoundaries)
    {
        var screenY = (pixelY + 0.5) * viewportHeight / height;
        var tileSpaceY = originY + screenY / zoom;
        if (tileSpaceY < 0 || tileSpaceY >= snapshot.Definition.TilesY)
        {
            return;
        }

        var tileY = (int)Math.Floor(tileSpaceY);
        for (var pixelX = 0; pixelX < width; pixelX++)
        {
            var screenX = (pixelX + 0.5) * viewportWidth / width;
            var tileSpaceX = originX + screenX / zoom;
            if (tileSpaceX < 0 || tileSpaceX >= snapshot.Definition.TilesX)
            {
                continue;
            }

            var tileX = (int)Math.Floor(tileSpaceX);
            var seasonIndex = snapshot.GetSeasonIndex(tileX, tileY);
            var color = snapshot.GetColor(seasonIndex);
            var alpha = snapshot.GetAlpha(seasonIndex);
            if (blendBoundaries)
            {
                color = BlendSeasonBoundary(
                    snapshot,
                    tileX,
                    tileY,
                    tileSpaceX - tileX,
                    tileSpaceY - tileY,
                    seasonIndex,
                    color);
            }

            var pixel = checked((pixelY * width + pixelX) * 4);
            pixels[pixel] = Premultiply(color.B, alpha);
            pixels[pixel + 1] = Premultiply(color.G, alpha);
            pixels[pixel + 2] = Premultiply(color.R, alpha);
            pixels[pixel + 3] = alpha;
        }
    }

    private static Rgb BlendSeasonBoundary(
        SeasonRasterSnapshot snapshot,
        int x,
        int y,
        double localX,
        double localY,
        ushort seasonIndex,
        Rgb color)
    {
        const double blendWidth = 0.16;
        var nearestDistance = blendWidth;
        var neighborX = x;
        var neighborY = y;
        if (localX < nearestDistance)
        {
            nearestDistance = localX;
            neighborX = x - 1;
        }

        if (1 - localX < nearestDistance)
        {
            nearestDistance = 1 - localX;
            neighborX = x + 1;
            neighborY = y;
        }

        if (localY < nearestDistance)
        {
            nearestDistance = localY;
            neighborX = x;
            neighborY = y - 1;
        }

        if (1 - localY < nearestDistance)
        {
            nearestDistance = 1 - localY;
            neighborX = x;
            neighborY = y + 1;
        }

        if (nearestDistance >= blendWidth ||
            (uint)neighborX >= (uint)snapshot.Definition.TilesX ||
            (uint)neighborY >= (uint)snapshot.Definition.TilesY)
        {
            return color;
        }

        var neighborIndex = snapshot.GetSeasonIndex(neighborX, neighborY);
        if (neighborIndex == seasonIndex)
        {
            return color;
        }

        var amount = 0.5 * (1 - nearestDistance / blendWidth);
        var neighbor = snapshot.GetColor(neighborIndex);
        return new Rgb(
            (byte)Math.Round(Lerp(color.R, neighbor.R, amount)),
            (byte)Math.Round(Lerp(color.G, neighbor.G, amount)),
            (byte)Math.Round(Lerp(color.B, neighbor.B, amount)));
    }

    private void EnsureResourceBitmap(
        CampaignResourceMap resources,
        CampaignResourceDefinition definition)
    {
        var boundsWidth = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
        var boundsHeight = Math.Max(1, (int)Math.Ceiling(Bounds.Height));
        var scale = Math.Max(
            1,
            Math.Max((double)boundsWidth / MaximumRasterWidth, (double)boundsHeight / MaximumRasterHeight));
        var rasterWidth = Math.Max(1, (int)Math.Ceiling(boundsWidth / scale));
        var rasterHeight = Math.Max(1, (int)Math.Ceiling(boundsHeight / scale));
        var key = new ResourceRasterKey(
            resources.Revision,
            definition.Id,
            rasterWidth,
            rasterHeight,
            Bounds.Width,
            Bounds.Height,
            _originX,
            _originY,
            _zoom);
        if (_resourceBitmap is not null &&
            _resourceRasterSnapshot is not null &&
            _resourceRasterKey == key)
        {
            return;
        }

        var pixelSize = new PixelSize(rasterWidth, rasterHeight);
        if (_resourceBitmap is null || _resourceBitmap.PixelSize != pixelSize)
        {
            _resourceBitmap?.Dispose();
            _resourceBitmap = new WriteableBitmap(
                pixelSize,
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
        }

        _resourceRasterSnapshot?.Dispose();
        _resourceRasterSnapshot = ResourceRasterSnapshot.Create(
            resources,
            definition,
            Bounds.Width,
            Bounds.Height,
            _originX,
            _originY,
            _zoom);
        FillResourceBitmap(
            _resourceBitmap,
            _resourceRasterSnapshot,
            Bounds.Width,
            Bounds.Height,
            _originX,
            _originY,
            _zoom);
        _resourceRasterKey = key;
    }

    private static unsafe void FillResourceBitmap(
        WriteableBitmap bitmap,
        ResourceRasterSnapshot snapshot,
        double viewportWidth,
        double viewportHeight,
        double originX,
        double originY,
        double zoom)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        var sourceRowBytes = checked(width * 4);
        var pixels = ArrayPool<byte>.Shared.Rent(checked(sourceRowBytes * height));
        try
        {
            Array.Clear(pixels, 0, sourceRowBytes * height);
            if ((long)width * height >= ParallelRasterPixelThreshold &&
                Environment.ProcessorCount > 1)
            {
                Parallel.For(
                    0,
                    height,
                    pixelY => FillResourceRow(
                        pixels,
                        pixelY,
                        width,
                        height,
                        snapshot,
                        viewportWidth,
                        viewportHeight,
                        originX,
                        originY,
                        zoom));
            }
            else
            {
                for (var pixelY = 0; pixelY < height; pixelY++)
                {
                    FillResourceRow(
                        pixels,
                        pixelY,
                        width,
                        height,
                        snapshot,
                        viewportWidth,
                        viewportHeight,
                        originX,
                        originY,
                        zoom);
                }
            }

            using var framebuffer = bitmap.Lock();
            fixed (byte* sourceAddress = pixels)
            {
                var destinationAddress = (byte*)framebuffer.Address;
                for (var pixelY = 0; pixelY < height; pixelY++)
                {
                    Buffer.MemoryCopy(
                        sourceAddress + pixelY * sourceRowBytes,
                        destinationAddress + pixelY * framebuffer.RowBytes,
                        framebuffer.RowBytes,
                        sourceRowBytes);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }

    private static void FillResourceRow(
        byte[] pixels,
        int pixelY,
        int width,
        int height,
        ResourceRasterSnapshot snapshot,
        double viewportWidth,
        double viewportHeight,
        double originX,
        double originY,
        double zoom)
    {
        var screenY = (pixelY + 0.5) * viewportHeight / height;
        var tileSpaceY = originY + screenY / zoom;
        if (tileSpaceY < 0 || tileSpaceY >= snapshot.Definition.TilesY)
        {
            return;
        }

        var tileY = (int)Math.Floor(tileSpaceY);
        for (var pixelX = 0; pixelX < width; pixelX++)
        {
            var screenX = (pixelX + 0.5) * viewportWidth / width;
            var tileSpaceX = originX + screenX / zoom;
            if (tileSpaceX < 0 || tileSpaceX >= snapshot.Definition.TilesX)
            {
                continue;
            }

            var tileX = (int)Math.Floor(tileSpaceX);
            var potential = snapshot.GetPotential(tileX, tileY);
            if (potential == 0)
            {
                continue;
            }

            var normalized = (potential - CampaignResourceOccurrence.MinimumPotential) /
                (double)(CampaignResourceOccurrence.MaximumPotential -
                         CampaignResourceOccurrence.MinimumPotential);
            var alpha = (byte)Math.Round(Lerp(58, 232, normalized));
            var color = Shade(snapshot.Color, Lerp(0.58, 1.16, normalized));
            var pixel = checked((pixelY * width + pixelX) * 4);
            pixels[pixel] = Premultiply(color.B, alpha);
            pixels[pixel + 1] = Premultiply(color.G, alpha);
            pixels[pixel + 2] = Premultiply(color.R, alpha);
            pixels[pixel + 3] = alpha;
        }
    }

    private static unsafe void FillSurfaceBitmap(
        WriteableBitmap bitmap,
        SurfaceRasterSnapshot snapshot,
        double viewportWidth,
        double viewportHeight,
        double originX,
        double originY,
        double zoom,
        bool grayscale)
    {
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        var sourceRowBytes = checked(width * 4);
        var pixels = ArrayPool<byte>.Shared.Rent(checked(sourceRowBytes * height));
        try
        {
            if ((long)width * height >= ParallelRasterPixelThreshold &&
                Environment.ProcessorCount > 1)
            {
                Parallel.For(
                    0,
                    height,
                    pixelY => FillSurfaceRow(
                        pixels,
                        pixelY,
                        width,
                        height,
                        snapshot,
                        viewportWidth,
                        viewportHeight,
                        originX,
                        originY,
                        zoom,
                        grayscale));
            }
            else
            {
                for (var pixelY = 0; pixelY < height; pixelY++)
                {
                    FillSurfaceRow(
                        pixels,
                        pixelY,
                        width,
                        height,
                        snapshot,
                        viewportWidth,
                        viewportHeight,
                        originX,
                        originY,
                        zoom,
                        grayscale);
                }
            }

            using var framebuffer = bitmap.Lock();
            fixed (byte* sourceAddress = pixels)
            {
                var destinationAddress = (byte*)framebuffer.Address;
                for (var pixelY = 0; pixelY < height; pixelY++)
                {
                    Buffer.MemoryCopy(
                        sourceAddress + pixelY * sourceRowBytes,
                        destinationAddress + pixelY * framebuffer.RowBytes,
                        framebuffer.RowBytes,
                        sourceRowBytes);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }

    private static void FillSurfaceRow(
        byte[] pixels,
        int pixelY,
        int width,
        int height,
        SurfaceRasterSnapshot snapshot,
        double viewportWidth,
        double viewportHeight,
        double originX,
        double originY,
        double zoom,
        bool grayscale)
    {
        var screenY = (pixelY + 0.5) * viewportHeight / height;
        var tileSpaceY = originY + screenY / zoom;
        for (var pixelX = 0; pixelX < width; pixelX++)
        {
            var screenX = (pixelX + 0.5) * viewportWidth / width;
            var tileSpaceX = originX + screenX / zoom;
            Rgb color;
            if (tileSpaceX < 0 || tileSpaceY < 0 ||
                tileSpaceX > snapshot.Definition.TilesX || tileSpaceY > snapshot.Definition.TilesY)
            {
                color = new Rgb(16, 25, 30);
            }
            else
            {
                var tileX = Math.Min((int)Math.Floor(tileSpaceX), snapshot.Definition.TilesX - 1);
                var tileY = Math.Min((int)Math.Floor(tileSpaceY), snapshot.Definition.TilesY - 1);
                var tile = snapshot.GetTile(tileX, tileY);
                snapshot.GetDerivedSurface(
                    tileSpaceX,
                    tileSpaceY,
                    out var heightMeters,
                    out var gradeX,
                    out var gradeY);
                var fractionX = tileSpaceX - Math.Floor(tileSpaceX);
                var fractionY = tileSpaceY - Math.Floor(tileSpaceY);
                color = GetSurfaceColor(
                    snapshot,
                    tile,
                    tileX,
                    tileY,
                    tileSpaceX,
                    tileSpaceY,
                    fractionX,
                    fractionY,
                    heightMeters,
                    gradeX,
                    gradeY,
                    zoom,
                    grayscale);

            }

            var pixel = checked((pixelY * width + pixelX) * 4);
            pixels[pixel] = color.B;
            pixels[pixel + 1] = color.G;
            pixels[pixel + 2] = color.R;
            pixels[pixel + 3] = 255;
        }
    }

    private static Rgb GetSurfaceColor(
        SurfaceRasterSnapshot snapshot,
        CampaignTileData tile,
        int tileX,
        int tileY,
        double tileSpaceX,
        double tileSpaceY,
        double localX,
        double localY,
        double heightMeters,
        double gradeX,
        double gradeY,
        double zoom,
        bool grayscale)
    {
        var normalized = Normalize(
            heightMeters,
            snapshot.Definition.MinimumHeightMeters,
            snapshot.Definition.MaximumHeightMeters);
        if (grayscale)
        {
            var value = (byte)Math.Round(Lerp(22, 238, normalized));
            return new Rgb(value, value, value);
        }

        var appearance = GetSurfaceAppearance(snapshot, tile, tileX, tileY, localX, localY);
        var textureStrength = Math.Clamp((zoom - 1.25) / 5.0, 0, 1);
        var textureFactor = textureStrength <= 0
            ? 1
            : Lerp(
                1,
                GetTextureFactor(appearance.Texture, tileSpaceX, tileSpaceY),
                textureStrength);
        var reliefFactor = tile.Type.IsWater() ? 1 : GetReliefLightingFactor(gradeX, gradeY);
        return Shade(appearance.Color, (0.58 + normalized * 0.72) * textureFactor * reliefFactor);
    }

    private static double GetReliefLightingFactor(double gradeX, double gradeY)
    {
        const double verticalExaggeration = 5.0;
        const double lightX = -0.45;
        const double lightY = -0.55;
        const double lightZ = 0.70;
        var normalX = -gradeX * verticalExaggeration;
        var normalY = -gradeY * verticalExaggeration;
        var inverseLength = 1 / Math.Sqrt((normalX * normalX) + (normalY * normalY) + 1);
        var illumination =
            (normalX * inverseLength * lightX) +
            (normalY * inverseLength * lightY) +
            (inverseLength * lightZ);
        return Math.Clamp(1 + ((illumination - lightZ) * 0.45), 0.72, 1.18);
    }

    private static SurfaceAppearance GetSurfaceAppearance(
        SurfaceRasterSnapshot snapshot,
        CampaignTileData tile,
        int tileX,
        int tileY,
        double localX,
        double localY)
    {
        var automaticCoast = snapshot.GetAutomaticCoastSurfaceMaterial(
            tile.Type,
            tileX,
            tileY,
            localX,
            localY);
        if (automaticCoast == AutomaticCoastSurfaceMaterial.Sea)
        {
            return new SurfaceAppearance(new Rgb(30, 106, 139), SurfaceTexture.SeaWater);
        }

        if (automaticCoast == AutomaticCoastSurfaceMaterial.Lake)
        {
            return new SurfaceAppearance(new Rgb(45, 142, 163), SurfaceTexture.LakeWater);
        }

        var appearance = GetBuiltInSurfaceAppearance(tile.Type);
        return snapshot.TryGetCustomTerrainColor(tile.CustomTerrainId, out var customColor)
            ? appearance with { Color = customColor }
            : appearance;
    }

    private static SurfaceAppearance GetBuiltInSurfaceAppearance(CampaignTileType type)
    {
        return type switch
        {
            CampaignTileType.Water or CampaignTileType.Sea => new SurfaceAppearance(
                new Rgb(30, 106, 139),
                SurfaceTexture.SeaWater),
            CampaignTileType.Plains => new SurfaceAppearance(
                new Rgb(115, 148, 93),
                SurfaceTexture.Grass),
            CampaignTileType.Steppe => new SurfaceAppearance(
                new Rgb(164, 154, 88),
                SurfaceTexture.Steppe),
            CampaignTileType.Desert => new SurfaceAppearance(
                new Rgb(201, 145, 66),
                SurfaceTexture.Desert),
            CampaignTileType.Forest => new SurfaceAppearance(
                new Rgb(47, 104, 79),
                SurfaceTexture.Forest),
            CampaignTileType.Hills => new SurfaceAppearance(
                new Rgb(139, 138, 98),
                SurfaceTexture.Hills),
            CampaignTileType.Mountain => new SurfaceAppearance(
                new Rgb(133, 135, 132),
                SurfaceTexture.Rock),
            CampaignTileType.Lake => new SurfaceAppearance(
                new Rgb(45, 142, 163),
                SurfaceTexture.LakeWater),
            CampaignTileType.River => new SurfaceAppearance(
                new Rgb(102, 139, 84),
                SurfaceTexture.Grass),
            CampaignTileType.LargeRiver => new SurfaceAppearance(
                new Rgb(94, 134, 78),
                SurfaceTexture.Grass),
            CampaignTileType.RiverJunction => new SurfaceAppearance(
                new Rgb(98, 137, 82),
                SurfaceTexture.Grass),
            CampaignTileType.Beach => new SurfaceAppearance(
                new Rgb(195, 168, 109),
                SurfaceTexture.Sand),
            CampaignTileType.Cliff => new SurfaceAppearance(
                new Rgb(111, 102, 94),
                SurfaceTexture.Cliff),
            CampaignTileType.Coastal => new SurfaceAppearance(
                new Rgb(115, 148, 93),
                SurfaceTexture.Grass),
            _ => new SurfaceAppearance(new Rgb(89, 102, 106), SurfaceTexture.Neutral),
        };
    }

    private static double GetTextureFactor(SurfaceTexture texture, double x, double y)
    {
        return texture switch
        {
            SurfaceTexture.Grass => 1 + BroadNoise(x, y) * 0.08 + FineNoise(x, y) * 0.045,
            SurfaceTexture.Steppe => GetSteppeTextureFactor(x, y),
            SurfaceTexture.Forest => 0.98 + BroadNoise(x, y) * 0.15 + FineNoise(x, y) * 0.07,
            SurfaceTexture.Hills => GetHillTextureFactor(x, y),
            SurfaceTexture.Rock => 0.98 + BroadNoise(x, y) * 0.13 + Math.Abs(FineNoise(x, y)) * 0.07,
            SurfaceTexture.SeaWater => 1 +
                Math.Sin((x * 4.2 + y * 1.4) * Math.Tau) * 0.045 +
                Math.Sin((y * 8.5 - x * 0.9) * Math.Tau) * 0.025,
            SurfaceTexture.LakeWater => 1 +
                Math.Sin((x * 3.2 + y * 1.1) * Math.Tau) * 0.03 + BroadNoise(x, y) * 0.025,
            SurfaceTexture.Sand => 1 + BroadNoise(x, y) * 0.035 + FineNoise(x, y) * 0.06,
            SurfaceTexture.Desert => GetDesertTextureFactor(x, y),
            SurfaceTexture.Cliff => GetCliffTextureFactor(x, y),
            _ => 1,
        };
    }

    private static double GetHillTextureFactor(double x, double y)
    {
        var broad = BroadNoise(x, y);
        return 1 + broad * 0.08 +
               Math.Sin((x * 3.2 + y * 6.4 + broad * 0.8) * Math.Tau) * 0.055;
    }

    private static double GetSteppeTextureFactor(double x, double y)
    {
        var broad = BroadNoise(x, y);
        var dryGrass = Math.Sin((x * 7.2 + y * 2.1 + broad * 0.45) * Math.Tau);
        return 1 + broad * 0.09 + FineNoise(x, y) * 0.035 + dryGrass * 0.025;
    }

    private static double GetDesertTextureFactor(double x, double y)
    {
        var broad = BroadNoise(x, y);
        var dune = Math.Sin((x * 2.4 + y * 0.85 + broad * 0.6) * Math.Tau);
        return 1 + broad * 0.075 + FineNoise(x, y) * 0.03 + dune * 0.04;
    }

    private static double GetCliffTextureFactor(double x, double y)
    {
        var fine = FineNoise(x, y);
        return 0.96 + BroadNoise(x, y) * 0.12 +
               Math.Sin((y * 13 + fine * 0.6) * Math.Tau) * 0.065;
    }

    private static double BroadNoise(double x, double y) =>
        ValueNoise(x * 5.5, y * 5.5, 0xA24BAED4963EE407UL);

    private static double FineNoise(double x, double y) =>
        ValueNoise(x * 18, y * 18, 0x9FB21C651E98DF25UL);

    private static double ValueNoise(double x, double y, ulong seed)
    {
        var floorX = Math.Floor(x);
        var floorY = Math.Floor(y);
        var x0 = (long)floorX;
        var y0 = (long)floorY;
        var fractionX = Smooth(x - floorX);
        var fractionY = Smooth(y - floorY);
        var top = Lerp(HashNoise(x0, y0, seed), HashNoise(x0 + 1, y0, seed), fractionX);
        var bottom = Lerp(HashNoise(x0, y0 + 1, seed), HashNoise(x0 + 1, y0 + 1, seed), fractionX);
        return Lerp(top, bottom, fractionY) * 2 - 1;
    }

    private static double HashNoise(long x, long y, ulong seed)
    {
        unchecked
        {
            var hash = (ulong)x * 0x9E3779B185EBCA87UL;
            hash ^= (ulong)y * 0xC2B2AE3D27D4EB4FUL;
            hash ^= seed;
            hash ^= hash >> 30;
            hash *= 0xBF58476D1CE4E5B9UL;
            hash ^= hash >> 27;
            hash *= 0x94D049BB133111EBUL;
            hash ^= hash >> 31;
            return (hash >> 11) / 9007199254740992.0;
        }
    }

    private static double Smooth(double value) => value * value * (3 - 2 * value);

    private void DrawWorldBorder(DrawingContext context, CampaignWorld world)
    {
        var left = -_originX * _zoom;
        var top = -_originY * _zoom;
        var width = world.Definition.TilesX * _zoom;
        var height = world.Definition.TilesY * _zoom;
        context.DrawRectangle(null, WorldBorderPen, new Rect(left, top, width, height));
    }

    private void DrawCampaignGrid(DrawingContext context, CampaignWorld world)
    {
        var stride = GetNiceStride(8 / Math.Max(_zoom, double.Epsilon));
        var visibleMinX = Math.Max(0, _originX);
        var visibleMaxX = Math.Min(world.Definition.TilesX, _originX + Bounds.Width / _zoom);
        var visibleMinY = Math.Max(0, _originY);
        var visibleMaxY = Math.Min(world.Definition.TilesY, _originY + Bounds.Height / _zoom);
        var firstX = Math.Max(0L, (long)Math.Floor(visibleMinX));
        var lastX = Math.Min(world.Definition.TilesX, (long)Math.Ceiling(visibleMaxX));
        var firstY = Math.Max(0L, (long)Math.Floor(visibleMinY));
        var lastY = Math.Min(world.Definition.TilesY, (long)Math.Ceiling(visibleMaxY));
        var left = -_originX * _zoom;
        var right = (world.Definition.TilesX - _originX) * _zoom;
        var top = -_originY * _zoom;
        var bottom = (world.Definition.TilesY - _originY) * _zoom;

        for (var tile = AlignToStride(firstX, stride); tile <= lastX; tile += stride)
        {
            var screenX = (tile - _originX) * _zoom;
            var major = tile % checked(stride * 5) == 0;
            var pen = major ? MajorGridPen : MinorGridPen;
            context.DrawLine(pen, new Point(screenX, top), new Point(screenX, bottom));
        }

        for (var tile = AlignToStride(firstY, stride); tile <= lastY; tile += stride)
        {
            var screenY = (tile - _originY) * _zoom;
            var major = tile % checked(stride * 5) == 0;
            var pen = major ? MajorGridPen : MinorGridPen;
            context.DrawLine(pen, new Point(left, screenY), new Point(right, screenY));
        }
    }

    private void DrawRivers(DrawingContext context, CampaignWorld world)
    {
        var visibleMinX = Math.Max(0, (int)Math.Floor(_originX) - 1);
        var visibleMaxX = Math.Min(
            world.Definition.TilesX - 1,
            (int)Math.Ceiling(_originX + Bounds.Width / _zoom) + 1);
        var visibleMinY = Math.Max(0, (int)Math.Floor(_originY) - 1);
        var visibleMaxY = Math.Min(
            world.Definition.TilesY - 1,
            (int)Math.Ceiling(_originY + Bounds.Height / _zoom) + 1);
        foreach (var entry in world.Tiles.GetRiverTiles())
        {
            if (entry.X < visibleMinX || entry.X > visibleMaxX ||
                entry.Y < visibleMinY || entry.Y > visibleMaxY)
            {
                continue;
            }

            var center = new Point(
                (entry.X + 0.5 - _originX) * _zoom,
                (entry.Y + 0.5 - _originY) * _zoom);
            var connections = GetRenderedRiverConnections(world, entry.X, entry.Y);
            var style = CreateRiverRenderStyle(
                preview: false,
                large: IsLargeRiverGlyph(world, entry.X, entry.Y, entry.Data.Type));
            DrawRiverGlyph(context, center, connections, style);

        }
    }

    private RiverRenderStyle CreateRiverRenderStyle(bool preview, bool large)
    {
        var cacheKey = new RiverRenderStyleKey(UseGrayscale, _zoom);
        var cached = preview
            ? (large ? _previewLargeRiverRenderStyleCache : _previewRiverRenderStyleCache)
            : (large ? _largeRiverRenderStyleCache : _riverRenderStyleCache);
        if (cached is { } existing && existing.Key == cacheKey)
        {
            return existing.Style;
        }

        var alpha = preview ? (byte)210 : byte.MaxValue;
        var bankColor = UseGrayscale
            ? Color.FromArgb(alpha, 55, 70, 75)
            : large
                ? Color.FromArgb(alpha, 45, 78, 59)
                : Color.FromArgb(alpha, 39, 73, 57);
        var waterColor = UseGrayscale
            ? large
                ? Color.FromArgb(alpha, 132, 169, 181)
                : Color.FromArgb(alpha, 146, 177, 185)
            : large
                ? Color.FromArgb(alpha, 35, 127, 166)
                : Color.FromArgb(alpha, 43, 142, 173);
        var highlightColor = UseGrayscale
            ? Color.FromArgb(alpha, 216, 235, 238)
            : Color.FromArgb(alpha, 145, 217, 231);
        var bankWidth = large
            ? Math.Clamp(_zoom * 0.38, 2.8, 38)
            : Math.Clamp(_zoom * 0.14, 1.8, 16);
        var waterWidth = large
            ? Math.Clamp(_zoom * 0.28, 1.8, 28)
            : Math.Clamp(_zoom * 0.08, 1.1, 10);
        var highlightWidth = large
            ? Math.Clamp(_zoom * 0.025, 0.55, 3.5)
            : Math.Clamp(_zoom * 0.02, 0.45, 2.5);
        var style = new RiverRenderStyle(
            new Pen(new SolidColorBrush(bankColor), bankWidth),
            new Pen(new SolidColorBrush(waterColor), waterWidth),
            new Pen(new SolidColorBrush(highlightColor), highlightWidth),
            bankWidth,
            waterWidth,
            highlightWidth);
        var nextCache = new RiverRenderStyleCache(cacheKey, style);
        if (preview && large)
        {
            _previewLargeRiverRenderStyleCache = nextCache;
        }
        else if (preview)
        {
            _previewRiverRenderStyleCache = nextCache;
        }
        else if (large)
        {
            _largeRiverRenderStyleCache = nextCache;
        }
        else
        {
            _riverRenderStyleCache = nextCache;
        }

        return style;
    }

    private void DrawRiverGlyph(
        DrawingContext context,
        Point center,
        RiverConnections connections,
        RiverRenderStyle style)
    {
        if (connections == RiverConnections.None)
        {
            var poolScale = Math.Clamp(_zoom * 0.065, 0.8, 8);
            context.DrawEllipse(
                style.BankPen.Brush,
                null,
                center,
                Math.Max(poolScale, style.BankWidth * 0.55),
                Math.Max(poolScale, style.BankWidth * 0.55));
            context.DrawEllipse(
                style.WaterPen.Brush,
                null,
                center,
                Math.Max(poolScale * 0.68, style.WaterWidth * 0.55),
                Math.Max(poolScale * 0.68, style.WaterWidth * 0.55));
            context.DrawEllipse(
                style.HighlightPen.Brush,
                null,
                center,
                Math.Max(style.HighlightWidth * 0.55, 0.35),
                Math.Max(style.HighlightWidth * 0.55, 0.35));
            return;
        }

        DrawRiverConnections(context, center, connections, style.BankPen);
        DrawRiverConnections(context, center, connections, style.WaterPen);
        DrawRiverConnections(context, center, connections, style.HighlightPen);
        context.DrawEllipse(
            style.BankPen.Brush,
            null,
            center,
            style.BankWidth * 0.5,
            style.BankWidth * 0.5);
        context.DrawEllipse(
            style.WaterPen.Brush,
            null,
            center,
            style.WaterWidth * 0.5,
            style.WaterWidth * 0.5);
    }

    private void DrawRiverConnections(
        DrawingContext context,
        Point center,
        RiverConnections connections,
        IPen pen)
    {
        DrawRiverConnection(context, center, connections, RiverConnections.North, 0, -0.5, pen);
        DrawRiverConnection(context, center, connections, RiverConnections.East, 0.5, 0, pen);
        DrawRiverConnection(context, center, connections, RiverConnections.South, 0, 0.5, pen);
        DrawRiverConnection(context, center, connections, RiverConnections.West, -0.5, 0, pen);
    }

    private void DrawRiverConnection(
        DrawingContext context,
        Point center,
        RiverConnections connections,
        RiverConnections requiredConnection,
        double xOffset,
        double yOffset,
        IPen pen)
    {
        if ((connections & requiredConnection) == 0)
        {
            return;
        }

        var edge = new Point(center.X + xOffset * _zoom, center.Y + yOffset * _zoom);
        context.DrawLine(pen, center, edge);
    }

    private static RiverConnections GetRenderedRiverConnections(
        CampaignWorld world,
        int x,
        int y)
    {
        var connections = RiverConnections.None;
        var riverNeighborCount = 0;
        var firstWaterConnection = RiverConnections.None;
        foreach (var neighbor in RiverRenderNeighbors)
        {
            var neighborX = x + neighbor.X;
            var neighborY = y + neighbor.Y;
            if (!world.Tiles.IsValidCoordinate(neighborX, neighborY))
            {
                continue;
            }

            var neighborType = world.Tiles.GetTile(neighborX, neighborY).Type;
            if (neighborType.IsRiver())
            {
                connections |= neighbor.Connection;
                riverNeighborCount++;
                continue;
            }

            if (firstWaterConnection == RiverConnections.None &&
                neighborType is CampaignTileType.Water or CampaignTileType.Sea or CampaignTileType.Lake)
            {
                firstWaterConnection = neighbor.Connection;
            }
        }

        return riverNeighborCount >= 2
            ? connections
            : connections | firstWaterConnection;
    }

    private static bool IsLargeRiverGlyph(
        CampaignWorld world,
        int x,
        int y,
        CampaignTileType type)
    {
        if (type == CampaignTileType.LargeRiver)
        {
            return true;
        }

        if (type != CampaignTileType.RiverJunction)
        {
            return false;
        }

        foreach (var neighbor in RiverRenderNeighbors)
        {
            var neighborX = x + neighbor.X;
            var neighborY = y + neighbor.Y;
            if (world.Tiles.IsValidCoordinate(neighborX, neighborY) &&
                world.Tiles.GetTile(neighborX, neighborY).Type == CampaignTileType.LargeRiver)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawElevationNumbers(DrawingContext context, CampaignWorld world)
    {
        if (_zoom < MinimumElevationLabelZoom)
        {
            return;
        }

        var minimumX = Math.Max(0, (int)Math.Floor(_originX));
        var maximumX = Math.Min(
            world.Definition.TilesX - 1,
            (int)Math.Ceiling(_originX + Bounds.Width / _zoom) - 1);
        var minimumY = Math.Max(0, (int)Math.Floor(_originY));
        var maximumY = Math.Min(
            world.Definition.TilesY - 1,
            (int)Math.Ceiling(_originY + Bounds.Height / _zoom) - 1);
        var fontSize = (int)Math.Round(Math.Clamp(_zoom * 0.25, 7, 11));

        for (var y = minimumY; y <= maximumY; y++)
        {
            for (var x = minimumX; x <= maximumX; x++)
            {
                var heightMeters = world.Tiles.GetTile(x, y).HeightMeters;
                var label = GetElevationLabel(heightMeters, fontSize);
                if (label.Foreground.Width > _zoom - 2 || label.Foreground.Height > _zoom - 2)
                {
                    continue;
                }

                var center = new Point(
                    (x + 0.5 - _originX) * _zoom,
                    (y + 0.5 - _originY) * _zoom);
                var origin = new Point(
                    center.X - label.Foreground.Width / 2,
                    center.Y - label.Foreground.Height / 2);

                context.DrawText(label.Outline, origin + new Vector(-1, 0));
                context.DrawText(label.Outline, origin + new Vector(1, 0));
                context.DrawText(label.Outline, origin + new Vector(0, -1));
                context.DrawText(label.Outline, origin + new Vector(0, 1));
                context.DrawText(label.Foreground, origin);
            }
        }
    }

    private void DrawResourcePotentialNumbers(DrawingContext context, CampaignWorld world)
    {
        if (_zoom < MinimumResourcePotentialLabelZoom ||
            _resourceRasterSnapshot is not { } snapshot ||
            !string.Equals(snapshot.ResourceId, SelectedResourceId, StringComparison.Ordinal))
        {
            return;
        }

        var minimumX = Math.Max(0, (int)Math.Floor(_originX));
        var maximumX = Math.Min(
            world.Definition.TilesX - 1,
            (int)Math.Ceiling(_originX + Bounds.Width / _zoom) - 1);
        var minimumY = Math.Max(0, (int)Math.Floor(_originY));
        var maximumY = Math.Min(
            world.Definition.TilesY - 1,
            (int)Math.Ceiling(_originY + Bounds.Height / _zoom) - 1);
        var fontSize = (int)Math.Round(Math.Clamp(_zoom * 0.25, 7, 11));

        for (var y = minimumY; y <= maximumY; y++)
        {
            for (var x = minimumX; x <= maximumX; x++)
            {
                var potential = snapshot.GetPotential(x, y);
                if (potential == 0)
                {
                    continue;
                }

                var label = GetResourcePotentialLabel(potential, fontSize);
                if (label.Foreground.Width > _zoom - 2 || label.Foreground.Height > _zoom - 2)
                {
                    continue;
                }

                var center = new Point(
                    (x + 0.5 - _originX) * _zoom,
                    (y + 0.5 - _originY) * _zoom);
                var origin = new Point(
                    center.X - label.Foreground.Width / 2,
                    center.Y - label.Foreground.Height / 2);

                context.DrawText(label.Outline, origin + new Vector(-1, 0));
                context.DrawText(label.Outline, origin + new Vector(1, 0));
                context.DrawText(label.Outline, origin + new Vector(0, -1));
                context.DrawText(label.Outline, origin + new Vector(0, 1));
                context.DrawText(label.Foreground, origin);
            }
        }
    }

    private void DrawSeasonLabels(DrawingContext context, CampaignWorld world)
    {
        if (_zoom < MinimumSeasonLabelZoom || SeasonMap is not { } seasons)
        {
            return;
        }

        var minimumX = Math.Max(0, (int)Math.Floor(_originX));
        var maximumX = Math.Min(
            world.Definition.TilesX - 1,
            (int)Math.Ceiling(_originX + Bounds.Width / _zoom) - 1);
        var minimumY = Math.Max(0, (int)Math.Floor(_originY));
        var maximumY = Math.Min(
            world.Definition.TilesY - 1,
            (int)Math.Ceiling(_originY + Bounds.Height / _zoom) - 1);
        var fontSize = (int)Math.Round(Math.Clamp(_zoom * 0.24, 7, 11));

        for (var y = minimumY; y <= maximumY; y++)
        {
            for (var x = minimumX; x <= maximumX; x++)
            {
                if (SelectedSeasonId is not { } seasonId ||
                    !seasons.TryGetOccurrence(x, y, seasonId, out var occurrence))
                {
                    continue;
                }

                var definition = seasons.Catalog.Get(seasonId);
                var label = GetSeasonLabel(definition, occurrence.Locked, fontSize);
                if (label.Foreground.Width > _zoom - 2 || label.Foreground.Height > _zoom - 2)
                {
                    continue;
                }

                var center = new Point(
                    (x + 0.5 - _originX) * _zoom,
                    (y + 0.5 - _originY) * _zoom);
                var origin = new Point(
                    center.X - label.Foreground.Width / 2,
                    center.Y - label.Foreground.Height / 2);
                DrawOutlinedLabel(context, label, origin);
            }
        }
    }

    private ElevationLabelText GetElevationLabel(short heightMeters, int fontSize)
    {
        var key = new ElevationLabelKey(heightMeters, fontSize);
        if (_elevationLabelCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_elevationLabelCache.Count >= 2048)
        {
            _elevationLabelCache.Clear();
        }

        var text = heightMeters.ToString("0", CultureInfo.InvariantCulture);
        var label = new ElevationLabelText(
            new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ElevationLabelTypeface,
                fontSize,
                ElevationLabelOutlineBrush),
            new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ElevationLabelTypeface,
                fontSize,
                ElevationLabelTextBrush));
        _elevationLabelCache.Add(key, label);
        return label;
    }

    private ElevationLabelText GetResourcePotentialLabel(byte potential, int fontSize)
    {
        var key = new ResourcePotentialLabelKey(potential, fontSize);
        if (_resourcePotentialLabelCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_resourcePotentialLabelCache.Count >= 512)
        {
            _resourcePotentialLabelCache.Clear();
        }

        var text = potential.ToString(CultureInfo.InvariantCulture);
        var label = new ElevationLabelText(
            new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ElevationLabelTypeface,
                fontSize,
                ElevationLabelOutlineBrush),
            new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ElevationLabelTypeface,
                fontSize,
                ElevationLabelTextBrush));
        _resourcePotentialLabelCache.Add(key, label);
        return label;
    }

    private ElevationLabelText GetSeasonLabel(
        CampaignSeasonDefinition definition,
        bool locked,
        int fontSize)
    {
        var key = new SeasonLabelKey(definition.Id, definition.Name, locked, fontSize);
        if (_seasonLabelCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_seasonLabelCache.Count >= 1_024)
        {
            _seasonLabelCache.Clear();
        }

        var letters = new string(definition.Name
            .Where(char.IsLetterOrDigit)
            .Take(3)
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (letters.Length == 0)
        {
            letters = "SEA";
        }

        var text = locked ? $"{letters} L" : letters;
        var label = new ElevationLabelText(
            new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ElevationLabelTypeface,
                fontSize,
                ElevationLabelOutlineBrush),
            new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ElevationLabelTypeface,
                fontSize,
                ElevationLabelTextBrush));
        _seasonLabelCache.Add(key, label);
        return label;
    }

    private static void DrawOutlinedLabel(
        DrawingContext context,
        ElevationLabelText label,
        Point origin)
    {
        context.DrawText(label.Outline, origin + new Vector(-1, 0));
        context.DrawText(label.Outline, origin + new Vector(1, 0));
        context.DrawText(label.Outline, origin + new Vector(0, -1));
        context.DrawText(label.Outline, origin + new Vector(0, 1));
        context.DrawText(label.Foreground, origin);
    }

    private void DrawStampCursor(DrawingContext context)
    {
        if (_hover is not { } hover)
        {
            return;
        }

        if (IsSeasonWorkspace)
        {
            DrawSeasonStampCursor(context, hover.Coordinate);
            return;
        }

        if (IsResourceWorkspace)
        {
            DrawResourceStampCursor(context, hover.Coordinate);
            return;
        }

        if (World is { } world)
        {
            var area = GetPaintArea(world, hover.Coordinate);
            var rectangle = GetTileAreaScreenRect(area);
            var data = new CampaignTileData(
                SelectedCampaignTileType,
                GetStampHeight(world),
                SelectedCustomTerrainId);
            var canStamp = CanStampArea(world, area, data);
            var cursorPen = canStamp ? TileCursorPen : BlockedTileCursorPen;
            context.DrawRectangle(GetPreviewBrush(world), cursorPen, rectangle);
            var center = new Point(
                (hover.Coordinate.X + 0.5 - _originX) * _zoom,
                (hover.Coordinate.Y + 0.5 - _originY) * _zoom);
            if (SelectedCampaignTileType.IsRiver() && canStamp)
            {
                DrawRiverGlyph(
                    context,
                    center,
                    GetRenderedRiverConnections(world, hover.Coordinate.X, hover.Coordinate.Y),
                    CreateRiverRenderStyle(
                        preview: true,
                        large: SelectedCampaignTileType == CampaignTileType.LargeRiver));
            }

            if (!canStamp)
            {
                context.DrawLine(BlockedTileCursorPen, rectangle.TopLeft, rectangle.BottomRight);
                context.DrawLine(BlockedTileCursorPen, rectangle.TopRight, rectangle.BottomLeft);
            }
        }
    }

    private void DrawResourceStampCursor(
        DrawingContext context,
        CampaignTileCoordinate coordinate)
    {
        if (World is not { } world ||
            !TryGetActiveResource(world, out _, out var definition))
        {
            return;
        }

        var area = CampaignTileArea.Centered(
            world.Definition,
            coordinate,
            EffectiveResourcePaintAreaRadius);
        var rectangle = GetTileAreaScreenRect(area);
        var pen = EraseSelectedResource ? BlockedTileCursorPen : TileCursorPen;
        context.DrawRectangle(GetResourcePreviewBrush(definition), pen, rectangle);
        if (EraseSelectedResource)
        {
            context.DrawLine(pen, rectangle.TopLeft, rectangle.BottomRight);
            context.DrawLine(pen, rectangle.TopRight, rectangle.BottomLeft);
        }
    }

    private void DrawSeasonStampCursor(
        DrawingContext context,
        CampaignTileCoordinate coordinate)
    {
        if (World is not { } world || !TryGetActiveSeason(world, out var seasons))
        {
            return;
        }

        var area = CampaignTileArea.Centered(
            world.Definition,
            coordinate,
            EffectiveSeasonPaintAreaRadius);
        var rectangle = GetTileAreaScreenRect(area);
        IBrush brush;
        if (SeasonPaintTool == CampaignSeasonPaintTool.Paint &&
            seasons.Catalog.TryGet(SelectedSeasonId, out var selected))
        {
            var color = Color.Parse(selected.ColorHex);
            brush = new SolidColorBrush(Color.FromArgb(112, color.R, color.G, color.B));
        }
        else if (SeasonPaintTool == CampaignSeasonPaintTool.Erase)
        {
            brush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        }
        else
        {
            brush = new SolidColorBrush(Color.FromArgb(44, 227, 181, 87));
        }

        context.DrawRectangle(brush, TileCursorPen, rectangle);
    }

    private void DrawSelection(DrawingContext context)
    {
        if (SelectedArea is { } area)
        {
            context.DrawRectangle(
                AreaSelectionBrush,
                SelectionPen,
                GetTileAreaScreenRect(area).Deflate(1.5));
        }

        if (_selectedCoordinate is not { } coordinate)
        {
            return;
        }

        context.DrawRectangle(null, SelectionPen, GetTileScreenRect(coordinate).Deflate(1.5));
    }

    private void DrawKeyboardCursor(DrawingContext context)
    {
        if (!IsKeyboardFocusWithin ||
            World is not { } world ||
            _keyboardCoordinate is not { } coordinate ||
            !world.Tiles.IsValidCoordinate(coordinate.X, coordinate.Y))
        {
            return;
        }

        CampaignTileArea area;
        if (IsSeasonWorkspace)
        {
            area = CampaignTileArea.Centered(
                world.Definition,
                coordinate,
                EffectiveSeasonPaintAreaRadius);
        }
        else if (IsResourceWorkspace)
        {
            area = CampaignTileArea.Centered(
                world.Definition,
                coordinate,
                EffectiveResourcePaintAreaRadius);
        }
        else
        {
            area = GetPaintArea(world, coordinate);
        }

        var rectangle = GetTileAreaScreenRect(area);
        if (rectangle.Width < 6 || rectangle.Height < 6)
        {
            var center = new Point(
                (coordinate.X + 0.5 - _originX) * _zoom,
                (coordinate.Y + 0.5 - _originY) * _zoom);
            rectangle = new Rect(center.X - 3, center.Y - 3, 6, 6);
        }
        else
        {
            rectangle = rectangle.Deflate(1.2);
        }

        context.DrawRectangle(null, KeyboardCursorPen, rectangle);
    }

    private Rect GetTileScreenRect(CampaignTileCoordinate coordinate) =>
        new(
            (coordinate.X - _originX) * _zoom,
            (coordinate.Y - _originY) * _zoom,
            _zoom,
            _zoom);

    private Rect GetTileAreaScreenRect(CampaignTileArea area) =>
        new(
            (area.MinimumX - _originX) * _zoom,
            (area.MinimumY - _originY) * _zoom,
            area.Width * _zoom,
            area.Height * _zoom);

    internal static CampaignTileArea CreateAreaSelection(
        CampaignWorldDefinition definition,
        CampaignTileCoordinate start,
        CampaignTileCoordinate end)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CampaignWorldDefinition.EnsureValid(definition);
        var maximumX = definition.TilesX - 1;
        var maximumY = definition.TilesY - 1;
        var startX = Math.Clamp(start.X, 0, maximumX);
        var startY = Math.Clamp(start.Y, 0, maximumY);
        var endX = Math.Clamp(end.X, 0, maximumX);
        var endY = Math.Clamp(end.Y, 0, maximumY);
        return new CampaignTileArea(
            Math.Min(startX, endX),
            Math.Min(startY, endY),
            Math.Max(startX, endX),
            Math.Max(startY, endY));
    }

    private readonly record struct ElevationLabelKey(short HeightMeters, int FontSize);

    private readonly record struct ResourcePotentialLabelKey(byte Potential, int FontSize);

    private readonly record struct SeasonLabelKey(
        string SeasonId,
        string Name,
        bool Locked,
        int FontSize);

    private sealed record ElevationLabelText(FormattedText Outline, FormattedText Foreground);

    private readonly record struct ResourcePreviewBrushKey(
        string ResourceId,
        string ColorHex,
        bool Erase);

    private sealed record ResourcePreviewBrushCache(ResourcePreviewBrushKey Key, IBrush Brush);

    private static bool CanStampArea(
        CampaignWorld world,
        CampaignTileArea area,
        CampaignTileData data)
    {
        foreach (var coordinate in area.EnumerateCoordinates())
        {
            if (!world.Tiles.CanSetTile(coordinate.X, coordinate.Y, data, out _))
            {
                return false;
            }
        }

        return true;
    }

    private IBrush GetPreviewBrush(CampaignWorld world)
    {
        if (world.Tiles.TryGetCustomTerrainDefinition(SelectedCustomTerrainId, out var definition))
        {
            var color = Color.Parse(definition.ColorHex);
            return new SolidColorBrush(Color.FromArgb(92, color.R, color.G, color.B));
        }

        return PreviewBrushes[SelectedCampaignTileType];
    }

    private IBrush GetResourcePreviewBrush(CampaignResourceDefinition definition)
    {
        var key = new ResourcePreviewBrushKey(definition.Id, definition.ColorHex, EraseSelectedResource);
        if (_resourcePreviewBrushCache is { } cached && cached.Key == key)
        {
            return cached.Brush;
        }

        var color = Color.Parse(definition.ColorHex);
        var brush = new SolidColorBrush(EraseSelectedResource
            ? Color.FromArgb(34, 255, 107, 107)
            : Color.FromArgb(92, color.R, color.G, color.B));
        _resourcePreviewBrushCache = new ResourcePreviewBrushCache(key, brush);
        return brush;
    }

    private void RaiseZoomChanged() =>
        ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(_zoom));

    private void RaiseViewportChanged() =>
        ViewportChanged?.Invoke(
            this,
            new WorldCanvasViewportChangedEventArgs(CaptureViewport()));

    private static void ValidateViewport(WorldCanvasViewport viewport)
    {
        if (!double.IsFinite(viewport.Zoom))
        {
            throw new ArgumentOutOfRangeException(
                nameof(viewport),
                viewport.Zoom,
                "Viewport zoom must be finite.");
        }

        if (!double.IsFinite(viewport.OriginX) || !double.IsFinite(viewport.OriginY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(viewport),
                viewport,
                "Viewport origins must be finite.");
        }
    }

    private void MarkSurfaceBitmapDirty()
    {
        _rasterKey = null;
    }

    private void MarkResourceBitmapDirty()
    {
        _resourceRasterKey = null;
        _resourceRasterSnapshot?.Dispose();
        _resourceRasterSnapshot = null;
    }

    private void MarkSeasonBitmapDirty()
    {
        _seasonRasterKey = null;
        _seasonRasterSnapshot?.Dispose();
        _seasonRasterSnapshot = null;
    }

    private static long GetNiceStride(double minimumStride)
    {
        if (minimumStride <= 1)
        {
            return 1;
        }

        var power = Math.Pow(10, Math.Floor(Math.Log10(minimumStride)));
        var normalized = minimumStride / power;
        var multiplier = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return Math.Max(1, (long)Math.Ceiling(multiplier * power));
    }

    private static long AlignToStride(long value, long stride)
    {
        var remainder = value % stride;
        return remainder == 0 ? value : value + stride - remainder;
    }

    private static double Normalize(double value, double minimum, double maximum) =>
        maximum <= minimum ? 0 : Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    private static double Lerp(double left, double right, double amount) =>
        left + (right - left) * amount;

    private static Rgb Shade(Rgb color, double factor) =>
        new(
            (byte)Math.Clamp(Math.Round(color.R * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.G * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.B * factor), 0, 255));

    private static byte Premultiply(byte component, byte alpha) =>
        (byte)((component * alpha + 127) / byte.MaxValue);

    private static Rgb FromColor(Color color) => new(color.R, color.G, color.B);

    private sealed class ResourceRasterSnapshot : IDisposable
    {
        private const int SamplingPaddingTiles = 1;

        private readonly byte[] _potentials;
        private readonly int _minimumX;
        private readonly int _minimumY;
        private readonly int _width;
        private readonly int _height;
        private bool _disposed;

        private ResourceRasterSnapshot(
            CampaignWorldDefinition definition,
            string resourceId,
            Rgb color,
            byte[] potentials,
            int minimumX,
            int minimumY,
            int width,
            int height)
        {
            Definition = definition;
            ResourceId = resourceId;
            Color = color;
            _potentials = potentials;
            _minimumX = minimumX;
            _minimumY = minimumY;
            _width = width;
            _height = height;
        }

        public CampaignWorldDefinition Definition { get; }

        public string ResourceId { get; }

        public Rgb Color { get; }

        public static ResourceRasterSnapshot Create(
            CampaignResourceMap resources,
            CampaignResourceDefinition definition,
            double viewportWidth,
            double viewportHeight,
            double originX,
            double originY,
            double zoom)
        {
            var worldDefinition = resources.Definition;
            var maximumWorldX = worldDefinition.TilesX - 1;
            var maximumWorldY = worldDefinition.TilesY - 1;
            var minimumX = ClampFloor(originX - SamplingPaddingTiles, maximumWorldX);
            var minimumY = ClampFloor(originY - SamplingPaddingTiles, maximumWorldY);
            var maximumX = ClampCeiling(
                originX + viewportWidth / zoom + SamplingPaddingTiles,
                maximumWorldX);
            var maximumY = ClampCeiling(
                originY + viewportHeight / zoom + SamplingPaddingTiles,
                maximumWorldY);
            var width = checked(maximumX - minimumX + 1);
            var height = checked(maximumY - minimumY + 1);
            var length = checked(width * height);
            var potentials = ArrayPool<byte>.Shared.Rent(length);
            Array.Clear(potentials, 0, length);

            try
            {
                var area = new CampaignTileArea(minimumX, minimumY, maximumX, maximumY);
                foreach (var entry in resources.GetOccurrences(area, definition.Id))
                {
                    var localX = entry.X - minimumX;
                    var localY = entry.Y - minimumY;
                    potentials[localY * width + localX] = entry.Occurrence.Potential;
                }

                return new ResourceRasterSnapshot(
                    worldDefinition,
                    definition.Id,
                    FromColor(Avalonia.Media.Color.Parse(definition.ColorHex)),
                    potentials,
                    minimumX,
                    minimumY,
                    width,
                    height);
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(potentials);
                throw;
            }
        }

        public byte GetPotential(int x, int y)
        {
            var localX = x - _minimumX;
            var localY = y - _minimumY;
            return (uint)localX < (uint)_width && (uint)localY < (uint)_height
                ? _potentials[localY * _width + localX]
                : (byte)0;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ArrayPool<byte>.Shared.Return(_potentials);
        }

        private static int ClampFloor(double value, int maximum) =>
            (int)Math.Floor(Math.Clamp(value, 0, maximum));

        private static int ClampCeiling(double value, int maximum) =>
            (int)Math.Ceiling(Math.Clamp(value, 0, maximum));
    }

    private sealed class SeasonRasterSnapshot : IDisposable
    {
        private const int SamplingPaddingTiles = 1;
        private const ushort AbsentSeasonIndex = ushort.MaxValue;

        private readonly ushort[] _seasonIndexes;
        private readonly Rgb[] _colors;
        private readonly byte[] _alphas;
        private readonly int _minimumX;
        private readonly int _minimumY;
        private readonly int _width;
        private readonly int _height;
        private bool _disposed;

        private SeasonRasterSnapshot(
            CampaignWorldDefinition definition,
            ushort[] seasonIndexes,
            Rgb[] colors,
            byte[] alphas,
            int minimumX,
            int minimumY,
            int width,
            int height)
        {
            Definition = definition;
            _seasonIndexes = seasonIndexes;
            _colors = colors;
            _alphas = alphas;
            _minimumX = minimumX;
            _minimumY = minimumY;
            _width = width;
            _height = height;
        }

        public CampaignWorldDefinition Definition { get; }

        public static SeasonRasterSnapshot Create(
            CampaignSeasonMap seasons,
            string? selectedSeasonId,
            double viewportWidth,
            double viewportHeight,
            double originX,
            double originY,
            double zoom)
        {
            var definition = seasons.Definition;
            var maximumWorldX = definition.TilesX - 1;
            var maximumWorldY = definition.TilesY - 1;
            var minimumX = ClampFloor(originX - SamplingPaddingTiles, maximumWorldX);
            var minimumY = ClampFloor(originY - SamplingPaddingTiles, maximumWorldY);
            var maximumX = ClampCeiling(
                originX + viewportWidth / zoom + SamplingPaddingTiles,
                maximumWorldX);
            var maximumY = ClampCeiling(
                originY + viewportHeight / zoom + SamplingPaddingTiles,
                maximumWorldY);
            var width = checked(maximumX - minimumX + 1);
            var height = checked(maximumY - minimumY + 1);
            var length = checked(width * height);
            var indexes = ArrayPool<ushort>.Shared.Rent(length);
            Array.Fill(indexes, AbsentSeasonIndex, 0, length);
            var colors = seasons.Catalog.Definitions
                .Select(static definition => FromColor(Color.Parse(definition.ColorHex)))
                .ToArray();
            var alphas = seasons.Catalog.Definitions
                .Select(static definition => (byte)Math.Clamp(
                    125 + definition.TintStrengthPercent * 1.1,
                    125,
                    235))
                .ToArray();

            try
            {
                var area = new CampaignTileArea(minimumX, minimumY, maximumX, maximumY);
                if (selectedSeasonId is not null && seasons.Catalog.Contains(selectedSeasonId))
                {
                    var selectedIndex = seasons.Catalog.GetIndex(selectedSeasonId);
                    foreach (var entry in seasons.GetOccurrences(area, selectedSeasonId))
                    {
                        var localX = entry.X - minimumX;
                        var localY = entry.Y - minimumY;
                        indexes[localY * width + localX] = selectedIndex;
                    }
                }

                return new SeasonRasterSnapshot(
                    definition,
                    indexes,
                    colors,
                    alphas,
                    minimumX,
                    minimumY,
                    width,
                    height);
            }
            catch
            {
                ArrayPool<ushort>.Shared.Return(indexes);
                throw;
            }
        }

        public ushort GetSeasonIndex(int x, int y)
        {
            x = Math.Clamp(x, 0, Definition.TilesX - 1);
            y = Math.Clamp(y, 0, Definition.TilesY - 1);
            var localX = x - _minimumX;
            var localY = y - _minimumY;
            if ((uint)localX >= (uint)_width || (uint)localY >= (uint)_height)
            {
                return AbsentSeasonIndex;
            }

            return _seasonIndexes[localY * _width + localX];
        }

        public Rgb GetColor(ushort seasonIndex) => seasonIndex == AbsentSeasonIndex
            ? default
            : _colors[seasonIndex];

        public byte GetAlpha(ushort seasonIndex) => seasonIndex == AbsentSeasonIndex
            ? (byte)0
            : _alphas[seasonIndex];

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ArrayPool<ushort>.Shared.Return(_seasonIndexes);
        }

        private static int ClampFloor(double value, int maximum) =>
            (int)Math.Floor(Math.Clamp(value, 0, maximum));

        private static int ClampCeiling(double value, int maximum) =>
            (int)Math.Ceiling(Math.Clamp(value, 0, maximum));
    }

    private sealed class SurfaceRasterSnapshot : IDisposable
    {
        private const int SamplingPaddingTiles = 2;

        private readonly CampaignTileMap _source;
        private readonly CampaignTileData[] _tiles;
        private readonly int _minimumX;
        private readonly int _minimumY;
        private readonly int _width;
        private readonly int _height;
        private readonly IReadOnlyDictionary<string, Rgb> _customTerrainColors;

        private SurfaceRasterSnapshot(
            CampaignWorldDefinition definition,
            CampaignTileMap source,
            CampaignTileData[] tiles,
            int minimumX,
            int minimumY,
            int width,
            int height,
            IReadOnlyDictionary<string, Rgb> customTerrainColors)
        {
            Definition = definition;
            _source = source;
            _tiles = tiles;
            _minimumX = minimumX;
            _minimumY = minimumY;
            _width = width;
            _height = height;
            _customTerrainColors = customTerrainColors;
        }

        public CampaignWorldDefinition Definition { get; }

        public static SurfaceRasterSnapshot Create(
            CampaignWorld world,
            double viewportWidth,
            double viewportHeight,
            double originX,
            double originY,
            double zoom)
        {
            var definition = world.Definition;
            var maximumWorldX = definition.TilesX - 1;
            var maximumWorldY = definition.TilesY - 1;
            var minimumX = ClampFloor(originX - SamplingPaddingTiles, maximumWorldX);
            var minimumY = ClampFloor(originY - SamplingPaddingTiles, maximumWorldY);
            var maximumX = ClampCeiling(
                originX + viewportWidth / zoom + SamplingPaddingTiles,
                maximumWorldX);
            var maximumY = ClampCeiling(
                originY + viewportHeight / zoom + SamplingPaddingTiles,
                maximumWorldY);
            var width = checked(maximumX - minimumX + 1);
            var height = checked(maximumY - minimumY + 1);
            var tiles = ArrayPool<CampaignTileData>.Shared.Rent(checked(width * height));
            var customTerrainColors = world.Tiles.CustomTerrainDefinitions.ToDictionary(
                static definition => definition.Id,
                static definition => FromColor(Color.Parse(definition.ColorHex)),
                StringComparer.Ordinal);

            try
            {
                for (var y = minimumY; y <= maximumY; y++)
                {
                    var rowOffset = (y - minimumY) * width;
                    for (var x = minimumX; x <= maximumX; x++)
                    {
                        tiles[rowOffset + x - minimumX] = world.Tiles.GetTile(x, y);
                    }
                }

                return new SurfaceRasterSnapshot(
                    definition,
                    world.Tiles,
                    tiles,
                    minimumX,
                    minimumY,
                    width,
                    height,
                    customTerrainColors);
            }
            catch
            {
                ArrayPool<CampaignTileData>.Shared.Return(tiles);
                throw;
            }
        }

        public void Dispose() => ArrayPool<CampaignTileData>.Shared.Return(_tiles);

        public CampaignTileData GetTile(int x, int y)
        {
            x = Math.Clamp(x, 0, Definition.TilesX - 1);
            y = Math.Clamp(y, 0, Definition.TilesY - 1);
            var localX = x - _minimumX;
            var localY = y - _minimumY;
            return (uint)localX < (uint)_width && (uint)localY < (uint)_height
                ? _tiles[localY * _width + localX]
                : _source.GetTile(x, y);
        }

        public bool TryGetCustomTerrainColor(string? id, out Rgb color)
        {
            if (id is not null && _customTerrainColors.TryGetValue(id, out var found))
            {
                color = found;
                return true;
            }

            color = default;
            return false;
        }

        public void GetDerivedSurface(
            double tileSpaceX,
            double tileSpaceY,
            out double heightMeters,
            out double gradeX,
            out double gradeY)
        {
            var centeredX = tileSpaceX - 0.5;
            var centeredY = tileSpaceY - 0.5;
            var floorX = Math.Floor(centeredX);
            var floorY = Math.Floor(centeredY);
            var fractionX = centeredX - floorX;
            var fractionY = centeredY - floorY;
            var x0 = Math.Clamp((int)floorX, 0, Definition.TilesX - 1);
            var y0 = Math.Clamp((int)floorY, 0, Definition.TilesY - 1);
            var x1 = Math.Clamp((int)floorX + 1, 0, Definition.TilesX - 1);
            var y1 = Math.Clamp((int)floorY + 1, 0, Definition.TilesY - 1);

            var topLeft = GetTile(x0, y0).HeightMeters;
            var topRight = GetTile(x1, y0).HeightMeters;
            var bottomLeft = GetTile(x0, y1).HeightMeters;
            var bottomRight = GetTile(x1, y1).HeightMeters;
            var top = Lerp(topLeft, topRight, fractionX);
            var bottom = Lerp(bottomLeft, bottomRight, fractionX);
            heightMeters = Lerp(top, bottom, fractionY);
            gradeX = Lerp(topRight - topLeft, bottomRight - bottomLeft, fractionY) /
                Definition.CampaignTileSizeMeters;
            gradeY = Lerp(bottomLeft - topLeft, bottomRight - topRight, fractionX) /
                Definition.CampaignTileSizeMeters;
        }

        public AutomaticCoastSurfaceMaterial GetAutomaticCoastSurfaceMaterial(
            CampaignTileType currentType,
            int x,
            int y,
            double localX,
            double localY)
        {
            if (currentType.IsWater())
            {
                return AutomaticCoastSurfaceMaterial.Original;
            }

            var closestDistance = double.PositiveInfinity;
            var closestWaterType = CampaignTileType.Unassigned;
            foreach (var neighbor in RiverRenderNeighbors)
            {
                var edgeDistance = neighbor.Connection switch
                {
                    RiverConnections.North => localY,
                    RiverConnections.East => 1 - localX,
                    RiverConnections.South => 1 - localY,
                    RiverConnections.West => localX,
                    _ => double.PositiveInfinity,
                };
                if (edgeDistance >= closestDistance ||
                    edgeDistance >= CampaignTileMap.AutomaticCoastWaterBandFraction)
                {
                    continue;
                }

                var neighborX = x + neighbor.X;
                var neighborY = y + neighbor.Y;
                if ((uint)neighborX >= (uint)Definition.TilesX ||
                    (uint)neighborY >= (uint)Definition.TilesY)
                {
                    continue;
                }

                var waterType = NormalizeWaterType(GetTile(neighborX, neighborY).Type);
                if (waterType is not (CampaignTileType.Sea or CampaignTileType.Lake))
                {
                    continue;
                }

                closestDistance = edgeDistance;
                closestWaterType = waterType;
            }

            if (closestDistance < CampaignTileMap.AutomaticCoastWaterBandFraction)
            {
                return closestWaterType == CampaignTileType.Lake
                    ? AutomaticCoastSurfaceMaterial.Lake
                    : AutomaticCoastSurfaceMaterial.Sea;
            }

            return AutomaticCoastSurfaceMaterial.Original;
        }

        private static int ClampFloor(double value, int maximum) =>
            (int)Math.Floor(Math.Clamp(value, 0, maximum));

        private static int ClampCeiling(double value, int maximum) =>
            (int)Math.Ceiling(Math.Clamp(value, 0, maximum));

        private static CampaignTileType NormalizeWaterType(CampaignTileType type) =>
            type == CampaignTileType.Water ? CampaignTileType.Sea : type;
    }

    private readonly record struct RasterKey(
        long Revision,
        int Width,
        int Height,
        double ViewportWidth,
        double ViewportHeight,
        double OriginX,
        double OriginY,
        double Zoom,
        bool Grayscale);

    private readonly record struct ResourceRasterKey(
        long Revision,
        string ResourceId,
        int Width,
        int Height,
        double ViewportWidth,
        double ViewportHeight,
        double OriginX,
        double OriginY,
        double Zoom);

    private readonly record struct SeasonRasterKey(
        long Revision,
        string SeasonId,
        int Width,
        int Height,
        double ViewportWidth,
        double ViewportHeight,
        double OriginX,
        double OriginY,
        double Zoom,
        bool BlendBoundaries);

    private readonly record struct Rgb(byte R, byte G, byte B);

    private readonly record struct SurfaceAppearance(Rgb Color, SurfaceTexture Texture);

    private enum SurfaceTexture : byte
    {
        Neutral,
        Grass,
        Steppe,
        Forest,
        Hills,
        Rock,
        SeaWater,
        LakeWater,
        Sand,
        Desert,
        Cliff,
    }

    private readonly record struct RiverRenderStyle(
        IPen BankPen,
        IPen WaterPen,
        IPen HighlightPen,
        double BankWidth,
        double WaterWidth,
        double HighlightWidth);

    private readonly record struct RiverRenderStyleKey(bool Grayscale, double Zoom);

    private readonly record struct RiverRenderStyleCache(
        RiverRenderStyleKey Key,
        RiverRenderStyle Style);
}

public sealed class CampaignTilePointerEventArgs(CampaignTilePointerInfo? info) : EventArgs
{
    public CampaignTilePointerInfo? Info { get; } = info;
}

public sealed class CampaignTileAreaSelectedEventArgs(CampaignTileArea area) : EventArgs
{
    public CampaignTileArea Area { get; } = area;
}

public sealed class CampaignTileStrokeEventArgs(
    CampaignTileStampCommand command,
    int blockedRiverTileCount) : EventArgs
{
    public CampaignTileStampCommand Command { get; } = command;

    public int BlockedRiverTileCount { get; } = blockedRiverTileCount;
}

public sealed class CampaignResourceStrokeEventArgs(
    CampaignResourceEditCommand command) : EventArgs
{
    public CampaignResourceEditCommand Command { get; } = command;
}

public sealed class CampaignSeasonStrokeEventArgs(
    CampaignSeasonEditCommand command) : EventArgs
{
    public CampaignSeasonEditCommand Command { get; } = command;
}

public sealed class ZoomChangedEventArgs(double pixelsPerTile) : EventArgs
{
    public double PixelsPerTile { get; } = pixelsPerTile;
}
