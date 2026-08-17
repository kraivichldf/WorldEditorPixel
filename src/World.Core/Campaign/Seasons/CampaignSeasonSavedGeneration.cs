namespace Kingdom.World.Core.Campaign.Seasons;

/// <summary>
/// Saved generation recipe plus fingerprints used to report whether its source or inputs are stale.
/// Fingerprints are diagnostics; the authoritative tile layer remains independent.
/// </summary>
public sealed class CampaignSeasonSavedGeneration
{
    public CampaignSeasonSavedGeneration(
        CampaignSeasonGenerationSettings settings,
        string sourceTerrainFingerprint,
        string inputFingerprint)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        SourceTerrainFingerprint = NormalizeFingerprint(
            sourceTerrainFingerprint,
            nameof(sourceTerrainFingerprint));
        InputFingerprint = NormalizeFingerprint(inputFingerprint, nameof(inputFingerprint));
    }

    public CampaignSeasonGenerationSettings Settings { get; }

    public string SourceTerrainFingerprint { get; }

    public string InputFingerprint { get; }

    private static string NormalizeFingerprint(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
        {
            throw new ArgumentException(
                "Season generation fingerprints must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        foreach (var character in value)
        {
            if ((character is >= '0' and <= '9') ||
                (character is >= 'a' and <= 'f') ||
                (character is >= 'A' and <= 'F'))
            {
                continue;
            }

            throw new ArgumentException(
                "Season generation fingerprints must contain only hexadecimal characters.",
                parameterName);
        }

        return value.ToLowerInvariant();
    }
}
