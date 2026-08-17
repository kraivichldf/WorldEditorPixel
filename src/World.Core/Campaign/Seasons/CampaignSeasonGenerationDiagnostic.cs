namespace Kingdom.World.Core.Campaign.Seasons;

public sealed record CampaignSeasonGenerationDiagnostic(
    CampaignSeasonTerrainSample Terrain,
    CampaignSeasonSupportSample Support,
    string WinningSeasonId,
    IReadOnlyList<string> MatchingSeasonIds,
    IReadOnlyList<string> ShadowedSeasonIds);

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
        var priority = settings.GetPriorityDefinitions(catalog);
        var matches = new List<string>(priority.Count);
        for (var index = 0; index < priority.Count; index++)
        {
            var definition = priority[index];
            if (index == priority.Count - 1 ||
                CampaignSeasonRuleEvaluator.MatchesValidated(definition.Rule, terrain, support))
            {
                matches.Add(definition.Id);
            }
        }

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                "The validated season priority did not produce its required final catch-all match.");
        }

        return new CampaignSeasonGenerationDiagnostic(
            terrain,
            support,
            matches[0],
            Array.AsReadOnly(matches.ToArray()),
            Array.AsReadOnly(matches.Skip(1).ToArray()));
    }
}
