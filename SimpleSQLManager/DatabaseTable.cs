using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;

namespace SimpleSQLManager;

public class DatabaseTable : NavigationItem
{
    public DatabaseTable(string name, Database database) : base(name)
    {
        TableName = name;
        Database = database;

        Actions.Add(new ActionItem("Select Top 1000", SelectTop1000Command));
        Actions.Add(new ActionItem("Edit Data", EditDataCommand));
        Actions.Add(new ActionItem("Refresh", ReloadCommand));
    }

    public string TableName { get; }

    public Database Database { get; }

    public override async Task Load()
    {
        if (Nodes is not null)
        {
            return;
        }

        var query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{TableName}'";
        var columnNames = await SQLExecutor.QueryList(this, query);

        var columns = new ObservableCollection<NavigationItem>(columnNames.Select(name => new DatabaseColumn(name, this)));

        Nodes = columns;
    }

    public ICommand SelectTop1000Command => new Command(async () => await SelectTop1000());

    private async Task SelectTop1000()
    {
        var queryTab = Database.Server.ActionManager.CreateNewTab(Database);

        queryTab.SQLText = $"SELECT TOP (1000) * FROM [{TableName}]";

        await queryTab.ExecuteSQL();
    }

    public ICommand EditDataCommand => new Command(async () => await EditData());

    private async Task EditData()
    {
        var header = $"Edit {Database.DatabaseName}.{TableName}";

        var queryTab = Database.Server.ActionManager.CreateNewTab(header, Database);

        queryTab.ReadOnly = false;

        queryTab.SQLText = $"SELECT * FROM [{TableName}]";

        await queryTab.ExecuteSQL();
    }

    public override string ToString()
    {
        return TableName;
    }
}
