using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SQLManager;

public abstract class NavigationItem(string name) : Model
{
    private ObservableCollection<NavigationItem>? _Nodes;

    public string Name { get; } = name;

    public ObservableCollection<NavigationItem>? Nodes
    {
        get => _Nodes;
        set
        {
            _Nodes = value;
            NotifyPropertyChanged();
        }
    }

    public abstract Task Load();

    public ICommand ReloadCommand => new Command(async () => await Reload());

    public async Task Reload()
    {
        Nodes = null;
        await Load();
    }

    public ObservableCollection<ActionItem> Actions { get; } = [];
}

public record ActionItem(string Header, ICommand Command);