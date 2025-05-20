using Dapper;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace SQLManager;

public class MainWindowModel : Model
{
    private string? _ServerName = "localhost\\sqlexpress";
    private ObservableCollection<Database> _Databases = [];
    private string? _SQLText;
    private string? _SelectedDatabaseName;
    private string? _SQLResponse;

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

        Databases = new ObservableCollection<Database>(databaseNames.Select(name => new Database(name)));
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
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var query = "SELECT name FROM sys.databases";
        var databaseNames = await connection.QueryAsync<string>(query);
        return databaseNames.ToArray();
    }

    public string? SelectedDatabaseName
    {
        get => _SelectedDatabaseName;
        set
        {
            _SelectedDatabaseName = value;
            NotifyPropertyChanged();
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

        if (string.IsNullOrEmpty(SelectedDatabaseName))
        {
            MessageBox.Show("Please select a database.");
            return;
        }

        var connectionString = CreateConnectionString(ServerName, SelectedDatabaseName);

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

public class Database(string name) : Model
{
    private ObservableCollection<DatabaseTable> _Tables = [ new DatabaseTable("Table1"), new DatabaseTable("Table2"), new DatabaseTable("Table3") ];

    public string DatabaseName { get; } = name;

    public ObservableCollection<DatabaseTable> Tables
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

public class DatabaseTable(string name) : Model
{
    private ObservableCollection<DatabaseColumn> _Columns = [new DatabaseColumn("Col1"), new DatabaseColumn("Col2"), new DatabaseColumn("Col3")];

    public string TableName { get; } = name;

    public ObservableCollection<DatabaseColumn> Columns
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