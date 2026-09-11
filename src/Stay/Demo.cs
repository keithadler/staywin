using Stay.Core;

namespace Stay;

/// <summary>
/// A made-up PC for the screenshots: Sam Rivera's five-year-old laptop, on Windows 10 22H2, not enrolled in ESU,
/// with a TPM switched off in firmware and a processor a generation too old. Never this PC, never real data.
/// </summary>
public static class Demo
{
    public static Engine Engine()
    {
        var registry = new FakeRegistry();
        // A PC somebody has already done a little to: Remote Desktop off, updates not paused.
        registry.Set(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections", RegValue.DWord(1));
        registry.Set(Hive.LocalMachine, @"SOFTWARE\Microsoft\Windows Script Host\Settings", "Enabled", RegValue.DWord(0));

        var pc = new FakeMachine
        {
            Build = new("Microsoft Windows 10 Pro", "Professional", "22H2", 10, 19045, 4291, "x64"),
            TpmChip = (false, "2.0"),                 // there, but switched off in firmware: the fixable kind
            SecureBoot = false,
            Processor = ("Intel(R) Core(TM) i5-7200U CPU @ 2.50GHz", "GenuineIntel", 6, 142, 4, 2.5),
            Memory = 8L * 1024 * 1024 * 1024,
            Disk = 238L * 1024 * 1024 * 1024,
        };
        pc.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));
        pc.Licences_.Add(("Windows(R), Windows10ESUConsumer", "Extended Security Updates", true));
        pc.Programs.Add("Microsoft 365");
        pc.Installed_.Add(new Browser("Google Chrome", "141.0.7390.68", true));
        pc.Installed_.Add(new Browser("Microsoft Edge", "141.0.3537.57", false));
        pc.Installed_.Add(new Browser("Mozilla Firefox", "144.0.1", false));

        // A slow morning on a tired laptop: a 94 second start, four things holding it up, a disk nearly full.
        var pace = new FakePerformance
        {
            Booted = new Boot(new DateTimeOffset(2026, 9, 10, 8, 41, 0, TimeSpan.Zero), 94_000),
            TheDisk = new SystemDisk(DiskKind.Spinning, 19L << 30, 238L << 30),
        };
        pace.Starters.Add(new StartupEntry("OneDrive", @"C:\Program Files\Microsoft OneDrive\OneDrive.exe /background", StartsIn.UserRun, true, 4_200));
        pace.Starters.Add(new StartupEntry("Adobe Updater", @"C:\Program Files (x86)\Common Files\Adobe\AdobeGCClient\AGCInvokerUtility.exe", StartsIn.MachineRun, true, 3_100));
        pace.Starters.Add(new StartupEntry("Spotify", @"C:\Users\sam\AppData\Roaming\Spotify\Spotify.exe --autostart", StartsIn.UserRun, true, 2_600));
        pace.Starters.Add(new StartupEntry("Teams", @"C:\Users\sam\AppData\Local\Microsoft\Teams\Update.exe --processStart", StartsIn.UserRun, true, 2_400));
        pace.Starters.Add(new StartupEntry("SecurityHealthSystray", @"C:\Windows\system32\SecurityHealthSystray.exe", StartsIn.MachineRun, true));
        pace.Starters.Add(new StartupEntry("Steam", @"C:\Program Files (x86)\Steam\steam.exe -silent", StartsIn.UserRun, false));
        pace.Reclaimables.Add(new Reclaimable("Windows Update's downloaded installers", 6L << 30, "Disk Cleanup, ticking \"Windows Update Cleanup\". Windows downloads them again if it needs them."));
        pace.Reclaimables.Add(new Reclaimable("Temporary files your programs made", 2L << 30, "Storage settings, under Temporary files."));

        // Enrolled in ESU, and yet nothing has arrived since February. This is the case the app exists to catch,
        // so it is the one the screenshots show.
        var watch = new FakeProtection
        {
            Installed = new DateOnly(2026, 2, 11),
            Waiting = true,
            Checked = new DateTimeOffset(2026, 9, 9, 7, 12, 0, TimeSpan.Zero),
            Watching = new Guarded("Microsoft Defender", true, true, 0, true),
        };

        var apps = new FakePackages(new[]
        {
            Pkg("Microsoft.BingWeather", "Weather"),
            Pkg("Microsoft.MicrosoftSolitaireCollection", "Solitaire Collection"),
            Pkg("Clipchamp.Clipchamp", "Clipchamp"),
            Pkg("Microsoft.BingNews", "News"),
            Pkg("Microsoft.Todos", "To Do"),
            Pkg("Microsoft.WindowsCalculator", "Calculator"),
            Pkg("Microsoft.Windows.Photos", "Photos"),
            Pkg("PineStreetHoldings.Portal", "Pine Street Holdings Portal", "Pine Street Holdings"),
        });

        var store = new MemoryReceiptStore();
        var engine = new Core.Engine(registry, pc, pace, watch, apps, store, Cli.Version, "SAMS-LAPTOP", "sam")
        {
            Now = () => new DateTimeOffset(2026, 9, 10, 9, 14, 0, TimeSpan.Zero),
        };

        // One receipt from last week, so the Receipts pane shows what one looks like rather than an empty page.
        var earlier = new Core.Engine(registry, pc, pace, watch, apps, store, Cli.Version, "SAMS-LAPTOP", "sam")
        {
            Now = () => new DateTimeOffset(2026, 9, 3, 16, 20, 0, TimeSpan.Zero),
        };
        earlier.Apply(earlier.PlanFor(new[] { Guards.Find("autorun")!, Guards.Find("assist")! }), restorePoint: true);

        return engine;
    }

    private static InstalledApp Pkg(string name, string display, string publisher = "Microsoft Corporation")
        => new($"{name}_8wekyb3d8bbwe", name, $"{name}_1.0.0.0_neutral__8wekyb3d8bbwe", display, publisher,
               "1.0.0.0", IsFramework: false, IsSystem: false);
}
