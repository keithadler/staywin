namespace Stay.Core.Tests;

public static class SpeedSuite
{
    private static (Engine engine, FakeRegistry reg, FakePerformance pace) Bench(DiskKind disk = DiskKind.Spinning)
    {
        var reg = new FakeRegistry();
        var pc = new FakeMachine();
        var pace = new FakePerformance { TheDisk = new SystemDisk(disk, 18L << 30, 238L << 30) };
        var engine = new Engine(reg, pc, pace, new FakeProtection(), new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(), "1.0.0", "PC", "sam")
            { Now = () => new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero) };
        return (engine, reg, pace);
    }

    public static Suite Run()
    {
        var s = new Suite("what is making it slow");

        // ---- the sentence that comes before any switch ----
        var (spin, _, _) = Bench(DiskKind.Spinning);
        s.Check("a spinning disk is named as the real problem", spin.Pace().Truth.Contains("SSD"));

        var (ssd, _, ssdPace) = Bench(DiskKind.Solid);
        var pc = new FakeMachine { Memory = 4L << 30 };
        var small = new Engine(new FakeRegistry(), pc, ssdPace, new FakeProtection(), new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(), "1.0.0", "PC", "sam");
        s.Check("on an SSD with little memory, the memory is named instead",
            small.Pace().Truth.Contains("memory"));
        s.Check("and on a PC where neither is the problem, it says the switches are worth doing",
            ssd.Pace().Truth.Contains("worth doing"));

        // Windows reports usable memory, which is always a little under what is fitted. A PC with 8 GB in it
        // answers about 7.9, and a threshold written at exactly 8 told such a PC it was short of memory.
        var eight = new Engine(new FakeRegistry(), new FakeMachine { Memory = (long)(7.9 * (1L << 30)) },
                               ssdPace, new FakeProtection(), new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(), "1.0.0", "PC", "sam");
        s.Check("a PC with 8 GB fitted is not told it is short of memory",
            !eight.Pace().Truth.Contains("more memory"));
        var four = new Engine(new FakeRegistry(), new FakeMachine { Memory = 4L << 30 },
                              ssdPace, new FakeProtection(), new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(), "1.0.0", "PC", "sam");
        s.Check("a PC with 4 GB still is", four.Pace().Truth.Contains("more memory"));

        // ---- advice that depends on the disk ----
        var spinning = spin.Pace();
        s.Check("the search index is offered on a spinning disk",
            spin.SuggestedPace(spinning).Any(g => g.Id == "speed.search"));
        s.Check("and SysMain is not",
            spin.SuggestedPace(spinning).All(g => g.Id != "speed.sysmain"),
            "SysMain was built for spinning disks; switching it off there is the wrong advice");

        var solid = ssd.Pace();
        s.Check("on an SSD it is the other way round",
            ssd.SuggestedPace(solid).Any(g => g.Id == "speed.sysmain")
            && ssd.SuggestedPace(solid).All(g => g.Id != "speed.search"));

        s.Check("the animations switch is offered whatever the disk",
            spin.SuggestedPace(spinning).Any(g => g.Id == "speed.visual")
            && ssd.SuggestedPace(solid).Any(g => g.Id == "speed.visual"));

        // ---- startup programs ----
        var (engine, reg, pace) = Bench();
        pace.Starters.Add(new StartupEntry("OneDrive", @"C:\Program Files\Microsoft OneDrive\OneDrive.exe /background",
                                           StartsIn.UserRun, Enabled: true, Milliseconds: 4200));
        pace.Starters.Add(new StartupEntry("Spotify", @"C:\Users\sam\AppData\Roaming\Spotify\Spotify.exe",
                                           StartsIn.UserRun, Enabled: false));

        var found = engine.Pace();
        s.Equal("both startup programs are listed", 2, found.Startup.Count);
        s.Equal("only the one that runs is counted as running", 1, found.Running.Count);
        s.Equal("and its measured cost is added up", 4.2, Math.Round(found.SecondsAtStartup, 1));
        s.Equal("a measured cost reads as seconds, not milliseconds", "4.2 seconds at sign-in", found.Startup[0].Cost);
        s.Equal("one Windows never measured says nothing rather than zero", "", found.Startup[1].Cost);

        var stop = engine.Stop(found.Startup[0]);
        s.Equal("stopping one is a single registry write", 1, stop.Edits.Count);
        s.Equal("under StartupApproved, where Windows keeps that", true,
            stop.Edits[0].Key.Contains("StartupApproved"));
        s.Equal("as binary, which is what Windows reads there", ValueKind.Binary, stop.Edits[0].Wanted.Kind);
        s.Check("and the bytes say off", !Speed.ReadsAsEnabled(stop.Edits[0].Wanted));

        var receipt = engine.Apply(engine.PlanFor(new[] { stop }));
        s.Equal("it goes through the ordinary path and lands on a receipt", 1, receipt.Changed);
        s.Check("the value really is in the registry now",
            reg.Read(stop.Edits[0].Hive, stop.Edits[0].Key, "OneDrive").Kind == ValueKind.Binary);

        engine.Undo(receipt);
        s.Equal("and undo takes it away again", ValueKind.Absent,
            reg.Read(stop.Edits[0].Hive, stop.Edits[0].Key, "OneDrive").Kind);

        // ---- reading Windows' own record of on and off ----
        s.Check("no record at all means it runs", Speed.ReadsAsEnabled(RegValue.Absent));
        s.Check("an even first byte means it runs", Speed.ReadsAsEnabled(RegValue.Bytes(new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })));
        s.Check("an odd one means it does not", !Speed.ReadsAsEnabled(RegValue.Bytes(new byte[] { 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })));
        s.Check("and the other pair Windows uses works the same way",
            Speed.ReadsAsEnabled(RegValue.Bytes(new byte[] { 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }))
            && !Speed.ReadsAsEnabled(RegValue.Bytes(new byte[] { 0x07, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })));
        s.Equal("the off bytes are the twelve Windows writes", 12, Speed.OffBytes(DateTimeOffset.Now).Length);

        // ---- a binary value survives a receipt being written and read ----
        var value = RegValue.Bytes(Speed.OffBytes(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero)));
        var change = new RegChange("startup:OneDrive", Hive.CurrentUser, "k", "OneDrive", RegValue.Absent, value);
        var kept = Receipt.FromJson(new Receipt("x", DateTimeOffset.Now, "PC", "sam", "1.0.0", new[] { change }, Array.Empty<AppRemoval>(), false).ToJson());
        s.Equal("binary values come back out of a receipt unchanged", value, kept.Registry[0].After);

        // ---- space ----
        pace.Reclaimables.Add(new Reclaimable("Windows Update's downloaded files", 3L << 30, "Disk Cleanup"));
        pace.Reclaimables.Add(new Reclaimable("Temporary files", 1L << 30, "Storage settings"));
        s.Equal("space that could come back is added up", 4L << 30, engine.Pace().Reclaimable);
        s.Check("a disk with under a tenth free is called cramped", engine.Pace().Disk.Cramped);

        // ---- the switches themselves ----
        s.Check("every speed switch says what it costs", Speed.Switches.All(g => g.Costs.Length > 10));
        s.Check("and none of them is folklore about page files or registry cleaning",
            Speed.Switches.All(g => !g.Title.Contains("registry", StringComparison.OrdinalIgnoreCase)
                                 && !g.Title.Contains("page file", StringComparison.OrdinalIgnoreCase)));
        s.Check("nothing here repeats a switch the junk list already has",
            Speed.Switches.All(g => Junk.Items.All(j => j.Id != g.Id)));
        s.Check("and none of them collides with the security list",
            Speed.Switches.All(g => Guards.All.All(k => k.Id != g.Id)));
        return s;
    }
}
