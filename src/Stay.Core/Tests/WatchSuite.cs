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

        // ---- the licence list is one signal, not the only one ----
        //
        // Telling somebody who is enrolled that they are not is the worst mistake this app can make: they go and
        // pay again for what they have, or believe they are unprotected and give up on the PC. So when the
        // licence list and what actually arrived disagree, the app says it cannot tell.
        var bare = new FakeMachine();
        bare.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));

        var arrivingAnyway = new Engine(new FakeRegistry(), bare, new FakePerformance(),
                                        new FakeProtection { Installed = new DateOnly(2026, 9, 9) },
                                        new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(),
                                        "1.0.0", "PC", "sam").Scan();
        s.Equal("no licence, but updates arriving since support ended, is not called 'not enrolled'",
            EsuState.Unknown, arrivingAnyway.Esu.State);
        s.Check("it says plainly why it will not answer",
            arrivingAnyway.Esu.Detail.Contains("disagree"));
        s.Check("and shows the evidence it is troubled by",
            arrivingAnyway.Esu.Because!.Contains("9 September 2026"));
        s.Check("naming the other thing it could be",
            arrivingAnyway.Esu.Because!.Contains(".NET"));

        var nothingArriving = new Engine(new FakeRegistry(), bare, new FakePerformance(),
                                         new FakeProtection { Installed = new DateOnly(2025, 9, 1) },
                                         new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(),
                                         "1.0.0", "PC", "sam").Scan();
        s.Equal("no licence and nothing arriving since is 'not enrolled', confidently",
            EsuState.NotEnrolled, nothingArriving.Esu.State);
        s.Check("with both signals given as the reason",
            nothingArriving.Esu.Because!.Contains("Both point the same way"));

        var enrolledAndWorking = On(new FakeProtection { Installed = new DateOnly(2026, 9, 9) }, Enrolled()).Scan();
        s.Equal("a licence plus updates arriving is enrolled", EsuState.Enrolled, enrolledAndWorking.Esu.State);
        s.Check("and the app says the two agree", enrolledAndWorking.Esu.Because!.Contains("agree"));

        // Enrolled, and nothing has arrived since before Windows 10 even ended: the licence is the only thing
        // saying this PC is covered, and nothing it did backs that up.
        var enrolledNothingEver = On(new FakeProtection { Installed = new DateOnly(2025, 6, 1) }, Enrolled()).Scan();
        s.Equal("a licence with nothing arriving since support ended is still enrolled",
            EsuState.Enrolled, enrolledNothingEver.Esu.State);
        s.Check("but the reason says so", enrolledNothingEver.Esu.Because!.Contains("worth watching"));

        // Enrolled, updates did arrive after support ended, but the last was seven months ago. The licence and
        // the evidence agree that ESU works; the fault list is what says it has stopped.
        var enrolledThenStopped = On(new FakeProtection { Installed = new DateOnly(2026, 2, 1) }, Enrolled()).Scan();
        s.Check("an enrolment that worked and then stopped still reads as enrolled",
            enrolledThenStopped.Esu.State == EsuState.Enrolled);
        s.Check("and it is the fault list, not the licence, that raises it",
            enrolledThenStopped.Wrong.Any(w => w.Serious) && !enrolledThenStopped.Working);

        var nothingEver = new Engine(new FakeRegistry(), bare, new FakePerformance(),
                                     new FakeProtection { Installed = null },
                                     new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(),
                                     "1.0.0", "PC", "sam").Scan();
        s.Check("a PC with no record of any update at all is still answerable",
            nothingEver.Esu.Because!.Contains("no record"));

        var noLicences = new Engine(new FakeRegistry(), new FakeMachine(), new FakePerformance(), new FakeProtection(),
                                    new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(),
                                    "1.0.0", "PC", "sam").Scan();
        s.Equal("a PC that would not list its licences is unknown, not unenrolled",
            EsuState.Unknown, noLicences.Esu.State);
        s.Check("and it says why, which is usually that nobody ran it as an administrator",
            noLicences.Esu.Detail.Contains("administrator"));

        // The headline and the verdict have to agree. They came apart once: the detail said "cannot tell" while
        // the line above it said "NOT getting security updates", which is the alarming answer, confidently given.
        s.Check("a PC it cannot judge is not declared unpatched",
            arrivingAnyway.Esu.State == EsuState.Unknown && !arrivingAnyway.Patched && !arrivingAnyway.Working,
            "Unknown is its own answer and must not be rendered as either of the other two");

        s.Check("every answer it gives carries its reasoning, except the ones about another Windows",
            new[] { arrivingAnyway, nothingArriving, enrolledAndWorking, enrolledNothingEver }
                .All(x => !string.IsNullOrWhiteSpace(x.Esu.Because)));

        // ---- a switch for a feature this Windows never had is not a switch ----
        var ten = On(new FakeProtection(), Enrolled());
        var here = ten.Scan();
        var offered = here.Junk.Where(g => ten.Applies(g.Guard, here.Windows)).ToList();
        s.Equal("eight of the junk switches are Windows 11 features", 8, here.Junk.Count - offered.Count);
        s.Check("and none of them is offered on Windows 10",
            offered.All(g => g.Guard.Id is not ("ai.recall" or "ai.clicktodo" or "ai.paint" or "ai.notepad"
                                               or "ai.service" or "noise.spotlight" or "noise.suggested_actions"
                                               or "ads.settings_home")),
            "writing a value that does nothing leaves somebody believing they turned something off");
        s.Check("the ones that do apply still are", offered.Any(g => g.Guard.Id == "tel.diagnostics"));

        var elevenPc = new FakeMachine { Build = new("Microsoft Windows 11 Pro", "Professional", "24H2", 10, 26100, 1742, "arm64") };
        var onEleven = On(new FakeProtection(), elevenPc);
        var there = onEleven.Scan();
        s.Equal("on Windows 11 the app offers nothing whatever, which is its whole stance", 0,
            there.Junk.Count(g => onEleven.Applies(g.Guard, there.Windows)));

        // Two of them were written for Windows 11 when Windows 10 does the same thing under another name. Those
        // are not hidden: they are fixed, because the feature really is there.
        var bing = Core.Junk.Find("ads.bing")!;
        s.Check("the Bing switch reaches Windows 10's own search keys",
            bing.Edits.Any(e => e.Name == "BingSearchEnabled") && bing.Edits.Any(e => e.Name == "CortanaConsent"));
        s.Check("and is still offered here", ten.Applies(bing, here.Windows));

        var widgets = Core.Junk.Find("noise.widgets")!;
        s.Check("the widgets switch reaches Windows 10's News and interests",
            widgets.Edits.Any(e => e.Name == "EnableFeeds"));
        s.Check("as well as Windows 11's Widgets",
            widgets.Edits.Any(e => e.Name == "AllowNewsAndInterests"));

        s.Check("every junk switch either works on Windows 10 or is marked as not",
            Core.Junk.Items.All(g => g.OnlyIf is null or "win11" or "win10"));

        // ---- the browser, which is where the risk actually is ----
        var edge = Lifecycle.BrowserSupport("Microsoft Edge");
        s.Equal("Microsoft has given a date for Edge, so the app gives one", Lifecycle.EdgeEnds, edge.Until);

        var chrome = Lifecycle.BrowserSupport("Google Chrome");
        s.Check("Google has not, so the app does not invent one", chrome.Until is null);
        s.Check("it says what is expected and marks it as an expectation",
            chrome.Said.Contains("expectation, not a promise"));

        var firefox = Lifecycle.BrowserSupport("Mozilla Firefox");
        s.Check("Mozilla said there is no end date, which is not the same as silence", firefox.Until is null);
        s.Check("and the app says Firefox does not depend on the other two",
            firefox.Said.Contains("does not use Chrome's engine"));

        var unknown = Lifecycle.BrowserSupport("Some Other Browser");
        s.Check("a browser the app has not heard of gets an honest shrug",
            unknown.Until is null && unknown.Said.Contains("does not know"));

        var withBrowsers = new FakeMachine();
        withBrowsers.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));
        withBrowsers.Installed_.Add(new Browser("Google Chrome", "141.0.0.0", false));
        withBrowsers.Installed_.Add(new Browser("Microsoft Edge", "141.0.3021.0", true));
        var seen = On(new FakeProtection(), withBrowsers).Scan();
        s.Equal("the browsers on the PC are reported", 2, seen.Browsers.Count);
        s.Check("with the one that opens links first", seen.Browsers[0].Default);

        // ---- a PC that has never recorded an update is not accused of anything ----
        s.Check("no record of an update is not treated as a fault",
            On(new FakeProtection { Installed = null }, Enrolled()).Scan().Wrong.Count == 0);
        return s;
    }
}
