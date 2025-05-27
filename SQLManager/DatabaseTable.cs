using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;

namespace SQLManager;

public class DatabaseTable(string tableName, Database database) : Model, ICanQuery
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

    public async Task Load()
    {
        if (Columns is not null)
        {
            return;
        }

        var query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{TableName}'";
        var columnNames = await SQLExecutor.QueryList(this, query);

        var columns = new ObservableCollection<DatabaseColumn>(columnNames.Select(name => new DatabaseColumn(name, this)));

        Columns = columns;
    }

    public ICommand ReloadCommand => new Command(async () => await Reload());

    public async Task Reload()
    {
        Columns = null;
        await Load();
    }

    public ICommand SelectTop1000Command => new Command(async () => await SelectTop1000());

    private async Task SelectTop1000()
    {
        var queryTab = Database.Server.QueryTabManager.CreateNewTab(Database);

        queryTab.SQLText = $"SELECT TOP (1000) * FROM [{TableName}]";

        await queryTab.ExecuteSQL();
    }

    public override string ToString()
    {
        return TableName;
    }
}
