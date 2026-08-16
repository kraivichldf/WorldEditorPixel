using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.V3;

public sealed class CampaignTileMapV3
{
    private readonly Dictionary<long, CampaignTileDataV3> _tiles = [];

    internal CampaignTileMapV3(CampaignWorldDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CampaignWorldDefinition.EnsureValid(definition);
    }

    public CampaignWorldDefinition Definition { get; }

    public long Revision { get; private set; }

    public int MaterializedTileCount => _tiles.Count;

    public CampaignTileDataV3 DefaultTile => new(
        CampaignSurfaceType.Unassigned,
        Definition.DefaultTileHeightMeters);

    public bool IsValidCoordinate(int x, int y) =>
        (uint)x < (uint)Definition.TilesX && (uint)y < (uint)Definition.TilesY;

    public CampaignTileDataV3 GetTile(int x, int y)
    {
        EnsureValidCoordinate(x, y);
        return GetTileUnchecked(x, y);
    }

    public IEnumerable<CampaignTileEntryV3> GetMaterializedTiles()
    {
        foreach (var (key, data) in _tiles)
        {
            yield return new CampaignTileEntryV3((int)(uint)key, (int)(key >> 32), data);
        }
    }

    public TerrainForm GetTerrainForm(int x, int y, TerrainFormProfile? profile = null) =>
        TerrainFormProjector.Project(this, x, y, profile ?? TerrainFormProfile.Default);

    public TerrainFormAnalysis AnalyzeTerrainForm(
        int x,
        int y,
        TerrainFormProfile? profile = null) =>
        TerrainFormProjector.Analyze(this, x, y, profile ?? TerrainFormProfile.Default);

    public double GetDerivedHeight(double tileSpaceX, double tileSpaceY)
    {
        if (!double.IsFinite(tileSpaceX) || !double.IsFinite(tileSpaceY) ||
            tileSpaceX < 0 || tileSpaceY < 0 ||
            tileSpaceX > Definition.TilesX || tileSpaceY > Definition.TilesY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tileSpaceX),
                "Tile-space position must lie inside the world bounds.");
        }

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

        var top = Lerp(
            GetTileUnchecked(x0, y0).HeightMeters,
            GetTileUnchecked(x1, y0).HeightMeters,
            fractionX);
        var bottom = Lerp(
            GetTileUnchecked(x0, y1).HeightMeters,
            GetTileUnchecked(x1, y1).HeightMeters,
            fractionX);
        return Lerp(top, bottom, fractionY);
    }

    internal int SetTiles(IEnumerable<CampaignTileEntryV3> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var updates = new Dictionary<long, CampaignTileDataV3>();
        foreach (var entry in entries)
        {
            EnsureValidCoordinate(entry.X, entry.Y);
            EnsureValidData(entry.Data);
            if (!updates.TryAdd(GetKey(entry.X, entry.Y), entry.Data))
            {
                throw new ArgumentException(
                    $"Campaign tile ({entry.X}, {entry.Y}) appears more than once in one update batch.",
                    nameof(entries));
            }
        }

        var changed = 0;
        foreach (var (key, data) in updates)
        {
            var previous = _tiles.GetValueOrDefault(key, DefaultTile);
            if (previous == data)
            {
                continue;
            }

            if (data == DefaultTile)
            {
                _tiles.Remove(key);
            }
            else
            {
                _tiles[key] = data;
            }

            Revision++;
            changed++;
        }

        return changed;
    }

    internal void EnsureValidData(CampaignTileDataV3 data)
    {
        if (!Enum.IsDefined(data.Surface))
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                data.Surface,
                "Unknown campaign surface type.");
        }

        if (data.HeightMeters < Definition.MinimumHeightMeters ||
            data.HeightMeters > Definition.MaximumHeightMeters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                data.HeightMeters,
                $"Tile height must be between {Definition.MinimumHeightMeters} and {Definition.MaximumHeightMeters} metres.");
        }
    }

    internal void EnsureValidCoordinate(int x, int y)
    {
        if (!IsValidCoordinate(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Campaign tile ({x}, {y}) is outside 0..{Definition.TilesX - 1}, 0..{Definition.TilesY - 1}.");
        }
    }

    internal CampaignTileDataV3 GetTileUnchecked(int x, int y) =>
        _tiles.GetValueOrDefault(GetKey(x, y), DefaultTile);

    internal static long GetKey(int x, int y) => ((long)y << 32) | (uint)x;

    internal static (int X, int Y) GetCoordinate(long key) =>
        ((int)(uint)key, (int)(key >> 32));

    private static double Lerp(double left, double right, double amount) =>
        left + (right - left) * amount;
}
