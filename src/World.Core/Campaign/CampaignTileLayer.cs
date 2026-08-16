using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign;

public sealed class CampaignTileLayer
{
    private readonly Dictionary<long, CampaignTileType> _assignments = [];

    public CampaignTileLayer(WorldDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public WorldDefinition Definition { get; }

    public long Revision { get; private set; }

    public int AssignedTileCount => _assignments.Count;

    public long TilesX => Definition.CampaignTilesX;

    public long TilesY => Definition.CampaignTilesY;

    public bool IsValidCoordinate(int x, int y) =>
        x >= 0 && y >= 0 && (long)x < TilesX && (long)y < TilesY;

    public CampaignTileType GetTileType(int x, int y)
    {
        EnsureValidCoordinate(x, y);
        return _assignments.GetValueOrDefault(GetKey(x, y), CampaignTileType.Unassigned);
    }

    public bool SetTileType(int x, int y, CampaignTileType type)
    {
        EnsureValidCoordinate(x, y);
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown campaign tile type.");
        }

        var key = GetKey(x, y);
        var previous = _assignments.GetValueOrDefault(key, CampaignTileType.Unassigned);
        if (previous == type)
        {
            return false;
        }

        if (type == CampaignTileType.Unassigned)
        {
            _assignments.Remove(key);
        }
        else
        {
            _assignments[key] = type;
        }

        Revision++;
        return true;
    }

    public IEnumerable<CampaignTileAssignment> GetAssignedTiles()
    {
        foreach (var (key, type) in _assignments)
        {
            yield return new CampaignTileAssignment((int)(uint)key, (int)(key >> 32), type);
        }
    }

    private static long GetKey(int x, int y) => ((long)y << 32) | (uint)x;

    private void EnsureValidCoordinate(int x, int y)
    {
        if (!IsValidCoordinate(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Campaign tile ({x}, {y}) is outside 0..{TilesX - 1}, 0..{TilesY - 1}.");
        }
    }
}
