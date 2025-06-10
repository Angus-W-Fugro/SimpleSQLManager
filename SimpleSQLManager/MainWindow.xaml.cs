using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace SimpleSQLManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private MainWindowModel _Model;

    public MainWindow()
    {
        _Model = new MainWindowModel();
        DataContext = _Model;

        InitializeComponent();
    }

    private async void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        await _Model.LoadSelected(e.NewValue);
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];

        if (files is null)
        {
            return;
        }

        await _Model.HandleFiles(files);
    }
}