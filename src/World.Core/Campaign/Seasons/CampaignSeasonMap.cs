using System.Collections.ObjectModel;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonMap
{
    private readonly Dictionary<long, Dictionary<string, CampaignSeasonOccurrence>> _tiles = [];
    private int _occurrenceCount;
    private int _lockedOccurrenceCount;

    public CampaignSeasonMap(
        CampaignWorldDefinition definition,
        CampaignSeasonCatalog? catalog = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CampaignWorldDefinition.EnsureValid(definition);
        Catalog = catalog ?? new CampaignSeasonCatalog();
    }

    internal static CampaignSeasonMap CreateSnapshot(
        CampaignWorldDefinition definition,
        CampaignSeasonCatalog catalog,
        IEnumerable<CampaignSeasonEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var map = new CampaignSeasonMap(definition, catalog);
        map.Apply(entries.Select(static entry =>
            CampaignSeasonMutation.Upsert(entry.X, entry.Y, entry.Occurrence)));
        map.Revision = 0;
        return map;
    }

    public CampaignWorldDefinition Definition { get; }

    public CampaignSeasonCatalog Catalog { get; }

    public long Revision { get; private set; }

    public int MaterializedTileCount => _tiles.Count;

    public int OccurrenceCount => _occurrenceCount;

    public int LockedOccurrenceCount => _lockedOccurrenceCount;

    public long TileCount => Definition.TileCount;

    public bool IsValidCoordinate(int x, int y) =>
        (uint)x < (uint)Definition.TilesX && (uint)y < (uint)Definition.TilesY;

    public bool TryGetOccurrence(
        int x,
        int y,
        string seasonId,
        out CampaignSeasonOccurrence occurrence)
    {
        EnsureValidCoordinate(x, y);
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        if (_tiles.TryGetValue(GetTileKey(x, y), out var tile) &&
            tile.TryGetValue(seasonId, out var found))
        {
            occurrence = found;
            return true;
        }

        occurrence = default;
        return false;
    }

    public IReadOnlyList<CampaignSeasonOccurrence> GetOccurrences(int x, int y)
    {
        EnsureValidCoordinate(x, y);
        return _tiles.TryGetValue(GetTileKey(x, y), out var tile)
            ? tile.Values.OrderBy(static value => value.SeasonId, StringComparer.Ordinal).ToArray()
            : [];
    }

    public IReadOnlyList<CampaignSeasonEntry> GetOccurrences(
        CampaignTileArea area,
        string? seasonId = null)
    {
        EnsureValidArea(area);
        if (seasonId is not null)
        {
            EnsureKnownSeasonId(seasonId, nameof(seasonId));
        }

        var areaTileCount = (long)area.Width * area.Height;
        return _tiles.Count <= areaTileCount
            ? GetOccurrencesBySparseFiltering(area, seasonId)
            : GetOccurrencesByCoordinateTraversal(area, seasonId);
    }

    public IReadOnlyList<CampaignSeasonEntry> GetMaterializedOccurrences() =>
        _tiles
            .SelectMany(static pair =>
            {
                var (x, y) = GetCoordinate(pair.Key);
                return pair.Value.Values.Select(value => new CampaignSeasonEntry(x, y, value));
            })
            .OrderBy(static entry => entry.Y)
            .ThenBy(static entry => entry.X)
            .ThenBy(static entry => entry.Occurrence.SeasonId, StringComparer.Ordinal)
            .ToArray();

    public int GetUsageCount(string seasonId)
    {
        EnsureKnownSeasonId(seasonId, nameof(seasonId));
        return _tiles.Values.Count(tile => tile.ContainsKey(seasonId));
    }

    public IReadOnlyDictionary<string, int> GetUsageCounts(IEnumerable<string> seasonIds)
    {
        ArgumentNullException.ThrowIfNull(seasonIds);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var seasonId in seasonIds)
        {
            EnsureKnownSeasonId(seasonId, nameof(seasonIds));
            if (!counts.TryAdd(seasonId, 0))
            {
                throw new ArgumentException(
                    $"Season usage query contains '{seasonId}' more than once.",
                    nameof(seasonIds));
            }
        }

        foreach (var tile in _tiles.Values)
        {
            foreach (var seasonId in tile.Keys)
            {
                if (counts.TryGetValue(seasonId, out var count))
                {
                    counts[seasonId] = count + 1;
                }
            }
        }

        return new ReadOnlyDictionary<string, int>(counts);
    }

    public bool Upsert(int x, int y, CampaignSeasonOccurrence occurrence) =>
        Apply([CampaignSeasonMutation.Upsert(x, y, occurrence)]) > 0;

    public bool Remove(int x, int y, string seasonId) =>
        Apply([CampaignSeasonMutation.Remove(x, y, seasonId)]) > 0;

    public bool SetLocked(int x, int y, string seasonId, bool locked)
    {
        if (!TryGetOccurrence(x, y, seasonId, out var current))
        {
            return false;
        }

        return Upsert(x, y, current with { Locked = locked });
    }

    public int Apply(IEnumerable<CampaignSeasonMutation> mutations)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        var pending = mutations.ToArray();
        var seen = new HashSet<(long TileKey, string SeasonId)>();
        foreach (var mutation in pending)
        {
            EnsureValidCoordinate(mutation.X, mutation.Y);
            EnsureKnownSeasonId(mutation.SeasonId, nameof(mutations));
            if (mutation.Value is { } value)
            {
                value.EnsureValid();
                if (!string.Equals(value.SeasonId, mutation.SeasonId, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Season mutation identity does not match its occurrence value.",
                        nameof(mutations));
                }
            }

            var identity = (GetTileKey(mutation.X, mutation.Y), mutation.SeasonId);
            if (!seen.Add(identity))
            {
                throw new ArgumentException(
                    $"Season '{mutation.SeasonId}' at ({mutation.X}, {mutation.Y}) appears more than once in one update batch.",
                    nameof(mutations));
            }
        }

        var changed = 0;
        foreach (var mutation in pending)
        {
            var tileKey = GetTileKey(mutation.X, mutation.Y);
            if (mutation.Value is null)
            {
                if (!_tiles.TryGetValue(tileKey, out var existingTile) ||
                    !existingTile.Remove(mutation.SeasonId, out var removed))
                {
                    continue;
                }

                _occurrenceCount--;
                if (removed.Locked)
                {
                    _lockedOccurrenceCount--;
                }

                if (existingTile.Count == 0)
                {
                    _tiles.Remove(tileKey);
                }
            }
            else
            {
                var value = mutation.Value.Value;
                if (!_tiles.TryGetValue(tileKey, out var tile))
                {
                    tile = new Dictionary<string, CampaignSeasonOccurrence>(StringComparer.Ordinal);
                    _tiles.Add(tileKey, tile);
                }

                if (tile.TryGetValue(mutation.SeasonId, out var previous))
                {
                    if (previous == value)
                    {
                        continue;
                    }

                    if (previous.Locked != value.Locked)
                    {
                        _lockedOccurrenceCount += value.Locked ? 1 : -1;
                    }
                }
                else
                {
                    _occurrenceCount++;
                    if (value.Locked)
                    {
                        _lockedOccurrenceCount++;
                    }
                }

                tile[mutation.SeasonId] = value;
            }

            Revision++;
            changed++;
        }

        return changed;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var countedOccurrences = 0;
        var countedLocks = 0;
        foreach (var entry in GetMaterializedOccurrences())
        {
            countedOccurrences++;
            if (entry.Occurrence.Locked)
            {
                countedLocks++;
            }

            if (!IsValidCoordinate(entry.X, entry.Y))
            {
                errors.Add($"Season coordinate ({entry.X}, {entry.Y}) is outside the campaign grid.");
            }

            if (!Catalog.Contains(entry.Occurrence.SeasonId))
            {
                errors.Add($"Unknown season ID '{entry.Occurrence.SeasonId}'.");
            }

            try
            {
                entry.Occurrence.EnsureValid();
            }
            catch (ArgumentException exception)
            {
                errors.Add(exception.Message);
            }
        }

        if (countedOccurrences != _occurrenceCount)
        {
            errors.Add(
                $"Season occurrence count {_occurrenceCount} does not match materialized count {countedOccurrences}.");
        }

        if (countedLocks != _lockedOccurrenceCount)
        {
            errors.Add(
                $"Season lock count {_lockedOccurrenceCount} does not match materialized count {countedLocks}.");
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

    private IReadOnlyList<CampaignSeasonEntry> GetOccurrencesBySparseFiltering(
        CampaignTileArea area,
        string? seasonId)
    {
        var entries = new List<CampaignSeasonEntry>();
        foreach (var (tileKey, tile) in _tiles)
        {
            var (x, y) = GetCoordinate(tileKey);
            if (x < area.MinimumX || x > area.MaximumX ||
                y < area.MinimumY || y > area.MaximumY)
            {
                continue;
            }

            if (seasonId is not null)
            {
                if (tile.TryGetValue(seasonId, out var occurrence))
                {
                    entries.Add(new CampaignSeasonEntry(x, y, occurrence));
                }

                continue;
            }

            entries.AddRange(tile.Values.Select(value =>
                new CampaignSeasonEntry(x, y, value)));
        }

        return entries
            .OrderBy(static entry => entry.Y)
            .ThenBy(static entry => entry.X)
            .ThenBy(static entry => entry.Occurrence.SeasonId, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<CampaignSeasonEntry> GetOccurrencesByCoordinateTraversal(
        CampaignTileArea area,
        string? seasonId)
    {
        var entries = new List<CampaignSeasonEntry>();
        for (var y = area.MinimumY; y <= area.MaximumY; y++)
        {
            for (var x = area.MinimumX; x <= area.MaximumX; x++)
            {
                if (!_tiles.TryGetValue(GetTileKey(x, y), out var tile))
                {
                    continue;
                }

                if (seasonId is not null)
                {
                    if (tile.TryGetValue(seasonId, out var occurrence))
                    {
                        entries.Add(new CampaignSeasonEntry(x, y, occurrence));
                    }

                    continue;
                }

                foreach (var occurrence in tile.Values.OrderBy(
                             static value => value.SeasonId,
                             StringComparer.Ordinal))
                {
                    entries.Add(new CampaignSeasonEntry(x, y, occurrence));
                }
            }
        }

        return entries;
    }

    private void EnsureKnownSeasonId(string seasonId, string parameterName)
    {
        if (!CampaignSeasonDefinition.IsValidIdentifier(seasonId) || !Catalog.Contains(seasonId))
        {
            throw new ArgumentException(
                $"Season query references unknown or invalid season '{seasonId}'.",
                parameterName);
        }
    }

    private void EnsureValidCoordinate(int x, int y)
    {
        if (!IsValidCoordinate(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Campaign season coordinate ({x}, {y}) is outside 0..{Definition.TilesX - 1}, 0..{Definition.TilesY - 1}.");
        }
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

    private static long GetTileKey(int x, int y) => ((long)y << 32) | (uint)x;

    private static (int X, int Y) GetCoordinate(long key) =>
        ((int)(uint)key, (int)(key >> 32));
}
