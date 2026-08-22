namespace Kingdom.World.Core.Campaign.Seasons;

public sealed record CampaignSeasonGenerationDiagnostic(
    CampaignSeasonTerrainSample Terrain,
    CampaignSeasonSupportSample Support,
    IReadOnlyList<string> MatchingSeasonIds,
    IReadOnlyList<string> NonMatchingSeasonIds);

public static class CampaignSeasonGenerationDiagnostics
{
    public static CampaignSeasonGenerationDiagnostic Evaluate(
        CampaignSeasonSupportFields supportFields,
        CampaignSeasonCatalog catalog,
        CampaignSeasonGenerationSettings settings,
        int x,
        int y)
    {
        ArgumentNullException.ThrowIfNull(supportFields);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        settings.EnsureValid(catalog, supportFields.Terrain.Definition);
        if (!ReferenceEquals(settings, supportFields.Settings) &&
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(catalog, settings) !=
            CampaignSeasonGenerationFingerprint.GetInputFingerprint(catalog, supportFields.Settings))
        {
            throw new ArgumentException(
                "Season diagnostic settings must match the immutable support-field settings.",
                nameof(settings));
        }

        var terrain = supportFields.Terrain.GetSample(x, y);
        var support = supportFields.GetSample(x, y);
        var enabled = settings.GetEnabledDefinitions(catalog);
        var matches = new List<string>(enabled.Count);
        var nonMatches = new List<string>(enabled.Count);
        foreach (var definition in enabled)
        {
            if (CampaignSeasonRuleEvaluator.MatchesValidated(definition.Rule, terrain, support))
            {
                matches.Add(definition.Id);
            }
            else
            {
                nonMatches.Add(definition.Id);
            }
        }

        return new CampaignSeasonGenerationDiagnostic(
            terrain,
            support,
            Array.AsReadOnly(matches.ToArray()),
            Array.AsReadOnly(nonMatches.ToArray()));
    }
}
