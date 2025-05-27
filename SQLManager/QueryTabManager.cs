namespace SQLManager;

public class QueryTabManager(Func<string, Database, QueryTabModel> createNewTab)
{
    public QueryTabModel CreateNewTab(Database database)
    {
        var header = $"Query {database.DatabaseName}";
        return CreateNewTab(header, database);
    }

    public QueryTabModel CreateNewTab(string header, Database database) => createNewTab(header, database);
}
