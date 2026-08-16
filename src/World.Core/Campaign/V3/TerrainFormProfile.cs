using Kingdom.World.Core.Validation;

namespace Kingdom.World.Core.Campaign.V3;

public sealed record TerrainFormProfile
{
    public static TerrainFormProfile Default { get; } = new();

    public double RollingMinimumGrade { get; init; } = 0.01;

    public double HillsMinimumGrade { get; init; } = 0.04;

    public double MountainMinimumGrade { get; init; } = 0.12;

    public double CliffMinimumGrade { get; init; } = 0.30;

    public double MountainMinimumProminenceMeters { get; init; } = 600;

    public double MountainMinimumElevationAboveSeaMeters { get; init; } = 1_500;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        ValidateFiniteNonNegative(RollingMinimumGrade, nameof(RollingMinimumGrade), errors);
        ValidateFiniteNonNegative(HillsMinimumGrade, nameof(HillsMinimumGrade), errors);
        ValidateFiniteNonNegative(MountainMinimumGrade, nameof(MountainMinimumGrade), errors);
        ValidateFiniteNonNegative(CliffMinimumGrade, nameof(CliffMinimumGrade), errors);
        ValidateFiniteNonNegative(
            MountainMinimumProminenceMeters,
            nameof(MountainMinimumProminenceMeters),
            errors);
        ValidateFiniteNonNegative(
            MountainMinimumElevationAboveSeaMeters,
            nameof(MountainMinimumElevationAboveSeaMeters),
            errors);

        if (double.IsFinite(RollingMinimumGrade) &&
            double.IsFinite(HillsMinimumGrade) &&
            double.IsFinite(MountainMinimumGrade) &&
            double.IsFinite(CliffMinimumGrade) &&
            !(RollingMinimumGrade < HillsMinimumGrade &&
              HillsMinimumGrade < MountainMinimumGrade &&
              MountainMinimumGrade < CliffMinimumGrade))
        {
            errors.Add(
                "Terrain-form grades must be strictly ordered: " +
                "Rolling < Hills < Mountain < Cliff.");
        }

        return errors;
    }

    public void EnsureValid()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new WorldValidationException(errors);
        }
    }

    private static void ValidateFiniteNonNegative(
        double value,
        string name,
        ICollection<string> errors)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            errors.Add($"{name} must be finite and non-negative.");
        }
    }
}
