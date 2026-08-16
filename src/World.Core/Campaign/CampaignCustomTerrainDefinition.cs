namespace Kingdom.World.Core.Campaign;

/// <summary>
/// A designer-defined, land-only terrain category. Its <see cref="BaseType"/>
/// remains the portable fallback and material foundation for systems that do not understand custom terrain.
/// </summary>
public sealed record CampaignCustomTerrainDefinition(
    string Id,
    string Name,
    CampaignTileType BaseType,
    string ColorHex,
    int GenerationSharePercent = 0)
{
    public const int MaximumDefinitionCount = 12;

    public const int MaximumNameLength = 48;

    public const int MaximumIdentifierLength = 32;

    public static bool IsSupportedBaseType(CampaignTileType type) => type is
        CampaignTileType.Plains or
        CampaignTileType.Steppe or
        CampaignTileType.Desert or
        CampaignTileType.Forest or
        CampaignTileType.Hills or
        CampaignTileType.Mountain;

    public void EnsureValid()
    {
        if (!IsValidIdentifier(Id))
        {
            throw new ArgumentException(
                $"Custom terrain id '{Id}' must use 1–{MaximumIdentifierLength} lowercase letters, digits, or hyphens and begin with a letter.",
                nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > MaximumNameLength)
        {
            throw new ArgumentException(
                $"Custom terrain name must contain 1–{MaximumNameLength} visible characters.",
                nameof(Name));
        }

        if (!IsSupportedBaseType(BaseType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BaseType),
                BaseType,
                "Custom terrain must use Plains, Steppe, Desert, Forest, Hills, or Mountain as its safe land base.");
        }

        if (!IsValidColor(ColorHex))
        {
            throw new ArgumentException(
                "Custom terrain color must use #RRGGBB hexadecimal notation.",
                nameof(ColorHex));
        }

        if (GenerationSharePercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(GenerationSharePercent),
                GenerationSharePercent,
                "Custom terrain mix share must be from 0 through 100 percent.");
        }
    }

    public static IReadOnlyList<CampaignCustomTerrainDefinition> ValidateAll(
        IEnumerable<CampaignCustomTerrainDefinition>? definitions)
    {
        var values = definitions?.ToArray() ?? [];
        if (values.Length > MaximumDefinitionCount)
        {
            throw new ArgumentException(
                $"A world can define at most {MaximumDefinitionCount} custom terrain types.",
                nameof(definitions));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in values)
        {
            if (definition is null)
            {
                throw new ArgumentException("Custom terrain definitions cannot contain null values.", nameof(definitions));
            }

            definition.EnsureValid();
            if (!ids.Add(definition.Id))
            {
                throw new ArgumentException(
                    $"Custom terrain id '{definition.Id}' is defined more than once.",
                    nameof(definitions));
            }
        }

        var totalShare = values.Sum(static definition => definition.GenerationSharePercent);
        if (totalShare > 100)
        {
            throw new ArgumentException(
                $"Custom terrain mix shares total {totalShare}%; they cannot exceed 100% of the inland terrain mix.",
                nameof(definitions));
        }

        return values;
    }

    private static bool IsValidIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentifierLength ||
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
