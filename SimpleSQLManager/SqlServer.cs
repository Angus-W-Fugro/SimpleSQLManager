using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;

namespace SimpleSQLManager;

public class SqlServer : NavigationItem
{
    public SqlServer(string name, ActionManager queryTabManager) : base(name)
    {
        ServerName = name;
        ActionManager = queryTabManager;

        Actions.Add(new ActionItem("Restore Backup", RestoreBackupCommand));
        Actions.Add(new ActionItem("Refresh", ReloadCommand));
        Actions.Add(new ActionItem("Disconnect", DisconnectCommand));
    }

    public string ServerName { get; }

    public ActionManager ActionManager { get; }

    public override async Task Load()
    {
        if (Nodes is not null)
        {
            return;
        }

        var query = "SELECT name FROM sys.databases";

        var databaseNames = await SQLExecutor.QueryList(this, query);

        var databases = databaseNames.OrderBy(x => x).Select(name => new Database(name, this));

        Nodes = new ObservableCollection<NavigationItem>(databases);
    }

    public ICommand RestoreBackupCommand => new Command(async () => await RestoreBackup());

    private async Task RestoreBackup()
    {
        var backupPath = GetBackupFilePath();

        if (string.IsNullOrEmpty(backupPath))
        {
            return;
        }

        await RestoreBackup(backupPath);
    }

    public async Task RestoreBackup(string backupPath)
    {
        try
        {
            backupPath = await SQLExecutor.MakePathAccessible(backupPath, this);

            // Get list of files in the backup
            var query = $"RESTORE FILELISTONLY FROM DISK = '{backupPath}'";

            var filesInBackup = await SQLExecutor.QueryList(this, query);

            var databaseName = filesInBackup.FirstOrDefault(f => !f.EndsWith("_log"));

            if (databaseName is null)
            {
                throw new InvalidOperationException("Backup corrupted");
            }

            var restoreCmd = @$"IF EXISTS (SELECT name FROM sys.databases WHERE (name = '{databaseName}'))
                                     BEGIN
                                        ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
                                     END

                                RESTORE DATABASE [{databaseName}] FROM DISK = '{backupPath}' WITH REPLACE, RECOVERY
                                ALTER DATABASE [{databaseName}] SET MULTI_USER";

            await SQLExecutor.ExecuteAsync(this, restoreCmd);

            await Reload();

            MessageBox.Show($"Database '{databaseName}' restored");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error restoring backup: {ex.Message}");
            return;
        }
    }

    private string? GetBackupFilePath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select backup file",
            Filter = "Database backups (*.zip, *.bak)|*.zip;*.bak",
        };

        if (!dialog.ShowDialog().GetValueOrDefault())
        {
            return null;
        }

        return dialog.FileName;
    }

    public override string ToString()
    {
        return ServerName;
    }

    public ICommand DisconnectCommand => new Command(() => ActionManager.DisconnectServer(ServerName));
}
