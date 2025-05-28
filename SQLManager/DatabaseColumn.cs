
namespace SQLManager;

public class DatabaseColumn(string name, DatabaseTable table) : NavigationItem(name)
{
    public string ColumnName { get; } = name;

    public DatabaseTable Table { get; } = table;

    public override Task Load()
    {
        return Task.CompletedTask;
    }

    public override string ToString()
    {
        return ColumnName;
    }
}
