namespace Kingdom.World.Core.Campaign.Resources;

public readonly record struct CampaignResourceOccurrence(
    string ResourceId,
    byte Potential,
    bool Locked = false)
{
    public const byte MinimumPotential = 1;

    public const byte MaximumPotential = 100;

    public void EnsureValid()
    {
        if (!CampaignResourceDefinition.IsValidIdentifier(ResourceId))
        {
            throw new ArgumentException("Resource occurrence has an invalid resource ID.", nameof(ResourceId));
        }

        if (Potential is < MinimumPotential or > MaximumPotential)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Potential),
                Potential,
                $"Resource potential must be from {MinimumPotential} through {MaximumPotential}.");
        }
    }
}

public readonly record struct CampaignResourceEntry(
    int X,
    int Y,
    CampaignResourceOccurrence Occurrence);

public readonly record struct CampaignResourceMutation
{
    private CampaignResourceMutation(
        int x,
        int y,
        string resourceId,
        CampaignResourceOccurrence? value)
    {
        X = x;
        Y = y;
        ResourceId = resourceId;
        Value = value;
    }

    public int X { get; }

    public int Y { get; }

    public string ResourceId { get; }

    public CampaignResourceOccurrence? Value { get; }

    public static CampaignResourceMutation Upsert(
        int x,
        int y,
        CampaignResourceOccurrence occurrence) =>
        new(x, y, occurrence.ResourceId, occurrence);

    public static CampaignResourceMutation Remove(int x, int y, string resourceId) =>
        new(x, y, resourceId, value: null);
}
