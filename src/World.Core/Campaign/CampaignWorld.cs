using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign;

public sealed class CampaignWorld
{
    public CampaignWorld(
        CampaignWorldDefinition definition,
        IEnumerable<CampaignCustomTerrainDefinition>? customTerrainDefinitions = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        CampaignWorldDefinition.EnsureValid(definition);
        Tiles = new CampaignTileMap(definition, customTerrainDefinitions);
    }

    public CampaignWorldDefinition Definition { get; }

    public CampaignTileMap Tiles { get; }

    public long Revision => Tiles.Revision;
}
