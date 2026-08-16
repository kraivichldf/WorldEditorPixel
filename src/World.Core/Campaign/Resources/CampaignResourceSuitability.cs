namespace Kingdom.World.Core.Campaign.Resources;

public sealed class CampaignResourceSuitabilityEvaluation
{
    private readonly bool[] _eligible;
    private readonly float[] _suitability;

    internal CampaignResourceSuitabilityEvaluation(
        CampaignResourceDefinition definition,
        int tilesX,
        int tilesY,
        bool[] eligible,
        float[] suitability,
        int eligibleTileCount,
        IEnumerable<string> unsupportedFactorIds)
    {
        Definition = definition;
        TilesX = tilesX;
        TilesY = tilesY;
        _eligible = eligible;
        _suitability = suitability;
        EligibleTileCount = eligibleTileCount;
        UnsupportedFactorIds = Array.AsReadOnly(unsupportedFactorIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray());
    }

    public CampaignResourceDefinition Definition { get; }

    public int TilesX { get; }

    public int TilesY { get; }

    public int EligibleTileCount { get; }

    public IReadOnlyList<string> UnsupportedFactorIds { get; }

    public bool IsSupported => UnsupportedFactorIds.Count == 0;

    public bool IsEligible(int x, int y)
    {
        EnsureCoordinate(x, y);
        return _eligible[(y * TilesX) + x];
    }

    public float GetSuitability(int x, int y)
    {
        EnsureCoordinate(x, y);
        return _suitability[(y * TilesX) + x];
    }

    private void EnsureCoordinate(int x, int y)
    {
        if ((uint)x >= (uint)TilesX || (uint)y >= (uint)TilesY)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }
    }

    internal bool[] Eligible => _eligible;

    internal float[] Suitability => _suitability;
}

public static class CampaignResourceSuitabilityEvaluator
{
    private const double Epsilon = 0.0001;
    private const double SoftAffinityFloor = 0.12;
    private const double AlternativePeakWeight = 0.50;

    public static CampaignResourceSuitabilityEvaluation Evaluate(
        CampaignResourceDefinition definition,
        CampaignResourceTerrainSnapshot terrain,
        CampaignResourceSupportFields supportFields,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(supportFields);
        definition.EnsureValid();
        if (!ReferenceEquals(supportFields.Terrain, terrain))
        {
            throw new ArgumentException(
                "Support fields and suitability terrain must come from the same immutable snapshot.",
                nameof(supportFields));
        }

        var preferredFactors = new List<float[]>();
        foreach (var tag in definition.Rules.PreferredTerrainTags)
        {
            if (supportFields.TryGetValues(tag, out var values))
            {
                preferredFactors.Add(values);
            }
        }

        var avoidedFactors = new List<float[]>();
        foreach (var tag in definition.Rules.AvoidedTerrainTags)
        {
            if (supportFields.TryGetValues(tag, out var values))
            {
                avoidedFactors.Add(values);
            }
        }

        var exactFactors = new List<(float[] Values, double Weight)>();
        foreach (var pair in definition.Rules.FieldWeights.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (pair.Value != 0 && supportFields.TryGetValues(pair.Key, out var values))
            {
                exactFactors.Add((values, pair.Value));
            }
        }

        foreach (var pair in definition.Rules.AssociationWeights.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (pair.Value != 0 && supportFields.TryGetValues(pair.Key, out var values))
            {
                exactFactors.Add((values, pair.Value));
            }
        }

        var unsupported = definition.Rules.PreferredTerrainTags
            .Concat(definition.Rules.AvoidedTerrainTags)
            .Concat(definition.Rules.FieldWeights.Keys)
            .Concat(definition.Rules.AssociationWeights.Keys)
            .Where(id => !CampaignResourceSupportFieldIds.IsSupported(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        var count = checked((int)terrain.Definition.TileCount);
        var eligible = new bool[count];
        var suitability = new float[count];
        var samples = terrain.AsSpan();
        var eligibleCount = 0;
        for (var index = 0; index < count; index++)
        {
            if ((index & 0x0FFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var sample = samples[index];
            if (!PassesHardRules(definition, sample))
            {
                continue;
            }

            eligible[index] = true;
            eligibleCount++;
            if (unsupported.Length > 0)
            {
                continue;
            }

            if (preferredFactors.Count == 0 && avoidedFactors.Count == 0 && exactFactors.Count == 0)
            {
                suitability[index] = 1;
                continue;
            }

            var weightedLog = 0.0;
            var totalWeight = 0.0;
            if (preferredFactors.Count > 0)
            {
                AddResponse(
                    GetSoftAlternativeResponse(preferredFactors, index, invert: false),
                    magnitude: 1,
                    ref weightedLog,
                    ref totalWeight);
            }

            if (avoidedFactors.Count > 0)
            {
                AddResponse(
                    GetSoftAlternativeResponse(avoidedFactors, index, invert: true),
                    magnitude: 1,
                    ref weightedLog,
                    ref totalWeight);
            }

            foreach (var factor in exactFactors)
            {
                var response = factor.Values[index];
                var magnitude = Math.Abs(factor.Weight);
                if (factor.Weight < 0)
                {
                    response = 1 - response;
                }

                AddResponse(response, magnitude, ref weightedLog, ref totalWeight);
            }

            suitability[index] = totalWeight <= double.Epsilon
                ? 1
                : (float)Math.Clamp(Math.Exp(weightedLog / totalWeight), 0, 1);
        }

        return new CampaignResourceSuitabilityEvaluation(
            definition,
            terrain.Definition.TilesX,
            terrain.Definition.TilesY,
            eligible,
            suitability,
            eligibleCount,
            unsupported);
    }

    private static double GetSoftAlternativeResponse(
        IReadOnlyList<float[]> factors,
        int index,
        bool invert)
    {
        var maximum = 0.0;
        var total = 0.0;
        foreach (var values in factors)
        {
            var value = values[index];
            maximum = Math.Max(maximum, value);
            total += value;
        }

        // Preferred and avoided tag lists describe alternative ordinary cues, not a hidden
        // requirement that every cue peak on the same tile. Half peak response lets one strong
        // cue carry the group; half mean response still rewards agreement between several cues.
        var strength =
            (AlternativePeakWeight * maximum) +
            ((1 - AlternativePeakWeight) * (total / factors.Count));
        var directedStrength = invert ? 1 - strength : strength;
        return SoftAffinityFloor + ((1 - SoftAffinityFloor) * directedStrength);
    }

    private static void AddResponse(
        double response,
        double magnitude,
        ref double weightedLog,
        ref double totalWeight)
    {
        weightedLog += magnitude * Math.Log(Math.Max(Epsilon, response));
        totalWeight += magnitude;
    }

    private static bool PassesHardRules(
        CampaignResourceDefinition definition,
        CampaignResourceTerrainSample sample)
    {
        if (sample.Kind == CampaignResourceTerrainKind.Unassigned)
        {
            return false;
        }

        if (definition.DistributionProfile == CampaignResourceDistributionProfile.Aquatic &&
            sample.Kind != CampaignResourceTerrainKind.Water)
        {
            return false;
        }

        if (definition.Medium == CampaignResourceMedium.Land &&
            sample.Kind != CampaignResourceTerrainKind.Land)
        {
            return false;
        }

        if (definition.Medium == CampaignResourceMedium.Water &&
            sample.Kind != CampaignResourceTerrainKind.Water)
        {
            return false;
        }

        if (definition.Rules.ExcludedTerrainSurfaces.Contains(sample.Surface))
        {
            return false;
        }

        if (!InRange(sample.ElevationMeters, definition.Rules.ElevationMeters) ||
            !InRange(sample.MaximumCardinalGrade, definition.Rules.Grade) ||
            !InRange(sample.NearestWaterDistanceKilometers, definition.Rules.WaterDistanceKilometers))
        {
            return false;
        }

        if (sample.CustomTerrainId is not { } customTerrainId)
        {
            return true;
        }

        if (definition.Rules.CustomTerrainIncludes.Count > 0 &&
            !definition.Rules.CustomTerrainIncludes.Contains(customTerrainId, StringComparer.Ordinal))
        {
            return false;
        }

        return !definition.Rules.CustomTerrainExcludes.Contains(customTerrainId, StringComparer.Ordinal);
    }

    private static bool InRange(double value, CampaignResourceRange? range) =>
        range is null || (value >= range.Value.Minimum && value <= range.Value.Maximum);
}
