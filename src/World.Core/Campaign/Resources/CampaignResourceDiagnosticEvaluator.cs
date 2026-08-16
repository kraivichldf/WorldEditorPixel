namespace Kingdom.World.Core.Campaign.Resources;

public static class CampaignResourceDiagnosticEvaluator
{
    public static CampaignResourceDiagnosticResult Evaluate(
        CampaignResourceDefinition definition,
        CampaignResourceTerrainSample terrain)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.EnsureValid();
        terrain.EnsureValid();

        var issues = new List<CampaignResourceDiagnosticIssue>();
        if (terrain.Kind == CampaignResourceTerrainKind.Unassigned)
        {
            issues.Add(new CampaignResourceDiagnosticIssue(
                CampaignResourceDiagnosticCode.TerrainUnassigned,
                "The resource is on an unassigned terrain cell."));
            return new CampaignResourceDiagnosticResult(issues, GetUnevaluatedFactors(definition));
        }

        if (definition.Medium == CampaignResourceMedium.Land &&
            terrain.Kind != CampaignResourceTerrainKind.Land)
        {
            issues.Add(new CampaignResourceDiagnosticIssue(
                CampaignResourceDiagnosticCode.MediumRequiresLand,
                "This resource requires land terrain."));
        }
        else if (definition.Medium == CampaignResourceMedium.Water &&
                 terrain.Kind != CampaignResourceTerrainKind.Water)
        {
            issues.Add(new CampaignResourceDiagnosticIssue(
                CampaignResourceDiagnosticCode.MediumRequiresWater,
                "This resource requires Sea or Lake terrain."));
        }

        if (definition.Rules.ExcludedTerrainSurfaces.Contains(terrain.Surface))
        {
            issues.Add(new CampaignResourceDiagnosticIssue(
                CampaignResourceDiagnosticCode.TerrainSurfaceExcluded,
                $"The normalized {terrain.Surface} surface is hard-excluded for this resource."));
        }

        AddRangeIssues(
            terrain.ElevationMeters,
            definition.Rules.ElevationMeters,
            CampaignResourceDiagnosticCode.ElevationBelowMinimum,
            CampaignResourceDiagnosticCode.ElevationAboveMaximum,
            "elevation",
            issues);
        AddRangeIssues(
            terrain.MaximumCardinalGrade,
            definition.Rules.Grade,
            CampaignResourceDiagnosticCode.GradeBelowMinimum,
            CampaignResourceDiagnosticCode.GradeAboveMaximum,
            "maximum cardinal grade",
            issues);
        AddRangeIssues(
            terrain.NearestWaterDistanceKilometers,
            definition.Rules.WaterDistanceKilometers,
            CampaignResourceDiagnosticCode.WaterDistanceBelowMinimum,
            CampaignResourceDiagnosticCode.WaterDistanceAboveMaximum,
            "nearest-water distance",
            issues);

        if (terrain.CustomTerrainId is { } customTerrainId)
        {
            if (definition.Rules.CustomTerrainIncludes.Count > 0 &&
                !definition.Rules.CustomTerrainIncludes.Contains(customTerrainId, StringComparer.Ordinal))
            {
                issues.Add(new CampaignResourceDiagnosticIssue(
                    CampaignResourceDiagnosticCode.CustomTerrainNotIncluded,
                    $"Custom terrain '{customTerrainId}' is not in this resource's include whitelist."));
            }

            if (definition.Rules.CustomTerrainExcludes.Contains(customTerrainId, StringComparer.Ordinal))
            {
                issues.Add(new CampaignResourceDiagnosticIssue(
                    CampaignResourceDiagnosticCode.CustomTerrainExcluded,
                    $"Custom terrain '{customTerrainId}' is explicitly excluded for this resource."));
            }
        }

        return new CampaignResourceDiagnosticResult(issues, GetUnevaluatedFactors(definition));
    }

    private static void AddRangeIssues(
        double actual,
        CampaignResourceRange? range,
        CampaignResourceDiagnosticCode belowCode,
        CampaignResourceDiagnosticCode aboveCode,
        string label,
        ICollection<CampaignResourceDiagnosticIssue> issues)
    {
        if (range is null)
        {
            return;
        }

        if (actual < range.Value.Minimum)
        {
            issues.Add(new CampaignResourceDiagnosticIssue(
                belowCode,
                $"The {label} {actual:G6} is below the allowed minimum {range.Value.Minimum:G6}."));
        }
        else if (actual > range.Value.Maximum)
        {
            issues.Add(new CampaignResourceDiagnosticIssue(
                aboveCode,
                $"The {label} {actual:G6} is above the allowed maximum {range.Value.Maximum:G6}."));
        }
    }

    private static IReadOnlyList<CampaignResourceUnevaluatedFactor> GetUnevaluatedFactors(
        CampaignResourceDefinition definition)
    {
        var factors = new List<CampaignResourceUnevaluatedFactor>
        {
            CampaignResourceUnevaluatedFactor.ClimateProfile,
            CampaignResourceUnevaluatedFactor.GeologyProfile,
            CampaignResourceUnevaluatedFactor.DistributionShape,
            CampaignResourceUnevaluatedFactor.FinalGeneratorSuitability,
        };

        if (definition.Rules.PreferredTerrainTags.Count > 0)
        {
            factors.Add(CampaignResourceUnevaluatedFactor.PreferredTerrainTags);
        }

        if (definition.Rules.AvoidedTerrainTags.Count > 0)
        {
            factors.Add(CampaignResourceUnevaluatedFactor.AvoidedTerrainTags);
        }

        if (definition.Rules.FieldWeights.Count > 0)
        {
            factors.Add(CampaignResourceUnevaluatedFactor.FieldWeights);
        }

        if (definition.Rules.AssociationWeights.Count > 0)
        {
            factors.Add(CampaignResourceUnevaluatedFactor.AssociationWeights);
        }

        if (definition.Rules.RegionScaleKilometers is not null)
        {
            factors.Add(CampaignResourceUnevaluatedFactor.RegionScale);
        }

        return factors;
    }
}
