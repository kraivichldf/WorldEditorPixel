using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonMap
{
    private readonly ushort[] _seasonIndexes;
    private readonly ulong[] _lockWords;
    private int _lockedTileCount;

    public CampaignSeasonMap(
        CampaignWorldDefinition definition,
        CampaignSeasonCatalog? catalog = null,
        string defaultSeasonId = CampaignSeasonCatalog.SpringId)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CampaignWorldDefinition.EnsureValid(definition);
        if (definition.TileCount > int.MaxValue)
        {
            throw new ArgumentException(
                "Campaign season grids cannot exceed the supported 32-bit dense tile count.",
                nameof(definition));
        }

        Catalog = catalog ?? new CampaignSeasonCatalog();
        if (!Catalog.Contains(defaultSeasonId))
        {
            throw new ArgumentException(
                $"Default season '{defaultSeasonId}' is not present in the season catalog.",
                nameof(defaultSeasonId));
        }

        DefaultSeasonId = defaultSeasonId;
        var tileCount = checked((int)definition.TileCount);
        _seasonIndexes = new ushort[tileCount];
        Array.Fill(_seasonIndexes, Catalog.GetIndex(defaultSeasonId));
        _lockWords = new ulong[checked((int)((tileCount + 63L) / 64))];
    }

    internal static CampaignSeasonMap CreateSnapshot(
        CampaignWorldDefinition definition,
        CampaignSeasonCatalog catalog,
        string defaultSeasonId,
        IReadOnlyList<CampaignSeasonTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var map = new CampaignSeasonMap(definition, catalog, defaultSeasonId);
        if (tiles.Count != map.TileCount)
        {
            throw new ArgumentException(
                $"Season snapshot contains {tiles.Count:N0} tiles; expected {map.TileCount:N0}.",
                nameof(tiles));
        }

        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            tile.EnsureValid(catalog);
            map._seasonIndexes[index] = catalog.GetIndex(tile.SeasonId);
            map.SetLockBit(index, tile.Locked);
        }

        return map;
    }

    public CampaignWorldDefinition Definition { get; }

    public CampaignSeasonCatalog Catalog { get; }

    public string DefaultSeasonId { get; }

    public int TileCount => _seasonIndexes.Length;

    public int LockedTileCount => _lockedTileCount;

    public long Revision { get; private set; }

    public bool IsValidCoordinate(int x, int y) =>
        (uint)x < (uint)Definition.TilesX && (uint)y < (uint)Definition.TilesY;

    public CampaignSeasonTile GetTile(int x, int y)
    {
        var index = GetFlatIndex(x, y);
        return ReadTile(index);
    }

    public IReadOnlyList<CampaignSeasonEntry> GetTiles(CampaignTileArea area)
    {
        EnsureValidArea(area);
        var entries = new CampaignSeasonEntry[checked(area.Width * area.Height)];
        var destination = 0;
        for (var y = area.MinimumY; y <= area.MaximumY; y++)
        {
            for (var x = area.MinimumX; x <= area.MaximumX; x++)
            {
                entries[destination++] = new CampaignSeasonEntry(x, y, GetTile(x, y));
            }
        }

        return entries;
    }

    public IReadOnlyList<CampaignSeasonEntry> GetAllTiles()
    {
        var entries = new CampaignSeasonEntry[_seasonIndexes.Length];
        var destination = 0;
        for (var y = 0; y < Definition.TilesY; y++)
        {
            for (var x = 0; x < Definition.TilesX; x++)
            {
                entries[destination] = new CampaignSeasonEntry(x, y, ReadTile(destination));
                destination++;
            }
        }

        return entries;
    }

    public int GetUsageCount(string seasonId)
    {
        var targetIndex = GetCatalogIndex(seasonId, nameof(seasonId));
        var count = 0;
        foreach (var value in _seasonIndexes)
        {
            if (value == targetIndex)
            {
                count++;
            }
        }

        return count;
    }

    public IReadOnlyDictionary<string, int> GetUsageCounts(IEnumerable<string> seasonIds)
    {
        ArgumentNullException.ThrowIfNull(seasonIds);
        var requested = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var indexToId = new Dictionary<ushort, string>();
        foreach (var seasonId in seasonIds)
        {
            var index = GetCatalogIndex(seasonId, nameof(seasonIds));
            if (!requested.TryAdd(seasonId, 0))
            {
                throw new ArgumentException(
                    $"Season usage query contains '{seasonId}' more than once.",
                    nameof(seasonIds));
            }

            indexToId.Add(index, seasonId);
        }

        foreach (var value in _seasonIndexes)
        {
            if (indexToId.TryGetValue(value, out var seasonId))
            {
                requested[seasonId]++;
            }
        }

        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(requested);
    }

    public bool SetTile(int x, int y, CampaignSeasonTile value) =>
        Apply([new CampaignSeasonMutation(x, y, value)]) > 0;

    public bool Paint(int x, int y, string seasonId, bool locked = true) =>
        SetTile(x, y, new CampaignSeasonTile(seasonId, locked));

    public bool ResetToDefault(int x, int y, bool locked = false) =>
        SetTile(x, y, new CampaignSeasonTile(DefaultSeasonId, locked));

    public bool SetLocked(int x, int y, bool locked)
    {
        var current = GetTile(x, y);
        return SetTile(x, y, current with { Locked = locked });
    }

    public int Apply(IEnumerable<CampaignSeasonMutation> mutations)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        var pending = mutations.ToArray();
        var seen = new HashSet<int>();
        foreach (var mutation in pending)
        {
            var index = GetFlatIndex(mutation.X, mutation.Y);
            mutation.Value.EnsureValid(Catalog);
            if (!seen.Add(index))
            {
                throw new ArgumentException(
                    $"Season tile ({mutation.X}, {mutation.Y}) appears more than once in one update batch.",
                    nameof(mutations));
            }
        }

        var changed = 0;
        foreach (var mutation in pending)
        {
            var index = GetFlatIndex(mutation.X, mutation.Y);
            var previous = ReadTile(index);
            if (previous == mutation.Value)
            {
                continue;
            }

            _seasonIndexes[index] = Catalog.GetIndex(mutation.Value.SeasonId);
            SetLockBit(index, mutation.Value.Locked);
            Revision++;
            changed++;
        }

        return changed;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!Catalog.Contains(DefaultSeasonId))
        {
            errors.Add($"Unknown default season ID '{DefaultSeasonId}'.");
        }

        var countedLocks = 0;
        for (var index = 0; index < _seasonIndexes.Length; index++)
        {
            if (_seasonIndexes[index] >= Catalog.Definitions.Count)
            {
                errors.Add($"Season catalog index {_seasonIndexes[index]} at dense tile {index} is invalid.");
            }

            if (GetLockBit(index))
            {
                countedLocks++;
            }
        }

        if (countedLocks != _lockedTileCount)
        {
            errors.Add(
                $"Season lock count {_lockedTileCount} does not match the dense lock data count {countedLocks}.");
        }

        return errors;
    }

    public void EnsureValid()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new WorldValidationException(errors);
        }
    }

    private CampaignSeasonTile ReadTile(int flatIndex)
    {
        var definition = Catalog.GetByIndex(_seasonIndexes[flatIndex]);
        return new CampaignSeasonTile(definition.Id, GetLockBit(flatIndex));
    }

    private ushort GetCatalogIndex(string seasonId, string parameterName)
    {
        if (!CampaignSeasonDefinition.IsValidIdentifier(seasonId) || !Catalog.Contains(seasonId))
        {
            throw new ArgumentException(
                $"Season query references unknown or invalid season '{seasonId}'.",
                parameterName);
        }

        return Catalog.GetIndex(seasonId);
    }

    private int GetFlatIndex(int x, int y)
    {
        if (!IsValidCoordinate(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Campaign season coordinate ({x}, {y}) is outside 0..{Definition.TilesX - 1}, 0..{Definition.TilesY - 1}.");
        }

        return checked(y * Definition.TilesX + x);
    }

    private void EnsureValidArea(CampaignTileArea area)
    {
        if (!IsValidCoordinate(area.MinimumX, area.MinimumY) ||
            !IsValidCoordinate(area.MaximumX, area.MaximumY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(area),
                $"Campaign season area ({area.MinimumX}, {area.MinimumY}) through " +
                $"({area.MaximumX}, {area.MaximumY}) must lie inside the campaign grid.");
        }
    }

    private bool GetLockBit(int index)
    {
        var wordIndex = index >> 6;
        var mask = 1UL << (index & 63);
        return (_lockWords[wordIndex] & mask) != 0;
    }

    private void SetLockBit(int index, bool locked)
    {
        var wordIndex = index >> 6;
        var mask = 1UL << (index & 63);
        var wasLocked = (_lockWords[wordIndex] & mask) != 0;
        if (wasLocked == locked)
        {
            return;
        }

        if (locked)
        {
            _lockWords[wordIndex] |= mask;
            _lockedTileCount++;
        }
        else
        {
            _lockWords[wordIndex] &= ~mask;
            _lockedTileCount--;
        }
    }
}
