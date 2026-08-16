using Avalonia.Media;
using Kingdom.World.Core.Models;

namespace Kingdom.World.Editor.Models;

public static class ElevationColorScale
{
    private static readonly Color DeepWater = Color.Parse("#0A2638");
    private static readonly Color ShallowWater = Color.Parse("#2B93AF");
    private static readonly Color Lowland = Color.Parse("#2E6B50");
    private static readonly Color Meadow = Color.Parse("#7FA34F");
    private static readonly Color OpenUpland = Color.Parse("#B0AA55");
    private static readonly Color Earth = Color.Parse("#B77B43");
    private static readonly Color Highland = Color.Parse("#8B7561");
    private static readonly Color Rock = Color.Parse("#B3B2AA");
    private static readonly Color Alpine = Color.Parse("#ECEFEA");

    public static Color GetColor(double elevationMeters, CampaignWorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var clamped = Math.Clamp(
            elevationMeters,
            definition.MinimumHeightMeters,
            definition.MaximumHeightMeters);
        var aboveSeaLevel = clamped - definition.SeaLevelMeters;
        if (aboveSeaLevel <= 0)
        {
            return Lerp(
                DeepWater,
                ShallowWater,
                Normalize(clamped, definition.MinimumHeightMeters, definition.SeaLevelMeters));
        }

        if (aboveSeaLevel <= 50)
        {
            return Lerp(Lowland, Meadow, aboveSeaLevel / 50);
        }

        if (aboveSeaLevel <= 200)
        {
            return Lerp(Meadow, OpenUpland, (aboveSeaLevel - 50) / 150);
        }

        if (aboveSeaLevel <= 500)
        {
            return Lerp(OpenUpland, Earth, (aboveSeaLevel - 200) / 300);
        }

        if (aboveSeaLevel <= 1_000)
        {
            return Lerp(Earth, Highland, (aboveSeaLevel - 500) / 500);
        }

        if (aboveSeaLevel <= 2_000)
        {
            return Lerp(Highland, Rock, (aboveSeaLevel - 1_000) / 1_000);
        }

        return Lerp(
            Rock,
            Alpine,
            Normalize(
                clamped,
                definition.SeaLevelMeters + 2_000d,
                definition.MaximumHeightMeters));
    }

    public static string GetBandName(double elevationMeters, CampaignWorldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var aboveSeaLevel = elevationMeters - definition.SeaLevelMeters;
        return aboveSeaLevel switch
        {
            <= 0 => "Below sea level",
            <= 50 => "Lowland",
            <= 200 => "Open upland",
            <= 500 => "Earth",
            <= 1_000 => "Highland",
            <= 2_000 => "Rock",
            _ => "Alpine",
        };
    }

    private static double Normalize(double value, double minimum, double maximum) =>
        maximum <= minimum ? 0 : Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    private static Color Lerp(Color left, Color right, double amount)
    {
        var clamped = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            Lerp(left.R, right.R, clamped),
            Lerp(left.G, right.G, clamped),
            Lerp(left.B, right.B, clamped));
    }

    private static byte Lerp(byte left, byte right, double amount) =>
        (byte)Math.Round(left + (right - left) * amount);
}
