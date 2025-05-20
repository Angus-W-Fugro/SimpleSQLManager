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
    private string? _ServerName;
    private ObservableCollection<string> _DatabaseNames = [];
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

    public ObservableCollection<string> DatabaseNames
    {
        get => _DatabaseNames;
        set
        {
            _DatabaseNames = value;
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

        DatabaseNames = new ObservableCollection<string>(databaseNames);
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
        if (DatabaseNames.Count == 0)
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
