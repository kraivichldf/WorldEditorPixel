namespace Kingdom.World.Core.Brushes;

public sealed record BrushSettings
{
    public double RadiusSamples { get; init; } = 8;

    public double StrengthMeters { get; init; } = 25;

    public double Falloff { get; init; } = 0.55;

    public short TargetElevationMeters { get; init; }

    public void EnsureValid()
    {
        if (!double.IsFinite(RadiusSamples) || RadiusSamples <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RadiusSamples), "Brush radius must be finite and greater than zero.");
        }

        if (!double.IsFinite(StrengthMeters) || StrengthMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StrengthMeters), "Brush strength must be finite and greater than zero.");
        }

        if (!double.IsFinite(Falloff) || Falloff is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Falloff), "Brush falloff must be between zero and one.");
        }
    }
}
