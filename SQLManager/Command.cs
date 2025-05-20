using System.Windows.Input;

namespace SQLManager;

public class Command : ICommand
{
    private readonly Action _Action;
    private readonly Func<bool>? _CanExecute;

    public Command(Action action, Func<bool>? canExecute = null)
    {
        _Action = action;
        _CanExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _CanExecute is null || _CanExecute.Invoke();
    }

    public void Execute(object? parameter)
    {
        _Action();
    }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _Execute;
    private readonly Func<object?, bool>? _CanExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute)
    {
        _Execute = execute;
        _CanExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_CanExecute is null)
        {
            return true;
        }

        return _CanExecute(parameter);
    }

    public void Execute(object? parameter)
    {
        _Execute(parameter);
    }

    public static void CheckStatus()
    {
        CommandManager.InvalidateRequerySuggested();
    }

    public void OnCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
