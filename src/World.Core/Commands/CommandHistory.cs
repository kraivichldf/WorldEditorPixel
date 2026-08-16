namespace Kingdom.World.Core.Commands;

public sealed class CommandHistory
{
    private readonly List<IWorldCommand> _undo = [];
    private readonly List<IWorldCommand> _redo = [];

    public CommandHistory(int capacity = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public event EventHandler? Changed;

    public int Capacity { get; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? NextUndoDescription => CanUndo ? _undo[^1].Description : null;

    public string? NextRedoDescription => CanRedo ? _redo[^1].Description : null;

    public void Execute(IWorldCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        RecordExecuted(command);
    }

    public void RecordExecuted(IWorldCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.IsEmpty)
        {
            return;
        }

        _undo.Add(command);
        if (_undo.Count > Capacity)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        var command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        command.Undo();
        _redo.Add(command);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        command.Execute();
        _undo.Add(command);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
        {
            return;
        }

        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
