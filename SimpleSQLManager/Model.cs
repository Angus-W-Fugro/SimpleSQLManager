using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleSQLManager;

public class Model : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void NotifyAllPropertiesChanged()
    {
        NotifyPropertyChanged(null);
    }
}
