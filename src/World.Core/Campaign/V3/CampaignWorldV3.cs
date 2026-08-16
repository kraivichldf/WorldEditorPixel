using Kingdom.World.Core.Models;
using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Campaign.V3;

public sealed class CampaignWorldV3
{
    public CampaignWorldV3(
        CampaignWorldDefinition definition,
        TerrainFormProfile? terrainFormProfile = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CampaignWorldDefinition.EnsureValid(definition);
        TerrainFormProfile = terrainFormProfile ?? TerrainFormProfile.Default;
        TerrainFormProfile.EnsureValid();
        Tiles = new CampaignTileMapV3(definition);
        Rivers = new RiverNetworkV3(Tiles);
        Shores = new ShoreOverrideMapV3(Tiles, TerrainFormProfile);
    }

    public CampaignWorldDefinition Definition { get; }

    public TerrainFormProfile TerrainFormProfile { get; }

    public CampaignTileMapV3 Tiles { get; }

    public RiverNetworkV3 Rivers { get; }

    public ShoreOverrideMapV3 Shores { get; }

    public long Revision => Tiles.Revision + Rivers.Revision + Shores.Revision;

    public bool SetTile(int x, int y, CampaignTileDataV3 data) =>
        SetTiles([new CampaignTileEntryV3(x, y, data)]) > 0;

    public bool SetSurface(int x, int y, CampaignSurfaceType surface)
    {
        var previous = Tiles.GetTile(x, y);
        return SetTile(x, y, previous with { Surface = surface });
    }

    public bool SetHeight(int x, int y, short heightMeters)
    {
        var previous = Tiles.GetTile(x, y);
        return SetTile(x, y, previous with { HeightMeters = heightMeters });
    }

    public int SetTiles(IEnumerable<CampaignTileEntryV3> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var pending = entries.ToArray();
        var coordinates = new HashSet<long>();
        foreach (var entry in pending)
        {
            Tiles.EnsureValidCoordinate(entry.X, entry.Y);
            Tiles.EnsureValidData(entry.Data);
            if (!coordinates.Add(CampaignTileMapV3.GetKey(entry.X, entry.Y)))
            {
                throw new ArgumentException(
                    $"Campaign tile ({entry.X}, {entry.Y}) appears more than once in one update batch.",
                    nameof(entries));
            }

            if (!entry.Data.Surface.IsLand() && Rivers.HasRiver(entry.X, entry.Y))
            {
                throw new InvalidOperationException(
                    $"Campaign tile ({entry.X}, {entry.Y}) cannot change to {entry.Data.Surface} " +
                    "while it has a River overlay. Remove the River first.");
            }
        }

        var changed = Tiles.SetTiles(pending);
        if (changed > 0)
        {
            Shores.RemoveInvalidOverrides();
        }

        return changed;
    }

    public bool SetRiver(int x, int y, RiverTileData data) =>
        Rivers.SetRiver(x, y, data);

    public int SetRivers(IEnumerable<RiverTileEntryV3> entries) =>
        Rivers.SetRivers(entries);

    public bool RemoveRiver(int x, int y) => Rivers.RemoveRiver(x, y);

    public bool SetShoreOverride(
        int x,
        int y,
        CardinalDirection edge,
        ShoreStyle style) =>
        Shores.SetOverride(x, y, edge, style);

    public TerrainForm GetTerrainForm(int x, int y) =>
        Tiles.GetTerrainForm(x, y, TerrainFormProfile);

    public TerrainFormAnalysis AnalyzeTerrainForm(int x, int y) =>
        Tiles.AnalyzeTerrainForm(x, y, TerrainFormProfile);

    public ShoreStyle GetEffectiveShoreStyle(
        int x,
        int y,
        CardinalDirection edge) =>
        Shores.GetEffectiveStyle(x, y, edge);

    public IReadOnlyList<string> Validate(bool requireResolvedRiverOutflows = true)
    {
        var errors = new List<string>();
        errors.AddRange(TerrainFormProfile.Validate());
        errors.AddRange(Rivers.Validate(requireResolvedRiverOutflows));
        errors.AddRange(Shores.Validate());
        return errors;
    }

    public void EnsureValid(bool requireResolvedRiverOutflows = true)
    {
        var errors = Validate(requireResolvedRiverOutflows);
        if (errors.Count > 0)
        {
            throw new WorldValidationException(errors);
        }
    }
}
