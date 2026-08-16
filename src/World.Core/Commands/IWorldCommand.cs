namespace Kingdom.World.Core.Commands;

public interface IWorldCommand
{
    string Description { get; }

    bool IsEmpty { get; }

    void Execute();

    void Undo();
}
