using System.Windows;

namespace SortFileZ;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(Loc.F("Unexpected", args.Exception.Message),
                "SortFileZ", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
    }
}
