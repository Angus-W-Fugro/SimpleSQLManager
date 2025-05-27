namespace SQLManager;

public class QueryTabManager(Func<Database, QueryTabModel> createNewTab)
{
    public QueryTabModel CreateNewTab(Database database) => createNewTab(database);
}
