using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace SQLManager;

public class Database(string name, SqlServer server) : Model, ICanQuery
{
    private ObservableCollection<DatabaseTable>? _Tables;

    public string DatabaseName { get; } = name;

    public SqlServer Server { get; } = server;

    public ObservableCollection<DatabaseTable>? Tables
    {
        get => _Tables;
        set
        {
            _Tables = value;
            NotifyPropertyChanged();
        }
    }

    public ICommand ReloadCommand => new Command(async () => await Reload());

    public async Task Load()
    {
        if (Tables is not null)
        {
            return;
        }

        var connectionString = SQLExecutor.CreateConnectionString(Server.ServerName, DatabaseName);
        var query = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
        var tableNames = await SQLExecutor.QueryList(this, query);

        var tables = new ObservableCollection<DatabaseTable>(tableNames.Select(name => new DatabaseTable(name, this)));

        Tables = tables;
    }

    public async Task Reload()
    {
        Tables = null;
        await Load();
    }

    public ICommand CreateBackupCommand => new Command(async () => await CreateBackup());

    public async Task CreateBackup()
    {
        var tempFolder = await SQLExecutor.CreateAccessibleFolder(Server);
        var backupName = $"{DatabaseName}_{DateTime.Now:yyyy_MM_dd_HHmmss}.bak";
        var tempBackupPath = Path.Combine(tempFolder, backupName);
        var backupCmd = $"BACKUP DATABASE [{DatabaseName}] TO DISK = '{tempBackupPath}'";
        await SQLExecutor.ExecuteAsync(this, backupCmd);
        var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var backupPath = Path.Combine(downloadsFolder, backupName);
        
        File.Move(tempBackupPath, backupPath, true);

        // Open in Explorer
        var psi = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{backupPath}\"",
            UseShellExecute = true
        };

        Process.Start(psi);
    }

    public ICommand DropDatabaseCommand => new Command(async () => await DropDatabase());

    private async Task DropDatabase()
    {
        if (MessageBox.Show($"Are you sure you want to drop the database '{DatabaseName}'?", "Confirm Drop", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        var dropDatabaseCmd = $@"IF EXISTS (SELECT name FROM sys.databases WHERE (name = '{DatabaseName}'))
                                     BEGIN
                                        ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
                                        DROP DATABASE [{DatabaseName}]
                                     END";

        try
        {
            await SQLExecutor.ExecuteAsync(Server, dropDatabaseCmd);
            await Server.Reload();
            MessageBox.Show($"Database '{DatabaseName}' dropped successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error dropping database: {ex.Message}");
        }
    }

    public override string ToString()
    {
        return DatabaseName;
    }
}
