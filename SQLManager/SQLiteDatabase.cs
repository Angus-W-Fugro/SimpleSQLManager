using System.Collections.ObjectModel;
using System.IO;

namespace SQLManager;

public class SQLiteDatabase : NavigationItem
{
    public SQLiteDatabase(string filePath, QueryTabManager queryTabManager) : base(Path.GetFileName(filePath))
    {
        Actions.Add(new ActionItem("Refresh", ReloadCommand));
        FilePath = filePath;
        QueryTabManager = queryTabManager;
    }

    public string FilePath { get; }

    public QueryTabManager QueryTabManager { get; }

    public override async Task Load()
    {
        if (Nodes is not null)
        {
            return;
        }

        Nodes = await GetSQLiteTables();
    }

    private async Task<ObservableCollection<NavigationItem>?> GetSQLiteTables()
    {
        var query = "SELECT name FROM sqlite_master WHERE type='table';";
        var tableNames = await SQLExecutor.QueryList(this, query);
        var tables = tableNames.Select(tableName => new SQLiteTable(tableName));
        return new ObservableCollection<NavigationItem>(tables);
    }
}
