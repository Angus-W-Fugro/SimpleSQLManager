using System.Windows.Input;

namespace SQLManager;

/// <summary>
/// Interaction logic for QueryTab.xaml
/// </summary>
public partial class QueryTab
{
    public QueryTab()
    {
        DataContextChanged += QueryTab_DataContextChanged;
        InitializeComponent();
    }

    private void QueryTab_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        Model = e.NewValue as QueryTabModel;
    }

    public QueryTabModel? Model { get; private set; }

    private void SQLTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = Model?.ExecuteSQL();
        }
    }
}
