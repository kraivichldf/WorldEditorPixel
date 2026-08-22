namespace Kingdom.World.Core.Campaign.Seasons;

public sealed class CampaignSeasonCatalog
{
    public const int MaximumDefinitionCount = ushort.MaxValue;

    public const string SpringId = "spring";

    public const string SummerId = "summer";

    public const string FallId = "fall";

    public const string WinterId = "winter";

    private static readonly string[] BuiltInIdOrder =
    [
        SpringId,
        SummerId,
        FallId,
        WinterId,
    ];

    private static readonly IReadOnlyList<CampaignSeasonDefinition> DefaultBuiltIns =
        CreateDefaultBuiltIns();

    private static readonly IReadOnlyDictionary<string, CampaignSeasonDefinition> DefaultBuiltInsById =
        DefaultBuiltIns.ToDictionary(static value => value.Id, StringComparer.Ordinal);

    private readonly Dictionary<string, CampaignSeasonDefinition> _byId;
    private readonly Dictionary<string, ushort> _indexesById;

    public CampaignSeasonCatalog(
        IEnumerable<CampaignSeasonDefinition>? customDefinitions = null,
        IEnumerable<CampaignSeasonDefinition>? builtInDefinitions = null)
    {
        var builtIns = builtInDefinitions?.ToArray() ?? DefaultBuiltIns.ToArray();
        ValidateBuiltIns(builtIns, nameof(builtInDefinitions));
        var custom = customDefinitions?.ToArray() ?? [];
        if (builtIns.Length + custom.Length > MaximumDefinitionCount)
        {
            throw new ArgumentException(
                $"A season catalog can contain at most {MaximumDefinitionCount:N0} definitions.",
                nameof(customDefinitions));
        }

        _byId = new Dictionary<string, CampaignSeasonDefinition>(StringComparer.Ordinal);
        foreach (var definition in builtIns.Concat(custom))
        {
            if (definition is null)
            {
                throw new ArgumentException("Season definitions cannot contain null values.", nameof(customDefinitions));
            }

            definition.EnsureValid();
            if (!_byId.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException(
                    $"Season ID '{definition.Id}' is defined more than once or conflicts with a built-in season.",
                    nameof(customDefinitions));
            }
        }

        var orderedBuiltIns = BuiltInIdOrder.Select(id => _byId[id]);
        var orderedCustom = custom.OrderBy(static value => value.Id, StringComparer.Ordinal).ToArray();
        var ordered = orderedBuiltIns.Concat(orderedCustom).ToArray();
        Definitions = Array.AsReadOnly(ordered);
        BuiltInDefinitions = Array.AsReadOnly(BuiltInIdOrder.Select(id => _byId[id]).ToArray());
        CustomDefinitions = Array.AsReadOnly(orderedCustom);
        _indexesById = new Dictionary<string, ushort>(StringComparer.Ordinal);
        for (var index = 0; index < ordered.Length; index++)
        {
            _indexesById.Add(ordered[index].Id, checked((ushort)index));
        }
    }

    public IReadOnlyList<CampaignSeasonDefinition> Definitions { get; }

    public IReadOnlyList<CampaignSeasonDefinition> BuiltInDefinitions { get; }

    public IReadOnlyList<CampaignSeasonDefinition> CustomDefinitions { get; }

    public static IReadOnlyList<CampaignSeasonDefinition> DefaultBuiltInDefinitions => DefaultBuiltIns;

    public bool Contains(string? seasonId) =>
        seasonId is not null && _byId.ContainsKey(seasonId);

    public bool IsBuiltIn(string? seasonId) =>
        seasonId is not null && DefaultBuiltInsById.ContainsKey(seasonId);

    public CampaignSeasonDefinition Get(string seasonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        return _byId.TryGetValue(seasonId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown season ID '{seasonId}'.");
    }

    public bool TryGet(string? seasonId, out CampaignSeasonDefinition definition)
    {
        if (seasonId is not null && _byId.TryGetValue(seasonId, out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    public ushort GetIndex(string seasonId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seasonId);
        return _indexesById.TryGetValue(seasonId, out var index)
            ? index
            : throw new KeyNotFoundException($"Unknown season ID '{seasonId}'.");
    }

    public CampaignSeasonDefinition GetByIndex(ushort index) =>
        index < Definitions.Count
            ? Definitions[index]
            : throw new ArgumentOutOfRangeException(nameof(index), index, "Season catalog index is out of range.");

    public static string GetBuiltInId(CampaignBuiltInSeason season) => season switch
    {
        CampaignBuiltInSeason.Spring => SpringId,
        CampaignBuiltInSeason.Summer => SummerId,
        CampaignBuiltInSeason.Fall => FallId,
        CampaignBuiltInSeason.Winter => WinterId,
        _ => throw new ArgumentOutOfRangeException(nameof(season), season, "Unknown built-in season."),
    };

    private static void ValidateBuiltIns(
        IReadOnlyList<CampaignSeasonDefinition> definitions,
        string parameterName)
    {
        if (definitions.Count != BuiltInIdOrder.Length)
        {
            throw new ArgumentException(
                $"A season catalog must define exactly {BuiltInIdOrder.Length} built-in seasons.",
                parameterName);
        }

        var byId = new Dictionary<string, CampaignSeasonDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (definition is null)
            {
                throw new ArgumentException("Built-in season definitions cannot contain null values.", parameterName);
            }

            definition.EnsureValid();
            if (!DefaultBuiltInsById.TryGetValue(definition.Id, out var expected))
            {
                throw new ArgumentException(
                    $"Unknown built-in season ID '{definition.Id}'.",
                    parameterName);
            }

            if (!string.Equals(definition.Name, expected.Name, StringComparison.Ordinal) ||
                definition.Fallback != expected.Fallback)
            {
                throw new ArgumentException(
                    $"Built-in season '{definition.Id}' must retain its canonical name and fallback identity.",
                    parameterName);
            }

            if (!byId.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException(
                    $"Built-in season '{definition.Id}' appears more than once.",
                    parameterName);
            }
        }

        var missing = BuiltInIdOrder.FirstOrDefault(id => !byId.ContainsKey(id));
        if (missing is not null)
        {
            throw new ArgumentException($"Built-in season '{missing}' is missing.", parameterName);
        }
    }

    private static IReadOnlyList<CampaignSeasonDefinition> CreateDefaultBuiltIns() =>
        Array.AsReadOnly<CampaignSeasonDefinition>(
        [
            new(
                SpringId,
                "Spring",
                CampaignBuiltInSeason.Spring,
                "#7FCF6A",
                tintStrengthPercent: 45,
                effectIntensityPercent: 40,
                new CampaignSeasonRule(
                    warmSeasonTemperatureCelsius: new CampaignSeasonRange(5, 100),
                    seasonality: new CampaignSeasonRange(0.12, 1))),
            new(
                SummerId,
                "Summer",
                CampaignBuiltInSeason.Summer,
                "#E8C85A",
                tintStrengthPercent: 30,
                effectIntensityPercent: 25,
                new CampaignSeasonRule(
                    warmSeasonTemperatureCelsius: new CampaignSeasonRange(10, 100))),
            new(
                FallId,
                "Fall",
                CampaignBuiltInSeason.Fall,
                "#C9783D",
                tintStrengthPercent: 55,
                effectIntensityPercent: 45,
                new CampaignSeasonRule(
                    warmSeasonTemperatureCelsius: new CampaignSeasonRange(5, 100),
                    seasonality: new CampaignSeasonRange(0.12, 1))),
            new(
                WinterId,
                "Winter",
                CampaignBuiltInSeason.Winter,
                "#DCEAF4",
                tintStrengthPercent: 70,
                effectIntensityPercent: 70,
                new CampaignSeasonRule(
                    coldSeasonTemperatureCelsius: new CampaignSeasonRange(-273.15, 5),
                    seasonality: new CampaignSeasonRange(0.12, 1))),
        ]);
}
