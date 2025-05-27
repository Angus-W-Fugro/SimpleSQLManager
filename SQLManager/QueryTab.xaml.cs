using System.Windows.Input;

namespace SQLManager;

/// <summary>
/// Interaction logic for QueryTab.xaml
/// </summary>
public partial class QueryTab
{
    public QueryTab(QueryTabModel model)
    {
        Model = model;
        DataContext = model;
        InitializeComponent();
    }

    public string TabHeader => $"{Model.Database.DatabaseName}";

    public QueryTabModel Model { get; }

    private void SQLTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = Model.ExecuteSQL();
        }
    }
}
