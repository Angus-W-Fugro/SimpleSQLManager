using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using System.IO;

namespace SimpleSQLManager;

public class SQLExecutor
{
    public static string CreateConnectionString(SqlServer server)
    {
        return CreateConnectionString(server.ServerName);
    }

    public static string CreateConnectionString(Database database)
    {
        return CreateConnectionString(database.Server.ServerName, database.DatabaseName);
    }

    public static string CreateConnectionString(NavigationItem canQuery)
    {
        if (canQuery is SqlServer server)
        {
            return CreateConnectionString(server);
        }

        if (canQuery is Database database)
        {
            return CreateConnectionString(database);
        }

        if (canQuery is DatabaseTable table)
        {
            return CreateConnectionString(table.Database.Server.ServerName, table.Database.DatabaseName);
        }

        throw new ArgumentException("Unsupported type for ICanQuery");
    }

    public static string CreateConnectionString(string serverName)
    {
        return $"Data Source={serverName};Integrated Security=True;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public static string CreateConnectionString(string serverName, string databaseName)
    {
        return $"Data Source={serverName};Initial Catalog={databaseName};Integrated Security=True;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public static Task ExecuteAsync(NavigationItem canQuery, string sql)
    {
        var connectionString = CreateConnectionString(canQuery);

        return ExecuteAsync(connectionString, sql);
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<string[]> QueryList(NavigationItem canQuery, string query)
    {
        var connectionString = CreateConnectionString(canQuery);

        return await QueryList(connectionString, query);
    }

    public static async Task<string[]> QueryList(string connectionString, string query)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var result = await connection.QueryAsync<string>(query);
        return result.ToArray();
    }

    public static async Task<DataTable> QueryTable(NavigationItem canQuery, string query)
    {
        var connectionString = CreateConnectionString(canQuery);
        return await QueryTable(connectionString, query);
    }

    public static async Task<DataTable> QueryTable(string connectionString, string query)
    {
        var dataTable = new DataTable();

        await Task.Run(() =>
        {
            using var connection = new SqlConnection(connectionString);
            using var command = new SqlCommand(query, connection);
            using var adapter = new SqlDataAdapter(command);
            adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;

            adapter.Fill(dataTable);
        });

        return dataTable;
    }

    /// <summary>
    /// SQL server only has permission to write to folders it specifically creates
    /// </summary>
    public static async Task<string> CreateAccessibleFolder(SqlServer server)
    {
        var tempPath = @"C:\temp";
        var tempBackupsFolder = Path.Combine(tempPath, "Backups");
        Directory.CreateDirectory(tempBackupsFolder);
        var serverFolder = Path.Combine(tempBackupsFolder, server.ServerName);

        if (Directory.Exists(serverFolder))
        {
            return serverFolder;
        }

        var createFolderCmd = $"EXECUTE master.dbo.xp_create_subdir '{serverFolder}'";
        await ExecuteAsync(server, createFolderCmd);
        return serverFolder;
    }

    public static async Task<string> MakePathAccessible(string path, SqlServer server)
    {
        var tempFolder = await CreateAccessibleFolder(server);
        var fileName = Path.GetFileName(path);
        var accessiblePath = Path.Combine(tempFolder, fileName);
        File.Copy(path, accessiblePath, true);
        return accessiblePath;
    }
}