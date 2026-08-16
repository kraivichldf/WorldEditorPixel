using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Resources;

public interface ICampaignResourceTerrainQuery
{
    CampaignWorldDefinition Definition { get; }

    long Revision { get; }

    CampaignResourceTerrainSample GetSample(int x, int y);
}
