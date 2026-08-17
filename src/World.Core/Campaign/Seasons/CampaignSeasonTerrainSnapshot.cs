using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonTerrainSnapshot
{
    private readonly CampaignSeasonTerrainSample[] _samples;

    internal CampaignSeasonTerrainSnapshot(
        CampaignWorldDefinition definition,
        long revision,
        CampaignSeasonTerrainSample[] samples)
    {
        Definition = definition;
        Revision = revision;
        _samples = samples;
        Samples = Array.AsReadOnly(_samples);
    }

    public CampaignWorldDefinition Definition { get; }

    public long Revision { get; }

    public IReadOnlyList<CampaignSeasonTerrainSample> Samples { get; }

    public CampaignSeasonTerrainSample GetSample(int x, int y)
    {
        if ((uint)x >= (uint)Definition.TilesX || (uint)y >= (uint)Definition.TilesY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Season terrain snapshot coordinate ({x}, {y}) is outside the campaign grid.");
        }

        return _samples[(y * Definition.TilesX) + x];
    }

    internal ReadOnlySpan<CampaignSeasonTerrainSample> AsSpan() => _samples;
}

public sealed class CampaignSeasonGenerationSource
{
    private CampaignSeasonGenerationSource(
        CampaignSeasonTerrainSnapshot terrain,
        CampaignSeasonCatalog catalog,
        string defaultSeasonId,
        long seasonRevision,
        CampaignSeasonTile[] currentTiles)
    {
        Terrain = terrain;
        Catalog = catalog;
        DefaultSeasonId = defaultSeasonId;
        SeasonRevision = seasonRevision;
        CurrentTiles = Array.AsReadOnly(currentTiles);
    }

    public CampaignSeasonTerrainSnapshot Terrain { get; }

    public CampaignWorldDefinition Definition => Terrain.Definition;

    public CampaignSeasonCatalog Catalog { get; }

    public string DefaultSeasonId { get; }

    public long TerrainRevision => Terrain.Revision;

    public long SeasonRevision { get; }

    public IReadOnlyList<CampaignSeasonTile> CurrentTiles { get; }

    public static CampaignSeasonGenerationSource Capture(
        ICampaignSeasonTerrainQuery terrainQuery,
        CampaignSeasonMap seasonMap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terrainQuery);
        ArgumentNullException.ThrowIfNull(seasonMap);
        cancellationToken.ThrowIfCancellationRequested();
        var terrainRevisionBefore = terrainQuery.Revision;
        var seasonRevisionBefore = seasonMap.Revision;
        CampaignWorldDefinition.EnsureValid(terrainQuery.Definition);
        if (terrainQuery.Definition != seasonMap.Definition)
        {
            throw new ArgumentException(
                "Terrain query and season map must describe the same value-equal campaign world.",
                nameof(seasonMap));
        }

        seasonMap.EnsureValid();
        var definition = terrainQuery.Definition with { };
        var samples = new CampaignSeasonTerrainSample[checked((int)definition.TileCount)];
        for (var y = 0; y < definition.TilesY; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var x = 0; x < definition.TilesX; x++)
            {
                var sample = terrainQuery.GetSample(x, y);
                sample.EnsureValid();
                samples[(y * definition.TilesX) + x] = sample;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var currentEntries = seasonMap.GetAllTiles();
        var currentTiles = new CampaignSeasonTile[currentEntries.Count];
        for (var index = 0; index < currentEntries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentTiles[index] = currentEntries[index].Tile;
        }

        var terrainRevisionAfter = terrainQuery.Revision;
        var seasonRevisionAfter = seasonMap.Revision;
        if (terrainRevisionBefore != terrainRevisionAfter ||
            seasonRevisionBefore != seasonRevisionAfter)
        {
            throw new InvalidOperationException(
                "Terrain or seasons changed while the immutable generation source was being captured.");
        }

        return new CampaignSeasonGenerationSource(
            new CampaignSeasonTerrainSnapshot(definition, terrainRevisionBefore, samples),
            seasonMap.Catalog,
            seasonMap.DefaultSeasonId,
            seasonRevisionBefore,
            currentTiles);
    }
}
