namespace Kingdom.World.Core.Campaign.Seasons;

public readonly record struct CampaignSeasonOccurrence(
    string SeasonId,
    bool Locked = false)
{
    public void EnsureValid()
    {
        if (!CampaignSeasonDefinition.IsValidIdentifier(SeasonId))
        {
            throw new ArgumentException("Season occurrence has an invalid season ID.", nameof(SeasonId));
        }
    }
}

public readonly record struct CampaignSeasonEntry(
    int X,
    int Y,
    CampaignSeasonOccurrence Occurrence);

public readonly record struct CampaignSeasonMutation
{
    private CampaignSeasonMutation(
        int x,
        int y,
        string seasonId,
        CampaignSeasonOccurrence? value)
    {
        X = x;
        Y = y;
        SeasonId = seasonId;
        Value = value;
    }

    public int X { get; }

    public int Y { get; }

    public string SeasonId { get; }

    public CampaignSeasonOccurrence? Value { get; }

    public static CampaignSeasonMutation Upsert(
        int x,
        int y,
        CampaignSeasonOccurrence occurrence) =>
        new(x, y, occurrence.SeasonId, occurrence);

    public static CampaignSeasonMutation Remove(int x, int y, string seasonId) =>
        new(x, y, seasonId, value: null);
}
