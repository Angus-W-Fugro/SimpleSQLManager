using Dapper;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SQLManager;

public class MainWindowModel : Model
{
    private string? _ServerName = "localhost\\sqlexpress";
    private ObservableCollection<Database> _Databases = [];
    private string? _SQLText;
    private string? _SQLResponse;
    private Database? _SelectedDatabase;

    public string? ServerName
    {
        get => _ServerName;
        set
        {
            _ServerName = value;
            NotifyPropertyChanged();
        }
    }

    public ObservableCollection<Database> Databases
    {
        get => _Databases;
        set
        {
            _Databases = value;
            NotifyPropertyChanged();
        }
    }

    public Database? SelectedDatabase
    {
        get => _SelectedDatabase;
        set
        {
            _SelectedDatabase = value;
            NotifyPropertyChanged();

            if (_SelectedDatabase is not null)
            {
                _ = LoadDatabase(_SelectedDatabase);
            }
        }
    }

    public ICommand ConnectCommand => new Command(async () => await Connect());

    private async Task Connect()
    {
        if (string.IsNullOrEmpty(ServerName))
        {
            MessageBox.Show("Please enter a server name.");
            return;
        }

        var connectionString = CreateConnectionString(ServerName);

        var databaseNames = await GetDatabaseNames(connectionString);

        if (databaseNames.Length == 0)
        {
            MessageBox.Show("No databases found.");
            return;
        }

        Databases = new ObservableCollection<Database>(databaseNames.Select(name => new Database(name, LoadTable)));
    }

    private async void LoadTable(DatabaseTable table)
    {
        if (table.Columns is not null)
        {
            return;
        }

        var connectionString = CreateConnectionString(ServerName!, table.DatabaseName);

        var columnNames = await GetColumnNames(connectionString, table.TableName);

        var columns = new ObservableCollection<DatabaseColumn>(columnNames.Select(name => new DatabaseColumn(name)));

        table.Columns = columns;
    }

    private async Task<string[]> GetColumnNames(string connectionString, string tableName)
    {
        var query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'";
        return await QueryList(connectionString, query);
    }

    private static string CreateConnectionString(string serverName)
    {
        return $"Data Source={serverName};Integrated Security=True;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    private static string CreateConnectionString(string serverName, string databaseName)
    {
        return $"Data Source={serverName};Initial Catalog={databaseName};Integrated Security=True;Trusted_Connection=True;TrustServerCertificate=True;";
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

    private async Task LoadDatabase(Database database)
    {
        if (database.Tables is not null)
        {
            return;
        }

        var tableNames = await GetTableNames(database.DatabaseName);

        var tables = new ObservableCollection<DatabaseTable>(tableNames.Select(name => new DatabaseTable(name, database.DatabaseName)));

        database.Tables = tables;
    }

    private async Task<string[]> GetTableNames(string databaseName)
    {
        var connectionString = CreateConnectionString(ServerName!, databaseName);
        var query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
        return await QueryList(connectionString, query);
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

    public ICommand ExecuteSqlCommand => new Command(async () => await ExecuteSql());

    public async Task ExecuteSql()
    {
        if (Databases.Count == 0)
        {
            MessageBox.Show("Please connect to a server first.");
            return;
        }

        if (string.IsNullOrEmpty(SQLText))
        {
            MessageBox.Show("Please enter a SQL command.");
            return;
        }

        if (string.IsNullOrEmpty(ServerName))
        {
            MessageBox.Show("Please enter a server name.");
            return;
        }

        if (SelectedDatabase is null)
        {
            MessageBox.Show("Please select a database.");
            return;
        }

        var connectionString = CreateConnectionString(ServerName, SelectedDatabase.DatabaseName);

        using var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync();
            var reader = await connection.ExecuteReaderAsync(SQLText);
            var response = GetSQLReaderOutput(reader);
            SQLResponse = response;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error executing command: {ex.Message}");
        }
    }

    public string? SQLResponse
    {
        get => _SQLResponse;
        set
        {
            _SQLResponse = value;
            NotifyPropertyChanged();
        }
    }

    private static string GetSQLReaderOutput(DbDataReader reader)
    {
        if (!reader.HasRows)
        {
            return string.Empty;
        }

        var output = new StringBuilder();

        var columns = new List<string>();

        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        output.AppendLine(string.Join("\t", columns));

        var rows = new List<string[]>();

        while (reader.Read())
        {
            var rowValues = new string[reader.FieldCount];

            for (var i = 0; i < reader.FieldCount; i++)
            {
                rowValues[i] = reader.GetValue(i).ToString() ?? string.Empty;
            }

            rows.Add(rowValues);
        }

        foreach (var row in rows)
        {
            output.AppendLine(string.Join("\t", row));
        }

        return output.ToString();
    }
}

public class Database(string name, Action<DatabaseTable> loadTable) : Model
{
    private ObservableCollection<DatabaseTable>? _Tables;
    private DatabaseTable? _SelectedTable;
    private readonly Action<DatabaseTable> _LoadTable = loadTable;

    public string DatabaseName { get; } = name;

    public ObservableCollection<DatabaseTable>? Tables
    {
        get => _Tables;
        set
        {
            _Tables = value;
            NotifyPropertyChanged();
        }
    }

    public DatabaseTable? SelectedTable
    {
        get => _SelectedTable;
        set
        {
            _SelectedTable = value;
            NotifyPropertyChanged();

            if (_SelectedTable is not null)
            {
                _LoadTable(_SelectedTable);
            }
        }
    }

    public override string ToString()
    {
        return DatabaseName;
    }
}

public class DatabaseTable(string tableName, string databaseName) : Model
{
    private ObservableCollection<DatabaseColumn>? _Columns;

    public string TableName { get; } = tableName;

    public string DatabaseName { get; } = databaseName;

    public ObservableCollection<DatabaseColumn>? Columns
    {
        get => _Columns;
        set
        {
            _Columns = value;
            NotifyPropertyChanged();
        }
    }

    public override string ToString()
    {
        return TableName;
    }
}

public class DatabaseColumn(string name) : Model
{
    public string ColumnName { get; } = name;

    public override string ToString()
    {
        return ColumnName;
    }
}