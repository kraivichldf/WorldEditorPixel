using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Core.Serialization;

public sealed class CampaignSeasonProjectLoadResult
{
    public CampaignSeasonProjectLoadResult(
        CampaignSeasonMap seasonMap,
        CampaignSeasonSavedGeneration? savedGeneration,
        string sourceProjectDirectory,
        bool wasImplicitCompatibility)
    {
        SeasonMap = seasonMap ?? throw new ArgumentNullException(nameof(seasonMap));
        SavedGeneration = savedGeneration;
        SourceProjectDirectory = sourceProjectDirectory ??
            throw new ArgumentNullException(nameof(sourceProjectDirectory));
        WasImplicitCompatibility = wasImplicitCompatibility;
    }

    public CampaignSeasonMap SeasonMap { get; }

    public CampaignSeasonSavedGeneration? SavedGeneration { get; }

    public CampaignSeasonGenerationSettings? GenerationSettings => SavedGeneration?.Settings;

    public string SourceProjectDirectory { get; }

    public bool WasImplicitCompatibility { get; }
}
