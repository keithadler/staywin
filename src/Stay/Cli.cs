using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Stay.Core;

namespace Stay;

/// <summary>The console twin. Exit codes: 0 fine, 1 something to look at, 2 problem, 64 usage.</summary>
public static class Cli
{
    public const string Version = "1.0.0";

    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();

    /// <summary>Entry point for the window's exe when it is given arguments: borrow the console it was started from.</summary>
    public static int Run(string[] args)
    {
        if (!AttachConsole(-1)) AllocConsole();
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        try { return Dispatch(args, Console.Out); }
        catch (Exception ex) { Console.Error.WriteLine($"stay: {ex.Message}"); return 2; }
        finally { Console.Out.WriteLine(); }
    }

    public static Engine MakeEngine() => new(new WinRegistry(), new WinMachine(), new WinPerformance(), new WinProtection(), new WinPackages(), new FileReceiptStore(),
                                             Version, Environment.MachineName, Environment.UserName);

    private static bool Flag(string[] args, string name) => args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? Value(string[] args, string name)
    {
        int i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
    }

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private static void Json(TextWriter o, object what) => o.WriteLine(JsonSerializer.Serialize(what, Pretty));

    public static int Dispatch(string[] args, TextWriter o)
    {
        var verb = args.Length == 0 ? "help" : args[0].ToLowerInvariant();
        bool json = Flag(args, "--json");
        switch (verb)
        {
            case "help": case "--help": case "-h": o.WriteLine(Help); return 0;
            case "version": case "--version":
                o.WriteLine(json ? JsonSerializer.Serialize(new { version = Version }) : Version); return 0;
            case "selftest":
            {
                var filter = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
                return SelfTest.Run(o, filter, Flag(args, "--list")) == 0 ? 0 : 1;
            }
            case "status": return Status(o, json);
            case "updates": return Updates(o, json);
            case "eleven": case "11": return Eleven(o, json);
            case "guards": case "open": return GuardList(o, json);
            case "drift": return DriftPane(args, o, json);
            case "speed": return SpeedPane(args, o, json);
            case "junk": return JunkPane(args, o, json);
            case "harden": return Harden(args, o, json);
            case "receipts": return Receipts(o, json);
            case "undo": return Undo(args, o);
            case "report": return Report(o, json);
            case "screenshots":
            {
                var dir = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
                if (dir is null) { o.WriteLine("stay screenshots <folder> [--dark]"); return 64; }
#if CLI_BUILD
                o.WriteLine("screenshots need the window: run \"Stay for Windows 10.exe\" screenshots <folder>");
                return 2;
#else
                return Screenshots.Render(dir, o, Flag(args, "--dark"));
#endif
            }
            default:
                o.WriteLine($"stay: no such command \"{verb}\"");
                o.WriteLine(Help);
                return 64;
        }
    }

    // ---------- where this PC stands ----------

    private static int Status(TextWriter o, bool json)
    {
        var standing = MakeEngine().Scan();
        var w = standing.Windows;

        if (json)
        {
            Json(o, new
            {
                windows = new { name = w.Name, w.Caption, w.Edition, w.Release, w.Version, w.Build, w.Architecture, w.IsWindows10, w.IsLtsc },
                esu = new { state = standing.Esu.State.ToString(), standing.Esu.Detail, standing.Esu.Until },
                patched = standing.Patched,
                working = standing.Working,
                lastUpdate = standing.Watch.LastUpdate,
                restartPending = standing.Watch.RestartPending,
                wrong = standing.Wrong,
                openGuards = standing.Open,
                elevenReady = standing.Eleven.Ready,
                datesChecked = Lifecycle.Checked,
            });
            return standing.Working ? 0 : 1;
        }

        o.WriteLine($"{w.Name} {w.Release}  ({w.Version}, {w.Architecture})");
        if (w.IsWindows11)
        {
            o.WriteLine("This is Windows 11. It is supported in the ordinary way and there is nothing here for you.");
            return 0;
        }
        if (w.IsWindows10 && !w.IsFinalRelease)
            o.WriteLine($"This is an older Windows 10 than 22H2. Nothing, including ESU, covers it. Update to 22H2 first.");

        o.WriteLine();
        o.WriteLine(standing.Esu.State == EsuState.Unknown
            ? "This app cannot tell whether Windows on this PC is being patched."
            : standing.Working
                ? "Windows on this PC is still getting security updates."
                : standing.Patched
                    ? "Windows on this PC is entitled to security updates, but something is wrong."
                    : "Windows on this PC is NOT getting security updates.");
        o.WriteLine($"  {standing.Esu.Detail}");
        if (standing.Esu.Because is { } because) o.WriteLine($"  {because}");
        if (standing.Watch.LastUpdate is { } landed)
            o.WriteLine($"  The last update actually installed on {landed:d MMMM yyyy}, "
                        + $"{Lifecycle.HowLong(landed, standing.Today)}.");

        var windows = standing.Components.First(c => c.Id is "windows" or "ltsc");
        if (windows.Ends is { } ends)
            o.WriteLine(standing.Patched
                ? $"  That runs out on {ends:d MMMM yyyy}, {Lifecycle.HowLong(ends, standing.Today)}."
                : $"  Support ended on {ends:d MMMM yyyy}, {Lifecycle.HowLong(ends, standing.Today)}.");

        if (standing.Esu.State == EsuState.NotEnrolled)
        {
            o.WriteLine();
            o.WriteLine("Extended Security Updates are still open to join, three ways:");
            foreach (var route in Lifecycle.EsuRoutes) o.WriteLine($"  - {route}");
            o.WriteLine("  Settings > Update & Security > Windows Update, then \"Enroll now\".");
        }

        if (standing.Wrong.Count > 0)
        {
            o.WriteLine();
            o.WriteLine("WHAT IS WRONG");
            foreach (var wrong in standing.Wrong)
            {
                o.WriteLine($"  {(wrong.Serious ? "!" : "-")} {wrong.What}");
                o.WriteLine($"      {wrong.Why}");
                o.WriteLine($"      {wrong.Fix}");
            }
        }

        if (standing.Browsers.Count > 0)
        {
            o.WriteLine();
            o.WriteLine("YOUR BROWSER");
            o.WriteLine("  Almost everything that gets onto a PC arrives through the browser, so on a Windows that");
            o.WriteLine("  is not being patched it matters more than anything else here. It is still being updated.");
            foreach (var browser in standing.Browsers)
            {
                var (until, said) = Lifecycle.BrowserSupport(browser.Name);
                var when = until is null ? "no end date given" : $"until {until:MMMM yyyy}";
                o.WriteLine($"  {browser.Name} {browser.Version}{(browser.Default ? "  (opens your links)" : "")}  - {when}");
                o.WriteLine($"      {said}");
            }
        }

        o.WriteLine();
        o.WriteLine(standing.Open == 0
            ? "Nothing is left open that this app would shut."
            : $"{standing.Open} thing{(standing.Open == 1 ? " is" : "s are")} open that this app can shut. Run: stay guards");
        o.WriteLine($"Windows 11: {(standing.Eleven.Ready ? "this PC could take it" : $"{standing.Eleven.Failing.Count} check(s) fail. Run: stay eleven")}");
        o.WriteLine();
        o.WriteLine($"Dates last checked against Microsoft's pages on {Lifecycle.Checked:d MMMM yyyy}.");
        return standing.Working && standing.Open == 0 ? 0 : 1;
    }

    private static int Updates(TextWriter o, bool json)
    {
        var standing = MakeEngine().Scan();
        if (json) { Json(o, standing.Components.Select(c => new { c.Id, c.Title, c.Ends, c.Installed, c.Detail, c.Source, days = c.DaysLeft(standing.Today) })); return 0; }

        o.WriteLine("What on this PC is still getting security updates:");
        o.WriteLine();
        foreach (var c in standing.Components)
        {
            string when = c.Ends is null ? "no end announced"
                        : c.Over(standing.Today) ? $"ENDED {c.Ends:d MMM yyyy} ({Lifecycle.HowLong(c.Ends.Value, standing.Today)})"
                        : $"until {c.Ends:d MMM yyyy} ({Lifecycle.HowLong(c.Ends.Value, standing.Today)})";
            o.WriteLine($"  {(c.Installed ? " " : "-")} {c.Title}");
            o.WriteLine($"      {when}");
            o.WriteLine($"      {c.Detail}");
            o.WriteLine($"      source: {c.Source}");
            o.WriteLine();
        }
        return standing.Components.Any(c => c.Installed && c.Over(standing.Today)) ? 1 : 0;
    }

    private static int Eleven(TextWriter o, bool json)
    {
        var eleven = Elevenable.Check(new WinMachine());
        if (json) { Json(o, new { eleven.Ready, requirements = eleven.Requirements.Select(r => new { r.Id, r.Title, result = r.Result.ToString(), r.Found, r.Needed, r.Remedy }) }); return eleven.Ready ? 0 : 1; }

        foreach (var r in eleven.Requirements)
        {
            string mark = r.Result switch { CheckResult.Pass => "ok  ", CheckResult.Fail => "no  ", _ => "?   " };
            o.WriteLine($"{mark}{r.Title}");
            o.WriteLine($"      found: {r.Found}");
            if (r.Result != CheckResult.Pass) o.WriteLine($"      needs: {r.Needed}");
            if (r.Remedy is not null) o.WriteLine($"      {r.Remedy}");
        }
        o.WriteLine();
        o.WriteLine(eleven.Ready ? "This PC can take Windows 11."
                  : eleven.Fixable ? "Every failing check above is a setting, not a new PC."
                  : "At least one failing check cannot be changed on this PC.");
        return eleven.Ready ? 0 : 1;
    }

    // ---------- what is open ----------

    private static int GuardList(TextWriter o, bool json)
    {
        var engine = MakeEngine();
        var standing = engine.Scan();
        var applies = standing.Guards.Where(g => engine.Applies(g.Guard, standing.Windows)).ToList();

        if (json) { Json(o, applies.Select(g => new { g.Guard.Id, g.Guard.Group, g.Guard.Title, state = g.State.ToString(), g.Guard.DefaultOn, cost = g.Guard.Cost.ToString(), g.Guard.NeedsRestart })); return applies.Any(g => g.NeedsDoing && g.Guard.DefaultOn) ? 1 : 0; }

        foreach (var group in applies.GroupBy(g => g.Guard.Group))
        {
            o.WriteLine(group.Key.ToUpperInvariant());
            foreach (var g in group)
            {
                string state = g.State switch { GuardState.Closed => "shut  ", GuardState.Partly => "partly", _ => "OPEN  " };
                o.WriteLine($"  {state} {g.Guard.Id,-12} {g.Guard.Title}");
                if (g.NeedsDoing) o.WriteLine($"               costs you: {g.Guard.Costs}");
            }
            o.WriteLine();
        }
        o.WriteLine("Shut everything suggested:  stay harden --all");
        o.WriteLine("Shut one:                   stay harden --id rdp");
        o.WriteLine("See first, change nothing:  stay harden --all --dry-run");
        return applies.Any(g => g.NeedsDoing && g.Guard.DefaultOn) ? 1 : 0;
    }

    private static int Harden(string[] args, TextWriter o, bool json)
    {
        var engine = MakeEngine();
        var standing = engine.Scan();

        if (standing.Windows.IsWindows11)
        {
            o.WriteLine("This is Windows 11, which is supported in the ordinary way. Nothing to do.");
            return 0;
        }

        List<Guard> chosen;
        if (Value(args, "--id") is { } ids)
        {
            chosen = new List<Guard>();
            foreach (var id in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var guard = Guards.Find(id);
                if (guard is null) { o.WriteLine($"stay: no such thing to shut: \"{id}\". Run: stay guards"); return 64; }
                chosen.Add(guard);
            }
        }
        else if (Flag(args, "--all")) chosen = engine.Suggested(standing).ToList();
        else { o.WriteLine("stay harden needs --all or --id <name>. Run: stay guards"); return 64; }

        var plan = engine.PlanFor(chosen);
        if (plan.IsEmpty) { o.WriteLine("Nothing to change: all of that is already shut."); return 0; }

        if (Flag(args, "--dry-run"))
        {
            if (json) { Json(o, plan.Registry.Select(c => new { c.GuardId, c.Path, before = c.Before.ToString(), after = c.After.ToString() })); return 0; }
            o.WriteLine($"Would change {plan.Registry.Count} value(s) and write a receipt:");
            foreach (var c in plan.Registry) o.WriteLine($"  {c.Path}: {c.Before} -> {c.After}");
            if (plan.NeedsRestart) o.WriteLine("Some of it takes effect after a restart.");
            return 0;
        }

        bool wantsPoint = Flag(args, "--restore-point");
        bool madePoint = false;
        if (wantsPoint)
        {
            var (ok, reason) = RestorePoint.Create();
            madePoint = ok;
            if (!ok) o.WriteLine($"No restore point: {reason}. The receipt still records every change.");
        }

        var receipt = engine.Apply(plan, madePoint);
        if (json) { Json(o, new { receipt.Id, receipt.Changed, receipt.Failed, restorePoint = madePoint }); return receipt.Failed > 0 ? 2 : 0; }

        o.WriteLine($"Changed {receipt.Changed} value(s). Receipt {receipt.Id}.");
        foreach (var c in receipt.Registry.Where(c => c.Failed)) o.WriteLine($"  failed: {c.Path}: {c.Error}");
        if (receipt.Failed > 0) o.WriteLine("Some changes need an administrator. Run this from an elevated prompt.");
        if (plan.NeedsRestart) o.WriteLine("Some of it takes effect after a restart.");
        o.WriteLine($"Put it all back with: stay undo {receipt.Id}");
        return receipt.Failed > 0 ? 2 : 0;
    }

    // ---------- what has come back on its own ----------

    private static int DriftPane(string[] args, TextWriter o, bool json)
    {
        var engine = MakeEngine();
        var drifted = engine.Drift();

        if (json) { Json(o, drifted.Select(d => new { d.What, d.Why, d.ReceiptId, d.When, d.ExactlyBack, d.Change.Path })); return drifted.Count > 0 ? 1 : 0; }

        if (drifted.Count == 0)
        {
            o.WriteLine("Everything this app has turned off is still off.");
            return 0;
        }

        o.WriteLine(drifted.Count == 1
            ? "One thing you turned off has turned itself back on."
            : $"{drifted.Count} things you turned off have turned themselves back on.");
        o.WriteLine();
        foreach (var one in drifted)
        {
            o.WriteLine($"  {one.What}");
            o.WriteLine($"      You turned it off on {one.When:d MMMM yyyy}, receipt {one.ReceiptId}.");
            o.WriteLine($"      {one.Why}");
        }

        if (Flag(args, "--fix")) return Do(engine, engine.PlanFor(drifted), args, o, json);
        o.WriteLine();
        o.WriteLine("Turn them off again: stay drift --fix");
        return 1;
    }

    // ---------- what is making it slow ----------

    private static int SpeedPane(string[] args, TextWriter o, bool json)
    {
        var engine = MakeEngine();
        var pace = engine.Pace();

        if (Value(args, "--stop") is { } names)
        {
            var stopping = new List<Guard>();
            foreach (var name in names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var entry = pace.Startup.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
                if (entry is null) { o.WriteLine($"stay: nothing called \"{name}\" starts with this PC. Run: stay speed"); return 64; }
                stopping.Add(engine.Stop(entry));
            }
            return Do(engine, engine.PlanFor(stopping), args, o, json);
        }

        if (Flag(args, "--all"))
            return Do(engine, engine.PlanFor(engine.SuggestedPace(pace)), args, o, json);

        if (json)
        {
            Json(o, new
            {
                boot = pace.LastBoot is null ? null : new { pace.LastBoot.When, pace.LastBoot.Seconds },
                disk = new { kind = pace.Disk.Kind.ToString(), pace.Disk.FreeGb, pace.Disk.TotalGb, pace.Disk.Cramped },
                memoryGb = pace.MemoryGb,
                truth = pace.Truth,
                startup = pace.Startup.Select(e => new { e.Name, e.Enabled, e.Milliseconds, where = e.Where.ToString() }),
                switches = pace.Switches.Select(g => new { g.Guard.Id, g.Guard.Title, state = g.State.ToString() }),
                space = pace.Space,
            });
            return 0;
        }

        if (pace.LastBoot is { } boot)
            o.WriteLine($"This PC last took {boot.Seconds:0} seconds to start, on {boot.When:d MMMM} at {boot.When:HH:mm}.");
        else
            o.WriteLine("Windows has not recorded how long this PC takes to start.");

        o.WriteLine($"Disk: {Kind(pace.Disk.Kind)}, {pace.Disk.FreeGb:0} GB free of {pace.Disk.TotalGb:0}."
                    + $"  Memory: {pace.MemoryGb:0.#} GB.");
        o.WriteLine();
        o.WriteLine(pace.Truth);

        o.WriteLine();
        o.WriteLine($"STARTS WITH THIS PC ({pace.Running.Count} of {pace.Startup.Count} switched on"
                    + (pace.SecondsAtStartup > 0 ? $", {pace.SecondsAtStartup:0.0} seconds of it measured" : "") + ")");
        foreach (var entry in pace.Startup)
            o.WriteLine($"  {(entry.Enabled ? "on " : "off")}  {entry.Name,-28} {entry.Cost}");
        if (pace.Running.Count > 0) o.WriteLine("  Stop one with: stay speed --stop OneDrive");

        o.WriteLine();
        o.WriteLine("WORTH CHANGING");
        foreach (var status in pace.Switches.Where(g => engine.AppliesToPace(g.Guard, pace.Disk)))
        {
            o.WriteLine($"  {(status.NeedsDoing ? "not done" : "done    ")}  {status.Guard.Id,-20} {status.Guard.Title}");
            if (status.NeedsDoing) o.WriteLine($"                        costs you: {status.Guard.Costs}");
        }

        if (pace.Space.Count > 0)
        {
            o.WriteLine();
            o.WriteLine($"SPACE THAT COULD COME BACK ({pace.Reclaimable / 1024.0 / 1024 / 1024:0.#} GB)");
            foreach (var item in pace.Space)
            {
                o.WriteLine($"  {item.Gb,6:0.#} GB  {item.What}");
                o.WriteLine($"            {item.How}");
            }
            o.WriteLine("  This app does not delete files. Deleting cannot be undone by a receipt, so it tells you instead.");
        }

        o.WriteLine();
        o.WriteLine("Do everything suggested above: stay speed --all");
        return pace.Switches.Any(g => g.NeedsDoing && g.Guard.DefaultOn && engine.AppliesToPace(g.Guard, pace.Disk)) ? 1 : 0;
    }

    private static string Kind(DiskKind kind) => kind switch
    {
        DiskKind.Spinning => "a spinning hard disk",
        DiskKind.Solid => "solid state",
        _ => "kind unknown",
    };

    // ---------- the advertising, the AI and the telemetry ----------

    private static int JunkPane(string[] args, TextWriter o, bool json)
    {
        var engine = MakeEngine();
        var standing = engine.Scan();

        if (Flag(args, "--apps"))
        {
            var removing = engine.SuggestedApps(standing);
            if (removing.Count == 0) { o.WriteLine("None of the apps on this PC are ones the list calls junk."); return 0; }
            if (Flag(args, "--dry-run"))
            {
                o.WriteLine($"Would remove {removing.Count} app(s) and write a receipt:");
                foreach (var app in removing) o.WriteLine($"  {app.Title}  ({app.App.FamilyName})");
                return 0;
            }
            o.WriteLine("Removing an app is the only thing this app does that a receipt cannot undo. The receipt");
            o.WriteLine("keeps a Store link for each one so you can install it again.");
            var done = engine.Apply(new Plan(Array.Empty<Guard>(), Array.Empty<RegChange>()), removing, false);
            o.WriteLine($"Removed {done.Changed} app(s). Receipt {done.Id}.");
            foreach (var app in done.Apps.Where(a => a.Failed)) o.WriteLine($"  failed: {app.Title}: {app.Error}");
            return done.Failed > 0 ? 2 : 0;
        }

        if (Flag(args, "--all"))
            return Do(engine, engine.PlanFor(engine.SuggestedJunk(standing)), args, o, json);

        if (json)
        {
            Json(o, new
            {
                switches = standing.Junk.Select(g => new { g.Guard.Id, g.Guard.Group, g.Guard.Title, state = g.State.ToString() }),
                apps = standing.Apps.Select(a => new { a.Title, a.App.FamilyName, advice = a.Advice.ToString(), a.Suggested }),
            });
            return standing.Loud > 0 ? 1 : 0;
        }

        var here = standing.Junk.Where(g => engine.Applies(g.Guard, standing.Windows)).ToList();
        foreach (var group in here.GroupBy(g => g.Guard.Group))
        {
            o.WriteLine(group.Key.ToUpperInvariant());
            foreach (var status in group)
                o.WriteLine($"  {(status.NeedsDoing ? "ON  " : "off ")} {status.Guard.Id,-24} {status.Guard.Title}");
            o.WriteLine();
        }

        int elevenOnly = standing.Junk.Count - here.Count;
        if (elevenOnly > 0)
        {
            o.WriteLine($"{elevenOnly} more switches in this list are Windows 11 features Windows 10 never had.");
            o.WriteLine("They are left out rather than written, because a value that does nothing would leave you");
            o.WriteLine("believing you had turned something off.");
            o.WriteLine();
        }
        int junkApps = standing.Apps.Count(a => a.Suggested);
        if (standing.Apps.Count > 0)
        {
            o.WriteLine("APPS YOU DID NOT ASK FOR");
            foreach (var app in standing.Apps.Take(40))
                o.WriteLine($"  {(app.Suggested ? "junk  " : "      ")} {app.Title}");
            if (standing.Apps.Count > 40) o.WriteLine($"  ... and {standing.Apps.Count - 40} more");
            o.WriteLine();
        }

        o.WriteLine($"{standing.Loud} switches still on. Turn them all off: stay junk --all");
        if (junkApps > 0) o.WriteLine($"{junkApps} bundled apps the list calls junk. Remove them: stay junk --apps");
        return standing.Loud > 0 || junkApps > 0 ? 1 : 0;
    }

    /// <summary>The one path that changes anything: dry run, restore point, apply, receipt. Every verb goes through it.</summary>
    private static int Do(Engine engine, Plan plan, string[] args, TextWriter o, bool json)
    {
        if (plan.IsEmpty) { o.WriteLine("Nothing to change: all of that is already done."); return 0; }

        if (Flag(args, "--dry-run"))
        {
            if (json) { Json(o, plan.Registry.Select(c => new { c.GuardId, c.Path, before = c.Before.ToString(), after = c.After.ToString() })); return 0; }
            o.WriteLine($"Would change {plan.Registry.Count} value(s) and write a receipt:");
            foreach (var change in plan.Registry) o.WriteLine($"  {change.Path}: {change.Before} -> {change.After}");
            if (plan.NeedsRestart) o.WriteLine("Some of it takes effect after a restart.");
            return 0;
        }

        bool made = false;
        if (Flag(args, "--restore-point"))
        {
            var (ok, reason) = RestorePoint.Create();
            made = ok;
            if (!ok) o.WriteLine($"No restore point: {reason}. The receipt still records every change.");
        }

        var receipt = engine.Apply(plan, made);
        if (json) { Json(o, new { receipt.Id, receipt.Changed, receipt.Failed, restorePoint = made }); return receipt.Failed > 0 ? 2 : 0; }

        o.WriteLine($"Changed {receipt.Changed} value(s). Receipt {receipt.Id}.");
        foreach (var failed in receipt.Registry.Where(c => c.Failed)) o.WriteLine($"  failed: {failed.Path}: {failed.Error}");
        if (receipt.Failed > 0) o.WriteLine("Some changes need an administrator. Run this from an elevated prompt.");
        if (plan.NeedsRestart) o.WriteLine("Some of it takes effect after a restart.");
        if (plan.Guards.Any(g => g.NeedsSignOut)) o.WriteLine("Some of it takes effect after you sign out and back in.");
        o.WriteLine($"Put it all back with: stay undo {receipt.Id}");
        return receipt.Failed > 0 ? 2 : 0;
    }

    // ---------- receipts ----------

    private static int Receipts(TextWriter o, bool json)
    {
        var list = new FileReceiptStore().List();
        if (json) { Json(o, list.Select(r => new { r.Id, r.When, r.Changed, r.Failed, r.Undone })); return 0; }
        if (list.Count == 0) { o.WriteLine("No receipts. Nothing has been changed by this app on this PC."); return 0; }
        foreach (var r in list)
            o.WriteLine($"{r.Id}  {r.When:d MMM yyyy HH:mm}  {r.Changed} change(s){(r.Failed > 0 ? $", {r.Failed} failed" : "")}{(r.Undone is not null ? "  (undone)" : "")}");
        o.WriteLine();
        o.WriteLine("Put one back with: stay undo <id>");
        return 0;
    }

    private static int Undo(string[] args, TextWriter o)
    {
        var id = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
        if (id is null) { o.WriteLine("stay undo <receipt id>. Run: stay receipts"); return 64; }

        var store = new FileReceiptStore();
        var receipt = store.List().FirstOrDefault(r => r.Id == id);
        if (receipt is null) { o.WriteLine($"stay: no receipt {id}. Run: stay receipts"); return 64; }

        var result = MakeEngine().Undo(receipt);
        o.WriteLine($"Put back {result.Restored} value(s).");
        foreach (var e in result.Errors) o.WriteLine($"  failed: {e}");
        return result.Errors.Count > 0 ? 2 : 0;
    }

    // ---------- everything at once ----------

    private static int Report(TextWriter o, bool json)
    {
        var engine = MakeEngine();
        var standing = engine.Scan();
        if (json)
        {
            Json(o, new
            {
                host = Environment.MachineName,
                user = Environment.UserName,
                when = DateTimeOffset.Now,
                version = Version,
                datesChecked = Lifecycle.Checked,
                windows = standing.Windows,
                esu = new { state = standing.Esu.State.ToString(), standing.Esu.Detail },
                patched = standing.Patched,
                working = standing.Working,
                watch = standing.Watch,
                wrong = standing.Wrong,
                components = standing.Components,
                eleven = new { standing.Eleven.Ready, requirements = standing.Eleven.Requirements },
                guards = standing.Guards.Where(g => engine.Applies(g.Guard, standing.Windows))
                    .Select(g => new { g.Guard.Id, g.Guard.Title, state = g.State.ToString() }),
                receipts = engine.Store.List().Select(r => new { r.Id, r.When, r.Changed, r.Undone }),
            });
            return 0;
        }

        o.WriteLine($"Stay for Windows 10 {Version} — {Environment.MachineName}, {DateTimeOffset.Now:d MMMM yyyy HH:mm}");
        o.WriteLine(new string('-', 72));
        Status(o, false);
        o.WriteLine(new string('-', 72));
        Updates(o, false);
        o.WriteLine(new string('-', 72));
        Eleven(o, false);
        o.WriteLine(new string('-', 72));
        GuardList(o, false);
        return 0;
    }

    public const string Help = """
      Stay for Windows 10 — where this PC stands now that Windows 10 is not supported,
      and the things worth shutting on a PC nobody is patching any more.

        stay status                  is Windows on this PC being patched, and until when
        stay updates                 everything here that still gets security updates, and the date each stops
        stay eleven                  whether this PC could take Windows 11, and which check fails
        stay guards                  what is open that this app can shut
        stay drift                   what you turned off that has turned itself back on
        stay drift --fix             turn all of it off again, with a receipt
        stay speed                   what is making this PC slow, and what to do about it
        stay speed --all             do everything it suggests for speed, with a receipt
        stay speed --stop OneDrive   stop one program starting with the PC
        stay junk                    the advertising, AI hooks and telemetry that are still on
        stay junk --all              turn all of it off, with a receipt
        stay junk --apps             remove the bundled apps the list calls junk
        stay harden --all            shut everything it suggests, with a receipt
        stay harden --id rdp,smb1    shut named ones
        stay harden --all --dry-run  show the exact changes and make none
        stay harden --all --restore-point   ask Windows for a restore point first
        stay receipts                every change this app has made on this PC
        stay undo <id>               put a receipt's changes back exactly
        stay report                  all of the above, for keeping or sending
        stay selftest                the engine's own checks
        stay version

      --json works on every command that reports something.
      Exit codes: 0 fine, 1 something to look at, 2 problem, 64 usage.

      Changing anything under HKLM needs an administrator. Everything else reads fine without.
      """;
}
