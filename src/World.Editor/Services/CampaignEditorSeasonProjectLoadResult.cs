using Kingdom.World.Core.Campaign;
using Kingdom.World.Core.Campaign.Resources;
using Kingdom.World.Core.Campaign.Seasons;

namespace Kingdom.World.Editor.Services;

public sealed class CampaignEditorSeasonProjectLoadResult
{
    public CampaignEditorSeasonProjectLoadResult(
        CampaignWorld world,
        CampaignResourceMap resourceMap,
        CampaignResourceGenerationSettings? resourceGenerationSettings,
        CampaignSeasonMap seasonMap,
        IEnumerable<string> seasonEnabledIds,
        CampaignSeasonSavedGeneration? seasonSavedGeneration,
        bool wasConvertedFromLegacy,
        string sourceProjectDirectory,
        int normalizedLegacyCoastalTileCount = 0,
        bool seasonsWereImplicitCompatibility = false)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        ResourceMap = resourceMap ?? throw new ArgumentNullException(nameof(resourceMap));
        ResourceGenerationSettings = resourceGenerationSettings;
        SeasonMap = seasonMap ?? throw new ArgumentNullException(nameof(seasonMap));
        ArgumentNullException.ThrowIfNull(seasonEnabledIds);
        SeasonEnabledIds = Array.AsReadOnly(seasonEnabledIds.Order(StringComparer.Ordinal).ToArray());
        SeasonSavedGeneration = seasonSavedGeneration;
        WasConvertedFromLegacy = wasConvertedFromLegacy;
        SourceProjectDirectory = sourceProjectDirectory ??
            throw new ArgumentNullException(nameof(sourceProjectDirectory));
        NormalizedLegacyCoastalTileCount = normalizedLegacyCoastalTileCount;
        SeasonsWereImplicitCompatibility = seasonsWereImplicitCompatibility;
    }

    public CampaignWorld World { get; }

    public CampaignResourceMap ResourceMap { get; }

    public CampaignResourceGenerationSettings? ResourceGenerationSettings { get; }

    public CampaignSeasonMap SeasonMap { get; }

    public IReadOnlyList<string> SeasonEnabledIds { get; }

    public CampaignSeasonSavedGeneration? SeasonSavedGeneration { get; }

    public CampaignSeasonGenerationSettings? SeasonGenerationSettings =>
        SeasonSavedGeneration?.Settings;

    public bool WasConvertedFromLegacy { get; }

    public string SourceProjectDirectory { get; }

    public int NormalizedLegacyCoastalTileCount { get; }

    public bool SeasonsWereImplicitCompatibility { get; }
}
