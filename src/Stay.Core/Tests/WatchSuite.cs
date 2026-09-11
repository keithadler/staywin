namespace Stay.Core.Tests;

public static class WatchSuite
{
    private static readonly DateOnly Today = new(2026, 9, 10);

    private static Engine On(FakeProtection watch, FakeMachine? machine = null)
    {
        var pc = machine ?? new FakeMachine();
        if (pc.Licences_.Count == 0) pc.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));
        return new Engine(new FakeRegistry(), pc, new FakePerformance(), watch, new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(), "1.0.0", "PC", "sam")
            { Now = () => new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero) };
    }

    private static FakeMachine Enrolled()
    {
        var pc = new FakeMachine();
        pc.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));
        pc.Licences_.Add(("Windows(R), Windows10ESUConsumer", "Extended Security Updates", true));
        return pc;
    }

    public static Suite Run()
    {
        var s = new Suite("whether it is really being looked after");

        // ---- a PC where everything is as it should be ----
        var well = On(new FakeProtection(), Enrolled()).Scan();
        s.Equal("a PC that is enrolled and up to date has nothing wrong with it", 0, well.Wrong.Count);
        s.Check("and it is described as working, not merely entitled", well.Working);

        // ---- entitled but not receiving, which is the whole point of this ----
        var lapsed = On(new FakeProtection { Installed = new DateOnly(2026, 2, 1) }, Enrolled()).Scan();
        s.Check("an enrolment that stopped delivering is caught", lapsed.Wrong.Any(w => w.Serious));
        s.Check("it is still counted as entitled", lapsed.Patched);
        s.Check("but not as working", !lapsed.Working,
            "the gap between the two is the most useful thing this app can say");
        s.Check("and the wording says so", lapsed.Wrong[0].What.Contains("Enrolled"));
        s.Check("in months, not in days", lapsed.Wrong[0].What.Contains("months"));

        // ---- the same silence on a PC that was never enrolled is not a fault ----
        var never = On(new FakeProtection { Installed = new DateOnly(2026, 2, 1) }).Scan();
        s.Check("on an unenrolled PC the same silence is expected, not alarming",
            never.Wrong.All(w => !w.Serious));
        s.Check("and it is named as what an unsupported Windows looks like",
            never.Wrong.Any(w => w.What.Contains("last update")));

        // ---- one missed month is bad luck; two is something wrong ----
        s.Check("five weeks without an update is not yet a fault",
            On(new FakeProtection { Installed = Today.AddDays(-35) }, Enrolled()).Scan().Wrong.Count == 0);
        s.Check("ten weeks is", On(new FakeProtection { Installed = Today.AddDays(-70) }, Enrolled()).Scan().Wrong.Count > 0);

        // ---- the other ways a PC stops being looked after ----
        s.Check("an update waiting for a restart is serious",
            On(new FakeProtection { Waiting = true }, Enrolled()).Scan().Wrong.Any(w => w.What.Contains("restart") && w.Serious));
        s.Check("the update service switched off is serious",
            On(new FakeProtection { Reachable = false }, Enrolled()).Scan().Wrong.Any(w => w.What.Contains("service")));
        s.Check("no antivirus at all is serious",
            On(new FakeProtection { Watching = new Guarded("none", false, false, null, true) }, Enrolled())
                .Scan().Wrong.Any(w => w.What.Contains("Nothing is watching")));
        s.Check("an antivirus that is not watching in real time is named by its own name",
            On(new FakeProtection { Watching = new Guarded("Norton", true, false, 0, true) }, Enrolled())
                .Scan().Wrong.Any(w => w.What.StartsWith("Norton")));
        s.Check("definitions a fortnight old are serious",
            On(new FakeProtection { Watching = new Guarded("Microsoft Defender", true, true, 14, true) }, Enrolled())
                .Scan().Wrong.Any(w => w.What.Contains("definitions")));
        s.Check("definitions two days old are not",
            On(new FakeProtection { Watching = new Guarded("Microsoft Defender", true, true, 2, true) }, Enrolled())
                .Scan().Wrong.Count == 0);
        s.Check("the firewall off is serious",
            On(new FakeProtection { Watching = new Guarded("Microsoft Defender", true, true, 0, false) }, Enrolled())
                .Scan().Wrong.Any(w => w.What.Contains("firewall")));

        // ---- everything wrong at once, in a useful order ----
        var bad = On(new FakeProtection
        {
            Installed = new DateOnly(2025, 10, 1),
            Waiting = true,
            Reachable = false,
            Watching = new Guarded("Microsoft Defender", true, false, 30, false),
        }, Enrolled()).Scan();
        s.Check("several faults at once are all reported", bad.Wrong.Count >= 5);
        s.Check("the serious ones come first", bad.Wrong[0].Serious);
        s.Check("every one says what to do about it", bad.Wrong.All(w => w.Fix.Length > 15));
        s.Check("and why it matters", bad.Wrong.All(w => w.Why.Length > 30));

        // ---- nothing is claimed about a PC this app is not for ----
        var eleven = new FakeMachine { Build = new("Microsoft Windows 11 Pro", "Professional", "24H2", 10, 26100, 1742, "arm64") };
        s.Equal("on Windows 11 it finds nothing to say", 0,
            On(new FakeProtection { Installed = new DateOnly(2025, 1, 1), Waiting = true }, eleven).Scan().Wrong.Count);

        // ---- a PC that has never recorded an update is not accused of anything ----
        s.Check("no record of an update is not treated as a fault",
            On(new FakeProtection { Installed = null }, Enrolled()).Scan().Wrong.Count == 0);
        return s;
    }
}
