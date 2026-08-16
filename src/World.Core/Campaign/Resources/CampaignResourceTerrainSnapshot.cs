using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceTerrainSnapshot
{
    private readonly CampaignResourceTerrainSample[] _samples;

    internal CampaignResourceTerrainSnapshot(
        CampaignWorldDefinition definition,
        long revision,
        CampaignResourceTerrainSample[] samples)
    {
        Definition = definition;
        Revision = revision;
        _samples = samples;
        Samples = Array.AsReadOnly(_samples);
    }

    public CampaignWorldDefinition Definition { get; }

    public long Revision { get; }

    public IReadOnlyList<CampaignResourceTerrainSample> Samples { get; }

    public CampaignResourceTerrainSample GetSample(int x, int y)
    {
        if ((uint)x >= (uint)Definition.TilesX || (uint)y >= (uint)Definition.TilesY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Terrain snapshot coordinate ({x}, {y}) is outside the campaign grid.");
        }

        return _samples[(y * Definition.TilesX) + x];
    }

    internal ReadOnlySpan<CampaignResourceTerrainSample> AsSpan() => _samples;
}

public sealed class CampaignResourceGenerationSource
{
    private CampaignResourceGenerationSource(
        CampaignResourceTerrainSnapshot terrain,
        CampaignResourceCatalog catalog,
        long resourceRevision,
        CampaignResourceEntry[] currentEntries)
    {
        Terrain = terrain;
        Catalog = catalog;
        ResourceRevision = resourceRevision;
        CurrentEntries = Array.AsReadOnly(currentEntries);
    }

    public CampaignResourceTerrainSnapshot Terrain { get; }

    public CampaignWorldDefinition Definition => Terrain.Definition;

    public CampaignResourceCatalog Catalog { get; }

    public long TerrainRevision => Terrain.Revision;

    public long ResourceRevision { get; }

    public IReadOnlyList<CampaignResourceEntry> CurrentEntries { get; }

    public static CampaignResourceGenerationSource Capture(
        ICampaignResourceTerrainQuery terrainQuery,
        CampaignResourceMap resourceMap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terrainQuery);
        ArgumentNullException.ThrowIfNull(resourceMap);
        cancellationToken.ThrowIfCancellationRequested();
        var terrainRevisionBefore = terrainQuery.Revision;
        var resourceRevisionBefore = resourceMap.Revision;
        CampaignWorldDefinition.EnsureValid(terrainQuery.Definition);
        if (terrainQuery.Definition != resourceMap.Definition)
        {
            throw new ArgumentException(
                "Terrain query and resource map must describe the same value-equal campaign world.",
                nameof(resourceMap));
        }

        resourceMap.EnsureValid();
        cancellationToken.ThrowIfCancellationRequested();
        var definition = terrainQuery.Definition with { };
        var samples = new CampaignResourceTerrainSample[checked((int)definition.TileCount)];
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
        var entries = resourceMap.GetMaterializedOccurrences().ToArray();
        var terrainRevisionAfter = terrainQuery.Revision;
        var resourceRevisionAfter = resourceMap.Revision;
        if (terrainRevisionBefore != terrainRevisionAfter ||
            resourceRevisionBefore != resourceRevisionAfter)
        {
            throw new InvalidOperationException(
                "Terrain or resources changed while the immutable generation source was being captured.");
        }

        return new CampaignResourceGenerationSource(
            new CampaignResourceTerrainSnapshot(definition, terrainRevisionBefore, samples),
            resourceMap.Catalog,
            resourceRevisionBefore,
            entries);
    }
}
