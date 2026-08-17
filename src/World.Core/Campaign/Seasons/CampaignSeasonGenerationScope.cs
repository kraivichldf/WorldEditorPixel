using Kingdom.World.Core.Models;

namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonGenerationScope : IEquatable<CampaignSeasonGenerationScope>
{
    private CampaignSeasonGenerationScope(
        CampaignSeasonGenerationScopeKind kind,
        CampaignTileArea? area)
    {
        Kind = kind;
        Area = area;
    }

    public CampaignSeasonGenerationScopeKind Kind { get; }

    public CampaignTileArea? Area { get; }

    public static CampaignSeasonGenerationScope All { get; } =
        new(CampaignSeasonGenerationScopeKind.All, area: null);

    public static CampaignSeasonGenerationScope ForArea(CampaignTileArea area) =>
        new(CampaignSeasonGenerationScopeKind.Area, area);

    public bool Includes(int x, int y) => Kind switch
    {
        CampaignSeasonGenerationScopeKind.All => true,
        CampaignSeasonGenerationScopeKind.Area when Area is { } area =>
            x >= area.MinimumX && x <= area.MaximumX &&
            y >= area.MinimumY && y <= area.MaximumY,
        CampaignSeasonGenerationScopeKind.Area =>
            throw new InvalidOperationException("Area season scope is missing its tile area."),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown season generation scope."),
    };

    public void EnsureValid(CampaignWorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CampaignWorldDefinition.EnsureValid(definition);
        if (!Enum.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown season generation scope.");
        }

        if (Kind == CampaignSeasonGenerationScopeKind.All)
        {
            if (Area is not null)
            {
                throw new ArgumentException("All season scope cannot contain a tile area.", nameof(Area));
            }

            return;
        }

        if (Area is not { } area ||
            (uint)area.MinimumX >= (uint)definition.TilesX ||
            (uint)area.MaximumX >= (uint)definition.TilesX ||
            (uint)area.MinimumY >= (uint)definition.TilesY ||
            (uint)area.MaximumY >= (uint)definition.TilesY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Area),
                "Season generation area must lie inside the campaign grid.");
        }
    }

    public bool Equals(CampaignSeasonGenerationScope? other) =>
        other is not null &&
        Kind == other.Kind &&
        Nullable.Equals(Area, other.Area);

    public override bool Equals(object? obj) =>
        obj is CampaignSeasonGenerationScope other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, Area);

    public static bool operator ==(
        CampaignSeasonGenerationScope? left,
        CampaignSeasonGenerationScope? right) =>
        ReferenceEquals(left, right) || (left?.Equals(right) ?? false);

    public static bool operator !=(
        CampaignSeasonGenerationScope? left,
        CampaignSeasonGenerationScope? right) => !(left == right);
}
