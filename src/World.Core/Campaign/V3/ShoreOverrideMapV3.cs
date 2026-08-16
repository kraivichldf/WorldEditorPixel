namespace Kingdom.World.Core.Campaign.V3;

public sealed class ShoreOverrideMapV3
{
    private readonly CampaignTileMapV3 _tiles;
    private readonly TerrainFormProfile _terrainFormProfile;
    private readonly Dictionary<ShoreEdgeKey, ShoreStyle> _overrides = [];

    internal ShoreOverrideMapV3(
        CampaignTileMapV3 tiles,
        TerrainFormProfile terrainFormProfile)
    {
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        _terrainFormProfile = terrainFormProfile ??
            throw new ArgumentNullException(nameof(terrainFormProfile));
        _terrainFormProfile.EnsureValid();
    }

    public long Revision { get; private set; }

    public int OverrideCount => _overrides.Count;

    public ShoreStyle GetOverride(int x, int y, CardinalDirection edge)
    {
        EnsureValidRequest(x, y, edge);
        return _overrides.GetValueOrDefault(new ShoreEdgeKey(x, y, edge), ShoreStyle.Auto);
    }

    public ShoreStyle GetEffectiveStyle(int x, int y, CardinalDirection edge)
    {
        EnsureValidRequest(x, y, edge);
        if (!TryValidateShoreEdge(x, y, edge, out var failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }

        var overrideStyle = GetOverride(x, y, edge);
        if (overrideStyle != ShoreStyle.Auto)
        {
            return overrideStyle;
        }

        var neighborX = x + edge.OffsetX();
        var neighborY = y + edge.OffsetY();
        var landHeight = _tiles.GetTileUnchecked(x, y).HeightMeters;
        var waterHeight = _tiles.GetTileUnchecked(neighborX, neighborY).HeightMeters;
        var waterFacingGrade = Math.Abs(landHeight - waterHeight) /
                               (double)_tiles.Definition.CampaignTileSizeMeters;
        return waterFacingGrade >= _terrainFormProfile.CliffMinimumGrade
            ? ShoreStyle.Cliff
            : ShoreStyle.Beach;
    }

    public IEnumerable<ShoreEdgeOverrideV3> GetOverrides()
    {
        foreach (var (key, style) in _overrides)
        {
            yield return new ShoreEdgeOverrideV3(key.X, key.Y, key.Edge, style);
        }
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        foreach (var (key, _) in _overrides)
        {
            if (!TryValidateShoreEdge(key.X, key.Y, key.Edge, out var failureReason))
            {
                errors.Add(failureReason!);
            }
        }

        return errors;
    }

    internal bool SetOverride(
        int x,
        int y,
        CardinalDirection edge,
        ShoreStyle style)
    {
        EnsureValidRequest(x, y, edge);
        if (!Enum.IsDefined(style))
        {
            throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown shore style.");
        }

        var key = new ShoreEdgeKey(x, y, edge);
        if (style == ShoreStyle.Auto)
        {
            if (!_overrides.Remove(key))
            {
                return false;
            }

            Revision++;
            return true;
        }

        if (!TryValidateShoreEdge(x, y, edge, out var failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }

        if (_overrides.GetValueOrDefault(key, ShoreStyle.Auto) == style)
        {
            return false;
        }

        _overrides[key] = style;
        Revision++;
        return true;
    }

    internal int RemoveInvalidOverrides()
    {
        var invalidKeys = _overrides.Keys
            .Where(key => !TryValidateShoreEdge(key.X, key.Y, key.Edge, out _))
            .ToArray();
        foreach (var key in invalidKeys)
        {
            _overrides.Remove(key);
            Revision++;
        }

        return invalidKeys.Length;
    }

    private bool TryValidateShoreEdge(
        int x,
        int y,
        CardinalDirection edge,
        out string? failureReason)
    {
        var land = _tiles.GetTileUnchecked(x, y);
        if (!land.Surface.IsLand())
        {
            failureReason =
                $"Shore override at ({x}, {y}) {edge} requires a land surface; " +
                $"found {land.Surface}.";
            return false;
        }

        var neighborX = x + edge.OffsetX();
        var neighborY = y + edge.OffsetY();
        if (!_tiles.IsValidCoordinate(neighborX, neighborY))
        {
            failureReason =
                $"Shore override at ({x}, {y}) {edge} must face an in-world Sea or Lake tile.";
            return false;
        }

        var water = _tiles.GetTileUnchecked(neighborX, neighborY);
        if (!water.Surface.IsWater())
        {
            failureReason =
                $"Shore override at ({x}, {y}) {edge} must face Sea or Lake; " +
                $"found {water.Surface} at ({neighborX}, {neighborY}).";
            return false;
        }

        failureReason = null;
        return true;
    }

    private void EnsureValidRequest(int x, int y, CardinalDirection edge)
    {
        _tiles.EnsureValidCoordinate(x, y);
        if (!Enum.IsDefined(edge))
        {
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "Unknown cardinal direction.");
        }
    }

    private readonly record struct ShoreEdgeKey(
        int X,
        int Y,
        CardinalDirection Edge);
}
