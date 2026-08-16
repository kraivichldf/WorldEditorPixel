using Kingdom.World.Core.Campaign;

namespace Kingdom.World.Core.Serialization;

public sealed record CampaignWorldLoadResult(
    CampaignWorld World,
    bool WasConvertedFromLegacy,
    string SourceProjectDirectory,
    int NormalizedLegacyCoastalTileCount = 0);
