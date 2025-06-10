namespace SimpleSQLManager;

public class ActionManager(Func<string, Database, QueryTabModel> createNewTab, Action<string> disconnectServer)
{
    public QueryTabModel CreateNewTab(Database database)
    {
        var header = $"Query {database.DatabaseName}";
        return CreateNewTab(header, database);
    }

    public QueryTabModel CreateNewTab(string header, Database database) => createNewTab(header, database);

    public void DisconnectServer(string serverName) => disconnectServer(serverName);
}
