namespace ARGOS.Server;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && string.Equals(e.Args[0], "--run-scheduled-backup", StringComparison.Ordinal))
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            var exitCode = await ScheduledBackup.RunAsync(e.Args[1]);
            Shutdown(exitCode);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
