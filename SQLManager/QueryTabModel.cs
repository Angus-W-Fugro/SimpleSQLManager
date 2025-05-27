using System.Windows.Input;
using System.Windows;
using System.Data;

namespace SQLManager;

public class QueryTabModel(Database database) : Model
{
    private string? _SQLText;
    private DataTable? _SQLResponse;

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

    public DataTable? SQLResponse
    {
        get => _SQLResponse;
        set
        {
            _SQLResponse = value;
            NotifyPropertyChanged();
        }
    }
}
