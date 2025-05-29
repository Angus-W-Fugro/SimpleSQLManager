using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Data.Common;
using System.IO;

namespace SQLManager;

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

    public static DbConnection CreateConnection(NavigationItem navigationItem)
    {
        if (navigationItem is SqlServer server)
        {
            var connString = CreateConnectionString(server);
            var connection = new SqlConnection(connString);
            return connection;
        }

        if (navigationItem is Database database)
        {
            var connString = CreateConnectionString(database);
            var connection = new SqlConnection(connString);
            return connection;
        }

        if (navigationItem is DatabaseTable table)
        {
            var connString = CreateConnectionString(table.Database.Server.ServerName, table.Database.DatabaseName);
            var connection = new SqlConnection(connString);
            return connection;
        }

        if (navigationItem is SQLiteDatabase sqliteDatabase)
        {
            var connString = CreateConnectionStringFromPath(sqliteDatabase.FilePath);
            var connection = new SqliteConnection(connString);
            return connection;
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

    private static string CreateConnectionStringFromPath(string filePath)
    {
        return $"Data Source={filePath};";
    }

    public static async Task ExecuteAsync(NavigationItem navigationItem, string query)
    {
        using var connection = CreateConnection(navigationItem);
        await connection.OpenAsync();
        await connection.ExecuteAsync(query);
    }

    public static async Task<string[]> QueryList(NavigationItem navigationItem, string query)
    {
        var connection = CreateConnection(navigationItem);
        await connection.OpenAsync();
        var result = await connection.QueryAsync<string>(query);
        return result.ToArray();
    }

    public static async Task<DataTable> QueryTable(NavigationItem navigationItem, string query)
    {
        using var connection = CreateConnection(navigationItem);
        await connection.OpenAsync();

        var dataTable = new DataTable();

        await Task.Run(() =>
        {
            using var connection = new SqlConnection(connection);
            using var command = new SqlCommand(query, connection);
            using var adapter = new SqlDataAdapter(command);
            adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;

            adapter.Fill(dataTable);
        });

        return dataTable;

        return await QueryTable(connection, query);
    }

    public static async Task<DataTable> QueryTable(string connectionString, string query)
    {
        
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