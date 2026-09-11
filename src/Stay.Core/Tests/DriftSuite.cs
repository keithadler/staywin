namespace Stay.Core.Tests;

public static class DriftSuite
{
    public static Suite Run()
    {
        var s = new Suite("what has come back on its own");

        var reg = new FakeRegistry();
        var pc = new FakeMachine();
        pc.Licences_.Add(("Windows(R) Operating System", "Windows 10 Pro", true));
        var store = new MemoryReceiptStore();
        var engine = new Engine(reg, pc, new FakePerformance(), new FakeProtection(),
                                new FakePackages(Array.Empty<InstalledApp>()), store, "1.0.0", "PC", "sam")
            { Now = () => new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero) };

        var telemetry = Junk.Find("tel.diagnostics")!;
        var activity = Junk.Find("tel.activity")!;
        var receipt = engine.Apply(engine.PlanFor(new[] { telemetry, activity }));
        s.Check("the switches went off", receipt.Changed > 0);
        s.Equal("and nothing has drifted the moment after", 0, engine.Drift().Count);

        // What a Windows feature update does: puts its own value back and says nothing.
        var edit = telemetry.Edits[0];
        reg.Write(edit.Hive, edit.Key, edit.Name, RegValue.Absent);
        var drifted = engine.Drift();
        s.Equal("a switch put back by something else is noticed", 1, drifted.Count);
        s.Check("it is named in words, not as a registry path", drifted[0].What.Contains("Diagnostic data"));
        s.Check("it knows the value is exactly the one from before",
            drifted[0].ExactlyBack);
        s.Check("and says what does that", drifted[0].Why.Contains("feature update"));
        s.Equal("it remembers which receipt set it", receipt.Id, drifted[0].ReceiptId);
        s.Check("the switch that stayed off is not reported", drifted.All(d => d.Change.GuardId != "tel.activity"));

        // Set to a third value by something that is neither this app nor a reset.
        reg.Write(edit.Hive, edit.Key, edit.Name, RegValue.DWord(3));
        var odd = engine.Drift();
        s.Equal("a value changed to something else again is still drift", 1, odd.Count);
        s.Check("but it is not called a feature update", !odd[0].ExactlyBack);
        s.Check("it says something else has been changing it", odd[0].Why.Contains("something else"));

        // Putting it back goes through the ordinary path, so it lands on a receipt of its own.
        var back = engine.Apply(engine.PlanFor(odd));
        s.Equal("putting it back is one change", 1, back.Changed);
        s.Equal("and then nothing has drifted", 0, engine.Drift().Count);
        s.Check("the value really is as this app left it",
            Engine.Matches(reg.Read(edit.Hive, edit.Key, edit.Name), edit.Wanted));

        // The newest receipt wins: an old one setting the same value is history, not drift.
        var later = new Engine(reg, pc, new FakePerformance(), new FakeProtection(),
                               new FakePackages(Array.Empty<InstalledApp>()), store, "1.0.0", "PC", "sam")
            { Now = () => new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero) };
        later.Undo(receipt);
        s.Check("a switch deliberately undone later is not reported as drift",
            later.Drift().All(d => d.Change.GuardId != "tel.activity"),
            "undoing something on purpose is not the same as Windows putting it back");

        // A startup program that starts itself again reads as English, not as a registry key.
        var entry = new StartupEntry("OneDrive", @"C:\OneDrive.exe", StartsIn.UserRun, true, 4200);
        var stop = engine.Stop(entry);
        var stopped = engine.Apply(engine.PlanFor(new[] { stop }));
        s.Check("stopping one worked", stopped.Changed == 1);
        reg.Write(stop.Edits[0].Hive, stop.Edits[0].Key, stop.Edits[0].Name, RegValue.Absent);
        s.Check("and it coming back says so in plain words",
            engine.Drift().Any(d => d.What == "OneDrive is starting with the PC again"));
        return s;
    }
}
