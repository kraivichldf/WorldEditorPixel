using System.Collections.ObjectModel;
using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceMap
{
    private readonly Dictionary<long, Dictionary<string, CampaignResourceOccurrence>> _tiles = [];
    private int _occurrenceCount;

    public CampaignResourceMap(
        CampaignWorldDefinition definition,
        CampaignResourceCatalog? catalog = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CampaignWorldDefinition.EnsureValid(definition);
        Catalog = catalog ?? new CampaignResourceCatalog();
    }

    public CampaignWorldDefinition Definition { get; }

    public CampaignResourceCatalog Catalog { get; }

    public long Revision { get; private set; }

    public int MaterializedTileCount => _tiles.Count;

    public int OccurrenceCount => _occurrenceCount;

    public bool IsValidCoordinate(int x, int y) =>
        (uint)x < (uint)Definition.TilesX && (uint)y < (uint)Definition.TilesY;

    public bool TryGetOccurrence(
        int x,
        int y,
        string resourceId,
        out CampaignResourceOccurrence occurrence)
    {
        EnsureValidCoordinate(x, y);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        if (_tiles.TryGetValue(GetTileKey(x, y), out var tile) &&
            tile.TryGetValue(resourceId, out var found))
        {
            occurrence = found;
            return true;
        }

        occurrence = default;
        return false;
    }

    public IReadOnlyList<CampaignResourceOccurrence> GetOccurrences(int x, int y)
    {
        EnsureValidCoordinate(x, y);
        return _tiles.TryGetValue(GetTileKey(x, y), out var tile)
            ? tile.Values.OrderBy(static value => value.ResourceId, StringComparer.Ordinal).ToArray()
            : [];
    }

    /// <summary>
    /// Returns the materialized occurrences inside a bounded campaign-tile area.
    /// Supplying a resource ID limits the result to that one resource. The query
    /// chooses between sparse filtering and coordinate lookup so its work scales
    /// with the smaller of the materialized map and requested area.
    /// </summary>
    public IReadOnlyList<CampaignResourceEntry> GetOccurrences(
        CampaignTileArea area,
        string? resourceId = null)
    {
        EnsureValidArea(area);
        if (resourceId is not null)
        {
            if (!CampaignResourceDefinition.IsValidIdentifier(resourceId))
            {
                throw new ArgumentException("Resource area query has an invalid resource ID.", nameof(resourceId));
            }

            if (!Catalog.Contains(resourceId))
            {
                throw new ArgumentException(
                    $"Resource area query references unknown resource '{resourceId}'.",
                    nameof(resourceId));
            }
        }

        var areaTileCount = (long)area.Width * area.Height;
        return _tiles.Count <= areaTileCount
            ? GetOccurrencesBySparseFiltering(area, resourceId)
            : GetOccurrencesByCoordinateTraversal(area, resourceId);
    }

    public IReadOnlyList<CampaignResourceEntry> GetMaterializedOccurrences() =>
        _tiles
            .SelectMany(static pair =>
            {
                var (x, y) = GetCoordinate(pair.Key);
                return pair.Value.Values.Select(value => new CampaignResourceEntry(x, y, value));
            })
            .OrderBy(static entry => entry.Y)
            .ThenBy(static entry => entry.X)
            .ThenBy(static entry => entry.Occurrence.ResourceId, StringComparer.Ordinal)
            .ToArray();

    public int GetUsageCount(string resourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        return _tiles.Values.Count(tile => tile.ContainsKey(resourceId));
    }

    public IReadOnlyDictionary<string, int> GetUsageCounts(IEnumerable<string> resourceIds)
    {
        ArgumentNullException.ThrowIfNull(resourceIds);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var resourceId in resourceIds)
        {
            if (!CampaignResourceDefinition.IsValidIdentifier(resourceId) || !Catalog.Contains(resourceId))
            {
                throw new ArgumentException(
                    $"Resource usage query references unknown or invalid resource '{resourceId}'.",
                    nameof(resourceIds));
            }

            if (!counts.TryAdd(resourceId, 0))
            {
                throw new ArgumentException(
                    $"Resource usage query contains '{resourceId}' more than once.",
                    nameof(resourceIds));
            }
        }

        if (counts.Count == 0)
        {
            return new ReadOnlyDictionary<string, int>(counts);
        }

        foreach (var tile in _tiles.Values)
        {
            foreach (var resourceId in tile.Keys)
            {
                if (counts.TryGetValue(resourceId, out var count))
                {
                    counts[resourceId] = count + 1;
                }
            }
        }

        return new ReadOnlyDictionary<string, int>(counts);
    }

    public bool Upsert(int x, int y, CampaignResourceOccurrence occurrence) =>
        Apply([CampaignResourceMutation.Upsert(x, y, occurrence)]) > 0;

    public bool Remove(int x, int y, string resourceId) =>
        Apply([CampaignResourceMutation.Remove(x, y, resourceId)]) > 0;

    public int Apply(IEnumerable<CampaignResourceMutation> mutations)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        var pending = mutations.ToArray();
        var seen = new HashSet<(long TileKey, string ResourceId)>();
        foreach (var mutation in pending)
        {
            EnsureValidCoordinate(mutation.X, mutation.Y);
            if (!CampaignResourceDefinition.IsValidIdentifier(mutation.ResourceId))
            {
                throw new ArgumentException(
                    $"Resource mutation at ({mutation.X}, {mutation.Y}) has an invalid resource ID.",
                    nameof(mutations));
            }

            if (!Catalog.Contains(mutation.ResourceId))
            {
                throw new ArgumentException(
                    $"Resource mutation references unknown resource '{mutation.ResourceId}'.",
                    nameof(mutations));
            }

            if (mutation.Value is { } value)
            {
                value.EnsureValid();
                if (!string.Equals(value.ResourceId, mutation.ResourceId, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Resource mutation identity does not match its occurrence value.",
                        nameof(mutations));
                }
            }

            var identity = (GetTileKey(mutation.X, mutation.Y), mutation.ResourceId);
            if (!seen.Add(identity))
            {
                throw new ArgumentException(
                    $"Resource '{mutation.ResourceId}' at ({mutation.X}, {mutation.Y}) appears more than once in one update batch.",
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
                    !existingTile.Remove(mutation.ResourceId))
                {
                    continue;
                }

                _occurrenceCount--;
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
                    tile = new Dictionary<string, CampaignResourceOccurrence>(StringComparer.Ordinal);
                    _tiles.Add(tileKey, tile);
                }

                if (tile.TryGetValue(mutation.ResourceId, out var previous) && previous == value)
                {
                    continue;
                }

                if (!tile.ContainsKey(mutation.ResourceId))
                {
                    _occurrenceCount++;
                }

                tile[mutation.ResourceId] = value;
            }

            Revision++;
            changed++;
        }

        return changed;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        foreach (var entry in GetMaterializedOccurrences())
        {
            if (!IsValidCoordinate(entry.X, entry.Y))
            {
                errors.Add($"Resource coordinate ({entry.X}, {entry.Y}) is outside the campaign grid.");
            }

            if (!Catalog.Contains(entry.Occurrence.ResourceId))
            {
                errors.Add($"Unknown resource ID '{entry.Occurrence.ResourceId}'.");
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

    private void EnsureValidCoordinate(int x, int y)
    {
        if (!IsValidCoordinate(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Campaign resource coordinate ({x}, {y}) is outside 0..{Definition.TilesX - 1}, 0..{Definition.TilesY - 1}.");
        }
    }

    private IReadOnlyList<CampaignResourceEntry> GetOccurrencesBySparseFiltering(
        CampaignTileArea area,
        string? resourceId)
    {
        var entries = new List<CampaignResourceEntry>();
        foreach (var (tileKey, tile) in _tiles)
        {
            var (x, y) = GetCoordinate(tileKey);
            if (x < area.MinimumX || x > area.MaximumX ||
                y < area.MinimumY || y > area.MaximumY)
            {
                continue;
            }

            if (resourceId is not null)
            {
                if (tile.TryGetValue(resourceId, out var occurrence))
                {
                    entries.Add(new CampaignResourceEntry(x, y, occurrence));
                }

                continue;
            }

            entries.AddRange(tile.Values.Select(value =>
                new CampaignResourceEntry(x, y, value)));
        }

        return entries
            .OrderBy(static entry => entry.Y)
            .ThenBy(static entry => entry.X)
            .ThenBy(static entry => entry.Occurrence.ResourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<CampaignResourceEntry> GetOccurrencesByCoordinateTraversal(
        CampaignTileArea area,
        string? resourceId)
    {
        var entries = new List<CampaignResourceEntry>();
        for (var y = area.MinimumY; y <= area.MaximumY; y++)
        {
            for (var x = area.MinimumX; x <= area.MaximumX; x++)
            {
                if (!_tiles.TryGetValue(GetTileKey(x, y), out var tile))
                {
                    continue;
                }

                if (resourceId is not null)
                {
                    if (tile.TryGetValue(resourceId, out var occurrence))
                    {
                        entries.Add(new CampaignResourceEntry(x, y, occurrence));
                    }

                    continue;
                }

                foreach (var occurrence in tile.Values.OrderBy(
                             static value => value.ResourceId,
                             StringComparer.Ordinal))
                {
                    entries.Add(new CampaignResourceEntry(x, y, occurrence));
                }
            }
        }

        return entries;
    }

    private void EnsureValidArea(CampaignTileArea area)
    {
        if (!IsValidCoordinate(area.MinimumX, area.MinimumY) ||
            !IsValidCoordinate(area.MaximumX, area.MaximumY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(area),
                $"Campaign resource area ({area.MinimumX}, {area.MinimumY}) through " +
                $"({area.MaximumX}, {area.MaximumY}) must lie inside the campaign grid.");
        }
    }

    private static long GetTileKey(int x, int y) => ((long)y << 32) | (uint)x;

    private static (int X, int Y) GetCoordinate(long key) =>
        ((int)(uint)key, (int)(key >> 32));
}
