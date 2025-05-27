namespace SQLManager;

public class DatabaseColumn(string name, DatabaseTable table) : Model
{
    public string ColumnName { get; } = name;

    public DatabaseTable Table { get; } = table;

    public override string ToString()
    {
        return ColumnName;
    }
}
