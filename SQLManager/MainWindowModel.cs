using Dapper;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Xml;

namespace SQLManager;

public class MainWindowModel : Model
{
    private ObservableCollection<SqlServer> _Servers = [ new SqlServer("localhost\\sqlexpress") ];
    private SqlServer? _SelectedServer;
    private Database? _SelectedDatabase;
    private DatabaseTable? _SelectedTable;
    private DatabaseColumn? _SelectedColumn;

    private string? _SQLText;
    private DataTable? _SQLResponse;
    private string? _NewServerName;
    private bool _ReadOnly = true;

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
        var newServer = new SqlServer(NewServerName);
        currentServers.Add(newServer);

        Servers = new ObservableCollection<SqlServer>(currentServers);
    }

    public async Task LoadServer(SqlServer server)
    {
        if (server.Databases is not null)
        {
            return;
        }

        var connectionString = SQLExecutor.CreateConnectionString(server.ServerName);

        var databaseNames = await GetDatabaseNames(connectionString);

        var databases = new ObservableCollection<Database>(databaseNames.Select(name => new Database(name, server)));

        server.Databases = databases;
    }

    public async Task LoadTable(DatabaseTable table)
    {
        if (table.Columns is not null)
        {
            return;
        }

        var connectionString = SQLExecutor.CreateConnectionString(table.Database);

        var columnNames = await GetColumnNames(connectionString, table.TableName);

        var columns = new ObservableCollection<DatabaseColumn>(columnNames.Select(name => new DatabaseColumn(name, table)));

        table.Columns = columns;
    }

    private async Task<string[]> GetColumnNames(string connectionString, string tableName)
    {
        var query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'";
        return await QueryList(connectionString, query);
    }

    

    private async Task<string[]> GetDatabaseNames(string connectionString)
    {
        var query = "SELECT name FROM sys.databases";
        return await QueryList(connectionString, query);
    }

    private async Task<string[]> QueryList(string connectionString, string query)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var result = await connection.QueryAsync<string>(query);
        return result.OrderBy(x => x).ToArray();
    }

    public async Task LoadSelected(object selectedItem)
    {
        try
        {
            if (selectedItem is SqlServer server)
            {
                await LoadServer(server);
                SelectedServer = server;
                return;
            }

            if (selectedItem is Database database)
            {
                await LoadDatabase(database);
                SelectedDatabase = database;
                return;
            }

            if (selectedItem is DatabaseTable table)
            {
                await LoadTable(table);
                SelectedTable = table;
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading selected item: {ex.Message}");
        }
    }

    public async Task LoadDatabase(Database database)
    {
        if (database.Tables is not null)
        {
            return;
        }

        var connectionString = SQLExecutor.CreateConnectionString(database.Server.ServerName, database.DatabaseName);
        var query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
        var tableNames = await QueryList(connectionString, query);

        var tables = new ObservableCollection<DatabaseTable>(tableNames.Select(name => new DatabaseTable(name, database, ProgrammaticExecuteSQL)));

        database.Tables = tables;
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

        var connectionString = SQLExecutor.CreateConnectionString(SelectedDatabase);

        using var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync();
            var adapter = new SqlDataAdapter(SQLText, connection);
            adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
            var dataTable = new DataTable();
            adapter.Fill(dataTable);

            SQLResponse = dataTable;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error executing command: {ex.Message}");
        }
    }

    public async Task ProgrammaticExecuteSQL(DatabaseTable table, string sql)
    {
        SelectedServer = table.Database.Server;
        SelectedDatabase = table.Database;
        SelectedTable = table;
        SQLText = sql;
        await ExecuteSQL();
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
}

public class SqlServer(string name) : Model
{
    private ObservableCollection<Database>? _Databases;

    public string ServerName { get; } = name;

    public ObservableCollection<Database>? Databases
    {
        get => _Databases;
        set
        {
            _Databases = value;
            NotifyPropertyChanged();
        }
    }

    public override string ToString()
    {
        return ServerName;
    }
}

public class Database(string name, SqlServer server) : Model
{
    private ObservableCollection<DatabaseTable>? _Tables;

    public string DatabaseName { get; } = name;

    public SqlServer Server { get; } = server;

    public ObservableCollection<DatabaseTable>? Tables
    {
        get => _Tables;
        set
        {
            _Tables = value;
            NotifyPropertyChanged();
        }
    }

    public ICommand CreateBackupCommand => new Command(async () => await CreateBackup());

    public async Task CreateBackup()
    {
        var tempFolder = await CreateAccessibleFolder();
        var backupName = $"{DatabaseName}_{DateTime.Now:yyyy_MM_dd_HHmmss}.bak";
        var tempBackupPath = Path.Combine(tempFolder, backupName);
        var backupCmd = $"BACKUP DATABASE [{DatabaseName}] TO DISK = '{tempBackupPath}'";
        await SQLExecutor.ExecuteAsync(this, backupCmd);
        var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var backupPath = Path.Combine(downloadsFolder, backupName);
        
        File.Move(tempBackupPath, backupPath, true);

        // Open in Explorer
        var psi = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{backupPath}\"",
            UseShellExecute = true
        };

        Process.Start(psi);
    }

    /// <summary>
    /// SQL server only has permission to write to folders it specifically creates
    /// </summary>
    private async Task<string> CreateAccessibleFolder()
    {
        var tempPath = @"C:\temp";
        var tempBackupsFolder = Path.Combine(tempPath, "Backups");
        Directory.CreateDirectory(tempBackupsFolder);
        var tempFolder = Path.Combine(tempBackupsFolder, Guid.NewGuid().ToString());
        var createFolderCmd = $"EXECUTE master.dbo.xp_create_subdir '{tempFolder}'";
        await SQLExecutor.ExecuteAsync(this, createFolderCmd);
        return tempFolder;
    }

    public override string ToString()
    {
        return DatabaseName;
    }
}

public class DatabaseTable(string tableName, Database database, Func<DatabaseTable, string, Task> executeSQL) : Model
{
    private ObservableCollection<DatabaseColumn>? _Columns;

    public string TableName { get; } = tableName;

    public Database Database { get; } = database;

    public ObservableCollection<DatabaseColumn>? Columns
    {
        get => _Columns;
        set
        {
            _Columns = value;
            NotifyPropertyChanged();
        }
    }

    public ICommand SelectTop1000Command => new Command(async () => await SelectTop1000());

    private async Task SelectTop1000()
    {
        await executeSQL(this, $"SELECT TOP (1000) * FROM {TableName}");
    }

    public override string ToString()
    {
        return TableName;
    }
}

public class DatabaseColumn(string name, DatabaseTable table) : Model
{
    public string ColumnName { get; } = name;

    public DatabaseTable Table { get; } = table;

    public override string ToString()
    {
        return ColumnName;
    }
}

public class SQLExecutor
{
    public static string CreateConnectionString(SqlServer server)
    {
        return CreateConnectionString(server.ServerName);
    }

    public static string CreateConnectionString(Database database)
    {
        return CreateConnectionString(database.Server.ServerName, database.DatabaseName);
    }

    public static string CreateConnectionString(string serverName)
    {
        return $"Data Source={serverName};Integrated Security=True;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public static string CreateConnectionString(string serverName, string databaseName)
    {
        return $"Data Source={serverName};Initial Catalog={databaseName};Integrated Security=True;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public static Task ExecuteAsync(Database database, string sql)
    {
        var connectionString = CreateConnectionString(database);

        return ExecuteAsync(connectionString, sql);
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}