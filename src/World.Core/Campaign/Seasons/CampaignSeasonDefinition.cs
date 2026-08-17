namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonDefinition
{
    public const int MaximumIdentifierLength = 64;

    public const int MaximumNameLength = 64;

    public CampaignSeasonDefinition(
        string id,
        string name,
        CampaignBuiltInSeason fallback,
        string colorHex,
        int tintStrengthPercent,
        int effectIntensityPercent,
        CampaignSeasonRule? rule = null)
    {
        Id = id;
        Name = name;
        Fallback = fallback;
        ColorHex = colorHex;
        TintStrengthPercent = tintStrengthPercent;
        EffectIntensityPercent = effectIntensityPercent;
        Rule = rule ?? CampaignSeasonRule.Unrestricted;
        EnsureValid();
    }

    public string Id { get; }

    public string Name { get; }

    public CampaignBuiltInSeason Fallback { get; }

    public string ColorHex { get; }

    public int TintStrengthPercent { get; }

    public int EffectIntensityPercent { get; }

    public CampaignSeasonRule Rule { get; }

    public void EnsureValid()
    {
        if (!IsValidIdentifier(Id))
        {
            throw new ArgumentException(
                $"Season ID must use 1–{MaximumIdentifierLength} lowercase letters, digits, or hyphens and begin with a letter.",
                nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name) ||
            Name != Name.Trim() ||
            Name.Length > MaximumNameLength ||
            Name.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"Season name must contain 1–{MaximumNameLength} trimmed characters without control characters.",
                nameof(Name));
        }

        if (!Enum.IsDefined(Fallback))
        {
            throw new ArgumentOutOfRangeException(nameof(Fallback), Fallback, "Unknown built-in season fallback.");
        }

        if (!IsValidColor(ColorHex))
        {
            throw new ArgumentException("Season color must use #RRGGBB hexadecimal notation.", nameof(ColorHex));
        }

        if (TintStrengthPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TintStrengthPercent),
                TintStrengthPercent,
                "Season tint strength must be from 0 through 100 percent.");
        }

        if (EffectIntensityPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EffectIntensityPercent),
                EffectIntensityPercent,
                "Season effect intensity must be from 0 through 100 percent.");
        }

        Rule.EnsureValid();
    }

    public static bool IsValidIdentifier(string? value) =>
        IsValidPortableIdentifier(value, MaximumIdentifierLength);

    public static bool IsValidPortableIdentifier(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength ||
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
