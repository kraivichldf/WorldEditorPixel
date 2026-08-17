namespace Kingdom.World.Core.Campaign.Seasons;

public readonly record struct CampaignSeasonTile(string SeasonId, bool Locked = false)
{
    public void EnsureValid(CampaignSeasonCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!CampaignSeasonDefinition.IsValidIdentifier(SeasonId))
        {
            throw new ArgumentException("Season tile has an invalid season ID.", nameof(SeasonId));
        }

        if (!catalog.Contains(SeasonId))
        {
            throw new ArgumentException($"Season tile references unknown season '{SeasonId}'.", nameof(SeasonId));
        }
    }
}

public readonly record struct CampaignSeasonEntry(
    int X,
    int Y,
    CampaignSeasonTile Tile);

public readonly record struct CampaignSeasonMutation(
    int X,
    int Y,
    CampaignSeasonTile Value);
