namespace SQLManager;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public MainWindow()
    {
        DataContext = new MainWindowModel();
        InitializeComponent();
    }
}