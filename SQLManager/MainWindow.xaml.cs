using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace SQLManager;

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

    private void SQLTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = _Model.ExecuteSql();
        }
    }

    private async void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        await _Model.LoadSelected(e.NewValue);
    }
}