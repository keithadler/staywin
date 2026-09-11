using System.IO;
using System.Windows;
using Stay.Core;

namespace Stay;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Theme.Apply(this);
        DispatcherUnhandledException += (_, ex) => Log("unhandled: " + ex.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log("fatal: " + ex.ExceptionObject);

        var window = new MainWindow(Cli.MakeEngine());
        MainWindow = window;
        window.Show();
    }

    /// <summary>Appends a line to stay.log next to the receipts. Only errors go here; there is no chatter.</summary>
    public static void Log(string line)
    {
        try
        {
            Directory.CreateDirectory(Paths.Home);
            File.AppendAllText(Paths.Log, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
        }
        catch { }
    }
}
