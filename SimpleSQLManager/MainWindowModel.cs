using SimpleSQLManager.Properties;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace SimpleSQLManager;

public class MainWindowModel : Model
{
    private ObservableCollection<SqlServer> _Servers;
    private SqlServer? _SelectedServer;
    private Database? _SelectedDatabase;
    private DatabaseTable? _SelectedTable;
    private DatabaseColumn? _SelectedColumn;

    private string? _SQLText;
    private DataTable? _SQLResponse;
    private string? _NewServerName;
    private bool _ReadOnly = true;
    private string? _SelectedItemPath;
    private QueryTabModel? _SelectedTab;
    private ActionManager _ActionManager;

    public MainWindowModel()
    {
        _ActionManager = new ActionManager(CreateNewTab, Disconnect);
        _Servers = [];

        if (Settings.Default.Servers is null || Settings.Default.Servers.Count == 0)
        {
            Settings.Default.Servers =
            [
                @"localhost\sqlexpress", // Default server
            ];

            Settings.Default.Save();
        }

        foreach (var serverName in Settings.Default.Servers)
        {
            var sqlServer = new SqlServer(serverName!, _ActionManager);
            _Servers.Add(sqlServer);
        }

        SelectedServer = _Servers[0];
    }

    private void Disconnect(string serverName)
    {
        var server = _Servers.FirstOrDefault(s => s.ServerName == serverName);

        if (server is null)
        {
            MessageBox.Show($"Server '{serverName}' not found.");
            return;
        }

        Servers.Remove(server);
        Settings.Default.Servers.Remove(serverName);
        Settings.Default.Save();

        SelectedServer = null;
    }

    public string? SelectedItemPath
    {
        get => _SelectedItemPath;
        set
        {
            _SelectedItemPath = value;
            NotifyPropertyChanged();
        }
    }

    private void UpdateSelectedItemPath()
    {
        NavigationItem?[] selectedItems = [SelectedServer, SelectedDatabase, SelectedTable, SelectedColumn];

        var names = selectedItems.Where(item => item is not null)
                                 .Select(item => item!.Name);

        SelectedItemPath = string.Join(" > ", names);
    }

    public ObservableCollection<SqlServer> Servers
    {
        get => _Servers;
        set
        {
            _Servers = value;
            NotifyPropertyChanged();
        }
    }

    public SqlServer? SelectedServer
    {
        get => _SelectedServer;
        set
        {
            _SelectedServer = value;
            NotifyPropertyChanged();

            SelectedDatabase = null;
        }
    }

    public Database? SelectedDatabase
    {
        get => _SelectedDatabase;
        set
        {
            _SelectedDatabase = value;
            NotifyPropertyChanged();

            SelectedTable = null;
        }
    }

    public DatabaseTable? SelectedTable
    {
        get => _SelectedTable;
        set
        {
            _SelectedTable = value;
            NotifyPropertyChanged();

            SelectedColumn = null;
        }
    }

    public DatabaseColumn? SelectedColumn
    {
        get => _SelectedColumn;
        set
        {
            _SelectedColumn = value;
            NotifyPropertyChanged();

            UpdateSelectedItemPath();
        }
    }

    public string? NewServerName
    {
        get => _NewServerName;
        set
        {
            _NewServerName = value;
            NotifyPropertyChanged();
        }
    }

    public ICommand AddServerCommand => new Command(AddServer);

    private void AddServer()
    {
        if (string.IsNullOrEmpty(NewServerName))
        {
            MessageBox.Show("Please enter a server name.");
            return;
        }

        var currentServers = Servers.ToList();
        var newServer = new SqlServer(NewServerName, _ActionManager);
        currentServers.Add(newServer);

        Servers = new ObservableCollection<SqlServer>(currentServers);

        Settings.Default.Servers.Add(NewServerName);
        Settings.Default.Save();
    }

    public async Task LoadSelected(object selectedItem)
    {
        try
        {
            if (selectedItem is SqlServer server)
            {
                await server.Load();
                SelectedServer = server;
                return;
            }

            if (selectedItem is Database database)
            {
                await database.Load();
                SelectedDatabase = database;
                return;
            }

            if (selectedItem is DatabaseTable table)
            {
                await table.Load();
                SelectedTable = table;
                return;
            }

            if (selectedItem is DatabaseColumn column)
            {
                SelectedColumn = column;
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading selected item: {ex.Message}");
        }
    }

    public QueryTabModel CreateNewTab(string header, Database database)
    {
        var queryTab = new QueryTabModel(header, database, CloseTab);

        QueryTabs.Add(queryTab);

        SelectedTab = queryTab;

        return queryTab;
    }

    public void CloseTab(QueryTabModel queryTab)
    {
        QueryTabs.Remove(queryTab);
    }

    public ObservableCollection<QueryTabModel> QueryTabs { get; } = [];

    public QueryTabModel? SelectedTab
    {
        get => _SelectedTab;
        set
        {
            _SelectedTab = value;
            NotifyPropertyChanged();
        }
    }

    public async Task HandleFiles(string[] files)
    {
        var backupFiles = files.Where(f => Path.GetExtension(f).Equals(".bak", StringComparison.OrdinalIgnoreCase));

        var backupFile = backupFiles.FirstOrDefault();

        if (backupFile is null)
        {
            return;
        }

        if (SelectedServer is null)
        {
            MessageBox.Show("Please select a server first.");
            return;
        }

        await SelectedServer.RestoreBackup(backupFile);
    }
}
