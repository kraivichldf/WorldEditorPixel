using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

/// <summary>
/// Owner-thread projection of the terrain facts required by season generation.
/// Capture it before dispatching generation to a worker thread.
/// </summary>
public interface ICampaignSeasonTerrainQuery
{
    CampaignWorldDefinition Definition { get; }

    long Revision { get; }

    CampaignSeasonTerrainSample GetSample(int x, int y);
}
