namespace Stay.Core.Tests;

public static class EngineSuite
{
    private static (Engine engine, FakeRegistry reg, FakeMachine pc) Bench()
    {
        var reg = new FakeRegistry();
        var pc = new FakeMachine();
        pc.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));
        var engine = new Engine(reg, pc, new FakePerformance(), new FakeProtection(), new FakePackages(Array.Empty<InstalledApp>()), new MemoryReceiptStore(), "1.0.0", "PC", "sam")
            { Now = () => new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero) };
        return (engine, reg, pc);
    }

    private static InstalledApp App(string name, string display, bool framework = false, bool system = false)
        => new($"{name}_8wek", name, $"{name}_1.0_neutral__8wek", display, "Microsoft Corporation", "1.0", framework, system);

    public static Suite Run()
    {
        var s = new Suite("shutting things and putting them back");
        var (engine, reg, pc) = Bench();

        // ---- reading what is open ----
        var standing = engine.Scan();
        s.Check("a fresh PC has things open", standing.Open > 0);
        s.Check("a guard nothing has been done to reads as open",
            standing.Guards.First(g => g.Guard.Id == "rdp").State == GuardState.Open);

        reg.Set(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections", RegValue.DWord(1));
        s.Equal("once the value is right, it reads as closed",
            GuardState.Closed, engine.StatusOf(Guards.Find("rdp")!).State);

        var smb = Guards.Find("smb1")!;
        reg.Set(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\mrxsmb10", "Start", RegValue.DWord(4));
        s.Equal("half of a guard in place reads as partly", GuardState.Partly, engine.StatusOf(smb).State);

        // ---- what it suggests ----
        var suggested = engine.Suggested(engine.Scan());
        s.Check("what it suggests is only the default-on ones", suggested.All(g => g.DefaultOn));
        s.Check("it does not suggest uploading your files", suggested.All(g => g.Id != "cloud"));
        s.Check("it does not suggest turning off printing on a PC with a printer",
            suggested.Any(g => g.Id == "spooler"), "there is no printer on this fake PC, so it should");

        pc.Printer = true;
        s.Check("and does not once there is one", engine.Suggested(engine.Scan()).All(g => g.Id != "spooler"));
        pc.Printer = false;

        s.Check("it does not suggest the Office guard without Office",
            engine.Suggested(engine.Scan()).All(g => g.Id != "macros"));
        pc.Programs.Add("Microsoft 365");
        s.Check("and does once Office is there",
            engine.Suggested(engine.Scan()).Any(g => g.Id == "macros"));

        // ---- planning ----
        var plan = engine.PlanFor(new[] { Guards.Find("rdp")!, Guards.Find("autorun")! });
        s.Equal("a value already right is not a change", 2, plan.Registry.Count);
        s.Check("every change knows what it is changing from",
            plan.Registry.All(c => c.Before.Kind == ValueKind.Absent || c.Before.Kind == ValueKind.DWord));
        s.Check("a plan of only-restart guards says a restart is needed",
            engine.PlanFor(new[] { Guards.Find("smb1")! }).NeedsRestart);
        s.Check("and one without them does not",
            !engine.PlanFor(new[] { Guards.Find("autorun")! }).NeedsRestart);

        // ---- doing it ----
        var before = reg.Snapshot().Count;
        var receipt = engine.Apply(plan);
        s.Equal("everything in the plan was written", 2, receipt.Changed);
        s.Equal("and nothing failed", 0, receipt.Failed);
        s.Check("the registry grew", reg.Snapshot().Count > before);
        s.Equal("the guards are closed afterwards",
            GuardState.Closed, engine.StatusOf(Guards.Find("autorun")!).State);
        s.Check("the receipt was kept", engine.Store.List().Count == 1);
        s.Check("the receipt names the version that made it", receipt.Version == "1.0.0");

        // ---- undoing it ----
        var undone = engine.Undo(receipt);
        s.Equal("everything was put back", 2, undone.Restored);
        s.Equal("without complaint", 0, undone.Errors.Count);
        s.Equal("and the guard is open again",
            GuardState.Open, engine.StatusOf(Guards.Find("autorun")!).State);
        s.Check("the receipt records that it was undone", engine.Store.List()[0].Undone is not null);

        // ---- putting back exactly what was there, not a default ----
        var (two, reg2, _) = Bench();
        reg2.Set(Hive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", RegValue.DWord(4));
        var r2 = two.Apply(two.PlanFor(new[] { Guards.Find("autorun")! }));
        two.Undo(r2);
        s.Equal("a value that was 4 goes back to 4, not to absent",
            RegValue.DWord(4),
            reg2.Read(Hive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun"));

        // ---- when the registry refuses ----
        var (three, reg3, _) = Bench();
        var plan3 = three.PlanFor(new[] { Guards.Find("rdp")! });
        reg3.RefuseWrites = true;
        var refused = three.Apply(plan3);
        s.Equal("a refused write is recorded as failed", 1, refused.Failed);
        s.Equal("and not counted as done", 0, refused.Changed);
        s.Check("the reason is kept", refused.Registry[0].Error is not null);
        reg3.RefuseWrites = false;
        s.Equal("undoing a receipt of failures puts nothing back", 0, three.Undo(refused).Restored);

        // ---- the per-adapter guard ----
        s.Equal("with no adapters, NetBIOS has nothing to write", 0, engine.Expand(Guards.Find("netbios")!).Count);
        s.Equal("and it reads as closed rather than open",
            GuardState.Closed, engine.StatusOf(Guards.Find("netbios")!).State);

        // ---- bundled apps ----
        var pkgs = new FakePackages(new[]
        {
            App("Microsoft.BingWeather", "Weather"),
            App("Microsoft.WindowsCalculator", "Calculator"),
            App("Fictional.Thing", "Something nobody has heard of"),
            App("Microsoft.VCLibs.140.00", "VC Libraries", framework: true),
            App("Microsoft.Windows.ShellExperienceHost", "Shell", system: true),
        });
        var withApps = new Engine(new FakeRegistry(), pc, new FakePerformance(), new FakeProtection(), pkgs,
                                  new MemoryReceiptStore(), "1.0.0", "PC", "sam");
        var listed = withApps.Bundled();
        s.Check("a framework package is never offered", listed.All(a => a.App.Name != "Microsoft.VCLibs.140.00"));
        s.Check("nor is one of Windows' own pieces the catalogue does not name",
            listed.All(a => a.App.Name != "Microsoft.Windows.ShellExperienceHost"));
        s.Check("the junk ones come first", listed[0].Suggested);
        s.Check("an app nobody has heard of is not called junk",
            listed.First(a => a.App.Name == "Fictional.Thing").Advice == AppAdvice.Optional);
        s.Check("and it says so rather than guessing",
            listed.First(a => a.App.Name == "Fictional.Thing").What.Contains("leave it unless"));
        s.Check("an app worth keeping is not suggested",
            !listed.First(a => a.App.Name == "Microsoft.WindowsCalculator").Suggested);

        var standing2 = withApps.Scan();
        var toRemove = withApps.SuggestedApps(standing2);
        s.Equal("only the junk is suggested for removal", 1, toRemove.Count);

        var removed = withApps.Apply(new Plan(Array.Empty<Guard>(), Array.Empty<RegChange>()), toRemove);
        s.Equal("removing one counts as a change", 1, removed.Changed);
        s.Check("the receipt keeps a way to put it back", removed.NeedsStore);
        s.Check("which is a Store link", removed.Apps[0].StoreLink.StartsWith("ms-windows-store:"));
        s.Check("and it really went", withApps.Bundled().All(a => a.App.Name != "Microsoft.BingWeather"));

        var stubborn = new FakePackages(new[] { App("Fail.OnPurpose", "Will not go") });
        var failing = new Engine(new FakeRegistry(), pc, new FakePerformance(), new FakeProtection(), stubborn,
                                 new MemoryReceiptStore(), "1.0.0", "PC", "sam");
        var refusedApp = failing.Apply(new Plan(Array.Empty<Guard>(), Array.Empty<RegChange>()), failing.Bundled());
        s.Equal("an app Windows will not remove is recorded as failed", 1, refusedApp.Failed);
        s.Check("with the reason", refusedApp.Apps[0].Error is not null);
        s.Check("and it is not claimed as a way to put anything back", !refusedApp.NeedsStore);

        // ---- a PC this app has nothing to say about ----
        var (win11, _, pc11) = Bench();
        pc11.Build = new("Microsoft Windows 11 Pro", "Professional", "24H2", 10, 26100, 1742, "arm64");
        s.Equal("on Windows 11 it suggests nothing at all", 0, win11.Suggested(win11.Scan()).Count);
        return s;
    }
}
