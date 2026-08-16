namespace Kingdom.World.Core.Serialization;

public sealed class WorldFormatException : Exception
{
    public WorldFormatException(string message)
        : base(message)
    {
    }

    public WorldFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
