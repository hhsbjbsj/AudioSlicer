namespace AudioSlicer.Editing;

public sealed class UndoManager<TSnapshot>
{
    private readonly Stack<(TSnapshot Before, TSnapshot After)> _undo = new();
    private readonly Stack<(TSnapshot Before, TSnapshot After)> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Record(TSnapshot before, TSnapshot after)
    {
        _undo.Push((before, after));
        _redo.Clear();
    }

    public TSnapshot Undo(TSnapshot current)
    {
        if (_undo.Count == 0) return current;
        var command = _undo.Pop();
        _redo.Push(command);
        return command.Before;
    }

    public TSnapshot Redo(TSnapshot current)
    {
        if (_redo.Count == 0) return current;
        var command = _redo.Pop();
        _undo.Push(command);
        return command.After;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}

