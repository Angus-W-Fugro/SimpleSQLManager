using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;

namespace SQLManager;

public class SqlServer(string name, Func<DatabaseTable, string, Task> executeSQL) : Model, ICanQuery
{
    private ObservableCollection<Database>? _Databases;

    public string ServerName { get; } = name;

    public Func<DatabaseTable, string, Task> ExecuteSQL { get; } = executeSQL;

    public ObservableCollection<Database>? Databases
    {
        get => _Databases;
        set
        {
            _Databases = value;
            NotifyPropertyChanged();
        }
    }

    public async Task Load()
    {
        if (Databases is not null)
        {
            return;
        }

        var query = "SELECT name FROM sys.databases";

        var databaseNames = await SQLExecutor.QueryList(this, query);

        var databases = databaseNames.OrderBy(x => x).Select(name => new Database(name, this));

        Databases = new ObservableCollection<Database>(databases);
    }

    public ICommand ReloadCommand => new Command(async () => await Reload());

    public async Task Reload()
    {
        Databases = null;
        await Load();
    }

    public ICommand RestoreBackupCommand => new Command(async () => await RestoreBackup());

    private async Task RestoreBackup()
    {
        var backupPath = GetBackupFilePath();

        if (string.IsNullOrEmpty(backupPath))
        {
            return;
        }

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
}
