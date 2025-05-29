using Microsoft.Win32;

namespace SQLManager;

public static class DialogHelper
{
    public static string? ChooseFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
        };

        if (!dialog.ShowDialog().GetValueOrDefault())
        {
            return null;
        }

        return dialog.FileName;
    }
}
