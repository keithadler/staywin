namespace Stay;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
#if CLI_BUILD
        // stay.exe: a real console program, so pipes, --json and SSH all work.
        return Cli.Dispatch(args.Length == 0 ? new[] { "help" } : args, Console.Out);
#else
        // Stay for Windows 10.exe: the window; with arguments it still answers on the console it was started from.
        if (args.Length > 0) return Cli.Run(args);
        var app = new App();
        app.InitializeComponent();
        return app.Run();
#endif
    }
}
