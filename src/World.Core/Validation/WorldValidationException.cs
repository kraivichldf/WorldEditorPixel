namespace Kingdom.World.Core.Validation;

public sealed class WorldValidationException : Exception
{
    public WorldValidationException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
