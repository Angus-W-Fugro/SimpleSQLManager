using Dapper;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Windows;
using System.Windows.Input;

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

        var connectionString = CreateConnectionString(server.ServerName);

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

        var connectionString = CreateConnectionString(table.Database);

        var columnNames = await GetColumnNames(connectionString, table.TableName);

        var columns = new ObservableCollection<DatabaseColumn>(columnNames.Select(name => new DatabaseColumn(name, table)));

        table.Columns = columns;
    }

    private async Task<string[]> GetColumnNames(string connectionString, string tableName)
    {
        var query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'";
        return await QueryList(connectionString, query);
    }

    private static string CreateConnectionString(SqlServer server)
    {
        return CreateConnectionString(server.ServerName);
    }

    private static string CreateConnectionString(Database database)
    {
        return CreateConnectionString(database.Server.ServerName, database.DatabaseName);
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

        var connectionString = CreateConnectionString(database.Server.ServerName, database.DatabaseName);
        var query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
        var tableNames = await QueryList(connectionString, query);

        var tables = new ObservableCollection<DatabaseTable>(tableNames.Select(name => new DatabaseTable(name, database)));

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

    public ICommand ExecuteSqlCommand => new Command(async () => await ExecuteSql());

    public async Task ExecuteSql()
    {
        if (Servers.Count == 0)
        {
            MessageBox.Show("Please connect to a server first.");
            return;
        }

        if (string.IsNullOrEmpty(SQLText))
        {
            MessageBox.Show("Please enter a SQL command.");
            return;
        }

        if (SelectedDatabase is null)
        {
            MessageBox.Show("Please select a database.");
            return;
        }

        var connectionString = CreateConnectionString(SelectedDatabase);

        using var connection = new SqlConnection(connectionString);

        try
        {
            await connection.OpenAsync();
            var reader = await connection.ExecuteReaderAsync(SQLText);
            var dataTable = new DataTable();
            dataTable.Load(reader);
            SQLResponse = dataTable;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error executing command: {ex.Message}");
        }
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

    private void EditTable()
    {
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

        var changedRows = SQLResponse.Rows.OfType<DataRow>().Where(dr => dr.RowState == DataRowState.Modified);

        if (!changedRows.Any())
        {
            MessageBox.Show("No changes to save.");
            return;
        }

        var connectionString = CreateConnectionString(SelectedDatabase);

        using var connection = new SqlConnection(connectionString);

        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var row in changedRows)
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction;

                var updateQuery = new StringBuilder($"UPDATE {SelectedTable.TableName} SET ");

                var setClauses = new List<string>();
                var parameters = new List<SqlParameter>();

                // Assuming the first column is the primary key
                var primaryKeyColumn = SQLResponse.Columns[0];

                foreach (DataColumn column in SQLResponse.Columns)
                {
                    if (column == primaryKeyColumn)
                    {
                        continue;
                    }

                    if (row[column] is not DBNull)
                    {
                        setClauses.Add($"[{column.ColumnName}] = @{column.ColumnName}");
                        parameters.Add(new SqlParameter($"@{column.ColumnName}", row[column]));
                    }
                }

                updateQuery.Append(string.Join(", ", setClauses));
                updateQuery.Append(" WHERE ");

                updateQuery.Append($"[{primaryKeyColumn.ColumnName}] = @{primaryKeyColumn.ColumnName}");
                parameters.Add(new SqlParameter($"@{primaryKeyColumn.ColumnName}", row[primaryKeyColumn]));

                command.CommandText = updateQuery.ToString();
                command.Parameters.AddRange(parameters.ToArray());

                await command.ExecuteNonQueryAsync();
            }

            transaction.Commit();
            MessageBox.Show("Changes saved successfully.");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
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

    public override string ToString()
    {
        return DatabaseName;
    }
}

public class DatabaseTable(string tableName, Database database) : Model
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