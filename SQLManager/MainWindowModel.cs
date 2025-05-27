using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;

namespace SQLManager;

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
    private QueryTab? _SelectedTab;
    private QueryTabManager _QueryTabManager;

    public MainWindowModel()
    {
        _QueryTabManager = new QueryTabManager(CreateNewTab);
        _Servers = [new SqlServer("localhost\\sqlexpress", _QueryTabManager)];
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
        var selectedItemNames = new List<string>();

        if (SelectedServer is not null)
        {
            selectedItemNames.Add(SelectedServer.ServerName);
        }

        if (SelectedDatabase is not null)
        {
            selectedItemNames.Add(SelectedDatabase.DatabaseName);
        }

        if (SelectedTable is not null)
        {
            selectedItemNames.Add(SelectedTable.TableName);
        }

        if (SelectedColumn is not null)
        {
            selectedItemNames.Add(SelectedColumn.ColumnName);
        }

        SelectedItemPath = string.Join(" > ", selectedItemNames);
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
        var newServer = new SqlServer(NewServerName, _QueryTabManager);
        currentServers.Add(newServer);

        Servers = new ObservableCollection<SqlServer>(currentServers);
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

    public string? SQLText
    {
        get => _SQLText;
        set
        {
            _SQLText = value;
            NotifyPropertyChanged();
        }
    }

    public ICommand ExecuteSqlCommand => new Command(async () => await ExecuteSQL());

    public async Task ExecuteSQL()
    {
        if (SelectedDatabase is null)
        {
            MessageBox.Show("Database not selected");
            return;
        }

        if (string.IsNullOrEmpty(SQLText))
        {
            MessageBox.Show("No SQL query");
            return;
        }

        try
        {
            var dataTable = await SQLExecutor.QueryTable(SelectedDatabase, SQLText);
            SQLResponse = dataTable;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error executing command: {ex.Message}");
        }
    }

    public QueryTabModel CreateNewTab(Database database)
    {
        var queryTabModel = new QueryTabModel(database);

        var queryTab = new QueryTab(queryTabModel);

        QueryTabs.Add(queryTab);

        SelectedTab = queryTab;

        return queryTabModel;
    }

    public DataTable? SQLResponse
    {
        get => _SQLResponse;
        set
        {
            _SQLResponse = value;
            NotifyPropertyChanged();
        }
    }

    public ICommand EditTableCommand => new Command(EditTable);

    public bool ReadOnly
    {
        get => _ReadOnly;
        set
        {
            _ReadOnly = value;
            NotifyPropertyChanged();
        }
    }

    private async void EditTable()
    {
        if (SelectedTable is null)
        {
            return;
        }

        SQLText = $"SELECT * FROM {SelectedTable.TableName}";
        await ExecuteSQL();

        ReadOnly = false;
    }

    public ICommand SaveTableCommand => new Command(async () => await SaveTable());

    private async Task SaveTable()
    {
        if (SQLResponse is null || SelectedDatabase is null || SelectedTable is null)
        {
            return;
        }

        ReadOnly = true;

        var changedRows = SQLResponse.GetChanges();

        if (changedRows is null)
        {
            MessageBox.Show("No changes to save.");
            return;
        }

        var connectionString = SQLExecutor.CreateConnectionString(SelectedDatabase);

        using var connection = new SqlConnection(connectionString);

        connection.Open();

        try
        {
            var adapter = new SqlDataAdapter($"SELECT * FROM {SelectedTable.TableName}", connection);

            var builder = new SqlCommandBuilder(adapter);
            builder.QuotePrefix = "[";
            builder.QuoteSuffix = "]";

            builder.GetUpdateCommand();

            adapter.Update(changedRows);

            MessageBox.Show($"Changes to {changedRows.Rows.Count} row(s) saved.");
            SQLResponse.AcceptChanges();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving changes: {ex.Message}");
        }
    }

    public ObservableCollection<QueryTab> QueryTabs { get; } = [];

    public QueryTab? SelectedTab
    {
        get => _SelectedTab;
        set
        {
            _SelectedTab = value;
            NotifyPropertyChanged();
        }
    }
}
