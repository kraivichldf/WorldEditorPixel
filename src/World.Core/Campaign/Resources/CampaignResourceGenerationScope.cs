namespace Kingdom.World.Core.Campaign.Resources;

public enum CampaignResourceGenerationScopeKind
{
    All = 0,
    Category = 1,
    Resource = 2,
    Selection = 3,
}

public sealed class CampaignResourceGenerationScope : IEquatable<CampaignResourceGenerationScope>
{
    private readonly HashSet<string> _resourceIdSet;

    private CampaignResourceGenerationScope(
        CampaignResourceGenerationScopeKind kind,
        CampaignResourceCategory? category,
        string? resourceId,
        IEnumerable<string>? resourceIds = null)
    {
        Kind = kind;
        Category = category;
        ResourceId = resourceId;
        var resourceIdCopy = (resourceIds ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        ResourceIds = Array.AsReadOnly(resourceIdCopy);
        _resourceIdSet = resourceIdCopy.ToHashSet(StringComparer.Ordinal);
    }

    public CampaignResourceGenerationScopeKind Kind { get; }

    public CampaignResourceCategory? Category { get; }

    public string? ResourceId { get; }

    public IReadOnlyList<string> ResourceIds { get; }

    public static CampaignResourceGenerationScope All { get; } =
        new(CampaignResourceGenerationScopeKind.All, category: null, resourceId: null);

    public static CampaignResourceGenerationScope ForCategory(CampaignResourceCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown resource category.");
        }

        return new CampaignResourceGenerationScope(
            CampaignResourceGenerationScopeKind.Category,
            category,
            resourceId: null);
    }

    public static CampaignResourceGenerationScope ForResource(string resourceId)
    {
        if (!CampaignResourceDefinition.IsValidIdentifier(resourceId))
        {
            throw new ArgumentException("Generation scope has an invalid resource ID.", nameof(resourceId));
        }

        return new CampaignResourceGenerationScope(
            CampaignResourceGenerationScopeKind.Resource,
            category: null,
            resourceId);
    }

    public static CampaignResourceGenerationScope ForResources(IEnumerable<string> resourceIds)
    {
        ArgumentNullException.ThrowIfNull(resourceIds);
        var copy = resourceIds.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException(
                "Include at least one resource before generation.",
                nameof(resourceIds));
        }

        if (copy.Any(static id => !CampaignResourceDefinition.IsValidIdentifier(id)))
        {
            throw new ArgumentException(
                "Generation selection contains an invalid resource ID.",
                nameof(resourceIds));
        }

        return new CampaignResourceGenerationScope(
            CampaignResourceGenerationScopeKind.Selection,
            category: null,
            resourceId: null,
            copy);
    }

    public bool Includes(CampaignResourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Kind switch
        {
            CampaignResourceGenerationScopeKind.All => true,
            CampaignResourceGenerationScopeKind.Category => definition.Category == Category,
            CampaignResourceGenerationScopeKind.Resource =>
                string.Equals(definition.Id, ResourceId, StringComparison.Ordinal),
            CampaignResourceGenerationScopeKind.Selection => _resourceIdSet.Contains(definition.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown generation scope."),
        };
    }

    public void EnsureValid(CampaignResourceCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!Enum.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown generation scope.");
        }

        if (Kind == CampaignResourceGenerationScopeKind.Category &&
            (Category is null || !Enum.IsDefined(Category.Value)))
        {
            throw new ArgumentException("Category generation scope requires a valid category.", nameof(Category));
        }

        if (Kind == CampaignResourceGenerationScopeKind.Resource &&
            (ResourceId is null || !catalog.Contains(ResourceId)))
        {
            throw new ArgumentException(
                $"Resource generation scope references unknown resource '{ResourceId}'.",
                nameof(ResourceId));
        }

        if (Kind == CampaignResourceGenerationScopeKind.Selection)
        {
            if (ResourceIds.Count == 0)
            {
                throw new ArgumentException(
                    "Generation selection must include at least one resource.",
                    nameof(ResourceIds));
            }

            var unknown = ResourceIds.FirstOrDefault(id => !catalog.Contains(id));
            if (unknown is not null)
            {
                throw new ArgumentException(
                    $"Generation selection references unknown resource '{unknown}'.",
                    nameof(ResourceIds));
            }
        }
    }

    public bool Equals(CampaignResourceGenerationScope? other) =>
        other is not null &&
        Kind == other.Kind &&
        Category == other.Category &&
        string.Equals(ResourceId, other.ResourceId, StringComparison.Ordinal) &&
        ResourceIds.SequenceEqual(other.ResourceIds, StringComparer.Ordinal);

    public override bool Equals(object? obj) =>
        obj is CampaignResourceGenerationScope other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Category);
        hash.Add(ResourceId, StringComparer.Ordinal);
        foreach (var resourceId in ResourceIds)
        {
            hash.Add(resourceId, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(
        CampaignResourceGenerationScope? left,
        CampaignResourceGenerationScope? right) =>
        ReferenceEquals(left, right) || (left?.Equals(right) ?? false);

    public static bool operator !=(
        CampaignResourceGenerationScope? left,
        CampaignResourceGenerationScope? right) => !(left == right);
}
