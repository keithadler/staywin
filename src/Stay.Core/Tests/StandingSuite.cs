namespace Stay.Core.Tests;

public static class StandingSuite
{
    private static Engine On(FakeMachine machine, FakeRegistry? registry = null)
        => new(registry ?? new FakeRegistry(), machine, new FakePerformance(), new MemoryReceiptStore(), "1.0.0", "PC", "sam")
           { Now = () => new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero) };

    public static Suite Run()
    {
        var s = new Suite("where this PC stands");

        // ---- what Windows says it is ----
        var ten = new FakeMachine();
        s.Check("22H2 is recognised as Windows 10", ten.Windows().IsWindows10);
        s.Check("and as the last release", ten.Windows().IsFinalRelease);

        var eleven = new FakeMachine { Build = new("Microsoft Windows 11 Pro", "Professional", "24H2", 10, 26100, 1742, "arm64") };
        s.Check("build 26100 is Windows 11, whatever the major number says", eleven.Windows().IsWindows11);
        // Windows 11 still writes "Windows 10 Pro" into ProductName, and has since it shipped. An app about the
        // difference between the two cannot read that out loud.
        s.Equal("and it is called Windows 11, whatever the registry says",
            "Microsoft Windows 11 Pro", eleven.Windows().Name);
        s.Equal("a real Windows 10 keeps its name", "Microsoft Windows 10 Pro", ten.Windows().Name);
        s.Check("and is not Windows 10", !eleven.Windows().IsWindows10);

        // ---- ESU ----
        var none = On(ten).ReadEsu(ten.Windows());
        s.Equal("with no licences at all, the answer is not a guess", EsuState.Unknown, none.State);
        s.Check("and it says why", none.Detail.Contains("administrator"));

        var plain = new FakeMachine();
        plain.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));
        var notEnrolled = On(plain).ReadEsu(plain.Windows());
        s.Equal("licences but no ESU one means not enrolled", EsuState.NotEnrolled, notEnrolled.State);

        var enrolled = new FakeMachine();
        enrolled.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));
        enrolled.Licences_.Add(("Windows(R), Windows10ESUConsumer", "Extended Security Updates", true));
        var on = On(enrolled).ReadEsu(enrolled.Windows());
        s.Equal("an active ESU licence means enrolled", EsuState.Enrolled, on.State);
        s.Check("and it knows when that runs out", on.Until is not null);

        var lapsed = new FakeMachine();
        lapsed.Licences_.Add(("Windows(R), Windows10ESUConsumer", "Extended Security Updates", false));
        s.Equal("a licence that is present but not active is not enrolment",
            EsuState.NotEnrolled, On(lapsed).ReadEsu(lapsed.Windows()).State);

        var ltsc = new FakeMachine { Build = new("Microsoft Windows 10 Enterprise LTSC", "EnterpriseS", "21H2", 10, 19044, 3086, "x64") };
        s.Equal("LTSC does not use consumer ESU", EsuState.NotApplicable, On(ltsc).ReadEsu(ltsc.Windows()).State);
        s.Equal("nor does Windows 11", EsuState.NotApplicable, On(eleven).ReadEsu(eleven.Windows()).State);

        // ---- what is still being patched ----
        var standing = On(plain).Scan();
        var windows = standing.Components.First(c => c.Id == "windows");
        s.Check("an unenrolled PC is told Windows itself is over", windows.Over(standing.Today));
        s.Check("Edge is still going", !standing.Components.First(c => c.Id == "edge").Over(standing.Today));
        s.Check("Defender is still going", !standing.Components.First(c => c.Id == "defender").Over(standing.Today));
        s.Check("Office is listed even when it is not installed", standing.Components.Any(c => c.Id == "m365"));
        s.Check("but it is marked as not installed", !standing.Components.First(c => c.Id == "m365").Installed);
        s.Check("every component says where its date came from",
            standing.Components.All(c => c.Source.Length > 0));

        var withOffice = new FakeMachine();
        withOffice.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));
        withOffice.Programs.Add("Microsoft 365");
        s.Check("Office installed is noticed",
            On(withOffice).Scan().Components.First(c => c.Id == "m365").Installed);

        // ---- the sentence at the top of the window ----
        s.Check("an unenrolled Windows 10 PC is not being patched", !standing.Patched);
        s.Check("an enrolled one is", On(enrolled).Scan().Patched);
        s.Check("Windows 11 is", On(eleven).Scan().Patched);
        s.Check("LTSC in date is", On(ltsc).Scan().Patched);

        var deadLtsc = new FakeMachine { Build = ltsc.Build };
        var far = new Engine(new FakeRegistry(), deadLtsc, new FakePerformance(), new MemoryReceiptStore(), "1.0.0", "PC", "sam")
            { Now = () => new DateTimeOffset(2028, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        s.Check("LTSC past its date is not", !far.Scan().Patched);
        return s;
    }
}
