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
        pc.Programs.Add("Microsoft 365");

        var store = new MemoryReceiptStore();
        var engine = new Core.Engine(registry, pc, store, Cli.Version, "SAMS-LAPTOP", "sam")
        {
            Now = () => new DateTimeOffset(2026, 9, 10, 9, 14, 0, TimeSpan.Zero),
        };

        // One receipt from last week, so the Receipts pane shows what one looks like rather than an empty page.
        var earlier = new Core.Engine(registry, pc, store, Cli.Version, "SAMS-LAPTOP", "sam")
        {
            Now = () => new DateTimeOffset(2026, 9, 3, 16, 20, 0, TimeSpan.Zero),
        };
        earlier.Apply(earlier.PlanFor(new[] { Guards.Find("autorun")!, Guards.Find("assist")! }), restorePoint: true);

        return engine;
    }
}
