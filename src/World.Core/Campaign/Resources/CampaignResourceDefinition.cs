namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceDefinition
{
    public const int MaximumIdentifierLength = 64;

    public const int MaximumNameLength = 64;

    public const int MaximumSymbolIdentifierLength = 32;

    public const int MinimumMapPriority = 1;

    public const int MaximumMapPriority = 100;

    public CampaignResourceDefinition(
        string id,
        string name,
        CampaignResourceCategory category,
        CampaignResourceDistributionProfile distributionProfile,
        CampaignResourceMedium medium,
        string symbolId,
        string colorHex,
        int mapPriority,
        int coveragePercent,
        CampaignResourceRichness richness,
        CampaignResourceConcentration concentration,
        CampaignResourceRuleSet? rules = null)
    {
        Id = id;
        Name = name;
        Category = category;
        DistributionProfile = distributionProfile;
        SymbolId = symbolId;
        ColorHex = colorHex;
        MapPriority = mapPriority;
        CoveragePercent = coveragePercent;
        Richness = richness;
        Concentration = concentration;
        Rules = rules ?? new CampaignResourceRuleSet(medium);
        if (Rules.Medium != medium)
        {
            throw new ArgumentException(
                "Resource definition medium must match its rule-set medium.",
                nameof(medium));
        }

        EnsureValid();
    }

    public string Id { get; }

    public string Name { get; }

    public CampaignResourceCategory Category { get; }

    public CampaignResourceDistributionProfile DistributionProfile { get; }

    public CampaignResourceMedium Medium => Rules.Medium;

    public string SymbolId { get; }

    public string ColorHex { get; }

    public int MapPriority { get; }

    public int CoveragePercent { get; }

    public CampaignResourceRichness Richness { get; }

    public CampaignResourceConcentration Concentration { get; }

    public CampaignResourceRuleSet Rules { get; }

    public void EnsureValid()
    {
        if (!IsValidIdentifier(Id))
        {
            throw new ArgumentException(
                $"Resource ID must use 1–{MaximumIdentifierLength} lowercase letters, digits, or hyphens and begin with a letter.",
                nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name) ||
            Name != Name.Trim() ||
            Name.Length > MaximumNameLength ||
            Name.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Resource name must contain 1–{MaximumNameLength} trimmed characters without control characters.",
                nameof(Name));
        }

        if (!Enum.IsDefined(Category))
        {
            throw new ArgumentOutOfRangeException(nameof(Category), Category, "Unknown resource category.");
        }

        if (!Enum.IsDefined(DistributionProfile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DistributionProfile),
                DistributionProfile,
                "Unknown resource distribution profile.");
        }

        if (!IsValidSymbolIdentifier(SymbolId))
        {
            throw new ArgumentException(
                $"Resource symbol ID must use 1–{MaximumSymbolIdentifierLength} lowercase letters, digits, or hyphens and begin with a letter.",
                nameof(SymbolId));
        }

        if (!IsValidColor(ColorHex))
        {
            throw new ArgumentException("Resource color must use #RRGGBB hexadecimal notation.", nameof(ColorHex));
        }

        if (MapPriority is < MinimumMapPriority or > MaximumMapPriority)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MapPriority),
                MapPriority,
                $"Resource map priority must be from {MinimumMapPriority} through {MaximumMapPriority}.");
        }

        if (CoveragePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CoveragePercent),
                CoveragePercent,
                "Resource coverage must be from 0 through 100 percent.");
        }

        if (!Enum.IsDefined(Richness))
        {
            throw new ArgumentOutOfRangeException(nameof(Richness), Richness, "Unknown resource richness.");
        }

        if (!Enum.IsDefined(Concentration))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Concentration),
                Concentration,
                "Unknown resource concentration.");
        }

        Rules.EnsureValid();
    }

    public static bool IsValidIdentifier(string? value) =>
        IsValidPortableIdentifier(value, MaximumIdentifierLength);

    private static bool IsValidSymbolIdentifier(string? value) =>
        IsValidPortableIdentifier(value, MaximumSymbolIdentifierLength);

    private static bool IsValidPortableIdentifier(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength ||
            value[0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (var character in value)
        {
            if ((character is >= 'a' and <= 'z') ||
                (character is >= '0' and <= '9') ||
                character == '-')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsValidColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#')
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if ((character is >= '0' and <= '9') ||
                (character is >= 'A' and <= 'F') ||
                (character is >= 'a' and <= 'f'))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
