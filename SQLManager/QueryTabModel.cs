using System.Windows.Input;
using System.Windows;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SQLManager;

public class QueryTabModel(string header, Database database) : Model
{
    private string? _SQLText;
    private DataTable? _SQLResponse;
    private bool _ReadOnly = true;

    public string TabHeader { get; } = header;

    public Database Database { get; } = database;

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
        if (string.IsNullOrEmpty(SQLText))
        {
            MessageBox.Show("No SQL query");
            return;
        }

        try
        {
            var dataTable = await SQLExecutor.QueryTable(Database, SQLText);
            SQLResponse = dataTable;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error executing command: {ex.Message}");
        }
    }

    public bool ReadOnly
    {
        get => _ReadOnly;
        set
        {
            _ReadOnly = value;
            NotifyPropertyChanged();
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

    public ICommand SaveChangesCommand => new Command(async () => await SaveChanges());

    private async Task SaveChanges()
    {
        if (SQLText is null || SQLResponse is null)
        {
            return;
        }

        var changedRows = SQLResponse.GetChanges();

        if (changedRows is null)
        {
            MessageBox.Show("No changes to save.");
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                var connectionString = SQLExecutor.CreateConnectionString(Database);
                using var connection = new SqlConnection(connectionString);

                connection.Open();

                var adapter = new SqlDataAdapter(SQLText, connection);

                var builder = new SqlCommandBuilder(adapter);
                builder.QuotePrefix = "[";
                builder.QuoteSuffix = "]";

                builder.GetUpdateCommand();

                adapter.Update(changedRows);

                SQLResponse.AcceptChanges();
            });

            MessageBox.Show($"Changes to {changedRows.Rows.Count} row(s) saved.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving changes: {ex.Message}");
        }
    }

    public ICommand CancelChangesCommand => new Command(CancelChanges);

    private void CancelChanges()
    {
        if (SQLResponse is null)
        {
            return;
        }

        SQLResponse.RejectChanges();
    }
}
