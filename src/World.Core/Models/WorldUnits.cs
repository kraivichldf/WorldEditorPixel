namespace Kingdom.World.Core.Models;

public static class WorldUnits
{
    public const long MetersPerKilometer = 1_000;

    public static long KilometersToMeters(long kilometers)
    {
        if (kilometers < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kilometers), "Distance in kilometres cannot be negative.");
        }

        return checked(kilometers * MetersPerKilometer);
    }

    public static decimal MetersToKilometers(long meters) => meters / (decimal)MetersPerKilometer;
}
